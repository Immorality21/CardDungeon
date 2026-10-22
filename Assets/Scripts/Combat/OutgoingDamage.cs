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

        /// <summary>A percent added to every hit the unit lands, whatever its element - its Attack as
        /// well as its abilities. Today only a summon's <c>SummonDamage</c> nodes set it
        /// (<see cref="Heroes.SummonGrant.DamagePercent"/>); gear works per element.</summary>
        public float AllPercent;

        public static readonly OutgoingDamage None = new OutgoingDamage();

        public bool IsEmpty => Percent.Count == 0 && AbilityExtraPercent <= 0f && AllPercent == 0f;

        /// <summary>The percent an ability's hit of <paramref name="type"/> grows by: that element's
        /// share plus <see cref="AllPercent"/>.</summary>
        public float BonusFor(DamageType type)
        {
            return (Percent.TryGetValue(type, out float value) ? value : 0f) + AllPercent;
        }

        /// <summary>The factor on a basic attack's raw power. Only <see cref="AllPercent"/> counts:
        /// a gear element bonus has always ridden on abilities alone.</summary>
        public float AttackFactor => 1f + AllPercent / 100f;

        /// <summary>The outgoing damage a summon's upgrades give the unit it fields; <see cref="None"/>
        /// when they add nothing.</summary>
        public static OutgoingDamage ForSummon(Heroes.SummonGrant grant)
        {
            if (grant == null || grant.DamagePercent == 0)
            {
                return None;
            }
            return new OutgoingDamage { AllPercent = grant.DamagePercent };
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
