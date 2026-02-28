using System.Linq;
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
        public CombatResult ExecuteAttack(Party party, Room room)
        {
            var leader = party.Leader;
            var enemy = room.Enemies.FirstOrDefault(e => e != null && e.IsAlive);
            if (enemy == null)
            {
                return new CombatResult
                {
                    Outcome = CombatOutcome.Victory,
                    Log = "No enemies remain.",
                    RemainingEnemies = 0
                };
            }

            var log = "";

            // Leader attacks enemy
            int playerDmg = Mathf.Max(1, leader.GetEffectiveAttack() - enemy.Stats.Defense);
            enemy.Stats.Health -= playerDmg;
            log += $"You deal {playerDmg} damage to the enemy.\n";

            if (!enemy.IsAlive)
            {
                log += "Enemy defeated!";
                InventoryManager.Instance.TryDropItem(enemy.LootItem);
                Destroy(enemy.gameObject);
                room.Enemies.Remove(enemy);

                int remaining = room.Enemies.Count(e => e != null && e.IsAlive);
                if (remaining > 0)
                {
                    log += $"\n{remaining} enemies remaining!";
                    return new CombatResult
                    {
                        Outcome = CombatOutcome.EnemyDown,
                        Log = log,
                        RemainingEnemies = remaining
                    };
                }

                return new CombatResult
                {
                    Outcome = CombatOutcome.Victory,
                    Log = log,
                    RemainingEnemies = 0
                };
            }

            // Enemy attacks leader
            int enemyDmg = Mathf.Max(1, enemy.Stats.Attack - leader.GetEffectiveDefense());
            leader.Stats.Health -= enemyDmg;
            log += $"Enemy deals {enemyDmg} damage to you.\n";
            log += $"\nYour HP: {leader.Stats.Health}/{leader.GetEffectiveMaxHealth()}";
            log += $"\nEnemy HP: {enemy.Stats.Health}/{enemy.Stats.MaxHealth}";

            if (leader.Stats.Health <= 0)
            {
                return new CombatResult
                {
                    Outcome = CombatOutcome.PlayerDied,
                    Log = log,
                    RemainingEnemies = room.Enemies.Count(e => e != null && e.IsAlive)
                };
            }

            return new CombatResult
            {
                Outcome = CombatOutcome.Continue,
                Log = log,
                RemainingEnemies = room.Enemies.Count(e => e != null && e.IsAlive)
            };
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
