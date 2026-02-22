using System.Linq;
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
        public CombatResult ExecuteAttack(Player player, Room room)
        {
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

            // Player attacks enemy
            int playerDmg = Mathf.Max(1, player.Stats.Attack - enemy.Stats.Defense);
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

            // Enemy attacks player
            int enemyDmg = Mathf.Max(1, enemy.Stats.Attack - player.Stats.Defense);
            player.Stats.Health -= enemyDmg;
            log += $"Enemy deals {enemyDmg} damage to you.\n";
            log += $"\nYour HP: {player.Stats.Health}/{player.Stats.MaxHealth}";
            log += $"\nEnemy HP: {enemy.Stats.Health}/{enemy.Stats.MaxHealth}";

            if (player.Stats.Health <= 0)
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

        public bool CanFlee(Player player)
        {
            return player.PreviousRoom != null;
        }

        public void Flee(Player player, Door entryDoor, Room currentRoom)
        {
            currentRoom.EnableAllDoors();
            player.PlaceInRoom(player.PreviousRoom);
            GameManager.Instance.EnterRoom(player.CurrentRoom, entryDoor);
        }
    }
}
