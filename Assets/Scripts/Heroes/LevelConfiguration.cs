using System;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// What one level-up grants. <see cref="Gains"/> is a <c>StatBlock</c> rather than one int per
    /// stat, so a level can grant Intelligence or Luck without this class changing - the old
    /// four-int shape is exactly why the caster stats had no level gains when they were added.
    /// </summary>
    [Serializable]
    public class LevelConfiguration
    {
        public int Level;
        public int XpRequired;

        [Tooltip("Stat increases applied on reaching this level. Only list what actually grows.")]
        public StatBlock Gains = new StatBlock();
    }
}
