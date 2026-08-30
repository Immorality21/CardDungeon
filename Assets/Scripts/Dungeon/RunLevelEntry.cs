using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// One escort standing with the boss in the sealed exit room. Deliberately *not* an
    /// <see cref="EnemySpawnEntry"/>: a spawn entry rolls, and a climax that is a different fight
    /// depending on a die is neither readable to the player nor priceable by the balance model.
    /// An add is guaranteed, so the boss room's worst case is its expected case.
    /// </summary>
    [Serializable]
    public class BossAddEntry
    {
        public EnemySO Enemy;

        [Tooltip("How many of this enemy stand with the boss. Danger is superlinear in body count " +
                 "(see docs/BALANCING.md §5l) - two adds is already a large spend.")]
        [Range(1, 4)]
        public int Count = 1;
    }

    [Serializable]
    public class RunLevelEntry
    {
        public LevelDefinitionSO LevelTemplate;
        public string LevelName;
        public ManualLevelLayoutSO ManualLayout;

        [Tooltip("Optional boss for this level. When set, this enemy is guaranteed in the exit " +
                 "room, making the level's climax a boss fight. Leave null for a normal level.")]
        public EnemySO BossEnemy;

        [Tooltip("Optional escort standing with the boss. The exit room's rolled spawns are wiped " +
                 "either way, so these are the only company the boss keeps - and, with trash rooms " +
                 "capped at two bodies, the last place a level has left to spend danger. Ignored " +
                 "when BossEnemy is null.")]
        public List<BossAddEntry> BossAdds = new List<BossAddEntry>();

        [Tooltip("What this level does to the enemies it spawns. An EnemySO is a template - the " +
                 "same enemy appears across the whole campaign against wildly different parties - so " +
                 "its numbers belong to the level, not the asset. Difficulty 1 with no overrides " +
                 "means 'exactly as authored'.")]
        public LevelEnemyTuning EnemyTuning = new LevelEnemyTuning();

        [Tooltip("Optional captive hero for this level. When set — and the player does not already " +
                 "own them — they are placed in a room off the start/exit rooms and can be freed " +
                 "for nothing, joining the party mid-run. Like XP, the rescue is only committed on " +
                 "level clear: die first and they are lost with the run.")]
        public HeroSO RescueHero;

        /// <summary>
        /// The boss's escort, flattened to one <see cref="EnemySO"/> per body, skipping null and
        /// non-positive rows. Empty when this level has no boss - an add without a boss is an
        /// authoring slip, not a room the level is entitled to populate. Every consumer (spawning,
        /// the encounter model, the floor simulation) goes through this so they cannot disagree
        /// about what stands in the exit room.
        /// </summary>
        public IEnumerable<EnemySO> EnumerateBossAdds()
        {
            if (BossEnemy == null || BossAdds == null)
            {
                yield break;
            }

            foreach (var add in BossAdds)
            {
                if (add == null || add.Enemy == null)
                {
                    continue;
                }

                for (int i = 0; i < Mathf.Max(1, add.Count); i++)
                {
                    yield return add.Enemy;
                }
            }
        }
    }
}
