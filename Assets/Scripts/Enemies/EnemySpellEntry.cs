using System;
using Assets.Scripts.Cards;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// One spell in an enemy's own repertoire: what a <c>CastMagic</c> action picks from when it
    /// names no specific magic.
    ///
    /// <para><b>This used to be the Draw list.</b> Until 2026-09-04 the same entries were what the
    /// player extracted mid-combat with the Draw command, and <c>Charges</c> was the grant a draw
    /// handed over. Draw is gone — every spell a hero can cast now comes off that hero's sphere
    /// grid (<c>SphereNodeKind.MagicKnown</c>) — so the list kept only the half that was always
    /// the enemy's: what the monster itself can throw. Enemy casts are free and spend nothing,
    /// exactly as they always did.</para>
    ///
    /// <para>It is still the thing the Bestiary reveals: an entry is named once the player has
    /// actually seen this enemy cast it (<c>BestiaryEntry.ObservedSpellKeys</c>), so the discovery
    /// loop Draw used to carry now points at the enemy's own behaviour.</para>
    /// </summary>
    [Serializable]
    public class EnemySpellEntry
    {
        public MagicSO Magic;

        [Tooltip("Relative likelihood of this entry being the one cast, among the entries on this " +
                 "enemy. Only matters for a CastMagic action that leaves its Magic field empty. " +
                 "If every entry on an enemy is 0 the choice is uniform, which is also what assets " +
                 "authored before this field existed deserialize to.")]
        [Min(0f)]
        public float CastWeight = 1f;
    }
}
