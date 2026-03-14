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

        public string HeroKey => HeroSO != null ? HeroSO.Label : "";
        public string DisplayName => HeroKey;
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero => true;
        public Transform Transform => transform;

        Stats ICombatUnit.Stats => Stats;

        public void Initialize(HeroSO heroSO)
        {
            HeroSO = heroSO;
            Level = 1;
            CurrentXp = 0;
            Stats = new Stats(heroSO.BaseAttack, heroSO.BaseDefense, heroSO.BaseHealth, heroSO.BaseAgility);
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

        public void ApplyLevelUp(LevelConfiguration config)
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
    }
}
