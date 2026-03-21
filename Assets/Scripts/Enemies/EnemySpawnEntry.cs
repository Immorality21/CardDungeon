using System;
using Assets.Scripts.Cards;
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

        public ItemSO LootItem;
        public CardSO LootCard;

        [Range(0f, 1f)]
        public float SpawnChance;

        [Range(1, 10)]
        public int EvaluationCount = 1;
    }
}
