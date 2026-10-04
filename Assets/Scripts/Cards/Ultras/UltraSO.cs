using System.Collections.Generic;
using Assets.Scripts.Combat;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>What an Ultra does once it is used.</summary>
    public enum UltraKind
    {
        /// <summary>The hero becomes something else for a few of their own turns: a bigger health bar
        /// (the same share of it filled), a different basic attack, abilities of the form's own, and
        /// the form's art. The Warlock's Demon Form.</summary>
        Transform = 0,

        /// <summary>One big blow and it is over: <see cref="UltraSO.Effects"/> land on
        /// <see cref="UltraSO.TargetType"/>, resolved like a summon's special attack (no Forge bonus,
        /// no tags, no combo). The Warlock's Rain of Fire and Soul Harvest.</summary>
        Strike = 1
    }

    /// <summary>
    /// An Ultra (docs/plans/COMBAT_DEPTH.md §13): a hero's big move, used from the <b>Ultra</b>
    /// command once the hero's Ultra gauge is full. The gauge fills as the hero loses health in a
    /// fight - a comeback mechanic - and empties when the Ultra is used. Taught by a
    /// <c>SphereNodeKind.Ultra</c> node that names it by <see cref="Key"/>; resolved through
    /// <see cref="UltraCatalogSO"/>. Ultras are deliberately smaller than summons and universal, where
    /// a summon is the earned, charge-limited payoff (decided 2026-10-04).
    /// </summary>
    [CreateAssetMenu(fileName = "NewUltra", menuName = "SO/Ultra")]
    public class UltraSO : ScriptableObject
    {
        [Tooltip("Stable identifier. Grid nodes name the Ultra they teach by this key.")]
        public string Key;

        public string DisplayName;

        [TextArea]
        public string Description;

        public UltraKind Kind = UltraKind.Transform;

        [Header("Strike")]
        [Tooltip("Strike: who the effects land on. A whole side or the hero - the Ultra list has no " +
                 "target picker, so a single-enemy Strike hits the weakest enemy standing.")]
        public MagicTargetType TargetType = MagicTargetType.AllEnemies;

        [Tooltip("Strike: what lands, scaled off the hero's stats like an ability's effects.")]
        public List<SpellEffect> Effects = new List<SpellEffect>();

        [Header("Transform")]
        [Tooltip("How many of the hero's own turns the form lasts, not counting the turn it is taken on.")]
        [Min(1)] public int Turns = 3;

        [Tooltip("Max health added while transformed, as a percentage. The health bar keeps the same " +
                 "share filled both ways: half health before is half health after, and back.")]
        [Min(0)] public int MaxHealthPercent = 50;

        [Tooltip("The element the hero's basic Attack deals while transformed.")]
        public DamageType AttackDamageType = DamageType.Normal;

        [Tooltip("Abilities the form can use while it lasts, as often as it likes - no charges, no slot. " +
                 "They are not a hero's magic: never on a grid, never in the Forge.")]
        public List<MagicSO> Abilities = new List<MagicSO>();

        [Tooltip("The form's art. Empty keeps the hero's own.")]
        public Sprite[] FormFrames;

        [Min(1f)] public float FormFps = 4f;

        public string Label => string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
    }
}
