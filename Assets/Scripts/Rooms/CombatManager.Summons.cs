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
    /// <summary>The fight, by job: summons: arriving, acting and leaving.</summary>
    public partial class CombatManager
    {
        /// <summary>
        /// A special-attack summon (§4b): spend the charge, bring the creature onto the stage, then
        /// resolve its effects through the same <c>EffectResolver</c> an ability uses. The upgrades
        /// its grid nodes add are folded into a throwaway castable (<see cref="SummonOps.BuildCastable"/>),
        /// which takes no Forge bonus and triggers no combo.
        /// </summary>
        private IEnumerator ExecuteSummonAction(ICombatUnit caster, SummonSlot slot, ICombatUnit chosen = null)
        {
            var hero = caster as Hero;
            var summon = slot != null ? slot.Summon : null;
            var summons = DungeonManager.HasInstance ? DungeonManager.Instance.Summons : null;

            // Checked before the charge is spent. SummonSlot.CanUse already keeps the command greyed
            // for a kind combat cannot run; this is the backstop, and it must not cost the player anything.
            if (summon != null && !slot.IsImplemented)
            {
                Debug.LogWarning($"[Summon] {summon.Key} is {summon.Kind}, which is not implemented.");
                yield break;
            }

            // One at a time: nobody is left to summon a second, but a stale submit must not try.
            if (ReplacementOut)
            {
                yield break;
            }

            if (summon != null && !SummonOps.CanAfford(summon, caster))
            {
                yield break;
            }

            if (hero == null || summon == null || summons == null || !summons.TryUse(hero.HeroKey, summon.Key))
            {
                yield break;
            }

            // A summon with a blood price (the Blood Idol) takes it as it answers, floor 1.
            int bloodPrice = SummonOps.HealthCost(summon, hero);
            if (bloodPrice > 0)
            {
                int paid = HealthOps.Pay(hero, bloodPrice, new HealthSource(hero, HealthCause.Cost), Events).Landed;
                ShowFloatingLabel(hero.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Format("combat.float.health-cost", ("amount", paid)), new Color(0.9f, 0.35f, 0.35f), 0.15f);
            }

            if (summon.Kind == SummonKind.ReplaceParty)
            {
                yield return SummonReplacement(hero, slot);
                yield break;
            }
            if (summon.Kind == SummonKind.JoinParty)
            {
                yield return SummonAlly(hero, slot);
                yield break;
            }

            OnSummonStarted?.Invoke(caster, summon);
            yield return SummonPresenter.Present(summon);

            var castable = SummonOps.BuildCastable(summon, slot.Grant);
            var action = new SpellcastAction
            {
                Magic = castable,
                Caster = caster,
                Targets = SummonTargets(castable.TargetType, caster, chosen)
            };
            // A summon authored to lay tags (Glacius's Ice) lays them and can set off a combo, as a cast does.
            HoldHealthDisplay();
            var result = summon.UsesTags
                ? _calculator.Execute(action, BuffTracker, _tagTracker, _comboDetector, 0, 0,
                    MetaProgressManager.Instance.GetComboUpgradeLevel)
                : _calculator.Execute(action, BuffTracker);
            if (summon.UsesTags)
            {
                foreach (var comboKey in result.TriggeredComboKeys)
                {
                    MetaProgressManager.Instance.MarkComboDiscovered(comboKey);
                }
            }
            yield return _presenter.Present(result, caster, castable);
            HealthDisplay.ReleaseAll();

            // ...then what it gives the party (Glacius's frost hide).
            var partyCastable = SummonOps.BuildPartyCastable(summon, slot.Grant);
            if (partyCastable != null)
            {
                var partyAction = new SpellcastAction
                {
                    Magic = partyCastable, Caster = caster, Targets = HeroSideUnits()
                };
                HoldHealthDisplay();
                yield return _presenter.Present(_calculator.Execute(partyAction, BuffTracker), caster, partyCastable);
                HealthDisplay.ReleaseAll();
                Destroy(partyCastable);
            }

            // ...then the part that lands on one target at random (the Nightmare's Silence).
            var randomCastable = SummonOps.BuildRandomCastable(summon, slot.Grant);
            if (randomCastable != null)
            {
                var pool = action.Targets.Where(t => t != null && t.IsAlive).ToList();
                if (pool.Count > 0)
                {
                    var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                    var randomAction = new SpellcastAction { Magic = randomCastable, Caster = caster, Targets = new List<ICombatUnit> { pick } };
                    HoldHealthDisplay();
                    yield return _presenter.Present(_calculator.Execute(randomAction, BuffTracker), caster, randomCastable);
                    HealthDisplay.ReleaseAll();
                }
                Destroy(randomCastable);
            }
            OnSummonEnded?.Invoke();
            Destroy(castable);

            // A damaging summon can kill; the Bloodfang Boar does not, but the next one will.
            ResolveDeaths();
        }

        /// <summary>
        /// A party-replacing summon arrives (§4b). The summoner's turn is spent on the summoning and
        /// closes with the summoner's own upkeep (the loop runs it when this returns); the party then
        /// stands frozen - off the clock with their counters kept, untargetable, dimmed - and the
        /// summon takes an immediate turn of its own ahead of the clock, which is turn 1 of its stay.
        /// </summary>
        private IEnumerator SummonReplacement(Hero summoner, SummonSlot slot)
        {
            var summon = slot.Summon;
            OnSummonStarted?.Invoke(summoner, summon);

            // A replacement fights alone: every guest goes home first. A stand-in is not a guest - it
            // holds a hero's place - so it steps out with the party instead, frozen and hidden, and comes
            // back with them.
            // A mech is not sent home either: its rider is part of the party, so the mech steps out
            // with her and comes back, still ridden, when the summon leaves (owner, 2026-10-07). It
            // keeps no clock of its own - it follows its rider's, which the party's suspension freezes.
            var standing = _standIns.Where(a => a != null && a.IsAlive)
                .Concat(_mounts.Values.Where(m => m != null && m.IsAlive)).ToList();
            foreach (var guest in _allies.Where(a => !_mounts.ContainsValue(a)).ToList())
            {
                EndAlly(guest, SummonExit.Dismissed);
            }

            // A squad (the Demon Army) is several troops sharing the summon's stay; anything else is
            // the one unit it always was.
            int turns = SummonOps.TurnsFor(summon, slot.Grant);
            var units = SummonOps.SquadFor(summon, slot.Grant)
                .Select(troop => SummonUnit.Create(troop, slot.Grant, summoner, turns))
                .ToList();
            CombatStage.Instance.HideParty();
            CombatStage.Instance.HideUnits(standing.Cast<Component>());
            if (summon.IsSquad)
            {
                CombatStage.Instance.PlaceSquad(units);
                // They pour out together; only the first roars.
                for (int i = 1; i < units.Count; i++)
                {
                    StartCoroutine(SummonPresenter.Arrive(units[i], quiet: true));
                }
            }
            else
            {
                CombatStage.Instance.PlaceSummon(units[0]);
            }
            yield return SummonPresenter.Arrive(units[0]);
            OnSummonEnded?.Invoke();

            // The whole hero side swaps in one step, so nothing can ever see both or neither.
            _turnManager.Suspend(_currentParty.Heroes.Cast<ICombatUnit>().Concat(standing.Cast<ICombatUnit>()));
            _squad.Clear();
            _squad.AddRange(units);
            for (int i = 0; i < units.Count; i++)
            {
                // The front troop takes the immediate turn; the rest join the clock at their own pace.
                _turnManager.AddUnit(units[i], actsNext: i == 0);
            }
            EnsureHealthBars(units.Cast<ICombatUnit>().ToList());

            // A heavy locks onto its victim at wind-up and only re-aims at a *dead* one, so a blow
            // already aimed at a frozen hero would land on them. Every stored aim moves to the
            // summon - which is exactly the moment the summon exists for.
            foreach (var enemy in AliveEnemyComponents())
            {
                if (enemy.ChargeTarget != null)
                {
                    enemy.ChargeTarget = units[UnityEngine.Random.Range(0, units.Count)];
                }
            }

        }

        /// <summary>
        /// A summon that fights beside the party arrives (<see cref="SummonKind.JoinParty"/>). One per
        /// summoner: calling again sends the first home before the new one comes. It rises at the
        /// centre, strides into the vanguard column in front of the party, and takes an immediate
        /// turn of its own ahead of the clock - turn 1 of its stay, as a replacement's is. The party
        /// stays on the field, and the enemies may now hit either.
        /// </summary>
        private IEnumerator SummonAlly(Hero summoner, SummonSlot slot)
        {
            var summon = slot.Summon;
            OnSummonStarted?.Invoke(summoner, summon);

            // One of each kind per summoner: calling the same horror again replaces it; a different
            // one joins beside it (the Cultist's Spawn and Watcher together).
            var previous = AllyOf(summoner, summon);
            if (previous != null)
            {
                EndAlly(previous, SummonExit.Dismissed);
            }

            var unit = SummonUnit.Create(summon, slot.Grant, summoner);
            var firstRite = SummonOps.RotatingAbility(summon, 0);
            if (firstRite != null)
            {
                unit.OverrideAttack(firstRite);
            }
            _allies.Add(unit);
            CombatStage.Instance.PlaceAllies(ColumnAllies(), arriving: unit);
            yield return SummonPresenter.Arrive(unit);
            OnSummonEnded?.Invoke();

            _turnManager.AddUnit(unit, actsNext: true);
            EnsureHealthBars(new List<ICombatUnit> { unit });

        }

        /// <summary>Puts <paramref name="frames"/> on a summon's sprite (a mech with its rider aboard).
        /// Nothing to show keeps what is there.</summary>
        private static void SetUnitFrames(SummonUnit unit, Sprite[] frames, float fps)
        {
            var valid = frames != null ? frames.Where(f => f != null).ToArray() : new Sprite[0];
            if (unit == null || valid.Length == 0)
            {
                return;
            }
            var sr = unit.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = valid[0];
            }
            var animator = unit.GetComponent<SpriteAnimator>() ?? unit.gameObject.AddComponent<SpriteAnimator>();
            animator.Initialize(valid, Mathf.Max(1f, fps));
        }

        /// <summary>Whether <paramref name="unit"/> fights beside the party (a guest or a stand-in).</summary>
        private bool IsBesideParty(SummonUnit unit)
        {
            return unit != null && (_allies.Contains(unit) || _standIns.Contains(unit));
        }

        /// <summary>The <paramref name="summon"/> <paramref name="summoner"/> has on the field (any of
        /// theirs when no summon is named), or null.</summary>
        private SummonUnit AllyOf(Hero summoner, SummonSO summon = null)
        {
            return _allies.FirstOrDefault(a => a != null && ReferenceEquals(a.Summoner, summoner)
                                               && (summon == null || ReferenceEquals(a.Summon, summon)));
        }

        /// <summary>
        /// An ally leaves the field: off the clock, its threat forgotten, anything an enemy had aimed
        /// at it let go (the blow re-picks from the hero side when it lands), and off the stage -
        /// crumbling when it <see cref="SummonExit.Fell"/>, sinking away otherwise. The party is not
        /// touched: it never left.
        /// </summary>
        private void EndAlly(SummonUnit ally, SummonExit exit)
        {
            if (ally == null || !(_allies.Remove(ally) || _standIns.Remove(ally)))
            {
                return;
            }
            QueueParting(ally, exit);
            ally.Stay.Leave(exit);
            _turnManager.RemoveUnit(ally);
            _threat.Clear(ally);
            Dismount(ally);
            foreach (var enemy in AliveEnemyComponents())
            {
                if (ReferenceEquals(enemy.ChargeTarget, ally))
                {
                    enemy.ChargeTarget = null;
                }
            }
            StartCoroutine(SummonPresenter.Depart(ally, exit == SummonExit.Fell));

            // Close the gap it left in the column. Not at the end of the fight: the stage is coming down.
            if (exit != SummonExit.Victory && CombatStage.HasInstance)
            {
                CombatStage.Instance.PlaceAllies(ColumnAllies());
            }
        }

        private void EndAllAllies(SummonExit exit)
        {
            foreach (var ally in BesideParty.ToList())
            {
                EndAlly(ally, exit);
            }
        }

        /// <summary>A summoner who falls takes their allies with them: what bound them is gone.</summary>
        private void EndAlliesOf(Hero summoner)
        {
            if (summoner == null)
            {
                return;
            }
            foreach (var ally in _allies.Where(a => a != null && ReferenceEquals(a.Summoner, summoner)).ToList())
            {
                EndAlly(ally, SummonExit.Dismissed);
            }
        }

        /// <summary>
        /// A party-replacing summon's turn is over: count it, and send it home when that was its
        /// last or it was dismissed. Runs after the turn's upkeep, so the summon's own poison ticks
        /// on its own final turn. A no-op for any other unit.
        /// </summary>
        private void AfterSummonTurn(ICombatUnit unit, bool dismissed)
        {
            var summon = unit as SummonUnit;
            if (IsBesideParty(summon))
            {
                bool lastTurn = summon.Stay.EndTurn();
                // A rotating summon (the Blood Idol) moves on to its next rite.
                var nextRite = SummonOps.RotatingAbility(summon.Summon, summon.Stay.TurnsTaken);
                if (nextRite != null)
                {
                    summon.OverrideAttack(nextRite);
                }
                if (dismissed)
                {
                    EndAlly(summon, SummonExit.Dismissed);
                }
                else if (lastTurn)
                {
                    EndAlly(summon, SummonExit.TurnsSpent);
                }
                return;
            }
            if (summon == null || !_squad.Contains(summon))
            {
                return;
            }
            bool last = summon.Stay.EndTurn();
            if (dismissed)
            {
                // Dismiss is how the player brings the party back, so it sends the whole squad home.
                EndSummon(SummonExit.Dismissed);
            }
            else if (last)
            {
                LeaveSquad(summon, SummonExit.TurnsSpent);
            }
        }

        /// <summary>
        /// One unit of the replacement leaves. While others stand the fight goes on with them; the
        /// last one out brings the party back (<see cref="EndSummon"/>).
        /// </summary>
        private void LeaveSquad(SummonUnit unit, SummonExit exit)
        {
            bool othersStand = _squad.Any(u => u != null && u != unit && u.IsAlive);
            if (!othersStand)
            {
                EndSummon(exit);
                return;
            }
            QueueParting(unit, exit);
            unit.Stay.Leave(exit);
            _turnManager.RemoveUnit(unit);
            foreach (var enemy in AliveEnemyComponents())
            {
                if (ReferenceEquals(enemy.ChargeTarget, unit))
                {
                    enemy.ChargeTarget = null;
                }
            }
            StartCoroutine(SummonPresenter.Depart(unit, exit == SummonExit.Fell));
        }

        /// <summary>
        /// The summon leaves and the party returns exactly as it left: back on the clock at its
        /// frozen counters, statuses resuming (they only ever tick on a unit's own turn), the dead
        /// still dead. Anything an enemy has aimed at the summon lets go, and re-picks from the
        /// party when it lands.
        /// </summary>
        private void EndSummon(SummonExit exit)
        {
            if (_squad.Count == 0)
            {
                return;
            }
            var leaving = _squad.ToList();
            _squad.Clear();

            foreach (var unit in leaving)
            {
                // A troop that already went home (fell, or its time ran out) left the stage then.
                bool stillHere = !unit.Stay.HasLeft;
                if (stillHere)
                {
                    QueueParting(unit, exit);
                }
                unit.Stay.Leave(exit);
                _turnManager.RemoveUnit(unit);
                foreach (var enemy in AliveEnemyComponents())
                {
                    if (ReferenceEquals(enemy.ChargeTarget, unit))
                    {
                        enemy.ChargeTarget = null;
                    }
                }
                if (stillHere)
                {
                    StartCoroutine(SummonPresenter.Depart(unit, exit == SummonExit.Fell));
                }
            }

            _turnManager.Resume();
            if (CombatStage.HasInstance)
            {
                CombatStage.Instance.RestoreParty();
            }
            // A rider comes back aboard: the stage shows every hero's bar again, so hide hers.
            foreach (var rider in _mounts.Keys)
            {
                SetRiderShown(rider, false);
            }
        }

        /// <summary>A summon with a parting blow (<see cref="SummonSO.PartingAbility"/>) is leaving a fight
        /// that is not over: its blow lands before the turn closes (<see cref="ResolveParting"/>).</summary>
        private void QueueParting(SummonUnit unit, SummonExit exit)
        {
            if (unit != null && exit != SummonExit.Victory && unit.Summon != null
                && unit.Summon.PartingAbility != null && !_parting.Contains(unit))
            {
                _parting.Add(unit);
            }
        }

        /// <summary>
        /// Lands every queued parting blow (Ifrit's Hellfire): cast with the leaving summon as the caster
        /// on the Boar's path - no tags, no combo, no charge - at every enemy standing. Fallen or not, it
        /// casts off the stats it arrived with. Shown without a projectile: the caster is already going.
        /// </summary>
        private IEnumerator ResolveParting()
        {
            while (_parting.Count > 0)
            {
                var unit = _parting[0];
                _parting.RemoveAt(0);
                var enemies = GetAliveEnemies();
                if (enemies.Count == 0)
                {
                    _parting.Clear();
                    yield break;
                }
                var magic = unit.Summon.PartingAbility;
                var targets = magic.TargetType == MagicTargetType.SingleEnemy
                    ? new List<ICombatUnit> { enemies.OrderBy(e => e.Stats.Health).First() }
                    : enemies;
                HoldHealthDisplay();
                var result = _calculator.Execute(new SpellcastAction { Magic = magic, Caster = unit, Targets = targets }, BuffTracker);
                yield return _presenter.Present(result, null, magic);
                HealthDisplay.ReleaseAll();
                ResolveDeaths();
            }
        }

        /// <summary>
        /// The summon's health has run out. <b>The blow that did it is swallowed</b>: it hit the
        /// summon and nothing else, and whatever was left over goes nowhere - which is what makes a
        /// summon out when a boss signature lands a guarantee that the party survives it.
        /// </summary>
        private void ResolveSummonDamaged(SummonUnit summon)
        {
            if (summon == null || summon.Stats.Health > 0)
            {
                return;
            }
            // A summon beside the party simply falls; the party fighting beside it is untouched.
            if (IsBesideParty(summon))
            {
                HealthOps.Set(summon, 0, new HealthSource(null, HealthCause.Clamp), Events);
                EndAlly(summon, SummonExit.Fell);
                return;
            }
            if (!_squad.Contains(summon) || summon.Stay.HasLeft)
            {
                return;
            }
            HealthOps.Set(summon, 0, new HealthSource(null, HealthCause.Clamp), Events);
            LeaveSquad(summon, SummonExit.Fell);
        }

        /// <summary>
        /// One of a party-replacing summon's abilities. The Boar's resolution path, not a hero cast:
        /// no Forge bonus, no tag tracker, no combo detector, no charge - the ratios and the upgrade
        /// nodes are the summon's one progression (§4b). Silence stops it, as it stops any cast.
        /// </summary>
        private IEnumerator ExecuteSummonAbility(SummonUnit caster, MagicSO magic, List<ICombatUnit> targets)
        {
            bool isSignature = caster.Summon != null && ReferenceEquals(magic, caster.Summon.Signature);
            // Its own Attack (SummonSO.AttackAbility) is the basic attack: Silence never stops it.
            bool isAttack = ReferenceEquals(magic, caster.AttackAbility);
            if (!isAttack && BuffTracker.HasStatusEffect(caster, BuffType.Silenced))
            {
                yield break;
            }
            if (isSignature && !caster.Stay.CanUseSignature)
            {
                yield break;
            }

            var live = targets != null ? targets.Where(t => t != null && t.IsAlive).ToList() : new List<ICombatUnit>();
            if (live.Count == 0)
            {
                live = SummonAbilityTargets(magic, caster).Where(t => t != null && t.IsAlive).ToList();
                if (magic.TargetType == MagicTargetType.SingleEnemy && live.Count > 1)
                {
                    live = new List<ICombatUnit> { live[UnityEngine.Random.Range(0, live.Count)] };
                }
            }
            if (live.Count == 0)
            {
                yield break;
            }

            if (isSignature)
            {
                caster.Stay.MarkSignatureUsed();
                CombatFeedback.Instance.Shake(0.22f, 0.35f);
            }

            var action = new SpellcastAction { Magic = magic, Caster = caster, Targets = live };
            // A mech's abilities lay their tags and set off combos like a hero's cast (SummonSO.UsesTags);
            // every other summon's resolve tagless.
            bool tags = caster.Summon != null && caster.Summon.UsesTags;
            HoldHealthDisplay();
            var result = tags
                ? _calculator.Execute(action, BuffTracker, _tagTracker, _comboDetector, 0, 0,
                    MetaProgressManager.Instance.GetComboUpgradeLevel)
                : _calculator.Execute(action, BuffTracker);
            if (tags)
            {
                foreach (var comboKey in result.TriggeredComboKeys)
                {
                    MetaProgressManager.Instance.MarkComboDiscovered(comboKey);
                }
            }
            CombatAudio.Play(isSignature ? CombatSound.BossSignature : CombatSound.MagicCast);
            RecordCastDamageObserved(action);
            yield return _presenter.Present(result, caster, magic);
            HealthDisplay.ReleaseAll();
            ResolveDeaths();
        }

        private List<Enemy> AliveEnemyComponents()
        {
            if (_currentCombatRoom == null)
            {
                return new List<Enemy>();
            }
            return _currentCombatRoom.Enemies.Where(e => e != null && e.IsAlive).ToList();
        }

        /// <summary>Who a summon's effects land on: a whole side or the summoner, or - for a summon aimed
        /// at one ally or one fallen ally - the one picked on the summon list (<paramref name="chosen"/>).</summary>
        private List<ICombatUnit> SummonTargets(MagicTargetType type, ICombatUnit caster, ICombatUnit chosen = null)
        {
            switch (type)
            {
                case MagicTargetType.SingleEnemy:
                    // The enemy picked (Glacius's claw); the weakest standing if the pick is gone.
                    if (chosen != null && chosen.IsAlive && !chosen.IsHero)
                    {
                        return new List<ICombatUnit> { chosen };
                    }
                    var weakest = GetAliveEnemies().OrderBy(e => e.Stats.Health).FirstOrDefault();
                    return weakest != null ? new List<ICombatUnit> { weakest } : new List<ICombatUnit>();
                case MagicTargetType.AllEnemies:
                    return GetAliveEnemies();
                case MagicTargetType.Self:
                    return new List<ICombatUnit> { caster };
                case MagicTargetType.FallenAlly:
                    return chosen != null ? new List<ICombatUnit> { chosen } : new List<ICombatUnit>();
                case MagicTargetType.SingleAlly:
                    return new List<ICombatUnit> { chosen ?? caster };
                default:
                    return GetAliveHeroes(_currentParty);
            }
        }
    }
}
