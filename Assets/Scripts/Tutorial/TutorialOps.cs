using System.Collections.Generic;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Everything about a save the tutorial rules need, gathered by the hub so <see cref="TutorialOps"/>
    /// never touches a singleton — the same split <c>HubProgress</c> makes for <c>BuildingOps</c>.
    /// </summary>
    public struct TutorialProgress
    {
        /// <summary>A New Game started the tutorial. Saves older than the tutorial never set it.</summary>
        public bool Started;

        /// <summary>The player walked the whole loop (or it was skipped). Nothing is guided after this.</summary>
        public bool Finished;

        /// <summary>The run's opening floor has been cleared at least once.</summary>
        public bool FirstFloorCleared;

        /// <summary>The building the tutorial guides to is standing.</summary>
        public bool GuideBuilt;

        /// <summary>It is on offer and the inventory holds its price right now.</summary>
        public bool CanAffordGuide;

        /// <summary>The starting hero has bought at least one node (defaults do not count).</summary>
        public bool HeroHasSpent;

        /// <summary>The starting hero can buy a node right now — <see cref="TutorialOps.GuidedNodeKey"/> found one.</summary>
        public bool HeroCanAffordNode;
    }

    /// <summary>
    /// The rules of the guided first hour, pure and scene-free (<c>TutorialOpsTests</c>).
    ///
    /// <para><b>The loop it walks</b> is the one the opening content was priced for: a New Game goes
    /// straight into the first floor, which guarantees the one timber the Hall of Progression costs
    /// (<c>LevelDefinitionSO.GuaranteedMaterials</c>) and pays the starting hero more XP than their
    /// cheapest node (<c>TutorialContentTests</c>). Home again, the town is locked to the hall, the
    /// hall's panel to its Build button, and the grid to that one node — then everything opens and
    /// the road is pointed at once.</para>
    ///
    /// <para><b>It can never strand a save.</b> A step whose promise the save cannot keep — no timber
    /// to build with, no node the XP covers — is skipped rather than waited on, because a locked town
    /// with nothing to click is a soft-lock, not a lesson. See <see cref="CurrentStep"/>.</para>
    /// </summary>
    public static class TutorialOps
    {
        /// <summary>Where the save is. Each step is read off the save, so this is always current.</summary>
        public static TutorialStep CurrentStep(TutorialProgress p)
        {
            if (!p.Started || p.Finished)
            {
                return TutorialStep.None;
            }
            if (!p.FirstFloorCleared)
            {
                return TutorialStep.ClearFirstFloor;
            }
            if (!p.GuideBuilt && p.CanAffordGuide)
            {
                return TutorialStep.BuildHall;
            }
            if (p.GuideBuilt && !p.HeroHasSpent && p.HeroCanAffordNode)
            {
                return TutorialStep.SpendXp;
            }
            return TutorialStep.TakeTheRoad;
        }

        /// <summary>
        /// Whether the opening floor is behind this save: the run was finished, or the run save has
        /// moved past its first level. A wipe on the floor deletes the run save, so it correctly reads
        /// as not cleared. (The hub also treats a standing hall as proof — it is the only thing the
        /// floor's timber builds while the tutorial runs.)
        /// </summary>
        public static bool FirstFloorCleared(
            string tutorialRunKey,
            IReadOnlyList<string> completedRunKeys,
            string activeRunKey,
            int activeLevelIndex)
        {
            if (string.IsNullOrEmpty(tutorialRunKey))
            {
                return false;
            }
            if (completedRunKeys != null)
            {
                foreach (var key in completedRunKeys)
                {
                    if (key == tutorialRunKey)
                    {
                        return true;
                    }
                }
            }
            return activeRunKey == tutorialRunKey && activeLevelIndex >= 1;
        }

        /// <summary>
        /// The steps that take the town away: everything but the one thing the player is being
        /// walked to is disabled — the other lots, the road, the Main Menu button.
        /// </summary>
        public static bool LocksTown(TutorialStep step)
        {
            return step == TutorialStep.ClearFirstFloor
                || step == TutorialStep.BuildHall
                || step == TutorialStep.SpendXp;
        }

        /// <summary>Whether a lot can be clicked. While the town is locked, only the guide building can.</summary>
        public static bool AllowsLot(TutorialStep step, string lotKey, string guideKey)
        {
            if (step == TutorialStep.ClearFirstFloor)
            {
                return false;
            }
            if (step == TutorialStep.BuildHall || step == TutorialStep.SpendXp)
            {
                return !string.IsNullOrEmpty(lotKey) && lotKey == guideKey;
            }
            return true;
        }

        /// <summary>Whether a lot carries the tutorial's pointer.</summary>
        public static bool GuidesLot(TutorialStep step, string lotKey, string guideKey)
        {
            return (step == TutorialStep.BuildHall || step == TutorialStep.SpendXp)
                && !string.IsNullOrEmpty(lotKey) && lotKey == guideKey;
        }

        /// <summary>The road is shut exactly while the tutorial is pointing at something else.</summary>
        public static bool AllowsRoad(TutorialStep step)
        {
            return step != TutorialStep.BuildHall && step != TutorialStep.SpendXp;
        }

        /// <summary>Whether the road carries the pointer: to go back down, or once the loop is closed.</summary>
        public static bool GuidesRoad(TutorialStep step)
        {
            return step == TutorialStep.ClearFirstFloor || step == TutorialStep.TakeTheRoad;
        }

        /// <summary>Whether the guided screens (the hall's panel, the grid) may be backed out of.</summary>
        public static bool AllowsLeaving(TutorialStep step)
        {
            return step != TutorialStep.BuildHall && step != TutorialStep.SpendXp;
        }

        /// <summary>
        /// What the tutorial says on <paramref name="screen"/> at <paramref name="step"/>.
        /// <paramref name="guidedNodeSelected"/> splits the grid's line in two: first "click the
        /// node", then "now activate it".
        /// </summary>
        public static TutorialCue CueFor(TutorialStep step, TutorialScreen screen, bool guidedNodeSelected)
        {
            switch (step)
            {
                case TutorialStep.ClearFirstFloor:
                    return screen == TutorialScreen.Town ? TutorialCue.RetryFirstFloor : TutorialCue.None;

                case TutorialStep.BuildHall:
                    if (screen == TutorialScreen.Town)
                    {
                        return TutorialCue.BuildHallInTown;
                    }
                    return screen == TutorialScreen.GuideLot ? TutorialCue.BuildHallOnPanel : TutorialCue.None;

                case TutorialStep.SpendXp:
                    switch (screen)
                    {
                        case TutorialScreen.Town:
                            return TutorialCue.OpenHallInTown;
                        case TutorialScreen.GuideLot:
                            return TutorialCue.EnterHallOnPanel;
                        case TutorialScreen.Grid:
                            return guidedNodeSelected ? TutorialCue.ActivateNode : TutorialCue.PickNode;
                        default:
                            return TutorialCue.None;
                    }

                case TutorialStep.TakeTheRoad:
                    if (screen == TutorialScreen.Town)
                    {
                        return TutorialCue.TakeTheRoad;
                    }
                    return screen == TutorialScreen.Grid ? TutorialCue.NodeLearned : TutorialCue.None;

                default:
                    return TutorialCue.None;
            }
        }

        /// <summary>
        /// The node the tutorial walks the player to: the cheapest one they can buy right now with XP
        /// alone, ties broken by the grid's own list order (the authored spine first). Null when there
        /// is none — which <see cref="CurrentStep"/> reads as "skip the step", never "wait for it".
        /// Nodes that also cost materials or Essence are passed over: the tutorial promises an XP spend.
        /// </summary>
        public static string GuidedNodeKey(SphereGridSO grid, ICollection<string> activated, int bank)
        {
            if (grid == null)
            {
                return null;
            }

            var owned = activated ?? new List<string>();
            SphereGridNode best = null;
            foreach (var node in SphereGridOps.Frontier(grid, owned))
            {
                if (SphereGridOps.HasMaterialCost(node)
                    || SphereGridOps.HasEssenceCost(node)
                    || !SphereGridOps.CanActivate(grid, owned, bank, node.Key))
                {
                    continue;
                }
                if (best == null || node.XpCost < best.XpCost)
                {
                    best = node;
                }
            }
            return best?.Key;
        }
    }
}
