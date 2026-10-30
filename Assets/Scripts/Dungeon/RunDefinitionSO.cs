using System.Collections.Generic;
using Assets.Scripts.Heroes;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Dungeon
{
    [CreateAssetMenu(menuName = "SO/Run Definition")]
    public class RunDefinitionSO : ScriptableObject
    {
        [TextKeys("run", "name", "description")]
        public string Key;
        [System.NonSerialized] private string _displayName;

        /// <summary>From the text table (<c>run.{key}.name</c>). Set only on instances built in code.</summary>
        public string DisplayName
        {
            get { return _displayName ?? Loc.AssetText("run", Key, this, "name"); }
            set { _displayName = value; }
        }

        [System.NonSerialized] private string _blurb;

        /// <summary>From the text table (<c>run.{key}.description</c>). Set only on instances built in code.</summary>
        public string Blurb
        {
            get { return _blurb ?? Loc.AssetText("run", Key, this, "description"); }
            set { _blurb = value; }
        }

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

        [Tooltip("Optional hero who joins when this run is cleared - a reward for finishing it, not a " +
                 "captive found along the way. Granted on the final level's clear, in the same commit " +
                 "as its XP and loot, so it cannot be lost afterwards. Skipped when already owned. " +
                 "Counts as guaranteed for every node that requires this run (CampaignOps), so a " +
                 "campaign node may gate on this hero.")]
        public HeroSO JoinsOnClear;

        public List<RunLevelEntry> Levels = new List<RunLevelEntry>();
    }
}
