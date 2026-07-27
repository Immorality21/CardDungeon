using System;
using Assets.Scripts.Cards;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>One magic an enemy offers on its Draw list, plus the charges a draw grants.</summary>
    [Serializable]
    public class DrawableMagicEntry
    {
        public MagicSO Magic;
        [Range(1, 9)]
        public int Charges = 3;
    }
}
