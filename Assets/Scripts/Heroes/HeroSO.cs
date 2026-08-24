using System.Collections.Generic;
using Assets.Scripts.UnitStats;
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

        public Sprite[] AnimationFrames;
        public float AnimationFps = 8f;

        [Tooltip("Starting stats. Absent entries read as 0, so only list what this hero has.")]
        public StatBlock BaseStats = StatBlock.Defaults();

        [Tooltip("Which stat this hero's basic Attack command scales off. Strength for a fighter, " +
                 "Agility for a duellist, Intelligence or Spirit for a caster who should not be " +
                 "useless with a stick. None falls back to Strength.")]
        public StatType AttackStat = StatType.Strength;

        /// <summary>
        /// <see cref="AttackStat"/> with the nonsense cases folded away: unset, or any stat that is a
        /// pool rather than an output, both mean Strength. Lives here rather than at each reader so
        /// the fallback rule cannot drift between the game and the balance model.
        ///
        /// <para>The pool test is <see cref="StatCatalog.CanScalePower"/>, not a hand-written check
        /// for MaxHealth. It used to name MaxHealth directly, which meant a second pool stat added
        /// later (Mana, Stamina) would let a hero swing off its own resource bar.</para>
        /// </summary>
        public StatType ResolvedAttackStat
        {
            get { return StatCatalog.CanScalePower(AttackStat) ? AttackStat : StatType.Strength; }
        }

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
