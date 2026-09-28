using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// A hero as an <see cref="ICombatUnit"/> where there is no live <see cref="Hero"/> - the hub.
    /// Built from an already-effective stat block (base + sphere grid + gear), so an ability
    /// described through it reads the numbers it will actually hit for in the next run, instead of
    /// the caster-less base values <c>AbilityDescriber</c> falls back to with a null caster.
    /// </summary>
    public class HeroSnapshotUnit : ICombatUnit
    {
        private readonly HeroSO _hero;
        private readonly StatBlock _effective;
        private readonly List<Resistance> _resistances;

        public HeroSnapshotUnit(HeroSO hero, StatBlock effectiveStats, List<Resistance> resistances = null)
        {
            _hero = hero;
            _effective = effectiveStats ?? new StatBlock();
            _resistances = resistances ?? new List<Resistance>();
            Stats = new Stats(_effective.Clone());
        }

        public string DisplayName => _hero != null ? _hero.DisplayName : string.Empty;
        public Sprite Icon => _hero != null ? _hero.Sprite : null;
        public Stats Stats { get; }
        public bool IsAlive => true;
        public bool IsHero => true;
        public Transform Transform => null;
        public List<Resistance> Resistances => _resistances;
        public DamageType AttackDamageType => DamageType.Normal;

        public StatType AttackStat => _hero != null ? _hero.ResolvedAttackStat : StatType.Strength;

        /// <summary>The stat as it stands with grid and gear folded in - already effective.</summary>
        public int GetEffectiveStat(StatType stat)
        {
            return _effective[stat];
        }

        public int GetEffectiveAttackPower()
        {
            return GetEffectiveStat(AttackStat);
        }
    }
}
