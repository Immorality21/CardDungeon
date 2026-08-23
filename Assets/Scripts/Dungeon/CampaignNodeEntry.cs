using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// One run's place in the campaign graph: the run itself, what has to be cleared before it opens,
    /// and where it sits on the map screen.
    ///
    /// <para>Prerequisites point at <see cref="RunDefinitionSO"/> assets rather than key strings so a
    /// renamed run cannot silently orphan a branch - the reference survives the rename and
    /// <c>CampaignOps</c> resolves it to a key only when it checks the save.</para>
    /// </summary>
    [Serializable]
    public class CampaignNodeEntry
    {
        public RunDefinitionSO Run;

        [Tooltip("Runs that must be cleared before this one opens. Empty means it is a starting " +
                 "point, available on a fresh save.")]
        public List<RunDefinitionSO> Requires = new List<RunDefinitionSO>();

        [Tooltip("All = every prerequisite must be cleared (a chain). Any = one is enough, which is " +
                 "how two branches rejoin the main line.")]
        public CampaignUnlockMode UnlockMode = CampaignUnlockMode.All;

        [Tooltip("Not shown on the map at all until it unlocks - for secret side branches. A locked " +
                 "non-secret node is still drawn, so the player can see what they are working toward.")]
        public bool Secret;

        [Tooltip("Flavour only: marks a branch as optional side content so the map can de-emphasise " +
                 "it. Does not affect unlocking.")]
        public bool Optional;

        [Tooltip("Position on the campaign map screen, in graph units. Authored in " +
                 "Tools > Dungeon > Campaign Map Editor.")]
        public Vector2 MapPosition;
    }
}
