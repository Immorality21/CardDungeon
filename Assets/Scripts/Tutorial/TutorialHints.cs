using System.Collections.Generic;
using Assets.Scripts.Dungeon;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// The first-time hints (docs/TUTORIAL.md, "First-time hints"): one line, said once per save, the
    /// first time a rule that breaks RPG habits matters - charges and health last the level, a refuge is
    /// one rest, a summon is once per level, an Ultra's gauge is kept, cleared levels come back harder,
    /// the party has four places. Pure rules; the screens that say them are <c>RoomActionUI</c> (the
    /// dungeon) and <c>HubManager</c> (the town and the story map). Which were seen is
    /// <c>MetaProgressSaveData.HintsSeen</c>, in the order they were seen.
    /// </summary>
    public static class TutorialHints
    {
        /// <summary>
        /// Every line the pause menu's Hints window may list: the first-time hints and the older lines
        /// said once (the first death, the challenge warning, the replay hint). The guided steps are not
        /// here - they are instructions for a screen, not something to read again.
        /// </summary>
        public static readonly IReadOnlyList<TutorialCue> Logged = new[]
        {
            TutorialCue.FirstDeath,
            TutorialCue.ChallengeAhead,
            TutorialCue.ReplayToGetStronger,
            TutorialCue.ChargesLastTheLevel,
            TutorialCue.RefugeOnce,
            TutorialCue.SummonOncePerLevel,
            TutorialCue.UltraReady,
            TutorialCue.RevisitAndFear,
            TutorialCue.PartyFull,
        };

        public static bool IsLogged(TutorialCue cue)
        {
            foreach (var logged in Logged)
            {
                if (logged == cue)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether <paramref name="cue"/> is still to be said: a logged line the save has not seen.</summary>
        public static bool IsDue(ICollection<int> seen, TutorialCue cue)
        {
            return IsLogged(cue) && (seen == null || !seen.Contains((int)cue));
        }

        /// <summary>The seen lines, oldest first, as the Hints window lists them: logged cues only, each once.</summary>
        public static List<TutorialCue> SeenInOrder(IEnumerable<int> seen)
        {
            var result = new List<TutorialCue>();
            if (seen == null)
            {
                return result;
            }
            foreach (int value in seen)
            {
                var cue = (TutorialCue)value;
                if (IsLogged(cue) && !result.Contains(cue))
                {
                    result.Add(cue);
                }
            }
            return result;
        }

        /// <summary>More heroes owned than the party can take down: someone stays at home.</summary>
        public static bool PartyIsFull(int owned, int cap)
        {
            return cap > 0 && owned > cap;
        }

        /// <summary>Whether the story map offers a cleared level to play again (a revisit, with its Fear picker).</summary>
        public static bool OffersRevisit(IReadOnlyList<CampaignNodeState> states)
        {
            if (states == null)
            {
                return false;
            }
            foreach (var state in states)
            {
                if (state != null && state.CanStart && state.Status == CampaignNodeStatus.Completed
                    && state.Node != null && RevisitOps.IsRevisitable(state.Node.Run))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
