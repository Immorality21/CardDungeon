using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
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
            Stats = new Stats(heroSO.BaseStrength, heroSO.BaseEndurance, heroSO.BaseHealth, heroSO.BaseAgility,
                heroSO.BaseIntelligence, heroSO.BaseSpirit, heroSO.BaseLuck);
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
            Stats.Strength += config.StrengthGain;
            Stats.Endurance += config.EnduranceGain;
            Stats.MaxHealth += config.HealthGain;
            Stats.Health += config.HealthGain;
            Stats.Agility += config.AgilityGain;
            Debug.Log($"{HeroKey} leveled up to {Level}!");
        }

        /// <summary>
        /// The attribute this hero swings with, per <see cref="HeroSO.AttackStat"/>. Falls back to
        /// Strength when no definition is set, which is what every hero did before the field existed.
        /// </summary>
        public int GetEffectiveAttackPower()
        {
            if (HeroSO == null)
            {
                return GetEffectiveStrength();
            }

            switch (HeroSO.AttackStat)
            {
                case StatType.Agility:
                    return GetEffectiveAgility();
                case StatType.Intelligence:
                    return GetEffectiveIntelligence();
                case StatType.Spirit:
                    return GetEffectiveSpirit();
                case StatType.Luck:
                    return GetEffectiveLuck();
                case StatType.Endurance:
                    return GetEffectiveEndurance();
                default:
                    return GetEffectiveStrength();
            }
        }

        public int GetEffectiveStrength()
        {
            return EffectiveStat(Stats.Strength, StatType.Strength);
        }

        public int GetEffectiveIntelligence()
        {
            return EffectiveStat(Stats.Intelligence, StatType.Intelligence);
        }

        public int GetEffectiveSpirit()
        {
            return EffectiveStat(Stats.Spirit, StatType.Spirit);
        }

        public int GetEffectiveLuck()
        {
            return EffectiveStat(Stats.Luck, StatType.Luck);
        }

        /// <summary>Base value plus this hero's raw then percentage gear bonuses for that stat.</summary>
        private int EffectiveStat(int baseValue, StatType stat)
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float value = baseValue + raw[stat];
            return Mathf.RoundToInt(value * (1f + pct[stat] / 100f));
        }

        public int GetEffectiveEndurance()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float baseVal = Stats.Endurance + raw[StatType.Endurance];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.Endurance] / 100f));
        }

        public int GetEffectiveMaxHealth()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float baseVal = Stats.MaxHealth + raw[StatType.MaxHealth];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.MaxHealth] / 100f));
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

        public int GetEffectiveAgility()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float baseVal = Stats.Agility + raw[StatType.Agility];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.Agility] / 100f));
        }
    }
}
