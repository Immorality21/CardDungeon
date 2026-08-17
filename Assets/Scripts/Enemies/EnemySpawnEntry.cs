using System;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// One row of a room's spawn table: which enemy to spawn (an <see cref="EnemySO"/>
    /// definition) plus the per-room roll parameters. The enemy's identity, sprite, stats,
    /// archetype, Draw list, resistances and loot all live on the <see cref="EnemySO"/>.
    /// </summary>
    [Serializable]
    public class EnemySpawnEntry
    {
        public EnemySO Enemy;

        [Range(0f, 1f)]
        public float SpawnChance;

        [Range(1, 10)]
        public int EvaluationCount = 1;
    }
}
