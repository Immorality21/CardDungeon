using UnityEngine;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// The arithmetic of the Rogue's Steal (FFX's Steal): <see cref="BaseChance"/> plus
    /// <see cref="PerLuck"/> per point of the thief's Luck, capped at <see cref="MaxChance"/>, halved
    /// against a boss. An effect's bonus points (Grand Heist's 100) are added after the cap, so a
    /// certain steal is certain even on a boss. Covered by <c>RogueKitTests</c>.
    /// </summary>
    public static class StealOps
    {
        public const float BaseChance = 0.40f;
        public const float PerLuck = 0.02f;
        public const float MaxChance = 0.90f;

        public static float Chance(int luck, bool isBoss, int bonusPercent = 0)
        {
            float chance = Mathf.Min(MaxChance, BaseChance + PerLuck * Mathf.Max(0, luck));
            if (isBoss)
            {
                chance *= 0.5f;
            }
            return Mathf.Clamp01(chance + bonusPercent / 100f);
        }

        /// <summary>The gold a steal pays when the enemy has nothing on its drop table: half its kill gold, at least 1.</summary>
        public static int FallbackGold(int killGold)
        {
            return Mathf.Max(1, killGold / 2);
        }

        /// <summary><paramref name="gold"/> with a fight's gold bonus (Fence) applied.</summary>
        public static int WithBonus(int gold, int bonusPercent)
        {
            return bonusPercent <= 0 ? gold : Mathf.RoundToInt(gold * (100 + bonusPercent) / 100f);
        }
    }
}
