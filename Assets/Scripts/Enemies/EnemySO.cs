using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
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

        [Tooltip("Marks this definition as a boss: drives the boss HP bar, the no-flee rule, " +
                 "the intro banner, and the run-complete fanfare. Placement is via RunLevelEntry.BossEnemy.")]
        public bool IsBoss;

        [Header("Base stats")]
        public int Attack = 3;
        public int Defense = 1;
        public int Health = 10;
        public int Agility = 5;

        [Header("Kill rewards")]
        public int XpReward = 10;    // awarded to the party leader immediately on kill
        public int GoldReward = 5;   // shown per combat; only banked (persisted) on level-clear

        public EnemyArchetype Archetype = EnemyArchetype.Aggressor;

        // The Draw list: magics the player can extract from this enemy, each with charges.
        public List<DrawableMagicEntry> DrawableMagics = new List<DrawableMagicEntry>();

        public List<Resistance> Resistances = new List<Resistance>();

        public ItemSO LootItem;
    }
}
