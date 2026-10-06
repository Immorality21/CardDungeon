using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// What a revisit does to the run being played, resolved once from the player's chosen conditions
    /// (<c>RevisitOps.Resolve</c>) and read by the combat and spawn code. Pure numbers, no assets, so
    /// the layers below Dungeon (Combat, Cards, Enemies) can read it without knowing a revisit exists.
    ///
    /// <para><b><see cref="Current"/> is a static on purpose</b>, the same call
    /// <c>EnemyManager.LevelTuning</c> made: the damage, healing and spawn paths that read it are
    /// reached from places with no idea which run they are in (a room event waking an enemy, a
    /// regeneration tick, a potion). <c>DungeonManager</c> sets it on every level build, from
    /// <c>Run.json</c>, so it can never outlive the run that set it. Anything that is not a revisit
    /// gets <see cref="None"/>, under which every method below is the identity.</para>
    /// </summary>
    public sealed class RunFear
    {
        /// <summary>A first clear, free play, the sandbox: nothing changes.</summary>
        public static readonly RunFear None = new RunFear();

        private static RunFear _current = None;

        /// <summary>The fear of the run being played. Never null.</summary>
        public static RunFear Current
        {
            get => _current;
            set => _current = value ?? None;
        }

        public bool IsRevisit { get; set; }

        /// <summary>
        /// The Fear level: the sum of the chosen conditions' +numbers. The revisit's own base costs
        /// nothing, so a plain revisit is Fear level 0. The player-facing name is deliberately ours,
        /// not Hades' "Heat" (owner, 2026-10-06).
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// The revisit's base: multiplies every enemy stat (bosses included) and the base power of
        /// its spells. The three multipliers below are conditions stacked on top of it.
        /// </summary>
        public float EnemyStatMultiplier { get; set; } = 1f;

        /// <summary>Multiplies every enemy's MaxHealth, on top of the base.</summary>
        public float EnemyHealthMultiplier { get; set; } = 1f;

        /// <summary>
        /// Multiplies what an enemy hits with, on top of the base: its Strength and Intelligence (so
        /// basic attacks and spells' stat part) and its spells' base power, damage-over-time included.
        /// Applied through the stats rather than to each hit, so the balance model prices it the same
        /// way the game rolls it.
        /// </summary>
        public float EnemyDamageMultiplier { get; set; } = 1f;

        /// <summary>Multiplies every enemy's Agility (how soon and how often it acts), on top of the base.</summary>
        public float EnemyAgilityMultiplier { get; set; } = 1f;

        /// <summary>Bodies added to every ordinary room that already rolled at least one enemy.</summary>
        public int ExtraEnemiesPerRoom { get; set; }

        /// <summary>Multiplies every heal a hero receives: spells, regeneration, drain, potions, refuges.</summary>
        public float HeroHealingMultiplier { get; set; } = 1f;

        /// <summary>Multiplies XP and gold per kill, and the gold and Essence a floor clear pays.</summary>
        public float RewardMultiplier { get; set; } = 1f;

        /// <summary>
        /// Item keys this run does not drop: the scarce materials, on a revisit that is not beating
        /// the run's best fear (<c>RevisitRulesSO.NewBestFearOnly</c>). Empty everywhere else.
        /// </summary>
        public List<string> WithheldItemKeys { get; set; } = new List<string>();

        /// <summary>Whether a drop of <paramref name="itemKey"/> is withheld on this run.</summary>
        public bool Withholds(string itemKey)
        {
            return !string.IsNullOrEmpty(itemKey) && WithheldItemKeys != null && WithheldItemKeys.Contains(itemKey);
        }

        /// <summary>
        /// Health a heal on <paramref name="target"/> restores, after the revisit. Only heroes (and
        /// the units fighting on their side) are affected, so an enemy healer still heals.
        /// Rounded down: "no healing" at 0% must mean zero, not one.
        /// </summary>
        public int ScaleHealing(ICombatUnit target, int amount)
        {
            if (amount <= 0 || target == null || !target.IsHero)
            {
                return amount;
            }
            return ScaleHeroHealing(amount);
        }

        /// <summary><see cref="ScaleHealing"/> for a caller that already knows the target is a hero.</summary>
        public int ScaleHeroHealing(int amount)
        {
            if (amount <= 0 || Mathf.Approximately(HeroHealingMultiplier, 1f))
            {
                return amount;
            }
            return Mathf.Max(0, Mathf.FloorToInt(amount * Mathf.Max(0f, HeroHealingMultiplier)));
        }

        /// <summary>
        /// An enemy's stats after the revisit: every stat by the base, then health, damage and
        /// Agility by their conditions. A stat the enemy does not have stays 0, and a positive one
        /// never scales to nothing. Read through <c>LevelEnemyTuning.StatsFor</c>, never directly,
        /// so the game and the balance model cannot disagree.
        /// </summary>
        public void ScaleEnemyStats(StatBlock stats)
        {
            if (stats == null)
            {
                return;
            }
            if (!Mathf.Approximately(EnemyStatMultiplier, 1f))
            {
                foreach (var stat in StatCatalog.Types)
                {
                    stats[stat] = Scale(stats[stat], EnemyStatMultiplier);
                }
            }
            stats[StatType.MaxHealth] = ScaleEnemyMaxHealth(stats[StatType.MaxHealth]);
            stats[StatType.Agility] = ScaleEnemyAgility(stats[StatType.Agility]);
            stats[StatType.Strength] = Scale(stats[StatType.Strength], EnemyDamageMultiplier);
            stats[StatType.Intelligence] = Scale(stats[StatType.Intelligence], EnemyDamageMultiplier);
        }

        /// <summary>The base power of an enemy's spells: the base, then the damage condition.</summary>
        public float EnemySpellPowerScale => EnemyStatMultiplier * EnemyDamageMultiplier;

        /// <summary>
        /// The authored base power of a damage-over-time effect an enemy applies (a poison, a
        /// burn), scaled like its spells. The caster's stat contribution is added after this and is
        /// already scaled through the stats, so only the base is scaled here (revisit DoT, 2026-10-06).
        /// </summary>
        public int ScaleEnemyOverTimePower(int basePower)
        {
            return Scale(basePower, EnemySpellPowerScale);
        }

        private static int Scale(int value, float multiplier)
        {
            if (value <= 0 || Mathf.Approximately(multiplier, 1f))
            {
                return value;
            }
            return Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
        }

        public int ScaleEnemyAgility(int agility)
        {
            if (agility <= 0 || Mathf.Approximately(EnemyAgilityMultiplier, 1f))
            {
                return agility;
            }
            return Mathf.Max(1, Mathf.RoundToInt(agility * EnemyAgilityMultiplier));
        }

        public int ScaleEnemyMaxHealth(int maxHealth)
        {
            if (maxHealth <= 0 || Mathf.Approximately(EnemyHealthMultiplier, 1f))
            {
                return maxHealth;
            }
            return Mathf.Max(1, Mathf.RoundToInt(maxHealth * EnemyHealthMultiplier));
        }

        /// <summary>A reward after the revisit's multiplier. A positive reward never scales to nothing.</summary>
        public int ScaleReward(int value)
        {
            if (value <= 0 || Mathf.Approximately(RewardMultiplier, 1f))
            {
                return Mathf.Max(0, value);
            }
            return Mathf.Max(1, Mathf.RoundToInt(value * RewardMultiplier));
        }
    }
}
