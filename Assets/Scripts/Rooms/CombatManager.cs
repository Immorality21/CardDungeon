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

    public class CombatResult
    {
        public CombatOutcome Outcome;
        public string Log;
        public int RemainingEnemies;
    }

    public class CombatManager : SingletonBehaviour<CombatManager>
    {
        [SerializeField] private float _turnDelay = 0.6f;

        public event Action OnCombatStarted;
        public event Action<string> OnTurnExecuted;
        public event Action<CombatResult> OnCombatEnded;

        public bool InCombat { get; private set; }

        private TurnManager _turnManager = new TurnManager();

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

                string turnLog;
                if (unit.IsHero)
                {
                    turnLog = ExecuteHeroTurn(unit, room);
                }
                else
                {
                    turnLog = ExecuteEnemyTurn(unit, party);
                }

                fullLog += turnLog + "\n";
                OnTurnExecuted?.Invoke(turnLog);

                yield return new WaitForSeconds(_turnDelay);
            }

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

        private string ExecuteHeroTurn(ICombatUnit hero, Room room)
        {
            var target = GetRandomAliveEnemy(room);
            if (target == null)
            {
                return $"{hero.DisplayName} has no target.";
            }

            int dmg = Mathf.Max(1, hero.GetEffectiveAttack() - target.GetEffectiveDefense());
            target.Stats.Health -= dmg;

            string log = $"{hero.DisplayName} attacks {target.DisplayName} for {dmg} damage.";

            if (!target.IsAlive)
            {
                log += $" {target.DisplayName} defeated!";
                HandleEnemyDeath(target as Enemy, room);
            }

            return log;
        }

        private string ExecuteEnemyTurn(ICombatUnit enemy, Party party)
        {
            var target = GetRandomAliveHero(party);
            if (target == null)
            {
                return $"{enemy.DisplayName} has no target.";
            }

            int dmg = Mathf.Max(1, enemy.GetEffectiveAttack() - target.GetEffectiveDefense());
            target.Stats.Health -= dmg;

            string log = $"{enemy.DisplayName} attacks {target.DisplayName} for {dmg} damage.";

            if (!target.IsAlive)
            {
                log += $" {target.DisplayName} has fallen!";
                HandleHeroDeath(target as Hero);
                _turnManager.RemoveUnit(target);
            }

            return log;
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
