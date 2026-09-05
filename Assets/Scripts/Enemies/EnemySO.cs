using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// Definition of an enemy type: its display name, sprite, base stats, behaviour archetype,
    /// spell repertoire, resistances and drop table. A room's spawn table references one of these; the
    /// shared Enemy prefab is stamped with it at spawn time via <see cref="Enemy.Initialize"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "SO/Enemy")]
    public class EnemySO : ScriptableObject
    {
        [Tooltip("Immutable identifier this enemy's persistent player knowledge is filed under - " +
                 "what the player has learned about it (resistances seen, weaknesses discovered). " +
                 "Never shown to the player. Changing it orphans every save that references the old " +
                 "value, so treat it as write-once. Same contract as HeroSO.Key.")]
        public string Key;

        [Tooltip("Name shown to the player. Safe to rename at any time - it is not a save key.")]
        public string DisplayName = "Enemy";

        public Sprite Sprite;

        public Sprite[] AnimationFrames;
        public float AnimationFps = 8f;

        [Tooltip("Marks this definition as a boss: drives the boss HP bar, the no-flee rule, " +
                 "the intro banner, and the run-complete fanfare. Placement is via RunLevelEntry.BossEnemy.")]
        public bool IsBoss;

        [Header("Base stats")]
        [Tooltip("This enemy's stats, MaxHealth included. Absent entries read as 0.")]
        public StatBlock BaseStats = StatBlock.Defaults();

        [Header("Kill rewards")]
        public int XpReward = 10;    // awarded to the party leader immediately on kill
        public int GoldReward = 5;   // shown per combat; only banked (persisted) on level-clear

        [Tooltip("Damage type this enemy's physical attacks deal (basic, heavy and boss signature). " +
                 "Normal bypasses the elemental layer, so leave it there for a purely physical enemy. " +
                 "Anything else makes the hero side's elemental resistance matter defensively.")]
        public DamageType AttackDamageType = DamageType.Normal;

        [Tooltip("Coarse label. It no longer selects any logic - Behavior below is the behaviour - " +
                 "but the analyzer's variety checks read it, and it is a useful shorthand. Kept in " +
                 "sync with the assigned Behavior's own Archetype by ArchetypeOf.")]
        public EnemyArchetype Archetype = EnemyArchetype.Aggressor;

        [Tooltip("This enemy's repertoire, as data: what it can do on a turn, when, and how often. " +
                 "Duplicate one of the presets under ScriptableObjects/Enemies/Behaviors to make a " +
                 "variant. Leave empty to fall back to the built-in preset for Archetype above, " +
                 "which is the original hard-coded behaviour minus casting.")]
        public Behaviors.EnemyBehaviorSO Behavior;

        // What this enemy can throw: a CastMagic action that names no specific magic picks from
        // here, weighted by CastWeight. Enemy casts spend nothing. Named in the Bestiary only once
        // the player has actually seen it cast (BestiaryEntry.ObservedSpellKeys) - this list carries
        // the discovery loop that used to hang off Draw.
        public List<EnemySpellEntry> Spells = new List<EnemySpellEntry>();

        public List<Resistance> Resistances = new List<Resistance>();

        [Tooltip("What this kill can yield. Every entry rolls on its own, so a monster can drop both " +
                 "a signature piece of gear and the raw stuff it is made of. An entry with Chance 0 " +
                 "uses the rarity + run-depth math (how gear has always dropped); an entry with an " +
                 "explicit Chance is that flat probability, which is what materials use so a drop is " +
                 "gated by *which* monster rather than by depth.")]
        public List<LootDrop> LootTable = new List<LootDrop>();

        /// <summary>
        /// The behaviour to fight with: the assigned asset, or the built-in preset for
        /// <see cref="Archetype"/>. Never null, so no caller has to branch.
        /// </summary>
        public Behaviors.EnemyBehaviorSO ResolvedBehavior =>
            Behavior != null ? Behavior : Behaviors.EnemyBehaviorSO.BuiltInPreset(Archetype);

        /// <summary>
        /// The archetype label to report. The assigned behaviour wins, so duplicating a Healer preset
        /// and pointing an enemy at it reports Healer without anyone having to remember to change two
        /// fields.
        /// </summary>
        public EnemyArchetype ArchetypeOf => Behavior != null ? Behavior.Archetype : Archetype;

        /// <summary>
        /// The identifier persistent knowledge about this enemy is written under. Falls back to
        /// <see cref="DisplayName"/> and then the asset name, so enemies authored before
        /// <see cref="Key"/> existed still resolve. Always key persistence off this, never off
        /// <see cref="DisplayName"/> - a display name is renameable by design, and keying off it
        /// would silently orphan the record the moment someone retitled an enemy.
        /// </summary>
        public string SaveKey
        {
            get
            {
                if (!string.IsNullOrEmpty(Key))
                {
                    return Key;
                }
                if (!string.IsNullOrEmpty(DisplayName))
                {
                    return DisplayName;
                }
                return name;
            }
        }

        /// <summary>
        /// The player-facing name, falling back to the asset name. Use this for anything on screen or
        /// in a report - it replaces the DisplayName-or-name ternary that was repeated at every call
        /// site.
        /// </summary>
        public string Label => string.IsNullOrEmpty(DisplayName) ? name : DisplayName;
    }
}
