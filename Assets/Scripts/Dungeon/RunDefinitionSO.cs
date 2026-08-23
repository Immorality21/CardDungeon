using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Run Definition")]
    public class RunDefinitionSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;

        [Tooltip("Where this run sits in the intended play order (0 = first). Runs are not chained yet — " +
                 "MainMenuManager still points at a single run — but the balance analyzer needs an order " +
                 "to report what each run unlocks relative to the ones before it.")]
        public int SequenceIndex;

        [Tooltip("Whether the run can be started again after it has been completed once. Off for " +
                 "one-shot content like the tutorial; on for farmable runs.")]
        public bool Repeatable;

        public List<RunLevelEntry> Levels = new List<RunLevelEntry>();
    }
}
