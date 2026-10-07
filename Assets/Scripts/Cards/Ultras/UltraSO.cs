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
        Strike = 1,

        /// <summary>The Cultist's rite: a living ally - himself included - falls for the rest of the
        /// floor, and <see cref="UltraSO.Creature"/> rises in their place, built off <b>their</b> stats,
        /// at full health, for the rest of the fight. Its Attack is picked by the fallen hero's highest
        /// stat (<see cref="UltraSO.StatAbilities"/>).</summary>
        Sacrifice = 2,

        /// <summary>The Tinkerer's mechs: <see cref="UltraSO.Mech"/> is assembled where the hero stands
        /// and the hero climbs on. The mech fights beside the party with its own menu, takes every blow
        /// aimed at its rider (<c>GuardTable</c>), and acts straight after each of the rider's turns -
        /// the two share the rider's pace. It stays until it breaks, the rider falls, or the fight
        /// ends; using it again rebuilds it.</summary>
        Mount = 3
    }

    /// <summary>Sacrifice: the Attack a horror rises with when <see cref="Stat"/> is the sacrificed
    /// hero's highest.</summary>
    [System.Serializable]
    public class UltraStatAbility
    {
        public Assets.Scripts.UnitStats.StatType Stat;
        public MagicSO Ability;
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

        [Header("Sacrifice")]
        [Tooltip("Sacrifice: the creature that rises. Its StatPercents read the SACRIFICED hero's stats " +
                 "(base + gear), so every stat they had carries over; it arrives at full health and stays " +
                 "until it falls or the fight ends.")]
        public SummonSO Creature;

        [Tooltip("Sacrifice: its Attack by the sacrificed hero's highest stat - a Warrior makes something " +
                 "that tears, a Cleric something that whispers. Ties go to the first listed.")]
        public List<UltraStatAbility> StatAbilities = new List<UltraStatAbility>();

        [Header("Mount")]
        [Tooltip("Mount: the mech assembled and ridden. Built like a summon fighting beside the party " +
                 "(StatPercents of the rider's stats, its own Attack and Signature) but with no turn " +
                 "limit - it stays until it breaks.")]
        public SummonSO Mech;

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
