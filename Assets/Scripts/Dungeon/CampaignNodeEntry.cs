using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
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

        [Tooltip("Heroes the player must own before this run opens - a *key*, not more of a " +
                 "general thing. Unlike Requires, this is never softened by UnlockMode: every hero " +
                 "listed is required. Empty means no hero gate. See NEXT_STEPS.md section 5b: a " +
                 "hero is access rather than power, which is the one axis the game has for saying " +
                 "'not yet'. " +
                 "The trap is that a hero can sit behind an optional branch or an un-taken captive, " +
                 "so a hero gate can strand a save in a way a run gate cannot. Only gate on a hero " +
                 "the player must pass to get here - CampaignAssetTests enforces exactly that.")]
        public List<HeroSO> RequiresHeroes = new List<HeroSO>();

        [Tooltip("All = every prerequisite must be cleared (a chain). Any = one is enough, which is " +
                 "how two branches rejoin the main line. Applies to Requires only - RequiresHeroes " +
                 "is always an All.")]
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
