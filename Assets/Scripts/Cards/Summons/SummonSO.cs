using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// What a summon does once it answers. Two kinds share one foundation — the grid node that
    /// teaches it, its per-run charges, the Summon command and the presentation — and differ only in
    /// what happens after (docs/plans/SPECIALIZATION.md §4b).
    /// </summary>
    public enum SummonKind
    {
        /// <summary>FF7–9: a big effect lands and it is over. No unit, no change to the turn order.</summary>
        SpecialAttack = 0,

        /// <summary>FFX: the party steps out and the summon fights alone. <b>Not built yet</b> — the
        /// field exists so the second kind does not reshape the first.</summary>
        ReplaceParty = 1
    }

    /// <summary>Which way the creature looks while it is on the stage.</summary>
    public enum SummonFacing
    {
        /// <summary>Toward the enemies (right) - for a summon that attacks.</summary>
        Enemies = 0,

        /// <summary>Toward the party (left) - for a summon that buffs or heals them.</summary>
        Party = 1
    }

    /// <summary>
    /// A summon: the capability a sphere-grid branch pays out (§4b). Learned from a
    /// <c>SphereNodeKind.Summon</c> node that names it by <see cref="Key"/>, always carried (it takes
    /// no ability slot), cast through the hero's <b>Summon</b> command, and limited by charges per
    /// run that refill with ability charges — run start and refuges.
    ///
    /// <para>A special-attack summon's effect is an ordinary list of <see cref="SpellEffect"/>s,
    /// resolved by the same <c>EffectResolver</c> every ability goes through. Upgrade nodes on the
    /// grid add to it (<see cref="SummonOps.EffectsFor"/>); the asset itself is never modified.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "NewSummon", menuName = "SO/Summon")]
    public class SummonSO : ScriptableObject
    {
        [Tooltip("Stable identifier. Grid nodes name the summon they teach or upgrade by this key.")]
        public string Key;

        public string DisplayName;

        [TextArea]
        public string Description;

        [Tooltip("The creature, drawn large on the battle stage while it is summoned.")]
        public Sprite Sprite;

        [Tooltip("Optional: frames the creature loops through while it is on the stage. Empty shows Sprite still.")]
        public Sprite[] AnimationFrames;

        [Min(1f)] public float AnimationFps = 6f;

        [Tooltip("Which way the creature faces on the stage. Art is authored facing right (the enemies); " +
                 "Party mirrors it, and its lurch moves toward the heroes instead.")]
        public SummonFacing Facing = SummonFacing.Enemies;

        public SummonKind Kind = SummonKind.SpecialAttack;

        [Tooltip("Who the effects land on. The Bloodfang Boar is AllAllies.")]
        public MagicTargetType TargetType = MagicTargetType.AllAllies;

        [Tooltip("SpecialAttack: what lands. A buff authored with PowerMode.PercentOfTargetStat reads " +
                 "Power as a percentage of each target's own stat.")]
        public List<SpellEffect> Effects = new List<SpellEffect>();

        [Tooltip("Charges per run before any upgrade node. Refilled with ability charges: at run " +
                 "start and when resting in a refuge.")]
        [Min(1)] public int BaseCharges = 1;

        public string Label => string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
    }
}
