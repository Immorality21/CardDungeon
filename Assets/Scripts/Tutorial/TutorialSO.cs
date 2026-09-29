using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>One authored line: what the tutorial says for a <see cref="TutorialCue"/>.</summary>
    [Serializable]
    public class TutorialLine
    {
        public TutorialCue Cue;

        [TextArea(2, 4)]
        public string Text;
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
