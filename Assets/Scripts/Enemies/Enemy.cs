using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    public class Enemy : MonoBehaviour, ICombatUnit
    {
        public Stats Stats;
        public Room Room;
        public ItemSO LootItem;

        // What this enemy can throw: the list a CastMagic action on its Behavior picks from when
        // it names no specific magic. Enemy casts spend nothing.
        public List<EnemySpellEntry> Spells = new List<EnemySpellEntry>();

        /// <summary>
        /// This enemy's repertoire, as data. Stamped from the definition (falling back to the
        /// built-in preset for its archetype), and what <c>EnemyActionPlanner</c> reads every turn.
        /// </summary>
        public Behaviors.EnemyBehaviorSO Behavior;

        public EnemyArchetype Archetype;
        public List<Resistance> Resistances = new List<Resistance>();

        // Runtime combat state (not persisted).

        /// <summary>
        /// Index into <see cref="Behavior"/>'s Actions of the telegraphed action in flight, or -1.
        /// Replaced a bare "is charging" bool: telegraphs are authored per action now, so knowing
        /// *that* the enemy is winding up no longer says what it is about to deliver.
        /// </summary>
        [System.NonSerialized] public int ChargingEntryIndex = -1;

        [System.NonSerialized] public ICombatUnit ChargeTarget;

        /// <summary>True while a telegraphed action is in flight.</summary>
        public bool IsCharging => ChargingEntryIndex >= 0;

        /// <summary>Clears any telegraph in flight.</summary>
        public void ClearCharge()
        {
            ChargingEntryIndex = -1;
            ChargeTarget = null;
        }

        /// <summary>Starts a telegraph for the authored action at <paramref name="entryIndex"/>.</summary>
        public void BeginCharge(int entryIndex, ICombatUnit target)
        {
            ChargingEntryIndex = entryIndex;
            ChargeTarget = target;
        }

        // How many turns this enemy has taken this combat. Drives cadence-based behaviors
        // (e.g. the boss's signature move). Reset per combat; not persisted.
        [System.NonSerialized] public int TurnsTaken;

        /// <summary>The definition this enemy was spawned from (set by <see cref="Initialize"/>).</summary>
        public EnemySO Definition { get; private set; }

        /// <summary>
        /// The level tuning this enemy was spawned under, or null for the template's own numbers.
        /// Held so rewards scale with the level the same way stats do.
        /// </summary>
        public LevelEnemyTuning Tuning { get; private set; }

        /// <summary>XP this kill pays, after the level's tuning.</summary>
        public int XpReward => LevelEnemyTuning.XpFor(Definition, Tuning);

        /// <summary>Gold this kill pays, after the level's tuning.</summary>
        public int GoldReward => LevelEnemyTuning.GoldFor(Definition, Tuning);

        /// <summary>
        /// Multiplier on the base Power of anything this enemy casts, so its magic escalates across
        /// the campaign the same way its attack does. See
        /// <see cref="LevelEnemyTuning.MagicPowerScaleFor(EnemySO)"/>.
        /// </summary>
        public float MagicPowerScale => LevelEnemyTuning.MagicPowerScaleFor(Definition, Tuning);

        /// <summary>Whether this enemy is a boss (from its definition) — drives boss-only combat/UI.</summary>
        public bool IsBoss => Definition != null && Definition.IsBoss;

        private SpriteRenderer _spriteRenderer;
        private SpriteAnimator _spriteAnimator;

        public string DisplayName => Definition != null ? Definition.DisplayName : gameObject.name;
        public Sprite Icon => GetIcon();
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero => false;
        public Transform Transform => transform;

        Stats ICombatUnit.Stats => Stats;
        List<Resistance> ICombatUnit.Resistances => Resistances;

        public DamageType AttackDamageType =>
            Definition != null ? Definition.AttackDamageType : DamageType.Normal;

        /// <summary>
        /// Stamps this (shared-prefab) instance with an enemy definition: sprite, stats,
        /// archetype, Draw list, resistances and loot. Called by <see cref="EnemyManager"/> at
        /// spawn time so a single prefab can become any enemy type.
        /// </summary>
        /// <summary>
        /// Stamps the shared enemy prefab with a definition. <paramref name="tuning"/> is the level's
        /// enemy tuning: the definition carries the enemy's identity, the level carries its numbers
        /// (see <see cref="LevelEnemyTuning"/>). Null means the template's own stats, which is what
        /// free-play in the scene and any un-tuned level get.
        /// </summary>
        public void Initialize(EnemySO definition, LevelEnemyTuning tuning = null)
        {
            Definition = definition;
            if (definition == null)
            {
                return;
            }

            Tuning = tuning;
            gameObject.name = definition.DisplayName;
            Stats = new Stats(LevelEnemyTuning.StatsFor(definition, tuning));
            Archetype = definition.ArchetypeOf;
            Spells = new List<EnemySpellEntry>(definition.Spells);
            Behavior = definition.ResolvedBehavior;
            Resistances = new List<Resistance>(definition.Resistances);
            LootItem = definition.LootItem;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && definition.Sprite != null)
            {
                sr.sprite = definition.Sprite;
            }

            var sa = GetComponent<SpriteAnimator>();

            if (sa == null)
            {
                sa = gameObject.AddComponent<SpriteAnimator>();
            }

            // Set enemy animation
            if (sa != null && definition.AnimationFrames != null && definition.AnimationFrames.Length > 0)
            {
                sa.Initialize(definition.AnimationFrames, definition.AnimationFps);
            }
        }

        public void PlaceInRoom(Room room, Vector3 position)
        {
            Room = room;
            transform.position = position;
        }

        /// <summary>Enemies always swing off Strength; only heroes pick an attack stat.</summary>
        public StatType AttackStat
        {
            get { return StatType.Strength; }
        }

        public int GetEffectiveAttackPower()
        {
            return GetEffectiveStat(AttackStat);
        }

        /// <summary>Enemies carry no gear, so effective equals authored.</summary>
        public int GetEffectiveStat(StatType stat)
        {
            return Stats[stat];
        }

        private Sprite GetIcon()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            return _spriteRenderer != null ? _spriteRenderer.sprite : null;
        }
    }
}
