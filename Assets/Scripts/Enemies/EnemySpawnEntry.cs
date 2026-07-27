using System;
using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [Serializable]
    public class EnemySpawnEntry
    {
        public GameObject Prefab;

        public Stats Stats = new Stats(3, 1, 10);

        public EnemyArchetype Archetype;

        public ItemSO LootItem;

        // The Draw list: magics the player can extract from this enemy, each with charges.
        public List<DrawableMagicEntry> DrawableMagics = new List<DrawableMagicEntry>();

        [Range(0f, 1f)]
        public float SpawnChance;

        [Range(1, 10)]
        public int EvaluationCount = 1;
    }
}
