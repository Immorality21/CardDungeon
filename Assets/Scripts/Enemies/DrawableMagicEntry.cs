using System;
using Assets.Scripts.Cards;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// One magic an enemy offers on its Draw list, plus the charges a draw grants.
    ///
    /// <para>The same list is also what a <c>CastMagic</c> action draws from when it names no
    /// specific magic: an enemy that hands the player Fireball can throw Fireball. Enemy casts
    /// never spend <see cref="Charges"/> — that field is the player's grant only, the same way FF
    /// enemies cast freely from the pool they can be drawn from.</para>
    /// </summary>
    [Serializable]
    public class DrawableMagicEntry
    {
        public MagicSO Magic;

        [Tooltip("Charges a successful Draw grants the player. Enemy casts do not consume these.")]
        [Range(1, 9)]
        public int Charges = 3;

        [Tooltip("Relative likelihood of this entry being the one cast, among the entries on this " +
                 "enemy. Only matters for a CastMagic action that leaves its Magic field empty. " +
                 "If every entry on an enemy is 0 the choice is uniform, which is also what assets " +
                 "authored before this field existed deserialize to.")]
        [Min(0f)]
        public float CastWeight = 1f;
    }
}
