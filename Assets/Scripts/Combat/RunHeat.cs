using System.Collections.Generic;
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
    public sealed class RunHeat
    {
        /// <summary>A first clear, free play, the sandbox: nothing changes.</summary>
        public static readonly RunHeat None = new RunHeat();

        private static RunHeat _current = None;

        /// <summary>The heat of the run being played. Never null.</summary>
        public static RunHeat Current
        {
            get => _current;
            set => _current = value ?? None;
        }

        public bool IsRevisit { get; set; }

        /// <summary>Total heat of the chosen conditions. The revisit's own base costs nothing.</summary>
        public int Heat { get; set; }

        /// <summary>Multiplies every enemy's MaxHealth, bosses included.</summary>
        public float EnemyHealthMultiplier { get; set; } = 1f;

        /// <summary>Multiplies the raw damage of everything an enemy deals, before defence.</summary>
        public float EnemyDamageMultiplier { get; set; } = 1f;

        /// <summary>Bodies added to every ordinary room that already rolled at least one enemy.</summary>
        public int ExtraEnemiesPerRoom { get; set; }

        /// <summary>Multiplies every heal a hero receives: spells, regeneration, drain, potions, refuges.</summary>
        public float HeroHealingMultiplier { get; set; } = 1f;

        /// <summary>Multiplies XP and gold per kill, and the gold and Essence a floor clear pays.</summary>
        public float RewardMultiplier { get; set; } = 1f;

        /// <summary>
        /// Item keys this run does not drop: the scarce materials, on a revisit that is not beating
        /// the run's best heat (<c>RevisitRulesSO.NewBestHeatOnly</c>). Empty everywhere else.
        /// </summary>
        public List<string> WithheldItemKeys { get; set; } = new List<string>();

        /// <summary>Whether a drop of <paramref name="itemKey"/> is withheld on this run.</summary>
        public bool Withholds(string itemKey)
        {
            return !string.IsNullOrEmpty(itemKey) && WithheldItemKeys != null && WithheldItemKeys.Contains(itemKey);
        }

        /// <summary>
        /// Raw damage <paramref name="source"/> deals, after the revisit. Only enemies are scaled; a
        /// positive hit never scales to nothing.
        /// </summary>
        public int ScaleOutgoingDamage(ICombatUnit source, int raw)
        {
            if (raw <= 0 || source == null || source.IsHero || Mathf.Approximately(EnemyDamageMultiplier, 1f))
            {
                return raw;
            }
            return Mathf.Max(1, Mathf.RoundToInt(raw * EnemyDamageMultiplier));
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
