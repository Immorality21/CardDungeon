using System.Collections.Generic;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// What a campfire level grants: <b>ways to divide the run's XP</b>, and nothing else.
    ///
    /// <para>This is the campfire's answer to <c>docs/plans/HUB.md</c> §7 phase 6, and it obeys the
    /// same rule the Ability Forge does — a level grants access, capacity or information, never a
    /// raw stat. Every mode hands out the identical total (<see cref="XpSplit"/>), so a taller
    /// campfire cannot make a run pay more; it only lets the player aim what the run already paid.
    /// That is what keeps buildings a <i>precondition</i> the investment frontier tests rather than
    /// a second route from gold to power.</para>
    ///
    /// <para><b>Why this is the campfire's job.</b> The fire is already where the player answers
    /// "who marches out"; the split is the same question carried one step further — who <i>grows</i>.
    /// And it is a knob on precisely the tension the sphere grid is built around: an even split is
    /// breadth, a mentored one is depth, and <c>docs/BALANCING.md</c> §0 rule 3 says how the grid is
    /// spent matters more than how much of it is owned.</para>
    ///
    /// <para>Pure and static, like every other <c>*Ops</c> here, so the rules are testable with no
    /// scene and no save.</para>
    /// </summary>
    public static class CampfireOps
    {
        /// <summary>
        /// The modes a campfire of <paramref name="level"/> offers, always starting with
        /// <see cref="XpSplitMode.Even"/>.
        ///
        /// <para>An unbuilt campfire still offers the even split. The campfire is
        /// <c>PlacedByDefault</c> and the hub has to be usable in minute one, so level 0 can only
        /// ever be a scene with no hub asset — and degrading to "the way it has always worked"
        /// beats degrading to a party that earns nothing.</para>
        /// </summary>
        public static List<XpSplitMode> ModesFor(int level)
        {
            var modes = new List<XpSplitMode> { XpSplitMode.Even };
            if (level >= 2)
            {
                modes.Add(XpSplitMode.Mentor);
            }
            if (level >= 3)
            {
                modes.Add(XpSplitMode.CatchUp);
            }
            return modes;
        }

        /// <summary>Whether a campfire of <paramref name="level"/> offers <paramref name="mode"/>.</summary>
        public static bool Offers(int level, XpSplitMode mode)
        {
            return ModesFor(level).Contains(mode);
        }

        /// <summary>
        /// The mode actually in force: what the player chose, unless the campfire no longer offers
        /// it, in which case the even split.
        ///
        /// <para>Every read of a saved mode goes through here. A save can name a mode the town
        /// cannot currently grant — a hand-edited file, or a campfire authored shorter than it once
        /// was — and the wrong failure would be a party quietly running a mode it never unlocked.</para>
        /// </summary>
        public static XpSplitMode EffectiveMode(int level, XpSplitMode saved)
        {
            return Offers(level, saved) ? saved : XpSplitMode.Even;
        }

        /// <summary>A mode's name as the player reads it on the campfire screen.</summary>
        public static string Label(XpSplitMode mode)
        {
            switch (mode)
            {
                case XpSplitMode.Mentor:
                    return "Mentor";
                case XpSplitMode.CatchUp:
                    return "Catch Up";
                default:
                    return "Even";
            }
        }

        /// <summary>One line on what a mode does, for the screen that offers the choice.</summary>
        public static string Describe(XpSplitMode mode)
        {
            switch (mode)
            {
                case XpSplitMode.Mentor:
                    return "A hero you name takes a double share. The rest split what is left.";
                case XpSplitMode.CatchUp:
                    return "Whoever is furthest behind takes the double share, chosen fresh each time.";
                default:
                    return "Everyone who marches out takes an equal share.";
            }
        }
    }
}
