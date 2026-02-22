using System;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [Serializable]
    public class EnemySpawnEntry
    {
        public GameObject Prefab;

        [Range(0f, 1f)]
        public float SpawnChance;

        [Range(1, 10)]
        public int EvaluationCount = 1;
    }
}
