using System;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunLevelEntry
    {
        public LevelDefinitionSO LevelTemplate;
        public string LevelName;
        public ManualLevelLayoutSO ManualLayout;
    }
}
