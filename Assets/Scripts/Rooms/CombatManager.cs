using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public enum CombatOutcome
    {
        Continue,
        EnemyDown,
        Victory,
        PlayerDied
    }

    public enum HeroAction
    {
        None,
        Attack,
        Skip,
        Cast,
        Draw,
        UseItem
    }

    public class CombatResult
    {
        public CombatOutcome Outcome;
        public string Log;
        public int RemainingEnemies;

        // Rewards earned this combat (for the victory summary).
        public List<ItemSO> Loot = new List<ItemSO>();
        public int XpGained;
        public int GoldGained;
        public bool LevelCleared;  // this victory cleared the exit room → level complete
        public bool BossDefeated;  // this victory felled a boss (drives the boss victory copy)
        public bool RunCompleted;  // the boss on the final run level fell → the run is won
    }

    public class CombatManager : SingletonBehaviour<CombatManager>
    {
        [SerializeField] private float _turnDelay = 0.6f;
        [SerializeField] private float _lungeDistance = 0.3f;
        [SerializeField] private float _lungeDuration = 0.12f;

        // Basic-attack critical hits (heroes and enemies).
        // Public so the balance model (Assets/Scripts/Balance) reads the real crit numbers
        // instead of duplicating them and drifting.
        /// <summary>Crit chance at zero Luck. Luck adds to this - see <see cref="CritChanceFor"/>.</summary>
        public const float CritChance = 0.12f;

        /// <summary>Most crit chance Luck can add on top of <see cref="CritChance"/>.</summary>
        public const float MaxLuckCritBonus = 0.30f;

        /// <summary>
        /// Luck at which half of <see cref="MaxLuckCritBonus"/> is reached. Diminishing rather than
        /// linear, using the same stat/(stat+K) shape as the defense curve, so Luck never runs away
        /// and the player only has to learn one curve.
        /// </summary>
        public const float LuckCritConstant = 20f;

        /// <summary>
        /// Crit chance for a specific unit. Everything that rolls or models a crit must go through
        /// this rather than reading <see cref="CritChance"/> directly, or Luck silently does nothing.
        /// </summary>
        public static float CritChanceFor(ICombatUnit unit)
        {
            if (unit == null)
            {
                return CritChance;
            }
            float luck = Mathf.Max(0, unit.GetEffectiveStat(StatType.Luck));
            return CritChance + MaxLuckCritBonus * (luck / (luck + LuckCritConstant));
        }
        public const float CritMultiplier = 1.6f;

        public event Action OnCombatStarted;
        public event Action<string> OnTurnExecuted;
        public event Action<CombatResult> OnCombatEnded;
        public event Action<List<ICombatUnit>> OnTurnOrderChanged;
        public event Action<ICombatUnit> OnHeroTurnStarted;
        public event Action<ICombatUnit, List<MagicSlot>> OnMagicSlotsRequested;
        public event Action<ICombatUnit, List<ICombatUnit>> OnAttackTargetRequested;
        public event Action<ICombatUnit, List<ICombatUnit>> OnDrawTargetRequested;
        public event Action<ICombatUnit, List<ItemSaveData>> OnItemListRequested;
        public event Action<ICombatUnit, List<ICombatUnit>> OnInspectTargetRequested;
        public event Action OnDungeonCleared;

        [SerializeField] private List<MagicComboSO> _cardCombos;

        public bool InCombat { get; private set; }
        public CombatBuffTracker BuffTracker { get; private set; }

        private TurnManager _turnManager = new TurnManager();
        private HeroAction _pendingAction = HeroAction.None;
        private SpellcastAction _pendingCastAction;
        private int _pendingCastSlot;
        private Enemy _pendingDrawSource;
        private MagicSO _pendingDrawMagic;
        private int _pendingDrawCharges;
        private int _pendingDrawSlot;
        private ItemSO _pendingUseItem;
        private ICombatUnit _pendingUseItemTarget;
        private ICombatUnit _pendingAttackTarget;
        private string _lastTurnLog;
        private Room _currentCombatRoom;
        private Party _currentParty;

        // Rewards accumulated during the current combat (surfaced in the victory summary).
        private readonly List<ItemSO> _combatLoot = new List<ItemSO>();
        private int _combatXp;
        private int _combatGold;
        private Room _lastVictoryRoom;
        private bool _currentCombatHadBoss;
        private MagicTagTracker _tagTracker;
        private ComboDetector _comboDetector;
        private EffectResolver _calculator = new EffectResolver();
        private EffectPresenter _presenter = new EffectPresenter();

        public void SubmitHeroAction(HeroAction action)
        {
            _pendingAction = action;
        }

        public void RequestMagicSlots(ICombatUnit hero, List<MagicSlot> slots)
        {
            OnMagicSlotsRequested?.Invoke(hero, slots);
        }

        public void RequestAttackTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            OnAttackTargetRequested?.Invoke(hero, enemies);
        }

        /// <summary>Raises the draw target picker with the enemies that have magic to draw.</summary>
        public void RequestDrawTargets(ICombatUnit hero, List<ICombatUnit> drawableEnemies)
        {
            OnDrawTargetRequested?.Invoke(hero, drawableEnemies);
        }

        /// <summary>Raises the consumable picker with the party's carried consumable stacks.</summary>
        public void RequestItemList(ICombatUnit hero, List<ItemSaveData> consumables)
        {
            OnItemListRequested?.Invoke(hero, consumables);
        }

        /// <summary>
        /// Raises the Inspect target picker. Inspect is the one hero command that is <b>free</b>:
        /// it never submits an action, so the turn is still the player's when the window closes. It
        /// reads back knowledge the party already earned in the field rather than granting any, so
        /// charging a turn for it would be charging for the UI.
        /// </summary>
        public void RequestInspectTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            OnInspectTargetRequested?.Invoke(hero, enemies);
        }

        /// <summary>
        /// The action this enemy will take next, for the intent icon on its HP bar — or null when the
        /// answer is not determined yet. Pure, so it has no side effects.
        ///
        /// <para>It reports **only** what is certain (see
        /// <see cref="EnemyActionPlanner.PredictCertain"/>): a telegraph already in flight, or a
        /// single ungated action that is the only thing the enemy can do. Authored behaviours are
        /// probabilistic, and an intent icon that guesses wrong teaches the player to distrust the
        /// telegraph, which is the one tell the fight depends on.</para>
        /// </summary>
        public EnemyActionType? PredictIntent(Enemy enemy)
        {
            if (!InCombat || enemy == null || !enemy.IsAlive || _currentParty == null)
            {
                return null;
            }

            var behavior = enemy.Behavior != null
                ? enemy.Behavior
                : EnemyBehaviorSO.BuiltInPreset(enemy.Archetype);

            var context = new EnemyCombatContext
            {
                Heroes = GetAliveHeroes(_currentParty),
                Allies = GetAliveEnemies().Where(u => !ReferenceEquals(u, enemy)).ToList(),
                BuffTracker = BuffTracker,
                ChargingEntryIndex = enemy.ChargingEntryIndex,
                SelfTurnCount = enemy.TurnsTaken,
                DrawableMagics = enemy.DrawableMagics
            };
            return EnemyActionPlanner.PredictCertain(enemy, context, behavior);
        }

        /// <summary>Enemies in the current combat room that have a non-empty Draw list.</summary>
        public List<ICombatUnit> GetDrawableEnemies()
        {
            return GetAliveEnemies()
                .Where(u => u is Enemy e && e.DrawableMagics != null && e.DrawableMagics.Count > 0)
                .ToList();
        }

        /// <summary>Submits a basic attack against the chosen target for the current hero turn.</summary>
        public void SubmitAttackAction(ICombatUnit target)
        {
            _pendingAttackTarget = target;
            _pendingAction = HeroAction.Attack;
        }

        /// <summary>Submits casting the magic in <paramref name="slotIndex"/> at the chosen targets.</summary>
        public void SubmitCastAction(MagicSO magic, int slotIndex, ICombatUnit caster, List<ICombatUnit> targets)
        {
            _pendingCastAction = new SpellcastAction
            {
                Magic = magic,
                Caster = caster,
                Targets = targets
            };
            _pendingCastSlot = slotIndex;
            _pendingAction = HeroAction.Cast;
        }

        /// <summary>Submits drawing <paramref name="magic"/> from <paramref name="source"/> into the chosen slot.</summary>
        public void SubmitDrawAction(Enemy source, MagicSO magic, int charges, int slotIndex)
        {
            _pendingDrawSource = source;
            _pendingDrawMagic = magic;
            _pendingDrawCharges = charges;
            _pendingDrawSlot = slotIndex;
            _pendingAction = HeroAction.Draw;
        }

        /// <summary>Submits using a consumable <paramref name="item"/> on <paramref name="target"/>.</summary>
        public void SubmitUseItemAction(ItemSO item, ICombatUnit target)
        {
            _pendingUseItem = item;
            _pendingUseItemTarget = target;
            _pendingAction = HeroAction.UseItem;
        }

        public List<ICombatUnit> GetAliveEnemies()
        {
            if (_currentCombatRoom == null)
            {
                return new List<ICombatUnit>();
            }
            return _currentCombatRoom.Enemies
                .Where(e => e != null && e.IsAlive)
                .Cast<ICombatUnit>()
                .ToList();
        }

        public List<ICombatUnit> GetAliveHeroes(Party party)
        {
            return party.Heroes
                .Where(h => h != null && h.IsAlive)
                .Cast<ICombatUnit>()
                .ToList();
        }

        public void StartCombat(Party party, Room room)
        {
            if (InCombat)
            {
                return;
            }

            StartCoroutine(RunCombat(party, room));
        }

        private IEnumerator RunCombat(Party party, Room room)
        {
            InCombat = true;
            _currentCombatRoom = room;
            _currentParty = party;
            _combatLoot.Clear();
            _combatXp = 0;
            _combatGold = 0;
            _currentCombatHadBoss = room.Enemies.Any(e => e != null && e.IsBoss);
            BuffTracker = new CombatBuffTracker();
            _turnManager.SetBuffTracker(BuffTracker);
            _tagTracker = new MagicTagTracker();
            // Prefer the shared combo catalog (single source of truth, also used by the hub
            // Forge); fall back to the scene-serialized list if no catalog is present.
            var combos = MagicComboCatalog.HasInstance
                ? new List<MagicComboSO>(MagicComboCatalog.Instance.AllCombos)
                : _cardCombos;
            _comboDetector = new ComboDetector(combos);

            // Charges are deliberately NOT refilled here. They are a run resource: drawn on one
            // floor, spent across the next, and topped up only by drawing again. Refilling per fight
            // made magic infinite - a dozen casts and two free Heals in every room, which is why a
            // whole run could be cleared without the party's health ever trending down. See
            // EquippedMagicState.RefillsOnLevelStart.

            // Seed the fresh buff tracker with anything room events hung on the party for this
            // level. The tracker is rebuilt per fight and ticks per turn, so a level-scoped curse
            // has to be re-applied here or it would only ever affect the fight it was picked up in
            // - and there is no fight when it is picked up.
            if (DungeonManager.HasInstance && DungeonManager.Instance.Afflictions != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null && hero.IsAlive)
                    {
                        DungeonManager.Instance.Afflictions.SeedCombat(hero.HeroKey, hero, BuffTracker);
                    }
                }
            }

            OnCombatStarted?.Invoke();

            // Raise the FF-style battle stage: heroes left, enemies right, over a background
            // that hides the dungeon. Must run before EnsureHealthBars so the bars anchor at
            // the battle positions. One frame lets the formation render before turn 1.
            CombatStage.Instance.Begin(party, room);
            yield return null;

            // Build the unit list
            var units = new List<ICombatUnit>();
            foreach (var hero in party.Heroes)
            {
                if (hero.IsAlive)
                {
                    units.Add(hero);
                }
            }
            foreach (var enemy in room.Enemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    // Fresh per-combat runtime state (cadence + charge) for behaviors.
                    enemy.TurnsTaken = 0;
                    enemy.ClearCharge();
                    units.Add(enemy);
                    RecordEnemySeen(enemy);
                }
            }

            _turnManager.Initialize(units);
            EnsureHealthBars(units);
            BroadcastTurnOrder();

            var fullLog = "";

            // Combat loop
            while (HasAliveHeroes(party) && HasAliveEnemies(room))
            {
                var unit = _turnManager.GetNextUnit();
                if (unit == null)
                {
                    break;
                }

                if (!unit.IsAlive)
                {
                    continue;
                }

                // Point the on-field turn marker at whoever is acting.
                TurnIndicator.Instance.SetTarget(unit);

                string skipMessage = GetTurnSkipMessage(unit);
                if (skipMessage != null)
                {
                    _lastTurnLog = skipMessage;
                    BuffTracker.TickBuffs(unit);
                    _tagTracker.TickTags(unit);
                    fullLog += _lastTurnLog + "\n";
                    OnTurnExecuted?.Invoke(_lastTurnLog);
                    BroadcastTurnOrder();
                    continue;
                }

                if (unit.IsHero)
                {
                    // Wait for player input
                    _pendingAction = HeroAction.None;
                    _pendingCastAction = null;
                    _pendingAttackTarget = null;
                    _pendingDrawSource = null;
                    _pendingDrawMagic = null;
                    _pendingUseItem = null;
                    _pendingUseItemTarget = null;
                    OnHeroTurnStarted?.Invoke(unit);

                    while (_pendingAction == HeroAction.None)
                    {
                        yield return null;
                    }

                    if (_pendingAction == HeroAction.Attack)
                    {
                        yield return ExecuteHeroTurn(unit, room);
                    }
                    else if (_pendingAction == HeroAction.Cast && _pendingCastAction != null)
                    {
                        yield return ExecuteCastAction(_pendingCastAction, _pendingCastSlot, room);
                    }
                    else if (_pendingAction == HeroAction.Draw && _pendingDrawMagic != null)
                    {
                        yield return ExecuteDrawAction(unit, _pendingDrawSource, _pendingDrawMagic, _pendingDrawCharges, _pendingDrawSlot);
                    }
                    else if (_pendingAction == HeroAction.UseItem && _pendingUseItem != null)
                    {
                        yield return ExecuteUseItemAction(unit, _pendingUseItem, _pendingUseItemTarget);
                    }
                    else
                    {
                        _lastTurnLog = $"{unit.DisplayName} skips their turn.";
                    }
                }
                else
                {
                    yield return new WaitForSeconds(_turnDelay);
                    yield return ExecuteEnemyTurn(unit, party);
                }

                BuffTracker.TickBuffs(unit);
                _tagTracker.TickTags(unit);
                fullLog += _lastTurnLog + "\n";
                OnTurnExecuted?.Invoke(_lastTurnLog);
                BroadcastTurnOrder();
            }

            // Clear turn order display + the on-field turn marker
            OnTurnOrderChanged?.Invoke(new List<ICombatUnit>());
            TurnIndicator.Instance.Clear();

            // Determine outcome
            CombatOutcome outcome;
            if (!HasAliveHeroes(party))
            {
                outcome = CombatOutcome.PlayerDied;
                fullLog += "\nYour party has been defeated!";
                CombatAudio.Play(CombatSound.Defeat);
                // Somber tint that lingers under the death screen (approximates a desaturate).
                ScreenFade.Instance.FadeTo(new Color(0.06f, 0f, 0.02f), 0.55f, 0.7f);

                // Tear the stage down (unfreeze camera, drop background) before the death screen.
                CombatStage.Instance.End(restoreEnemyPositions: false);
            }
            else
            {
                outcome = CombatOutcome.Victory;
                fullLog += "\nAll enemies defeated!";
                CombatAudio.Play(CombatSound.Victory);
                // A quick warm flash to punctuate the win before the summary appears.
                ScreenFade.Instance.Flash(new Color(1f, 0.92f, 0.55f), 0.5f, 0.06f, 0.4f);

                // Keep the battle stage up so the victory summary shows over it; it is torn down
                // (and doors enabled / level completed) when the summary is dismissed — see
                // FinishVictory, called by RoomActionUI's Continue button.
                _lastVictoryRoom = room;
            }

            // Save dungeon state (not party — deferred until level completion)
            if (DungeonSaveManager.Instance != null)
            {
                DungeonSaveManager.Instance.Save(party.CurrentRoom);
            }

            BuffTracker.Clear();
            _tagTracker.Clear();
            _currentCombatRoom = null;
            InCombat = false;

            bool levelCleared = outcome == CombatOutcome.Victory && room.IsExit && !HasAliveEnemies(room);
            bool bossDefeated = outcome == CombatOutcome.Victory && _currentCombatHadBoss;
            var result = new CombatResult
            {
                Outcome = outcome,
                Log = fullLog,
                RemainingEnemies = room.Enemies.Count(e => e != null && e.IsAlive),
                Loot = new List<ItemSO>(_combatLoot),
                XpGained = _combatXp,
                GoldGained = _combatGold,
                LevelCleared = levelCleared,
                BossDefeated = bossDefeated,
                RunCompleted = levelCleared && DungeonManager.IsFinalRunLevel
            };

            OnCombatEnded?.Invoke(result);
            // Stage teardown + OnDungeonCleared are deferred to FinishVictory (the summary's Continue).
        }

        /// <summary>
        /// Called when the player dismisses the victory summary: lowers the battle stage and re-opens
        /// the room's doors.
        ///
        /// <para>It no longer completes the level, even after clearing the exit room - that is the
        /// Descend button's job. Doors are re-enabled unconditionally, which also un-seals a boss
        /// room (<c>DisableAllDoors</c>) so the player can go back for anything they left.</para>
        /// </summary>
        public void FinishVictory()
        {
            CombatStage.Instance.End(restoreEnemyPositions: false);
            _lastVictoryRoom?.EnableAllDoors();
        }

        private IEnumerator ExecuteCastAction(SpellcastAction castAction, int slotIndex, Room room)
        {
            // Use Instance (auto-creates + loads Meta.json) rather than HasInstance: the
            // manager may not exist yet mid-combat, and we must still apply upgrades and
            // record combo discovery.
            var meta = MetaProgressManager.Instance;
            int powerBonus = castAction.Magic != null ? meta.GetMagicPowerBonus(castAction.Magic.Key) : 0;
            int magicLevel = castAction.Magic != null ? meta.GetMagicUpgradeLevel(castAction.Magic.Key) : 0;
            Func<string, int> comboLevelLookup = meta.GetComboUpgradeLevel;

            var result = _calculator.Execute(
                castAction, BuffTracker, _tagTracker, _comboDetector, powerBonus, magicLevel, comboLevelLookup);
            _lastTurnLog = result.BuildLog(castAction);
            CombatAudio.Play(CombatSound.MagicCast);

            // Record any triggered combos as discovered (permanent, survives death).
            foreach (var comboKey in result.TriggeredComboKeys)
            {
                meta.MarkComboDiscovered(comboKey);
            }

            // ...and what the cast taught the player about the enemies it hit. Read off the magic's
            // live Damage effects rather than the result entries, because an entry carries the
            // number and the popup word but not the element that produced them.
            RecordCastDamageObserved(castAction, magicLevel);

            yield return _presenter.Present(
                result,
                castAction.Caster,
                castAction.Magic
            );

            // Combo flourish: a camera punch + brief hit-stop so a triggered combo lands with weight
            // (the combo name already floats up in orange from the resolver).
            if (!string.IsNullOrEmpty(result.ComboName))
            {
                CombatFeedback.Instance.Shake(0.2f, 0.28f);
                yield return new WaitForSecondsRealtime(0.12f);
            }

            // Spend a charge from the cast slot
            var hero = castAction.Caster as Hero;
            if (hero != null && DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                DungeonManager.Instance.MagicState.TryCast(hero.HeroKey, slotIndex);
            }

            // Check for enemy deaths caused by the cast
            var deadEnemies = room.Enemies.Where(e => e != null && !e.IsAlive).ToList();
            foreach (var dead in deadEnemies)
            {
                _lastTurnLog += $" {dead.DisplayName} defeated!";
                HandleEnemyDeath(dead, room);
            }
        }

        private IEnumerator ExecuteDrawAction(ICombatUnit heroUnit, Enemy source, MagicSO magic, int charges, int slotIndex)
        {
            var hero = heroUnit as Hero;
            if (hero == null || magic == null)
            {
                _lastTurnLog = $"{heroUnit.DisplayName} finds no magic to draw.";
                yield break;
            }

            if (DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                DungeonManager.Instance.MagicState.DrawInto(hero.HeroKey, slotIndex, magic, charges);
            }

            // Record the drawn magic as discovered (permanent, survives death). Use Instance
            // (auto-creates + loads Meta.json) — the manager may not exist yet mid-combat.
            MetaProgressManager.Instance.MarkMagicDiscovered(magic.Key);

            string sourceName = source != null ? source.DisplayName : "the enemy";
            CombatAudio.Play(CombatSound.Draw);
            ShowFloatingLabel(heroUnit.Transform.position, $"Draw {magic.DisplayName}!", new Color(0.5f, 0.8f, 1f));
            _lastTurnLog = $"{heroUnit.DisplayName} draws {magic.DisplayName} from {sourceName}!";
            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteUseItemAction(ICombatUnit heroUnit, ItemSO item, ICombatUnit target)
        {
            if (item == null || item.Category != ItemCategory.Consumable)
            {
                _lastTurnLog = $"{heroUnit.DisplayName} has nothing to use.";
                yield break;
            }

            // Default a missing/fallen target to the user themselves.
            if (target == null || !target.IsAlive)
            {
                target = heroUnit;
            }

            // Spend the item first; if the party isn't actually carrying it, the turn is a no-op.
            if (!InventoryManager.Instance.TryConsume(item.Key))
            {
                _lastTurnLog = $"{heroUnit.DisplayName} has no {item.DisplayName} left.";
                yield break;
            }

            switch (item.ConsumableEffect)
            {
                case ConsumableEffectType.RestoreHealth:
                    int max = target.GetEffectiveStat(StatType.MaxHealth);
                    int before = target.Stats.Health;
                    target.Stats.Health = Mathf.Min(target.Stats.Health + item.ConsumableAmount, max);
                    int healed = target.Stats.Health - before;
                    CombatAudio.Play(CombatSound.ItemUse);
                    CombatAudio.Play(CombatSound.Heal);
                    ShowDamageText(target.Transform.position, healed, Color.green);
                    _lastTurnLog = $"{heroUnit.DisplayName} uses {item.DisplayName} on {target.DisplayName}, restoring {healed} HP.";
                    break;
                default:
                    _lastTurnLog = $"{heroUnit.DisplayName} uses {item.DisplayName}.";
                    break;
            }

            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteHeroTurn(ICombatUnit hero, Room room)
        {
            // Use the player's chosen target; fall back to a random enemy if it's
            // missing or already dead (e.g. single-enemy auto-target edge cases).
            ICombatUnit target = _pendingAttackTarget;
            if (target == null || !target.IsAlive)
            {
                target = GetRandomAliveEnemy(room);
            }

            if (target == null)
            {
                _lastTurnLog = $"{hero.DisplayName} has no target.";
                yield break;
            }

            yield return ExecuteAttack(hero, target, Vector3.right, Color.white);

            if (!target.IsAlive)
            {
                _lastTurnLog += $" {target.DisplayName} defeated!";
                HandleEnemyDeath(target as Enemy, room);
            }
        }

        private IEnumerator ExecuteEnemyTurn(ICombatUnit enemyUnit, Party party)
        {
            var enemy = enemyUnit as Enemy;
            var behavior = enemy != null && enemy.Behavior != null
                ? enemy.Behavior
                : EnemyBehaviorSO.BuiltInPreset(enemy != null ? enemy.Archetype : EnemyArchetype.Aggressor);

            var context = new EnemyCombatContext
            {
                Heroes = GetAliveHeroes(party),
                Allies = GetAliveEnemies().Where(u => u != enemyUnit).ToList(),
                BuffTracker = BuffTracker,
                ChargingEntryIndex = enemy != null ? enemy.ChargingEntryIndex : EnemyActionPlanner.NoCharge,
                SelfTurnCount = enemy != null ? enemy.TurnsTaken : 0,
                DrawableMagics = enemy != null ? enemy.DrawableMagics : null
            };

            // One authored list decides everything: gate, then priority, then weight. Casting is an
            // entry in that list rather than a pre-roll around it, so the whole repertoire - swings,
            // charges, heals, debuffs, the boss signature and spells - is chosen in one place.
            var decision = EnemyActionPlanner.Plan(
                enemyUnit, context, behavior, EnemyPlanRolls.Random(behavior.Actions.Count));

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
                    yield return ExecuteEnemyDebuff(enemyUnit, decision.Target, decision.DebuffStat, decision.Amount, decision.Duration);
                    break;
                default:
                    string verb = decision.Multiplier > 1f ? "strikes savagely at" : "attacks";
                    yield return ExecuteEnemyBasicAttack(enemyUnit, decision.Target, party, decision.Multiplier, verb);
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
        /// spent - <see cref="DrawableMagicEntry.Charges"/> is the player's Draw grant, and enemies
        /// cast freely. No upgrade bonus or upgrade level applies, so an enemy casts the base version
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
            var result = _calculator.Execute(
                castAction, BuffTracker, null, null, 0, 0, null, powerScale);

            _lastTurnLog = result.BuildLog(castAction);
            CombatAudio.Play(CombatSound.MagicCast);

            yield return _presenter.Present(
                result,
                castAction.Caster,
                castAction.Magic
            );

            foreach (var target in targets)
            {
                if (target is Hero)
                {
                    ResolveHeroDamaged(target);
                }
            }
        }

        private IEnumerator ExecuteEnemyBasicAttack(ICombatUnit enemyUnit, ICombatUnit target, Party party, float multiplier = 1f, string verb = "attacks")
        {
            if (target == null || !target.IsAlive)
            {
                target = GetRandomAliveHero(party);
            }

            if (target == null)
            {
                _lastTurnLog = $"{enemyUnit.DisplayName} has no target.";
                yield break;
            }

            yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red, multiplier, verb);
            ResolveHeroDamaged(target);
        }

        private IEnumerator ExecuteEnemyCharge(Enemy enemy, ICombatUnit target, int entryIndex)
        {
            if (enemy == null)
            {
                yield break;
            }

            enemy.BeginCharge(entryIndex, target);
            SetChargingVisual(enemy, true);
            ShowFloatingLabel(enemy.Transform.position, "Charging!", new Color(1f, 0.5f, 0.2f));
            _lastTurnLog = $"{enemy.DisplayName} is winding up a heavy blow!";
            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteEnemyHeavyAttack(ICombatUnit enemyUnit, Enemy enemy, Party party, float multiplier)
        {
            ICombatUnit target = enemy != null ? enemy.ChargeTarget : null;
            if (target == null || !target.IsAlive)
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
                _lastTurnLog = $"{enemyUnit.DisplayName} has no target.";
                yield break;
            }

            yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red, multiplier, "unleashes a heavy blow on");
            ResolveHeroDamaged(target);
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
            ShowFloatingLabel(enemy.Transform.position, "Channeling!", new Color(1f, 0.35f, 0.35f));

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

            _lastTurnLog = $"{enemy.DisplayName} is channeling a devastating attack!";
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
                _lastTurnLog = $"{enemyUnit.DisplayName} has no target.";
                yield break;
            }

            _lastTurnLog = $"{enemyUnit.DisplayName} unleashes a devastating blow on the whole party!";
            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                {
                    continue;
                }
                yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red, multiplier, "smashes");
                ResolveHeroDamaged(target);
            }
        }

        private IEnumerator ExecuteEnemyHeal(ICombatUnit enemyUnit, ICombatUnit target, int amount)
        {
            if (target == null || !target.IsAlive)
            {
                _lastTurnLog = $"{enemyUnit.DisplayName} has no one to heal.";
                yield break;
            }

            int before = target.Stats.Health;
            target.Stats.Health = Mathf.Min(
                target.Stats.Health + amount, target.GetEffectiveStat(StatType.MaxHealth));
            int healed = target.Stats.Health - before;

            CombatAudio.Play(CombatSound.Heal);
            ShowDamageText(target.Transform.position, healed, Color.green);
            _lastTurnLog = $"{enemyUnit.DisplayName} heals {target.DisplayName} for {healed}.";
            yield return new WaitForSeconds(_turnDelay);
        }

        private IEnumerator ExecuteEnemyDebuff(ICombatUnit enemyUnit, ICombatUnit target, StatType stat, int amount, int duration)
        {
            if (target == null || !target.IsAlive)
            {
                _lastTurnLog = $"{enemyUnit.DisplayName} has no target.";
                yield break;
            }

            BuffTracker.ApplyBuff(target, stat, -amount, duration);
            ShowFloatingLabel(target.Transform.position, $"-{amount} {stat}", new Color(0.7f, 0.4f, 1f));
            _lastTurnLog = $"{enemyUnit.DisplayName} weakens {target.DisplayName}'s {stat}!";
            yield return new WaitForSeconds(_turnDelay);
        }

        private void ResolveHeroDamaged(ICombatUnit target)
        {
            if (!target.IsAlive)
            {
                _lastTurnLog += $" {target.DisplayName} has fallen!";
                HandleHeroDeath(target as Hero);
                _turnManager.RemoveUnit(target);
            }
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

        private IEnumerator ExecuteAttack(ICombatUnit attacker, ICombatUnit target, Vector3 lungeDirection, Color damageColor, float damageMultiplier = 1f, string verb = "attacks")
        {
            CombatAudio.Play(CombatSound.MeleeSwing);
            yield return LungeAnimation(attacker.Transform, lungeDirection);

            // Buff the stat this attacker actually swings with, not Strength unconditionally.
            int attackBonus = BuffTracker.GetBuffAmount(attacker, attacker.AttackStat);
            int defenseBonus = BuffTracker.GetBuffAmount(target, StatType.Endurance);
            int rawAttack = Mathf.RoundToInt((attacker.GetEffectiveAttackPower() + attackBonus) * damageMultiplier);
            int defense = target.GetEffectiveStat(StatType.Endurance) + defenseBonus;

            // Physical attacks carry the attacker's element, so elemental resistance applies to them too.
            // Normal is the default on both sides and bypasses the elemental layer entirely.
            var damageType = attacker.AttackDamageType;
            float resistanceBonus = BuffTracker.GetResistanceBonus(target, damageType);
            int dmg = DamageCalculator.Calculate(
                rawAttack, defense, damageType, target.Resistances, resistanceBonus);

            // Critical hit: a chance for a harder blow, called out with a gold popup + bigger number.
            bool crit = dmg > 0 && UnityEngine.Random.Range(0f, 1f) < CritChanceFor(attacker);
            if (crit)
            {
                dmg = Mathf.Max(dmg + 1, Mathf.RoundToInt(dmg * CritMultiplier));
            }

            if (dmg < 0)
            {
                // Absorbed: resistance above 100% turns the hit into healing. Clamp to the target's
                // maximum — without this an absorbing unit heals past full and the popup reads "-7".
                int absorbed = Mathf.Min(
                    -dmg, Mathf.Max(0, target.GetEffectiveStat(StatType.MaxHealth) - target.Stats.Health));
                target.Stats.Health += absorbed;
                dmg = -absorbed;
            }
            else
            {
                target.Stats.Health -= dmg;
            }

            // Impact juice: flash + damage-scaled shake (extra punch on heavy/crit blows) + hit-stop.
            float punch = (damageMultiplier > 1f ? 1.6f : 1f) * (crit ? 1.4f : 1f);
            CombatFeedback.Instance.PlayImpact(target, Mathf.Abs(dmg), punch);
            CombatAudio.Play(CombatSound.Impact, damageMultiplier > 1f ? 1f : 0.9f);
            ShowDamageText(
                target.Transform.position,
                Mathf.Abs(dmg),
                dmg < 0 ? Color.green : damageColor,
                crit ? 0.24f : 0.15f);
            if (crit)
            {
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.45f, 0f), "CRIT!", new Color(1f, 0.82f, 0.2f), 0.16f);
            }
            ShowEffectiveness(target, damageType, resistanceBonus);

            // The player just watched this land, so the bestiary learns it: which element was tried
            // on the enemy, and - when the enemy is the one swinging - what it attacks with.
            RecordDamageObserved(target, damageType);
            RecordAttackTypeObserved(attacker);

            yield return new WaitForSecondsRealtime(crit ? 0.075f : 0.045f);

            _lastTurnLog = dmg < 0
                ? $"{attacker.DisplayName} {verb} {target.DisplayName}, who absorbs {-dmg} health!"
                : $"{attacker.DisplayName} {verb} {target.DisplayName} for {dmg} damage{(crit ? " (CRIT!)" : "")}.";
        }

        private IEnumerator LungeAnimation(Transform unit, Vector3 direction)
        {
            var startPos = unit.position;
            var lungePos = startPos + direction * _lungeDistance;

            // Lunge forward
            float elapsed = 0f;
            while (elapsed < _lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _lungeDuration;
                unit.position = Vector3.Lerp(startPos, lungePos, t);
                yield return null;
            }

            // Snap back
            elapsed = 0f;
            while (elapsed < _lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _lungeDuration;
                unit.position = Vector3.Lerp(lungePos, startPos, t);
                yield return null;
            }

            unit.position = startPos;
        }

        private string GetTurnSkipMessage(ICombatUnit unit)
        {
            foreach (var statusEffect in BuffTracker.GetActiveStatusEffects(unit))
            {
                var handler = BuffHandlerRegistry.Get(statusEffect);
                if (handler != null && handler.SkipsTurn)
                {
                    return handler.GetSkipTurnMessage(unit);
                }
            }

            return null;
        }

        private void BroadcastTurnOrder()
        {
            var order = _turnManager.GetTurnOrder(10);
            OnTurnOrderChanged?.Invoke(order);
        }

        /// <summary>Ensures every combat unit has an HP bar (idempotent — bars self-manage visibility).</summary>
        private void EnsureHealthBars(List<ICombatUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit?.Transform == null)
                {
                    continue;
                }
                var go = unit.Transform.gameObject;
                if (go.GetComponent<UnitHealthBar>() == null)
                {
                    go.AddComponent<UnitHealthBar>();
                }
                if (go.GetComponent<CombatIdleMotion>() == null)
                {
                    go.AddComponent<CombatIdleMotion>();
                }
            }
        }

        // ============================================================
        //  BESTIARY (what the player learns by fighting)
        // ============================================================
        //
        // Knowledge is recorded from the combat path, at the moment the player sees the thing, so
        // an Inspect window and the hub bestiary only ever show what was actually observed. All of
        // these use MetaProgressManager.Instance (auto-creates + loads Meta.json) for the same
        // reason ExecuteCastAction does: the manager may not exist yet mid-combat. Each mutator is
        // idempotent and persists only on a real change, so calling them per hit is cheap.

        /// <summary>The bestiary key for a combat unit, or null when it is not a keyed enemy.</summary>
        private static string BestiaryKeyOf(ICombatUnit unit)
        {
            var enemy = unit as Enemy;
            return enemy != null && enemy.Definition != null ? enemy.Definition.SaveKey : null;
        }

        /// <summary>Meeting an enemy is what puts it in the bestiary at all.</summary>
        private static void RecordEnemySeen(Enemy enemy)
        {
            if (enemy != null && enemy.Definition != null)
            {
                MetaProgressManager.Instance.MarkEnemySeen(enemy.Definition.SaveKey);
            }
        }

        /// <summary>
        /// A hit of this element landed on the enemy, so its resistance to that element is now
        /// observed. Recorded for <b>every</b> type including Normal - "Lightning does nothing
        /// special to it" is a real finding, and gating on a non-Normal classification (as the
        /// original plan did) would leave every neutral element permanently unreadable.
        /// </summary>
        private static void RecordDamageObserved(ICombatUnit target, DamageType type)
        {
            string key = BestiaryKeyOf(target);
            if (key != null)
            {
                MetaProgressManager.Instance.MarkResistanceObserved(key, type);
            }
        }

        /// <summary>Watching an enemy swing reveals the element it swings with.</summary>
        private static void RecordAttackTypeObserved(ICombatUnit attacker)
        {
            string key = BestiaryKeyOf(attacker);
            if (key != null)
            {
                MetaProgressManager.Instance.MarkAttackTypeObserved(key);
            }
        }

        /// <summary>Every element the cast actually delivered, against every enemy it landed on.</summary>
        private static void RecordCastDamageObserved(SpellcastAction castAction, int magicLevel)
        {
            if (castAction == null || castAction.Magic == null ||
                castAction.Magic.Effects == null || castAction.Targets == null)
            {
                return;
            }

            foreach (var effect in castAction.Magic.Effects)
            {
                // Skip effects the magic has not unlocked yet - they never fired, so they taught
                // the player nothing.
                if (effect == null || effect.EffectType != SpellEffectType.Damage ||
                    effect.UnlockLevel > magicLevel)
                {
                    continue;
                }

                foreach (var target in castAction.Targets)
                {
                    RecordDamageObserved(target, effect.DamageType);
                }
            }
        }

        private static void RecordEnemyKilled(Enemy enemy)
        {
            if (enemy != null && enemy.Definition != null)
            {
                MetaProgressManager.Instance.MarkEnemyKilled(enemy.Definition.SaveKey);
            }
        }

        private static void RecordLootObserved(Enemy enemy, ItemSO loot)
        {
            if (enemy != null && enemy.Definition != null && loot != null)
            {
                MetaProgressManager.Instance.MarkLootObserved(enemy.Definition.SaveKey, loot.Key);
            }
        }

        private void ShowDamageText(Vector3 position, int damage, Color color, float scale = 0.15f)
        {
            ShowFloatingLabel(position, damage.ToString(), color, scale);
        }

        /// <summary>Popup for a resistance outcome (Weak!/Resisted/Immune/Absorbed); no-op if Normal.</summary>
        private void ShowEffectiveness(ICombatUnit target, DamageType type, float resistanceBonus = 0f)
        {
            if (target == null || target.Transform == null)
            {
                return;
            }

            string word = null;
            Color color = Color.white;
            switch (DamageCalculator.Classify(type, target.Resistances, resistanceBonus))
            {
                case DamageEffectiveness.Weak:
                    word = "Weak!"; color = new Color(1f, 0.85f, 0.2f); break;
                case DamageEffectiveness.Resisted:
                    word = "Resisted"; color = new Color(0.6f, 0.7f, 0.85f); break;
                case DamageEffectiveness.Immune:
                    word = "Immune"; color = new Color(0.78f, 0.78f, 0.82f); break;
                case DamageEffectiveness.Absorbed:
                    word = "Absorbed"; color = new Color(0.4f, 0.95f, 0.5f); break;
            }

            if (word != null)
            {
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.45f, 0f), word, color, 0.13f);
            }
        }

        private void ShowFloatingLabel(Vector3 position, string text, Color color, float scale = 0.15f)
        {
            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    position,
                    text,
                    color,
                    1f,     // fadeSpeed — fade out over ~1 second
                    0.8f,   // fadeRange — gentle drift
                    scale,
                    TextFadeMode.FadeUp);
            }
        }

        private void HandleEnemyDeath(Enemy enemy, Room room)
        {
            if (enemy == null)
            {
                return;
            }

            // Kill rewards: XP (awarded now), gold (accumulated, banked on clear).
            // Through the enemy, not the definition: a level scales its rewards alongside its stats.
            int xp = enemy.XpReward;
            int gold = enemy.GoldReward;
            if (xp > 0)
            {
                _combatXp += xp;
                _currentParty?.DistributeXp(xp);
            }
            if (gold > 0)
            {
                _combatGold += gold;
                MetaProgressManager.Instance.AddPendingGold(gold);
            }

            // Loot: roll once (rarity + run-depth scaled) and only surface it in the victory
            // summary if it actually dropped into the bag.
            var loot = enemy.LootItem;
            if (loot != null &&
                LootRoller.ShouldDrop(loot, DungeonManager.RunLevelIndex, UnityEngine.Random.Range(0f, 1f)))
            {
                InventoryManager.Instance.AddItem(loot);
                Debug.Log($"Item dropped: {loot.DisplayName} ({loot.Key})");
                _combatLoot.Add(loot);
                RecordLootObserved(enemy, loot);
            }

            RecordEnemyKilled(enemy);
            _turnManager.RemoveUnit(enemy);
            room.Enemies.Remove(enemy);
            // Removed from combat immediately; the object lingers only for its pop/fade.
            CombatAudio.Play(CombatSound.EnemyDeath);
            CombatFeedback.Instance.KillWithEffect(enemy.gameObject);
        }

        private void HandleHeroDeath(Hero hero)
        {
            if (hero == null)
            {
                return;
            }

            // Disable the hero's sprite to show they've fallen
            var sr = hero.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;
            }
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

        private Hero GetRandomAliveHero(Party party)
        {
            var alive = party.Heroes.Where(h => h != null && h.IsAlive).ToList();
            if (alive.Count == 0)
            {
                return null;
            }
            return alive[UnityEngine.Random.Range(0, alive.Count)];
        }

        private bool HasAliveHeroes(Party party)
        {
            return party.Heroes.Any(h => h != null && h.IsAlive);
        }

        private bool HasAliveEnemies(Room room)
        {
            return room.Enemies.Any(e => e != null && e.IsAlive);
        }

        /// <summary>
        /// Completes the level. Raised by the player taking the stairs in a cleared exit room, so
        /// finishing a level is always a decision.
        /// </summary>
        public void NotifyDungeonCleared()
        {
            OnDungeonCleared?.Invoke();
        }

        public bool CanFlee(Party party)
        {
            return party.PreviousRoom != null;
        }

        public void Flee(Party party, Door entryDoor, Room currentRoom)
        {
            currentRoom.EnableAllDoors();
            party.PlaceInRoom(party.PreviousRoom);
            GameManager.Instance.EnterRoom(party.CurrentRoom, entryDoor);
        }
    }
}
