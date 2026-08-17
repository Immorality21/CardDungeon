using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using UnityEngine;

namespace Assets.Scripts.Enemies
{
    public class Enemy : MonoBehaviour, ICombatUnit
    {
        public Stats Stats;
        public Room Room;
        public ItemSO LootItem;

        // The Draw list: magics the player can extract from this enemy mid-combat,
        // each with the charges a successful draw grants (see EquippedMagicState).
        public List<DrawableMagicEntry> DrawableMagics = new List<DrawableMagicEntry>();

        public EnemyArchetype Archetype;
        public List<Resistance> Resistances = new List<Resistance>();

        // Runtime combat state (not persisted).
        [System.NonSerialized] public bool IsCharging;
        [System.NonSerialized] public ICombatUnit ChargeTarget;

        // How many turns this enemy has taken this combat. Drives cadence-based behaviors
        // (e.g. the boss's signature move). Reset per combat; not persisted.
        [System.NonSerialized] public int TurnsTaken;

        /// <summary>The definition this enemy was spawned from (set by <see cref="Initialize"/>).</summary>
        public EnemySO Definition { get; private set; }

        /// <summary>Whether this enemy is a boss (from its definition) — drives boss-only combat/UI.</summary>
        public bool IsBoss => Definition != null && Definition.IsBoss;

        private SpriteRenderer _spriteRenderer;

        public string DisplayName => Definition != null ? Definition.DisplayName : gameObject.name;
        public Sprite Icon => GetIcon();
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero => false;
        public Transform Transform => transform;

        Stats ICombatUnit.Stats => Stats;
        List<Resistance> ICombatUnit.Resistances => Resistances;

        /// <summary>
        /// Stamps this (shared-prefab) instance with an enemy definition: sprite, stats,
        /// archetype, Draw list, resistances and loot. Called by <see cref="EnemyManager"/> at
        /// spawn time so a single prefab can become any enemy type.
        /// </summary>
        public void Initialize(EnemySO definition)
        {
            Definition = definition;
            if (definition == null)
            {
                return;
            }

            gameObject.name = definition.DisplayName;
            Stats = new Stats(definition.Attack, definition.Defense, definition.Health, definition.Agility);
            Archetype = definition.Archetype;
            DrawableMagics = new List<DrawableMagicEntry>(definition.DrawableMagics);
            Resistances = new List<Resistance>(definition.Resistances);
            LootItem = definition.LootItem;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && definition.Sprite != null)
            {
                sr.sprite = definition.Sprite;
            }
        }

        public void PlaceInRoom(Room room, Vector3 position)
        {
            Room = room;
            transform.position = position;
        }

        public int GetEffectiveAttack()
        {
            return Stats.Attack;
        }

        public int GetEffectiveDefense()
        {
            return Stats.Defense;
        }

        public int GetEffectiveAgility()
        {
            return Stats.Agility;
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
