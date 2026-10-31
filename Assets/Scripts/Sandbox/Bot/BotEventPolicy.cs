using System;
using System.Collections.Generic;
using Assets.Scripts.Rooms.Events;

namespace Assets.Scripts.Sandbox.Bot
{
    /// <summary>Which option of a room event a profile takes. Pure, so a habit can be tested.</summary>
    public static class BotEventPolicy
    {
        /// <summary>The odds a <see cref="BotEventHabit.OddsOnly"/> player will gamble at.</summary>
        public const float EvenOdds = 0.5f;

        /// <summary>The odds a <see cref="BotEventHabit.Safe"/> player will gamble at.</summary>
        public const float NearlyCertain = 0.8f;

        /// <summary>
        /// The index of the option to take: the best one the habit accepts, else the event's Decline,
        /// else -1 (an event with no Decline is left by its Back button). <paramref name="available"/>
        /// says what the window offers: a blocked or unlabelled option cannot be taken. The odds are the
        /// true chance, which is at least what the player can read off the window.
        /// </summary>
        public static int Choose(IReadOnlyList<RoomEventOption> options, float chance, BotEventHabit habit,
            Func<RoomEventOption, bool> available)
        {
            int decline = -1;
            int best = -1;
            int bestScore = 0;
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null || (available != null && !available(option)))
                {
                    continue;
                }
                if (option.Kind == RoomEventOptionKind.Decline)
                {
                    decline = decline < 0 ? i : decline;
                    continue;
                }
                int score = Score(option, chance, habit);
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best >= 0 ? best : decline;
        }

        /// <summary>How much the habit wants this option; 0 is a no.</summary>
        private static int Score(RoomEventOption option, float chance, BotEventHabit habit)
        {
            bool gamble = option.Kind == RoomEventOptionKind.StatCheck;
            var risk = RoomEventResolver.RiskOf(option);
            switch (habit)
            {
                case BotEventHabit.Everything:
                    return gamble ? 3 : risk == OptionRisk.Safe ? 2 : 1;
                case BotEventHabit.OddsOnly:
                    if (gamble)
                    {
                        return chance >= EvenOdds ? 3 : 0;
                    }
                    return risk == OptionRisk.Safe ? 2 : risk == OptionRisk.Price ? 1 : 0;
                default:
                    if (gamble)
                    {
                        return chance >= NearlyCertain ? 1 : 0;
                    }
                    return risk == OptionRisk.Safe ? 2 : 0;
            }
        }
    }
}
