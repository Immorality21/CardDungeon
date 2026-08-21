using System;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Items
{
    [Serializable]
    public class ItemBonus
    {
        [Tooltip("Which stat this bonus changes. Defaults to Strength rather than None so a newly "
                 + "added bonus row does something visible immediately; None is the zero value and "
                 + "makes the row a silent no-op.")]
        public StatType StatType = StatType.Strength;
        public BonusType BonusType;
        public float Value;
    }
}
