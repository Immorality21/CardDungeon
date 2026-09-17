using System;
using System.Collections.Generic;

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
    /// <para>Since 2026-09-17 the even split is the <b>default</b> rather than the only shape: the
    /// campfire can favour one hero (<see cref="XpSplitMode"/>). Every mode hands out exactly the
    /// same total — favouring is redistribution, never a bonus — so nothing here can make a run pay
    /// more XP than it earned, and the balance model's accounting survives whatever the player
    /// picks.</para>
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
        /// The same division, with one hero at <paramref name="favouredIndex"/> taking a
        /// <see cref="FavouredShares"/> share while everyone else takes one. Sums exactly to
        /// <paramref name="total"/>, and falls back to the even split when the index names nobody —
        /// a party that has lost its mentor keeps earning rather than silently paying no one.
        ///
        /// <para>The remainder goes to the <b>favoured</b> hero rather than to the leader. The
        /// no-XP-is-dropped rule is the same one in the class summary, but who catches it matters
        /// more here: the pool divides into an extra part, so on a small award the remainder is a
        /// real slice, and handing it to index 0 could leave the leader tied with the hero the
        /// player deliberately chose. Giving it to the favoured hero makes their share
        /// unambiguously the largest at every award size.</para>
        /// </summary>
        public static int[] Split(int total, int partySize, int favouredIndex)
        {
            if (favouredIndex < 0 || favouredIndex >= partySize)
            {
                return Split(total, partySize);
            }
            if (partySize <= 0 || total <= 0)
            {
                return Array.Empty<int>();
            }

            // One extra share exists for the favoured hero, so the pool divides into
            // partySize + (FavouredShares - 1) parts rather than partySize.
            int parts = partySize + (FavouredShares - 1);
            var shares = new int[partySize];
            int unit = total / parts;
            int handedOut = 0;
            for (int i = 0; i < partySize; i++)
            {
                shares[i] = i == favouredIndex ? unit * FavouredShares : unit;
                handedOut += shares[i];
            }
            shares[favouredIndex] += total - handedOut;
            return shares;
        }

        /// <summary>
        /// How many shares the favoured hero takes. Two, because it has to be worth choosing in a
        /// party of four without making the other three feel unpaid: at a double share the mentor
        /// takes 40% of a four-hero run rather than 25%, and nobody drops below 20%.
        /// </summary>
        public const int FavouredShares = 2;

        /// <summary>
        /// Who the favoured share belongs to under <paramref name="mode"/>, or -1 for an even split.
        /// <paramref name="bankedXp"/> is read only by <see cref="XpSplitMode.CatchUp"/>, which picks
        /// the lowest — ties going to the earliest, so the choice is stable rather than flickering
        /// between two equally-behind heroes on consecutive kills.
        /// </summary>
        public static int FavouredIndex(XpSplitMode mode, int nominatedIndex, IReadOnlyList<int> bankedXp)
        {
            switch (mode)
            {
                case XpSplitMode.Mentor:
                    return nominatedIndex;

                case XpSplitMode.CatchUp:
                    if (bankedXp == null || bankedXp.Count == 0)
                    {
                        return -1;
                    }
                    int lowest = 0;
                    for (int i = 1; i < bankedXp.Count; i++)
                    {
                        if (bankedXp[i] < bankedXp[lowest])
                        {
                            lowest = i;
                        }
                    }
                    return lowest;

                default:
                    return -1;
            }
        }

        /// <summary>
        /// What one hero in a party of <paramref name="partySize"/> can expect from a pool of
        /// <paramref name="total"/> XP - the leader's remainder ignored. Used by the balance model,
        /// which asks "does the run pay enough XP to level a hero" and must not read the leader's
        /// rounding as everyone's income.
        ///
        /// <para>Deliberately the <b>even</b> share whatever mode the player has chosen. The model
        /// reports the baseline a run pays; favouring moves XP between heroes without changing the
        /// total, so an average over the party is unmoved by it and a mode-aware figure here would
        /// only describe one hero in the lineup.</para>
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
