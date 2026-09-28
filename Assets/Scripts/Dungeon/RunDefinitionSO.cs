using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Run Definition")]
    public class RunDefinitionSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;

        [TextArea(2, 4)]
        [Tooltip("One or two lines of flavour, shown on the campaign map when this run is selected.")]
        public string Blurb;

        [Tooltip("Where this run sits in the intended play order (0 = first). Runs are not chained yet — " +
                 "MainMenuManager still points at a single run — but the balance analyzer needs an order " +
                 "to report what each run unlocks relative to the ones before it.")]
        public int SequenceIndex;

        [Tooltip("Whether the run can be started again after it has been completed once. Off for " +
                 "one-shot content like the tutorial; on for farmable runs.")]
        public bool Repeatable;

        [Tooltip("Opens early, meant late. A challenge run is placed where the player can see it long " +
                 "before they can survive it - its campaign position says when it *opens*, not who it " +
                 "is for. The balance analyzer measures it against the strongest party the rest of " +
                 "the campaign produces, at the deepest tier, instead of the party that first reaches " +
                 "it; that is what keeps it honest (it must still be clearable by someone) without " +
                 "reporting the design as broken.")]
        public bool Challenge;

        public List<RunLevelEntry> Levels = new List<RunLevelEntry>();
    }
}
