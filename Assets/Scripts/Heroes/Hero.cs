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
        // folded in — exactly like GetEffectiveAttack() and friends.
        List<Resistance> ICombatUnit.Resistances => GetEffectiveResistances();

        /// <summary>Heroes deal physical damage; elemental output comes from magic, not basic attacks.</summary>
        public DamageType AttackDamageType => DamageType.Normal;

        public void Initialize(HeroSO heroSO)
        {
            HeroSO = heroSO;
            Level = 1;
            CurrentXp = 0;
            Stats = new Stats(heroSO.BaseAttack, heroSO.BaseDefense, heroSO.BaseHealth, heroSO.BaseAgility);
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
            Stats.Attack += config.AttackGain;
            Stats.Defense += config.DefenseGain;
            Stats.MaxHealth += config.HealthGain;
            Stats.Health += config.HealthGain;
            Stats.Agility += config.AgilityGain;
            Debug.Log($"{HeroKey} leveled up to {Level}!");
        }

        public int GetEffectiveAttack()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float baseVal = Stats.Attack + raw[StatType.Attack];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.Attack] / 100f));
        }

        public int GetEffectiveDefense()
        {
            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float baseVal = Stats.Defense + raw[StatType.Defense];
            return Mathf.RoundToInt(baseVal * (1f + pct[StatType.Defense] / 100f));
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
