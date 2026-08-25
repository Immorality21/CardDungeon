using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// Definition of an enemy type: its display name, sprite, base stats, behaviour archetype,
    /// Draw offerings, resistances and loot. A room's spawn table references one of these; the
    /// shared Enemy prefab is stamped with it at spawn time via <see cref="Enemy.Initialize"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "SO/Enemy")]
    public class EnemySO : ScriptableObject
    {
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

        // The Draw list: magics the player can extract from this enemy, each with charges. It is
        // also what a CastMagic action draws from when it names no specific magic - so what the enemy
        // throws is what you can steal from it.
        public List<DrawableMagicEntry> DrawableMagics = new List<DrawableMagicEntry>();

        public List<Resistance> Resistances = new List<Resistance>();

        public ItemSO LootItem;

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
    }
}
