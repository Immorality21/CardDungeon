using System;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [Serializable]
    public class EnemySpawnEntry
    {
        public GameObject Prefab;

        public Stats Stats = new Stats(3, 1, 10);

        [Range(0f, 1f)]
        public float SpawnChance;

        [Range(1, 10)]
        public int EvaluationCount = 1;
    }
}
