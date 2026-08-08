using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
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
        Draw
    }

    public class CombatResult
    {
        public CombatOutcome Outcome;
        public string Log;
        public int RemainingEnemies;
    }

    public class CombatManager : SingletonBehaviour<CombatManager>
    {
        [SerializeField] private float _turnDelay = 0.6f;
        [SerializeField] private float _lungeDistance = 0.3f;
        [SerializeField] private float _lungeDuration = 0.12f;

        public event Action OnCombatStarted;
        public event Action<string> OnTurnExecuted;
        public event Action<CombatResult> OnCombatEnded;
        public event Action<List<ICombatUnit>> OnTurnOrderChanged;
        public event Action<ICombatUnit> OnHeroTurnStarted;
        public event Action<ICombatUnit, List<MagicSlot>> OnMagicSlotsRequested;
        public event Action<ICombatUnit, List<ICombatUnit>> OnAttackTargetRequested;
        public event Action<ICombatUnit, List<ICombatUnit>> OnDrawTargetRequested;
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
        private ICombatUnit _pendingAttackTarget;
        private string _lastTurnLog;
        private Room _currentCombatRoom;
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
            BuffTracker = new CombatBuffTracker();
            _turnManager.SetBuffTracker(BuffTracker);
            _tagTracker = new MagicTagTracker();
            // Prefer the shared combo catalog (single source of truth, also used by the hub
            // Forge); fall back to the scene-serialized list if no catalog is present.
            var combos = MagicComboCatalog.HasInstance
                ? new List<MagicComboSO>(MagicComboCatalog.Instance.AllCombos)
                : _cardCombos;
            _comboDetector = new ComboDetector(combos);

            // Refill equipped-magic charges at the start of each combat (per-room refresh).
            if (DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                DungeonManager.Instance.MagicState.RefillCharges();
            }

            OnCombatStarted?.Invoke();

            // Fan out heroes into the room
            yield return party.FanOutHeroes(room);

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
                    units.Add(enemy);
                }
            }

            _turnManager.Initialize(units);
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

            // Clear turn order display
            OnTurnOrderChanged?.Invoke(new List<ICombatUnit>());

            // Determine outcome
            CombatOutcome outcome;
            if (!HasAliveHeroes(party))
            {
                outcome = CombatOutcome.PlayerDied;
                fullLog += "\nYour party has been defeated!";
            }
            else
            {
                outcome = CombatOutcome.Victory;
                fullLog += "\nAll enemies defeated!";

                // Gather heroes back to party
                yield return party.GatherHeroes();

                // Enable all doors
                room.EnableAllDoors();
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

            var result = new CombatResult
            {
                Outcome = outcome,
                Log = fullLog,
                RemainingEnemies = room.Enemies.Count(e => e != null && e.IsAlive)
            };

            OnCombatEnded?.Invoke(result);

            // Check if the exit room has been cleared
            if (outcome == CombatOutcome.Victory && room.IsExit && !HasAliveEnemies(room))
            {
                OnDungeonCleared?.Invoke();
            }
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

            // Record any triggered combos as discovered (permanent, survives death).
            foreach (var comboKey in result.TriggeredComboKeys)
            {
                meta.MarkComboDiscovered(comboKey);
            }

            yield return _presenter.Present(result);

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
            ShowFloatingLabel(heroUnit.Transform.position, $"Draw {magic.DisplayName}!", new Color(0.5f, 0.8f, 1f));
            _lastTurnLog = $"{heroUnit.DisplayName} draws {magic.DisplayName} from {sourceName}!";
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
            var archetype = enemy != null ? enemy.Archetype : EnemyArchetype.Aggressor;
            var behavior = EnemyBehaviorFactory.Get(archetype);

            var context = new EnemyCombatContext
            {
                Heroes = GetAliveHeroes(party),
                Allies = GetAliveEnemies().Where(u => u != enemyUnit).ToList(),
                BuffTracker = BuffTracker,
                SelfIsCharging = enemy != null && enemy.IsCharging
            };

            var decision = behavior.Decide(enemyUnit, context);

            switch (decision.Type)
            {
                case EnemyActionType.ChargeHeavy:
                    yield return ExecuteEnemyCharge(enemy, decision.Target);
                    break;
                case EnemyActionType.HeavyAttack:
                    yield return ExecuteEnemyHeavyAttack(enemyUnit, enemy, party, decision.Multiplier);
                    break;
                case EnemyActionType.Heal:
                    yield return ExecuteEnemyHeal(enemyUnit, decision.Target, decision.Amount);
                    break;
                case EnemyActionType.Debuff:
                    yield return ExecuteEnemyDebuff(enemyUnit, decision.Target, decision.DebuffStat, decision.Amount, decision.Duration);
                    break;
                default:
                    yield return ExecuteEnemyBasicAttack(enemyUnit, decision.Target, party);
                    break;
            }
        }

        private IEnumerator ExecuteEnemyBasicAttack(ICombatUnit enemyUnit, ICombatUnit target, Party party)
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

            yield return ExecuteAttack(enemyUnit, target, Vector3.left, Color.red);
            ResolveHeroDamaged(target);
        }

        private IEnumerator ExecuteEnemyCharge(Enemy enemy, ICombatUnit target)
        {
            if (enemy == null)
            {
                yield break;
            }

            enemy.IsCharging = true;
            enemy.ChargeTarget = target;
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
                enemy.IsCharging = false;
                enemy.ChargeTarget = null;
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

        private IEnumerator ExecuteEnemyHeal(ICombatUnit enemyUnit, ICombatUnit target, int amount)
        {
            if (target == null || !target.IsAlive)
            {
                _lastTurnLog = $"{enemyUnit.DisplayName} has no one to heal.";
                yield break;
            }

            int before = target.Stats.Health;
            target.Stats.Health = Mathf.Min(target.Stats.Health + amount, target.Stats.MaxHealth);
            int healed = target.Stats.Health - before;

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
            yield return LungeAnimation(attacker.Transform, lungeDirection);

            int attackBonus = BuffTracker.GetBuffAmount(attacker, StatType.Attack);
            int defenseBonus = BuffTracker.GetBuffAmount(target, StatType.Defense);
            int rawAttack = Mathf.RoundToInt((attacker.GetEffectiveAttack() + attackBonus) * damageMultiplier);
            int defense = target.GetEffectiveDefense() + defenseBonus;
            int dmg = DamageCalculator.Calculate(rawAttack, defense, DamageType.Normal, target.Resistances);
            target.Stats.Health -= dmg;

            ShowDamageText(target.Transform.position, dmg, damageColor);

            _lastTurnLog = $"{attacker.DisplayName} {verb} {target.DisplayName} for {dmg} damage.";
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
                if (handler.SkipsTurn)
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

        private void ShowDamageText(Vector3 position, int damage, Color color)
        {
            ShowFloatingLabel(position, damage.ToString(), color);
        }

        private void ShowFloatingLabel(Vector3 position, string text, Color color)
        {
            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    position,
                    text,
                    color,
                    1f,     // fadeSpeed — fade out over ~1 second
                    0.8f,   // fadeRange — gentle drift
                    0.15f,  // scale
                    TextFadeMode.FadeUp);
            }
        }

        private void HandleEnemyDeath(Enemy enemy, Room room)
        {
            if (enemy == null)
            {
                return;
            }

            InventoryManager.Instance.TryDropItem(enemy.LootItem);
            _turnManager.RemoveUnit(enemy);
            room.Enemies.Remove(enemy);
            Destroy(enemy.gameObject);
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
