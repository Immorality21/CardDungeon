using System;
using Assets.Scripts.Enemies;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunLevelEntry
    {
        public LevelDefinitionSO LevelTemplate;
        public string LevelName;
        public ManualLevelLayoutSO ManualLayout;

        [Tooltip("Optional boss for this level. When set, this enemy is guaranteed (alone) in " +
                 "the exit room, making the level's climax a boss fight. Leave null for a normal level.")]
        public EnemySO BossEnemy;
    }
}
