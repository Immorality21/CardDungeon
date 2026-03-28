using System;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    [Serializable]
    public class Resistance
    {
        public DamageType DamageType;

        [Range(-100f, 200f)]
        public float Percent;
    }
}
