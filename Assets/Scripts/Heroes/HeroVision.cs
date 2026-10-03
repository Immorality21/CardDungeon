using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// What a hero is <i>meant</i> to be — the design intent behind the stats, the grid and the art,
    /// written down so grid authoring, ability design and summons have something to answer to rather
    /// than an interpretation of the hero's name. Edited in <c>Tools ▸ Heroes ▸ Hero Vision</c>.
    ///
    /// <para>Developer-facing only: nothing in the game reads or shows it. It is plain serialized
    /// data rather than editor-only, because a field under <c>#if UNITY_EDITOR</c> changes the
    /// serialized layout between the editor and a build. It costs a few hundred bytes of text.</para>
    ///
    /// <para>Branches are described, never named: "specializations are not named"
    /// (docs/NEXT_STEPS.md) applies here too, so a branch has no label field — only what it becomes
    /// and what it grants.</para>
    /// </summary>
    [Serializable]
    public class HeroVision
    {
        [Tooltip("Who they are, in a sentence or two. The fantasy every other field has to serve.")]
        [TextArea(2, 6)]
        public string Fantasy;

        [Tooltip("What they do in a fight, and the stat their abilities scale off. The scaling stat " +
                 "decides which abilities they can use well at all — check it before planning a branch " +
                 "around a spell.")]
        [TextArea(2, 6)]
        public string Role;

        [Tooltip("The thing only this hero does. If another hero could have it, it is not the signature.")]
        [TextArea(2, 6)]
        public string SignatureMechanic;

        [Tooltip("One entry per grid branch: what committing to it makes of the hero.")]
        public List<BranchVision> Branches = new List<BranchVision>();

        [Tooltip("Heroes who reach the same role, and what keeps them from converging on the same " +
                 "numbers (e.g. Cleric vs Paladin as healers).")]
        [TextArea(2, 6)]
        public string Overlaps;

        [Tooltip("Scratchpad: undecided questions about this hero.")]
        [TextArea(2, 8)]
        public string OpenQuestions;
    }

    /// <summary>One branch of a hero's grid, as intended. See <see cref="HeroVision"/>.</summary>
    [Serializable]
    public class BranchVision
    {
        [Tooltip("Which grid branch this describes — the letter in the node keys (a, b, c). Lets the " +
                 "Hero Vision window put the intent beside what that branch actually grants today.")]
        public string GridBranch;

        [Tooltip("What the hero becomes on this branch, described rather than named.")]
        [TextArea(2, 6)]
        public string Becomes;

        [Tooltip("The abilities this branch should teach, in the order it should teach them.")]
        [TextArea(2, 6)]
        public string IntendedKit;

        [Tooltip("The summon at the end of this branch: what it is and which kind (special attack or " +
                 "party replacement).")]
        [TextArea(2, 6)]
        public string Summon;
    }
}
