using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Items
{
    [CreateAssetMenu(menuName = "SO/Item")]
    public class ItemSO : ScriptableObject
    {
        public string Key;
        public string DisplayName;
        public Sprite Icon;
        public ItemRarity Rarity;
        public int ItemLevel = 1;

        [Range(0f, 1f)]
        [Tooltip("Scales the rarity/depth chance this item wins a cache's or an event's item roll. 1 is " +
                 "plain rarity; lower makes a common item turn up less without making it uncommon - the " +
                 "Antidote Salve is 0.35, since a cache with three consumables to pick from handed one " +
                 "out most of the time (playtest 2026-10-19). Drop tables with an explicit Chance ignore it.")]
        public float FindChanceScale = 1f;

        [TextArea(2, 4)]
        [Tooltip("One line of flavour, shown where the item is looked at rather than used - the hub " +
                 "Materials tab today. Optional; blank simply shows nothing.")]
        public string Description;

        [Header("Category")]
        public ItemCategory Category = ItemCategory.Equipment;

        [Header("Equipment (Category == Equipment)")]
        public SlotType SlotType;
        public List<ItemBonus> Bonuses = new List<ItemBonus>();

        [Tooltip("Elemental resistance this item grants while equipped. Resistances sum across gear, " +
                 "innate resistance and temporary buffs, so a deliberately assembled set can pass 100% " +
                 "and absorb that element instead of taking it.")]
        public List<Combat.Resistance> Resistances = new List<Combat.Resistance>();

        [Tooltip("Reactions this item gives its wearer in a fight (docs/plans/EVENTS.md): when something " +
                 "happens to the wearer, the effects resolve with the wearer as their source. Simulated " +
                 "by the balance model exactly as they fire in play.")]
        public List<Combat.Triggers.TriggeredEffect> Triggers = new List<Combat.Triggers.TriggeredEffect>();

        [Tooltip("Thresholds on what this item has counted while worn (kills, boss kills, victories) and " +
                 "the bonuses reaching them adds - \"after 50 kills, +3 Strength\". Each copy counts for " +
                 "itself, in its save entry. See ItemGrowth.")]
        public List<ItemMilestone> Milestones = new List<ItemMilestone>();

        [Tooltip("The set this piece belongs to (Shadowweave), or none. Wearing several different pieces " +
                 "of one set adds the set's tiers - see ItemSetSO.")]
        public ItemSetSO Set;

        [Header("Consumable (Category == Consumable)")]
        public ConsumableEffectType ConsumableEffect = ConsumableEffectType.RestoreHealth;

        [Tooltip("Flat amount restored, before ConsumablePercent is added. Keep it small: this is " +
                 "the part that does NOT grow, so a potion carried by flat alone goes stale the " +
                 "moment a hero buys health on the grid.")]
        public int ConsumableAmount;

        [Min(0)]
        [Tooltip("Ability charges given back to the drinker's most-spent ability (RestoreCharge, " +
                 "HealAndCharge). The potion belt's potency steps add to it.")]
        public int ConsumableCharges;

        [Range(0f, 1f)]
        [Tooltip("Fraction of the TARGET's max health restored, added to ConsumableAmount. This is " +
                 "the half that keeps a potion worth drinking as heroes grow - a flat 5 HP was " +
                 "worth a fifth of an opening hero and a rounding error by the endgame.")]
        public float ConsumablePercent;

        [Tooltip("Stack ceiling for anything that stacks - consumables and materials. Equipment " +
                 "ignores it: a sword is always one entry so it can carry its own equipped slot.")]
        public int MaxStack = 99;

        [Tooltip("-1: drops wherever a table lists it. 0 or more: drops ONLY on a revisit at this Fear " +
                 "level or higher, so a first clear never pays it - the belt's late upgrade materials " +
                 "(playtest 3 finding 5). Enforced by RunFear.Withholds at every drop site.")]
        public int RevisitFear = -1;

        [Tooltip("A revisit-only material's run, in the player's words (\"the Sealed Archive\"). The Merchant " +
                 "names it when a belt step is short of this material - the one place the player would " +
                 "otherwise be stuck (docs/TUTORIAL.md).")]
        public string RevisitRun;

        /// <summary>
        /// What this consumable actually restores to a target with <paramref name="maxHealth"/>:
        /// the flat amount plus the scaling fraction, rounded up, and never zero for an item that
        /// claims to heal at all.
        ///
        /// <para><b>One rule, one place.</b> Combat, the balance model and the tooltip all ask
        /// here, because a healing number derived twice is a healing number that disagrees with
        /// itself - and the balance model's <c>SustainPool</c> is built on this answer.</para>
        ///
        /// <para><b>Why a potion needs the percentage at all.</b> A healing item competes against
        /// the <i>turn</i> it costs, not against the health bar: using one is a whole combat action
        /// while the enemy keeps swinging. A flat 5 HP against hits of 6.8-14.7 meant drinking a
        /// potion lost the party health on net, which is why they went unused. See
        /// <c>docs/BALANCING.md</c>.</para>
        /// </summary>
        public int HealAmountFor(int maxHealth)
        {
            return HealAmountFor(maxHealth, 0f);
        }

        /// <summary>
        /// The same, with the potion belt's bought potency (<c>PotionBeltOps.HealBonus</c>) added to
        /// the percentage. Play passes it; the balance model calls the overload above, so it keeps
        /// pricing the base potion (owner, 2026-10-27).
        /// </summary>
        public int HealAmountFor(int maxHealth, float bonusPercent)
        {
            if (ConsumableEffect != ConsumableEffectType.RestoreHealth
                && ConsumableEffect != ConsumableEffectType.HealAndCharge)
            {
                return 0;
            }

            float scaled = (ConsumablePercent + Mathf.Max(0f, bonusPercent)) * Mathf.Max(0, maxHealth);
            int total = Mathf.Max(0, ConsumableAmount) + Mathf.CeilToInt(scaled);
            if (total <= 0 && (ConsumableAmount > 0 || ConsumablePercent > 0f))
            {
                return 1;
            }
            return total;
        }

        /// <summary>
        /// Whether this item piles into one inventory entry with a quantity, rather than taking a
        /// row of its own. Consumables and materials do; equipment never can, because an entry
        /// carries which hero has it equipped.
        /// </summary>
        public bool Stacks => Category == ItemCategory.Consumable || Category == ItemCategory.Material;
    }
}
