using System;
using System.Collections.Generic;

namespace Assets.Scripts.Combat
{
    /// <summary>A bonus to the damage a unit deals of one element: "+10% Shadow damage".</summary>
    [Serializable]
    public class ElementDamageBonus
    {
        public DamageType DamageType = DamageType.Shadow;
        public float Percent;
    }

    /// <summary>
    /// What a unit's gear adds to the damage its abilities deal - today only a set bonus does
    /// (<c>Items.ItemSets</c>, the Shadowweave set). Read by <c>DamageEffectExecutor</c> when the unit
    /// is the caster, so the live fight and the balance model, which share that executor, agree.
    /// </summary>
    public sealed class OutgoingDamage
    {
        /// <summary>Per element, a percent added to the raw hit before defense.</summary>
        public readonly Dictionary<DamageType, float> Percent = new Dictionary<DamageType, float>();

        /// <summary>Every ability's hit also deals this percent of its raw power as
        /// <see cref="AbilityExtraType"/> damage, met by that element's resistance but not by defense.</summary>
        public float AbilityExtraPercent;

        public DamageType AbilityExtraType = DamageType.Shadow;

        public static readonly OutgoingDamage None = new OutgoingDamage();

        public bool IsEmpty => Percent.Count == 0 && AbilityExtraPercent <= 0f;

        public float BonusFor(DamageType type)
        {
            return Percent.TryGetValue(type, out float value) ? value : 0f;
        }

        public void Add(DamageType type, float percent)
        {
            Percent[type] = BonusFor(type) + percent;
        }
    }

    /// <summary>A unit whose gear changes the damage it deals (a hero, or the model's stand-in for one).</summary>
    public interface IOutgoingDamage
    {
        OutgoingDamage OutgoingDamage { get; }
    }
}
