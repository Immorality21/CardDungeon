using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// A combat unit that reacts when struck - the Final Fantasy hit reaction. Heroes always do; an
    /// enemy or a summon says so on its definition (<c>EnemySO.Flinches</c>, <c>SummonSO.Flinches</c>),
    /// because a boss or a golem standing its ground is part of what makes it read as heavy. Read by
    /// <see cref="CombatFeedback.PlayImpact"/>; presentation only, so the balance model never sees it.
    /// </summary>
    public interface IFlinches
    {
        bool Flinches { get; }

        /// <summary>The drawn hit reaction (<c>HeroSO.HitFrames</c>, <c>EnemySO.HitFrames</c>), played
        /// once over the idle loop; null or empty when the unit has none, and it only recoils.</summary>
        Sprite[] HitFrames { get; }
    }
}
