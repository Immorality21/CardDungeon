using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Minimal <see cref="ICombatUnit"/> for testing combat logic without MonoBehaviours.
    /// The positional constructor is kept because every existing test calls it that way; extra stats
    /// are set through <see cref="Stats"/>'s indexer.
    /// </summary>
    public class MockCombatUnit : ICombatUnit
    {
        public string DisplayName { get; set; }
        public Sprite Icon => null;
        public Stats Stats { get; set; }
        public bool IsAlive => Stats.Health > 0;
        public bool IsHero { get; set; }
        public Transform Transform => null;
        public List<Resistance> Resistances { get; set; } = new List<Resistance>();

        /// <summary>Element this unit's basic attacks carry. Normal keeps existing tests unaffected.</summary>
        public DamageType AttackDamageType { get; set; } = DamageType.Normal;

        /// <summary>When set, models item/level bonuses layered on top of the raw stat.</summary>
        public Dictionary<StatType, int> EffectiveOverrides { get; } = new Dictionary<StatType, int>();

        /// <summary>Back-compat shim for tests that only ever overrode Agility.</summary>
        public int? EffectiveAgilityOverride
        {
            get
            {
                int value;
                return EffectiveOverrides.TryGetValue(StatType.Agility, out value) ? value : (int?)null;
            }
            set
            {
                if (value.HasValue)
                {
                    EffectiveOverrides[StatType.Agility] = value.Value;
                }
                else
                {
                    EffectiveOverrides.Remove(StatType.Agility);
                }
            }
        }

        public MockCombatUnit(string name, int strength, int endurance, int health, int agility = 5, bool isHero = true)
        {
            DisplayName = name;
            Stats = new Stats(new StatBlock(
                new UnitStat(StatType.Strength, strength),
                new UnitStat(StatType.Endurance, endurance),
                new UnitStat(StatType.MaxHealth, health),
                new UnitStat(StatType.Agility, agility)));
            IsHero = isHero;
        }

        public int GetEffectiveStat(StatType stat)
        {
            int value;
            return EffectiveOverrides.TryGetValue(stat, out value) ? value : Stats[stat];
        }

        /// <summary>Mocks swing off Strength unless a test says otherwise.</summary>
        public StatType AttackStat { get; set; } = StatType.Strength;

        public int GetEffectiveAttackPower()
        {
            return GetEffectiveStat(AttackStat);
        }
    }
}
