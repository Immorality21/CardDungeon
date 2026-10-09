namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// What a <see cref="SphereNodeKind.MagicUpgrade"/> node deepens. Serialized by ordinal on grid
    /// nodes: append only.
    /// </summary>
    public enum AbilityUpgradeType
    {
        /// <summary>+Amount base power on the ability's Damage and Heal effects (not a percentage effect).</summary>
        Power = 0,

        /// <summary>+Amount percentage points of crit chance with it - and it can crit at all, even if not authored to.</summary>
        CritChance = 1,

        /// <summary>+Amount turns on every timed effect (its buffs, debuffs, poisons and burns).</summary>
        Duration = 2,

        /// <summary>+Amount percentage points on the chance its debuffs take hold (capped at 100).</summary>
        ApplyChance = 3,

        /// <summary>+Amount charges per run.</summary>
        Charges = 4
    }

    /// <summary>
    /// Everything a hero's grid has added to one ability, summed over every
    /// <see cref="SphereNodeKind.MagicUpgrade"/> node they hold for it. The ability asset is never
    /// changed (every hero and enemy shares it); these bonuses are folded into copies of its effects
    /// as it is cast (<c>EffectResolver</c>), the way <see cref="SummonGrant"/> is for a summon. By key,
    /// because Heroes does not know about Cards. Built by <see cref="SphereGridOps.AbilityUpgradesForNodes"/>.
    /// </summary>
    public class AbilityGrant
    {
        public string Key;

        /// <summary>Added to the base power of its Damage and Heal effects.</summary>
        public int PowerBonus;

        /// <summary>Percentage points added to the caster's crit chance with it.</summary>
        public int CritChanceBonus;

        /// <summary>A crit node was bought for it, so it can crit even when not authored to.</summary>
        public bool CanCrit;

        /// <summary>Added to every timed effect's duration.</summary>
        public int DurationBonus;

        /// <summary>Percentage points added to its debuffs' apply chance.</summary>
        public int ApplyChanceBonus;

        /// <summary>Added to its charges per run (folded into <see cref="SphereGridOps.KnownMagicForNodes"/>).</summary>
        public int ChargeBonus;

        /// <summary>True when nothing has been added.</summary>
        public bool IsEmpty =>
            PowerBonus == 0 && CritChanceBonus == 0 && !CanCrit && DurationBonus == 0
            && ApplyChanceBonus == 0 && ChargeBonus == 0;

        /// <summary>Adds one node's upgrade.</summary>
        public void Add(AbilityUpgradeType type, int amount)
        {
            switch (type)
            {
                case AbilityUpgradeType.Power:
                    PowerBonus += amount;
                    break;
                case AbilityUpgradeType.CritChance:
                    CanCrit = true;
                    CritChanceBonus += amount;
                    break;
                case AbilityUpgradeType.Duration:
                    DurationBonus += amount;
                    break;
                case AbilityUpgradeType.ApplyChance:
                    ApplyChanceBonus += amount;
                    break;
                case AbilityUpgradeType.Charges:
                    ChargeBonus += amount;
                    break;
            }
        }

        /// <summary>A copy under another key - an upgrade bought for a base ability carries to its awakening.</summary>
        public AbilityGrant CopyAs(string key)
        {
            var copy = (AbilityGrant)MemberwiseClone();
            copy.Key = key;
            return copy;
        }
    }

    /// <summary>
    /// A unit whose grid has deepened some abilities: a hero, or the balance model's stand-in for one.
    /// Asked by <c>EffectResolver</c> at every cast. Null for an ability with no upgrades.
    /// </summary>
    public interface IAbilityUpgrades
    {
        AbilityGrant UpgradesFor(string abilityKey);
    }
}
