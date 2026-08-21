using System.Collections.Generic;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    [CreateAssetMenu(fileName = "NewHero", menuName = "Card Dungeon/Hero")]
    public class HeroSO : ScriptableObject
    {
        [Tooltip("Immutable identifier this hero's save data is filed under — party XP (Party.json), " +
                 "equipped gear (ItemCollection.json) and equipped magic (Run.json) all key off it. " +
                 "Never shown to the player. Changing it orphans every save that references the old " +
                 "value, so treat it as write-once.")]
        public string Key;

        [Tooltip("Name shown to the player. Safe to rename at any time — it is not a save key.")]
        public string Label;

        [Tooltip("One-line pitch shown when recruiting or rescuing this hero. Flavour plus a hint " +
                 "at the stat line, since the player is spending gold on a role.")]
        public string Blurb;

        [Tooltip("Gold the tavern charges to recruit this hero. 0 falls back to a price derived " +
                 "from the stat line (see ShopPricing.RecruitPrice).")]
        public int RecruitCost;

        public Sprite Sprite;
        public int BaseStrength;
        public int BaseEndurance;
        public int BaseHealth;
        public int BaseAgility = 5;

        [Tooltip("Scales Intelligence-scaled spell power. A caster stat: it does nothing for basic attacks.")]
        public int BaseIntelligence;

        [Tooltip("Scales Spirit-scaled spell power - healing, shields and Holy.")]
        public int BaseSpirit;

        [Tooltip("Raises crit chance on every attack, and improves stat checks on room events.")]
        public int BaseLuck;

        [Tooltip("Which attribute this hero's basic Attack command scales off. Strength for a " +
                 "fighter, Agility for a finesse duellist, Intelligence or Spirit for a caster who " +
                 "should not be useless with a stick. MaxHealth is accepted but nonsensical.")]
        public StatType AttackStat = StatType.Strength;

        public List<LevelConfiguration> LevelProgression = new List<LevelConfiguration>();

        /// <summary>
        /// The identifier save data is written under. Falls back to <see cref="Label"/> and then the
        /// asset name, so heroes authored before <see cref="Key"/> existed still resolve to the save
        /// entries they already wrote. Always key persistence off this, never off <see cref="Label"/>.
        /// </summary>
        public string SaveKey
        {
            get
            {
                if (!string.IsNullOrEmpty(Key))
                {
                    return Key;
                }
                if (!string.IsNullOrEmpty(Label))
                {
                    return Label;
                }
                return name;
            }
        }

        /// <summary>The player-facing name. Use this for anything on screen.</summary>
        public string DisplayName => string.IsNullOrEmpty(Label) ? name : Label;
    }
}
