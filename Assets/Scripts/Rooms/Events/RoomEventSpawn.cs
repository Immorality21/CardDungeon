using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>
    /// Whether a room event turns up at all. Separate from <see cref="RoomEventResolver"/>, which
    /// decides what happens once the player engages one: this is the roll that decides the event is
    /// *there*.
    ///
    /// <para>Rarity is per event rather than per level. A level used to be handed a budget ("two
    /// rooms get an event"), which made every eligible room in a small level a near certainty; a base
    /// chance on the event itself lets a common find and a once-a-run find sit in the same room pool.
    /// </para>
    ///
    /// <para>Pure and caller-rolled, like <c>LootRoller</c> and <see cref="RoomEventResolver"/>, so
    /// the odds are testable without generating a dungeon.</para>
    /// </summary>
    public static class RoomEventSpawn
    {
        public const float MaxChancePercent = 100f;

        /// <summary>
        /// The event's chance to appear, as a percentage:
        /// <c>base + base * (stat * rate / 100)</c>.
        ///
        /// <para>The modifier is <b>relative to the base</b>, so it scales a rare find and a common
        /// one by the same proportion instead of swamping the rare one. A rate of 0 (or no modifier
        /// stat) leaves the base alone.</para>
        ///
        /// <para>Worth knowing when authoring: the boost is small unless the rate is well above 1.
        /// At base 5 with a rate of 1.5, a hero with 10 Luck reaches 5.75% - a difference no player
        /// will feel. Rates in the 5-20 range are where the stat starts to read.</para>
        /// </summary>
        public static float ChancePercent(float basePercent, int statValue, float modifierRate)
        {
            if (basePercent <= 0f)
            {
                return 0f;
            }

            if (statValue <= 0 || Mathf.Approximately(modifierRate, 0f))
            {
                return Mathf.Clamp(basePercent, 0f, MaxChancePercent);
            }

            float boosted = basePercent + basePercent * (statValue * modifierRate / 100f);
            return Mathf.Clamp(boosted, 0f, MaxChancePercent);
        }

        /// <summary>
        /// Whether the party clears an event's stat gate: <b>every</b> requirement must be met, though
        /// not necessarily by the same hero. An empty list is no gate at all.
        ///
        /// <para>Requirements of 10 Strength and 15 Intelligence pass for a party whose Warrior has 11
        /// Strength and whose Acolyte has 20 Intelligence - one each. They fail if nobody reaches 15
        /// Intelligence, however strong the Warrior is.</para>
        ///
        /// <para>That "not the same hero" rule is why this takes the party's best value <i>per stat</i>:
        /// the maximum over heroes, checked stat by stat, is exactly "somebody in the party covers
        /// this one".</para>
        ///
        /// <para>Rows still sitting at <see cref="StatType.None"/> are skipped rather than treated as
        /// impossible - that is the state a freshly added inspector row is in, and a half-authored
        /// row should do nothing rather than silently delete the event from the game.</para>
        /// </summary>
        public static bool MeetsRequirements(IReadOnlyList<UnitStat> requirements, StatBlock partyBest)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (requirement == null || requirement.Type == StatType.None)
                {
                    continue;
                }

                if (partyBest == null || partyBest[requirement.Type] < requirement.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether the event appears, given a caller-supplied <paramref name="roll"/> in [0,100)
        /// (e.g. <c>Random.Range(0f, 100f)</c>). Explicit so placement is deterministic under test
        /// and reproducible from a dungeon seed.
        /// </summary>
        public static bool Spawns(float chancePercent, float roll)
        {
            return roll < chancePercent;
        }
    }
}
