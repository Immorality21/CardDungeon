using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    public class Hero : MonoBehaviour, ICombatUnit
    {
        public HeroSO HeroSO;
        public Stats Stats;
        public int Level = 1;
        public int CurrentXp;
        [Tooltip("Innate elemental resistance. Gear resistance sums on top of this — see " +
                 "GetEffectiveResistances().")]
        public List<Resistance> Resistances = new List<Resistance>();

        public string HeroKey => HeroSO != null ? HeroSO.SaveKey : "";
        public string DisplayName => HeroSO != null ? HeroSO.DisplayName : "";
        public Sprite Icon => HeroSO != null ? HeroSO.Sprite : null;
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero => true;
        public Transform Transform => transform;

        Stats ICombatUnit.Stats => Stats;

        // The combat pipeline reads resistance through the interface, so this is where gear has to be
        // folded in — exactly like GetEffectiveAttackPower() and friends.
        List<Resistance> ICombatUnit.Resistances => GetEffectiveResistances();

        /// <summary>Heroes deal physical damage; elemental output comes from magic, not basic attacks.</summary>
        public DamageType AttackDamageType => DamageType.Normal;

        public void Initialize(HeroSO heroSO)
        {
            HeroSO = heroSO;
            Level = 1;
            CurrentXp = 0;
            Stats = new Stats(heroSO.BaseStats);
        }

        public void InitializeFromSave(HeroSO heroSO, int savedXp)
        {
            Initialize(heroSO);
            AddXp(savedXp);
        }

        public void AddXp(int amount)
        {
            CurrentXp += amount;

            while (true)
            {
                var nextLevel = HeroSO.LevelProgression.Find(l => l.Level == Level + 1);
                if (nextLevel == null || CurrentXp < nextLevel.XpRequired)
                {
                    break;
                }

                ApplyLevelUp(nextLevel);
            }
        }

        private void ApplyLevelUp(LevelConfiguration config)
        {
            Level = config.Level;
            // Every stat the level grants, whatever they are - no per-stat lines to forget.
            Stats.Attributes.Add(config.Gains);
            // A bigger bar heals you into it, matching the old behaviour.
            Stats.Health += config.Gains[StatType.MaxHealth];
            Debug.Log($"{HeroKey} leveled up to {Level}!");
        }

        /// <summary>
        /// The stat this hero swings with, per <see cref="HeroSO.AttackStat"/>. Falls back to
        /// Strength when unset or nonsensical (MaxHealth is a pool, not an output), which is what
        /// every hero did before the field existed.
        /// </summary>
        public StatType AttackStat
        {
            get { return HeroSO != null ? HeroSO.ResolvedAttackStat : StatType.Strength; }
        }

        public int GetEffectiveAttackPower()
        {
            return GetEffectiveStat(AttackStat);
        }

        /// <summary>
        /// Base stat plus this hero's raw then percentage gear bonuses. The one accessor for every
        /// stat, so adding a <see cref="StatType"/> needs no change here or on the interface.
        /// </summary>
        public int GetEffectiveStat(StatType stat)
        {
            if (stat == StatType.None)
            {
                return 0;
            }

            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float value = Stats[stat] + raw[stat];
            return Mathf.RoundToInt(value * (1f + pct[stat] / 100f));
        }

        /// <summary>Convenience for the HP bar and heal clamps; MaxHealth is just another stat.</summary>
        public int GetEffectiveMaxHealth()
        {
            return GetEffectiveStat(StatType.MaxHealth);
        }

        /// <summary>
        /// Innate resistance plus everything equipped gear grants, summed per damage type. Temporary
        /// combat buffs are <b>not</b> included: those stack at the damage call site through
        /// <c>CombatBuffTracker</c>, the same way stat buffs do.
        /// </summary>
        public List<Resistance> GetEffectiveResistances()
        {
            var gear = InventoryManager.Instance.ComputeResistances(HeroKey);
            if (gear.Count == 0)
            {
                return Resistances;
            }

            var combined = new List<Resistance>(Resistances);
            foreach (var entry in gear)
            {
                combined.Add(entry);
            }
            return combined;
        }
    }
}
