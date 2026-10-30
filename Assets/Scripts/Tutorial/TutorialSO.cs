using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using UnityEngine;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Tutorial
{
    /// <summary>One authored line: what the tutorial says for a <see cref="TutorialCue"/>.</summary>
    [Serializable]
    public class TutorialLine
    {
        [TextKeys("tutorial", "text")]
        public TutorialCue Cue;

        [System.NonSerialized] private string _text;

        /// <summary>From the text table (<c>tutorial.{cue}.text</c>). Set only on instances built in code.</summary>
        public string Text
        {
            get { return _text ?? Loc.GetOrEmpty(Loc.KeyFor("tutorial", Cue.ToString(), "text")); }
            set { _text = value; }
        }
    }

    /// <summary>
    /// The tutorial's content (<c>Assets/Resources/Tutorial.asset</c>, Resources-loaded like
    /// <c>CampaignSO</c> and <c>HubSO</c>): which run opens the game, which building it walks the
    /// player to, and every line it says. <b>Which</b> line is said where is
    /// <see cref="TutorialOps"/>' decision, not this asset's — see <c>docs/TUTORIAL.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Tutorial", menuName = "SO/Tutorial")]
    public class TutorialSO : ScriptableObject
    {
        public const string ResourcePath = "Tutorial";

        [Tooltip("The run a New Game is sent straight into. Its first level must guarantee the guide " +
                 "building's price and pay the starting hero at least one node (TutorialContentTests).")]
        public RunDefinitionSO FirstRun;

        [Tooltip("BuildingSO.Key of the lot the tutorial walks the player to: build it, enter it, spend XP.")]
        public string GuideBuildingKey = "sphere-hall";

        [Tooltip("Equipment awarded for clearing FirstRun's level GearLevelIndex while the tutorial runs - " +
                 "the piece the gear step has the player equip. Awarded only while the tutorial is " +
                 "active, so a revisit of the run never pays it again.")]
        public ItemSO GearReward;

        [Tooltip("Which level of FirstRun pays GearReward on clear (0-based: 1 is the second floor).")]
        [Min(0)] public int GearLevelIndex = 1;

        [Tooltip("BuildingSO.Key of the lot the gear step walks the player to: the inventory's building.")]
        public string GearBuildingKey = "storehouse";

        public List<TutorialLine> Lines = new List<TutorialLine>();

        /// <summary>The authored words for <paramref name="cue"/>, or empty when there are none.</summary>
        public string TextFor(TutorialCue cue)
        {
            if (cue == TutorialCue.None || Lines == null)
            {
                return string.Empty;
            }
            foreach (var line in Lines)
            {
                if (line != null && line.Cue == cue)
                {
                    return line.Text ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
