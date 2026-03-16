using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
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
        Skip
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

        public bool InCombat { get; private set; }

        private TurnManager _turnManager = new TurnManager();
        private HeroAction _pendingAction = HeroAction.None;
        private string _lastTurnLog;

        public void SubmitHeroAction(HeroAction action)
        {
            _pendingAction = action;
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

                if (unit.IsHero)
                {
                    // Wait for player input
                    _pendingAction = HeroAction.None;
                    OnHeroTurnStarted?.Invoke(unit);

                    while (_pendingAction == HeroAction.None)
                    {
                        yield return null;
                    }

                    if (_pendingAction == HeroAction.Attack)
                    {
                        yield return ExecuteHeroTurn(unit, room);
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

            // Save state
            if (DungeonSaveManager.Instance != null)
            {
                DungeonSaveManager.Instance.Save(party.CurrentRoom);
            }
            party.SaveParty();

            InCombat = false;

            var result = new CombatResult
            {
                Outcome = outcome,
                Log = fullLog,
                RemainingEnemies = room.Enemies.Count(e => e != null && e.IsAlive)
            };

            OnCombatEnded?.Invoke(result);
        }

        private IEnumerator ExecuteHeroTurn(ICombatUnit hero, Room room)
        {
            var target = GetRandomAliveEnemy(room);
            if (target == null)
            {
                _lastTurnLog = $"{hero.DisplayName} has no target.";
                yield break;
            }

            // Lunge forward (heroes lunge right)
            yield return LungeAnimation(hero.Transform, Vector3.right);

            int dmg = Mathf.Max(1, hero.GetEffectiveAttack() - target.GetEffectiveDefense());
            target.Stats.Health -= dmg;

            ShowDamageText(target.Transform.position, dmg, Color.white);

            string log = $"{hero.DisplayName} attacks {target.DisplayName} for {dmg} damage.";

            if (!target.IsAlive)
            {
                log += $" {target.DisplayName} defeated!";
                HandleEnemyDeath(target as Enemy, room);
            }

            _lastTurnLog = log;
        }

        private IEnumerator ExecuteEnemyTurn(ICombatUnit enemy, Party party)
        {
            var target = GetRandomAliveHero(party);
            if (target == null)
            {
                _lastTurnLog = $"{enemy.DisplayName} has no target.";
                yield break;
            }

            // Lunge forward (enemies lunge left)
            yield return LungeAnimation(enemy.Transform, Vector3.left);

            int dmg = Mathf.Max(1, enemy.GetEffectiveAttack() - target.GetEffectiveDefense());
            target.Stats.Health -= dmg;

            ShowDamageText(target.Transform.position, dmg, Color.red);

            string log = $"{enemy.DisplayName} attacks {target.DisplayName} for {dmg} damage.";

            if (!target.IsAlive)
            {
                log += $" {target.DisplayName} has fallen!";
                HandleHeroDeath(target as Hero);
                _turnManager.RemoveUnit(target);
            }

            _lastTurnLog = log;
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

        private void BroadcastTurnOrder()
        {
            var order = _turnManager.GetTurnOrder(10);
            OnTurnOrderChanged?.Invoke(order);
        }

        private void ShowDamageText(Vector3 position, int damage, Color color)
        {
            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    position,
                    damage.ToString(),
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
