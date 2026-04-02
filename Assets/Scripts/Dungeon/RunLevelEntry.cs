using System;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunLevelEntry
    {
        public LevelDefinitionSO LevelTemplate;
        public string LevelName;
        public bool IsStatic;
        [Tooltip("Only used when IsStatic is true")]
        public int FixedSeed;
    }
}
