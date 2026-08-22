using System;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// How a kill's XP is divided across the party. Pure math, kept out of <see cref="Party"/> so it
    /// can be tested without a scene.
    ///
    /// <para>XP used to go entirely to the leader, which meant followers never levelled and party
    /// width was a free upgrade. An even split is what makes party size a decision: going wide buys
    /// safety and faster clears, going narrow buys depth, because a solo hero levels four times as
    /// fast as one of four.</para>
    ///
    /// <para>Two rules worth stating because they are choices, not consequences:
    /// <list type="bullet">
    /// <item>The <b>remainder goes to the leader</b> rather than being dropped. Integer division
    /// loses up to <c>partySize - 1</c> XP per kill, and silently losing it would make wide parties
    /// worse than the split implies - which is exactly the thing this is trying not to do.</item>
    /// <item><b>Downed heroes are paid.</b> FFX pays only who acted, but excluding the downed
    /// punishes the tank role for doing its job; death's cost stays HP and items.</item>
    /// </list></para>
    /// </summary>
    public static class XpSplit
    {
        /// <summary>
        /// Per-hero XP for a party of <paramref name="partySize"/>, index 0 being the leader (who
        /// carries the remainder). Sums exactly to <paramref name="total"/>. Empty when there is
        /// nobody to pay or nothing to pay them.
        /// </summary>
        public static int[] Split(int total, int partySize)
        {
            if (partySize <= 0 || total <= 0)
            {
                return Array.Empty<int>();
            }

            var shares = new int[partySize];
            int share = total / partySize;
            for (int i = 0; i < partySize; i++)
            {
                shares[i] = share;
            }
            shares[0] += total - (share * partySize);
            return shares;
        }

        /// <summary>
        /// What one hero in a party of <paramref name="partySize"/> can expect from a pool of
        /// <paramref name="total"/> XP - the leader's remainder ignored. Used by the balance model,
        /// which asks "does the run pay enough XP to level a hero" and must not read the leader's
        /// rounding as everyone's income.
        /// </summary>
        public static float ExpectedShare(float total, int partySize)
        {
            if (partySize <= 0)
            {
                return 0f;
            }
            return total / partySize;
        }
    }
}
