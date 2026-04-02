using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Run Definition")]
    public class RunDefinitionSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;
        public List<RunLevelEntry> Levels = new List<RunLevelEntry>();
    }
}
