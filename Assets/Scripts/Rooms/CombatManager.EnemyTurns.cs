using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using Assets.Scripts.Events;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Rooms
{
    /// <summary>The fight, by job: what an enemy does on its turn.</summary>
    public partial class CombatManager
    {
        private IEnumerator ExecuteEnemyTurn(ICombatUnit enemyUnit, Party party)
        {
            var enemy = enemyUnit as Enemy;

            // Mass Hysteria: a confused enemy may turn on its own side instead of acting. The
            // confusion is spent with this turn whichever way the roll goes.
            if (ConfusedBuffHandler.IsConfused(BuffTracker, enemyUnit))
            {
                BuffTracker.RemoveStatusEffect(enemyUnit, BuffType.Confused);
                var allies = GetAliveEnemies().Where(u => u != enemyUnit).ToList();
                if (allies.Count > 0 && UnityEngine.Random.Range(0f, 1f) < ConfusedBuffHandler.Chance)
                {
                    var victim = allies[UnityEngine.Random.Range(0, allies.Count)];
                    ShowFloatingLabel(enemyUnit.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.status.confused"),
                        new Color(1f, 0.85f, 0.3f), 0.16f);
                    yield return ExecuteAttack(enemyUnit, victim, Vector3.left, Color.red, 1f, "combat.verb.turns-on");
                    ResolveDeaths();
                    if (enemy != null)
                    {
                        enemy.TurnsTaken++;
                    }
                    yield break;
                }
            }

            var behavior = enemy != null && enemy.Behavior != null
                ? enemy.Behavior
                : EnemyBehaviorSO.BuiltInPreset(enemy != null ? enemy.Archetype : EnemyArchetype.Aggressor);

            var context = new EnemyCombatContext
            {
                // A Vanished hero (the Rogue) is not offered as a target - the same list PredictIntent reads.
                Heroes = TargetableHeroes(),
                Allies = GetAliveEnemies().Where(u => u != enemyUnit).ToList(),
                BuffTracker = BuffTracker,
                ChargingEntryIndex = enemy != null ? enemy.ChargingEntryIndex : EnemyActionPlanner.NoCharge,
                SelfTurnCount = enemy != null ? enemy.TurnsTaken : 0,
                Spells = enemy != null ? enemy.Spells : null,
                Threat = _threat,
                Guards = Events.Guards,
                OpenSlots = EnemyFormation.DesignMax - GetAliveEnemies().Count,
                ActionUses = enemy != null ? enemy.ActionUses : null
            };

            // One authored list decides everything: gate, then priority, then weight. Casting is an
            // entry in that list rather than a pre-roll around it, so the whole repertoire - swings,
            // charges, heals, debuffs, the boss signature and spells - is chosen in one place.
            var decision = EnemyActionPlanner.Plan(
                enemyUnit, context, behavior, EnemyPlanRolls.Random(behavior.Actions.Count));

            string announced = EnemyActionPlanner.AnnouncedName(decision, behavior.Actions);
            if (!string.IsNullOrEmpty(announced))
            {
                OnEnemyAbilityUsed?.Invoke(enemyUnit, announced);
            }

            switch (decision.Type)
            {
                case EnemyActionType.CastMagic:
                    yield return ExecuteEnemyCast(enemyUnit, enemy, decision, party);
                    break;
                case EnemyActionType.ChargeHeavy:
                    yield return ExecuteEnemyCharge(enemy, decision.Target, decision.EntryIndex);
                    break;
                case EnemyActionType.HeavyAttack:
                    yield return ExecuteEnemyHeavyAttack(enemyUnit, enemy, party, decision.Multiplier);
                    break;
                case EnemyActionType.ChargeAoe:
                    yield return ExecuteEnemyChargeAoe(enemy, decision.EntryIndex);
                    break;
                case EnemyActionType.AoeAttack:
                    yield return ExecuteEnemyAoeAttack(enemyUnit, enemy, party, decision.Multiplier);
                    break;
                case EnemyActionType.Heal:
                    yield return ExecuteEnemyHeal(enemyUnit, decision.Target, decision.Amount);
                    break;
                case EnemyActionType.Debuff:
                    yield return ExecuteEnemyDebuff(enemyUnit, decision.Target, decision.DebuffStat, decision.Amount, decision.Duration, decision.ApplyChance);
                    break;
                case EnemyActionType.BuffAlly:
                    yield return ExecuteEnemyBuffAlly(enemyUnit, decision.Target, decision.DebuffStat, decision.Amount, decision.Duration);
                    break;
                case EnemyActionType.Guard:
                    yield return ExecuteEnemyGuard(enemyUnit, decision.Target, decision.Amount, decision.Duration);
                    break;
                case EnemyActionType.Summon:
                    yield return ExecuteEnemySummon(enemyUnit, enemy, decision);
                    break;
                default:
                    string verbKey = decision.Multiplier > 1f ? "combat.verb.strikes-savagely-at" : "combat.verb.attacks";
                    yield return ExecuteEnemyBasicAttack(enemyUnit, decision.Target, party, decision.Multiplier, verbKey);
                    break;
            }

            // Count the turn after it resolves so cadence-based behaviors (boss signature) advance.
            if (enemy != null)
            {
                enemy.TurnsTaken++;
            }
        }

        /// <summary>
        /// Resolves an enemy cast through the same effect engine a hero cast uses, so resistances,
        /// the defense curve, healing clamps and floating text all behave identically.
        ///
        /// <para>Three deliberate differences from <see cref="ExecuteCastAction"/>. No charge is
        /// spent - charges are a hero-side resource on an equipped slot, and enemies cast freely
        /// from what they know. No upgrade bonus or upgrade level applies, so an enemy casts the base version
        /// of the magic and any effect gated behind an <c>UnlockLevel</c> is skipped. And no tag
        /// tracker or combo detector is passed, so an enemy cast neither triggers combos nor leaves
        /// tags: combos carry player-facing discovery and upgrades, and crediting the player for a
        /// combo the monster set up would be wrong. Letting enemies into the tag layer is a
        /// deliberate follow-up, tracked in docs/NEXT_STEPS.md.</para>
        /// </summary>
        private IEnumerator ExecuteEnemyCast(ICombatUnit enemyUnit, Enemy enemy, EnemyDecision decision, Party party)
        {
            var targets = new List<ICombatUnit>();
            foreach (var target in decision.MagicTargets)
            {
                if (target != null && target.IsAlive)
                {
                    targets.Add(target);
                }
            }

            if (decision.Magic == null || targets.Count == 0)
            {
                // Everything it wanted to hit died before its turn came up; fall back to a swing.
                yield return ExecuteEnemyBasicAttack(enemyUnit, null, party);
                yield break;
            }

            var castAction = new SpellcastAction
            {
                Magic = decision.Magic,
                Caster = enemyUnit,
                Targets = targets
            };

            float powerScale = enemy != null ? enemy.MagicPowerScale : 1f;
            // The plates keep showing the health from before the cast until each hit lands: a
            // party-wide spell used to drop every hero's bar before its first bolt flew.
            HoldHealthDisplay();
            var result = _calculator.Execute(
                castAction, BuffTracker, null, null, 0, 0, null, powerScale);

            CombatAudio.Play(CombatSound.MagicCast);
            RecordEnemySpellObserved(enemy, decision.Magic);

            yield return _presenter.Present(
                result,
                castAction.Caster,
                castAction.Magic
            );
            HealthDisplay.ReleaseAll();

            ResolveDeaths();
        }

        /// <summary>Freezes every unit's HP plate at its current health until the presenter releases
        /// it as that unit's hit lands (<see cref="HealthDisplay"/>).</summary>
        private void HoldHealthDisplay()
        {
            HealthDisplay.Hold(HeroSideUnits());
            HealthDisplay.Hold(GetAliveEnemies());
        }

        private IEnumerator ExecuteEnemyBasicAttack(ICombatUnit enemyUnit, ICombatUnit target, Party party, float multiplier = 1f, string verbKey = "combat.verb.attacks")
        {
            if (target == null || !target.IsAlive)
            {
                target = GetRandomAliveHero(party);
            }

            if (target == null)
            {
                yield break;
            }

            yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red, multiplier, verbKey);
            ResolveDeaths();
        }

        private IEnumerator ExecuteEnemyCharge(Enemy enemy, ICombatUnit target, int entryIndex)
        {
            if (enemy == null)
            {
                yield break;
            }

            enemy.BeginCharge(entryIndex, target);
            SetChargingVisual(enemy, true);
            ShowFloatingLabel(enemy.Transform.position, Loc.Get("combat.float.charging"), new Color(1f, 0.5f, 0.2f));
            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteEnemyHeavyAttack(ICombatUnit enemyUnit, Enemy enemy, Party party, float multiplier)
        {
            // The aim was locked at wind-up. It moves if that hero has since fallen or Vanished (the
            // Rogue hiding mid-charge dodges it, owner 2026-10-25), picked again by threat from who can
            // still be seen - which is everyone again if the whole party is hidden.
            ICombatUnit target = enemy != null ? enemy.ChargeTarget : null;
            if (target == null || !target.IsAlive || HiddenBuffHandler.IsHidden(BuffTracker, target))
            {
                target = GetRandomAliveHero(party);
            }

            if (enemy != null)
            {
                enemy.ClearCharge();
                SetChargingVisual(enemy, false);
            }

            if (target == null)
            {
                yield break;
            }

            yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red, multiplier, "combat.verb.heavy-blow-on");
            ResolveDeaths();
        }

        private IEnumerator ExecuteEnemyChargeAoe(Enemy enemy, int entryIndex)
        {
            if (enemy == null)
            {
                yield break;
            }

            enemy.BeginCharge(entryIndex, null);
            SetChargingVisual(enemy, true);
            CombatAudio.Play(CombatSound.BossSignature);
            ShowFloatingLabel(enemy.Transform.position, Loc.Get("combat.float.channeling"), new Color(1f, 0.35f, 0.35f));

            // Telegraph who the party-wide signature will hit: a warning marker over every hero.
            if (_currentParty != null)
            {
                foreach (var hero in GetAliveHeroes(_currentParty))
                {
                    if (hero.Transform != null)
                    {
                        ShowFloatingLabel(hero.Transform.position + new Vector3(0f, 0.4f, 0f), "!", new Color(1f, 0.3f, 0.3f), 0.2f);
                    }
                }
            }

            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteEnemyAoeAttack(ICombatUnit enemyUnit, Enemy enemy, Party party, float multiplier)
        {
            if (enemy != null)
            {
                enemy.ClearCharge();
                SetChargingVisual(enemy, false);
            }

            var targets = GetAliveHeroes(party);
            if (targets == null || targets.Count == 0)
            {
                yield break;
            }

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                {
                    continue;
                }
                // A rider whose mech is also in the blast is covered by it: the mech takes one blow, not two.
                var guard = Events.Guards.GuardOf(target);
                if (guard != null && targets.Contains(guard))
                {
                    continue;
                }
                yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red, multiplier, "combat.verb.smashes");
                ResolveDeaths();
            }
        }

        private IEnumerator ExecuteEnemyHeal(ICombatUnit enemyUnit, ICombatUnit target, int amount)
        {
            if (target == null || !target.IsAlive)
            {
                yield break;
            }

            yield return SpellVfx.Cast(enemyUnit);
            int healed = HealthOps.Heal(target, amount, new HealthSource(enemyUnit, HealthCause.EnemyHeal), Events).Healed;

            SpellVfx.Afflict(target, SpellVfx.HealColor, false);
            CombatAudio.Play(CombatSound.Heal);
            ShowDamageText(target.Transform.position, healed, Color.green);
            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteEnemyDebuff(ICombatUnit enemyUnit, ICombatUnit target, StatType stat, int amount, int duration, int applyChance)
        {
            if (target == null || !target.IsAlive)
            {
                yield break;
            }

            // The enemy gathers its power before the curse lands - a Weaken used to show only a number.
            yield return SpellVfx.Cast(enemyUnit);
            CombatAudio.Play(CombatSound.MagicCast);

            // Every debuff is a chance (AfflictionRules), an enemy's own included.
            if (!AfflictionRules.Lands(applyChance))
            {
                ShowFloatingLabel(target.Transform.position, Cards.Effects.DebuffEffectExecutor.UnaffectedText, new Color(0.6f, 0.6f, 0.65f));
                yield return new WaitForSeconds(_turnDelay);
                yield break;
            }

            SpellVfx.Afflict(target, SpellVfx.HarmColor, true);
            yield return new WaitForSeconds(SpellVfx.AfflictLands);
            BuffTracker.ApplyBuff(target, stat, -amount, duration);
            ShowFloatingLabel(target.Transform.position, Loc.Format("combat.float.stat-down", ("amount", amount), ("stat", stat)), new Color(0.7f, 0.4f, 1f));
            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteEnemyBuffAlly(ICombatUnit enemyUnit, ICombatUnit target, StatType stat, int amount, int duration)
        {
            if (target == null || !target.IsAlive)
            {
                yield break;
            }

            CombatAudio.Play(CombatSound.MagicCast);
            yield return SpellVfx.Cast(enemyUnit);
            SpellVfx.Afflict(target, SpellVfx.BoonColor, false);
            yield return new WaitForSeconds(SpellVfx.AfflictLands);
            BuffTracker.ApplyBuff(target, stat, amount, duration);
            ShowFloatingLabel(target.Transform.position, Loc.Format("combat.float.stat-up", ("amount", amount), ("stat", StatCatalog.ShortName(stat))), new Color(1f, 0.6f, 0.25f));
            yield return new WaitForSeconds(_turnDelay);
        }

        /// <summary>
        /// An enemy covers an ally until its own next turn (<see cref="GuardTable.Cover"/>): a single-target
        /// blow aimed at the ally lands on the guard instead. <paramref name="brace"/> is an Endurance buff
        /// the guard takes for the job, 0 for none.
        /// </summary>
        private IEnumerator ExecuteEnemyGuard(ICombatUnit enemyUnit, ICombatUnit target, int brace, int duration)
        {
            if (target == null || !target.IsAlive)
            {
                yield break;
            }

            Events.Guards.Cover(target, enemyUnit);
            if (brace > 0)
            {
                BuffTracker.ApplyBuff(enemyUnit, StatType.Endurance, brace, duration);
            }
            CombatAudio.Play(CombatSound.ItemUse);
            ShowFloatingLabel(enemyUnit.Transform.position, Loc.Get("combat.float.guard"), new Color(0.75f, 0.85f, 1f));
            ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.float.covered"), new Color(0.75f, 0.85f, 1f), 0.14f);
            yield return new WaitForSeconds(_turnDelay);
        }

        /// <summary>
        /// An enemy calls more of its kind into the fight. Each body is spawned into the room, set down in
        /// the re-ranked formation, put on the clock to act after a full turn of its own, and marked
        /// <see cref="Enemy.IsSummoned"/> so its death pays nothing.
        /// </summary>
        private IEnumerator ExecuteEnemySummon(ICombatUnit enemyUnit, Enemy enemy, EnemyDecision decision)
        {
            if (enemy != null && decision.EntryIndex >= 0)
            {
                enemy.ActionUses.TryGetValue(decision.EntryIndex, out int used);
                enemy.ActionUses[decision.EntryIndex] = used + 1;
            }

            var called = new List<ICombatUnit>();
            if (decision.SummonDefinition != null && _currentCombatRoom != null && EnemyManager.HasInstance)
            {
                for (int i = 0; i < decision.SummonCount && GetAliveEnemies().Count < EnemyFormation.DesignMax; i++)
                {
                    var body = EnemyManager.Instance.SpawnSingle(decision.SummonDefinition, _currentCombatRoom);
                    if (body == null)
                    {
                        break;
                    }
                    body.IsSummoned = true;
                    body.ResetForCombat();
                    called.Add(body);
                }
            }

            if (called.Count == 0)
            {
                yield break;
            }

            CombatStage.Instance.RelayoutEnemies(GetAliveEnemies(), called);
            foreach (var body in called)
            {
                _turnManager.AddUnit(body, actsNext: false);
                RecordEnemySeen(body as Enemy);
                ShowFloatingLabel(body.Transform.position, "!", new Color(1f, 0.55f, 0.3f), 0.2f);
            }
            EnsureHealthBars(called);
            BroadcastTurnOrder();

            CombatAudio.Play(CombatSound.BossSignature);
            ShowFloatingLabel(enemyUnit.Transform.position, Loc.Get("combat.float.enemy-summon"), new Color(1f, 0.55f, 0.3f));
            yield return new WaitForSeconds(_turnDelay);
        }

        private void SetChargingVisual(Enemy enemy, bool charging)
        {
            var sr = enemy.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                return;
            }
            sr.color = charging ? new Color(1f, 0.55f, 0.4f) : Color.white;
        }

        /// <summary>
        /// The heroes an enemy may pick as a target: the hero side less anyone Vanished
        /// (<see cref="BuffType.Hidden"/>) - unless that leaves nobody, when everyone is fair game again.
        /// </summary>
        private List<ICombatUnit> TargetableHeroes()
        {
            var all = HeroSideUnits();
            var seen = all.Where(u => !HiddenBuffHandler.IsHidden(BuffTracker, u)).ToList();
            return seen.Count > 0 ? seen : all;
        }

        private Enemy GetRandomAliveEnemy(Room room)
        {
            var alive = room.Enemies.Where(e => e != null && e.IsAlive).ToList();
            if (alive.Count == 0)
            {
                return null;
            }
            return alive[UnityEngine.Random.Range(0, alive.Count)];
        }

        /// <summary>
        /// A fresh hero for an enemy whose chosen target fell before the blow landed - by threat,
        /// like the original pick, not by list order.
        /// </summary>
        private ICombatUnit GetRandomAliveHero(Party party)
        {
            return ThreatTable.Pick(TargetableHeroes(), _threat, UnityEngine.Random.Range(0f, 1f));
        }
    }
}
