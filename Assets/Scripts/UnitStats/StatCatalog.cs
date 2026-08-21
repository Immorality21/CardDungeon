using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assets.Scripts.UnitStats
{
    /// <summary>
    /// Everything the game needs to know about one stat, in one place.
    ///
    /// <para>This exists because "add a stat" kept meaning "and then hunt for the six other files
    /// that name stats one by one" — short names for table headers, display names for buff labels,
    /// a recruit-price weight, a power-score weight, an authoring default. Each of those was its own
    /// hand-maintained list, and each failed differently when a stat was missed: a blank column, a
    /// hero priced as if half its stats did not exist, a new enemy that spawned dead.</para>
    /// </summary>
    public sealed class StatDefinition
    {
        public StatType Type { get; private set; }

        /// <summary>Three-letter label for table columns and compact stat lines.</summary>
        public string ShortName { get; private set; }

        /// <summary>Full name for player-facing text and buff labels.</summary>
        public string DisplayName { get; private set; }

        /// <summary>One line on what the stat actually does, for tooltips.</summary>
        public string Description { get; private set; }

        /// <summary>
        /// What one point is worth when pricing a hero to recruit — the hero's *worth to the player*.
        /// Deliberately not the same scale as <see cref="PowerWeight"/>.
        /// </summary>
        public float RecruitWeight { get; private set; }

        /// <summary>
        /// What one point is worth in an enemy's power score — its *threat*. Seeds
        /// <c>BalanceRulesSO.PowerWeights</c>, which designers can then tune per project.
        /// </summary>
        public float PowerWeight { get; private set; }

        /// <summary>Value a newly authored hero or enemy starts this stat at.</summary>
        public int AuthoringDefault { get; private set; }

        /// <summary>
        /// True for stats that are a pool rather than an output — currently only MaxHealth. Nothing
        /// should scale damage off one, and the basic Attack command refuses to swing off one.
        /// </summary>
        public bool IsPool { get; private set; }

        public StatDefinition(StatType type, string shortName, string displayName, string description,
            float recruitWeight, float powerWeight, int authoringDefault, bool isPool = false)
        {
            Type = type;
            ShortName = shortName;
            DisplayName = displayName;
            Description = description;
            RecruitWeight = recruitWeight;
            PowerWeight = powerWeight;
            AuthoringDefault = authoringDefault;
            IsPool = isPool;
        }
    }

    /// <summary>
    /// The one mapping from <see cref="StatType"/> to everything per-stat. <b>Adding a stat means
    /// adding a member to <see cref="StatType"/> and a row here</b> — and if you forget the row,
    /// <c>StatCatalogTests</c> fails rather than the game quietly misbehaving.
    ///
    /// <para>Iteration order is <see cref="StatType"/>'s declaration order, so every generated table,
    /// stat line and sweep agrees on ordering without each caller deciding.</para>
    /// </summary>
    public static class StatCatalog
    {
        private static readonly StatDefinition[] Definitions =
        {
            new StatDefinition(StatType.Strength, "STR", "Strength",
                "Physical power. Scales melee-flavoured attacks and spells.",
                recruitWeight: 6f, powerWeight: 6f, authoringDefault: 5),

            new StatDefinition(StatType.Endurance, "END", "Endurance",
                "Damage reduction, through the diminishing curve in DamageCalculator.",
                recruitWeight: 4f, powerWeight: 4f, authoringDefault: 5),

            new StatDefinition(StatType.Agility, "AGI", "Agility",
                "Turn frequency - TurnManager schedules on it, so it compounds.",
                recruitWeight: 5f, powerWeight: 3f, authoringDefault: 5),

            new StatDefinition(StatType.Intelligence, "INT", "Intelligence",
                "Scales offensive and arcane spell power - a caster's Strength.",
                recruitWeight: 5f, powerWeight: 2f, authoringDefault: 0),

            new StatDefinition(StatType.Spirit, "SPR", "Spirit",
                "Scales restorative and protective spell power - healing, shields, Holy.",
                recruitWeight: 5f, powerWeight: 2f, authoringDefault: 0),

            new StatDefinition(StatType.Luck, "LCK", "Luck",
                "Raises crit chance, and improves stat checks on room events.",
                recruitWeight: 4f, powerWeight: 3f, authoringDefault: 0),

            new StatDefinition(StatType.MaxHealth, "HP", "Health",
                "Size of the health bar. Current health is a resource, not a stat.",
                recruitWeight: 2f, powerWeight: 1f, authoringDefault: 20, isPool: true)
        };

        private static readonly Dictionary<StatType, StatDefinition> ByType = BuildIndex();

        private static Dictionary<StatType, StatDefinition> BuildIndex()
        {
            var index = new Dictionary<StatType, StatDefinition>(Definitions.Length);
            foreach (var definition in Definitions)
            {
                index[definition.Type] = definition;
            }
            return index;
        }

        // Both public sequences are built by walking StatType itself rather than the Definitions
        // array, so "iteration order is declaration order" is true by construction. Ordering the
        // authoring array by hand and testing that it matched was the weaker arrangement: the test
        // compared Definitions with something derived from Definitions, so it could not fail.
        private static readonly StatDefinition[] Ordered = BuildOrdered();
        private static readonly StatType[] OrderedTypes = BuildOrderedTypes();

        // Wrapped once at init, not per access: these are the sequences every stat loop in the game
        // walks, so a defensive copy per call would allocate on a hot path. Returning the arrays as
        // IReadOnlyList would stop an accidental write but not a cast back to the array.
        private static readonly ReadOnlyCollection<StatDefinition> OrderedView = Array.AsReadOnly(Ordered);
        private static readonly ReadOnlyCollection<StatType> OrderedTypesView = Array.AsReadOnly(OrderedTypes);

        private static StatDefinition[] BuildOrdered()
        {
            var ordered = new List<StatDefinition>(Definitions.Length);
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                StatDefinition definition;
                if (stat != StatType.None && ByType.TryGetValue(stat, out definition))
                {
                    ordered.Add(definition);
                }
            }
            return ordered.ToArray();
        }

        private static StatType[] BuildOrderedTypes()
        {
            var types = new StatType[Ordered.Length];
            for (int i = 0; i < Ordered.Length; i++)
            {
                types[i] = Ordered[i].Type;
            }
            return types;
        }

        /// <summary>Every stat's definition, in <see cref="StatType"/> declaration order.</summary>
        public static IReadOnlyList<StatDefinition> All
        {
            get { return OrderedView; }
        }

        /// <summary>
        /// Every real stat, excluding <see cref="StatType.None"/>. The canonical iteration order for
        /// anything that displays or sweeps stats.
        ///
        /// <para>Exposed as a read-only <i>collection</i>, not the backing array: this is the sequence
        /// every stat loop in the game walks, and a <c>readonly</c> array field still lets any caller
        /// overwrite an element and silently corrupt all of them for the session.</para>
        /// </summary>
        public static IReadOnlyList<StatType> Types
        {
            get { return OrderedTypesView; }
        }

        /// <summary>
        /// Stats declared in <see cref="StatType"/> with no row here. Such a stat does not throw — it
        /// <b>disappears</b>, because every loop in the game iterates <see cref="Types"/>, which is
        /// built from the rows. It would still be storable in a <c>StatBlock</c>, selectable in an
        /// item's or spell's dropdown, and summed into gear bonuses, while contributing nothing to
        /// recruit price, power score, the inspector drawer or any analyzer column.
        ///
        /// <para>That is the original <c>ShopPricing</c> bug one level up, so it is reported two ways:
        /// <c>StatCatalogTests</c> fails, and the editor logs it on load (see
        /// <c>StatCatalogValidator</c>) for anyone who has not run the tests.</para>
        /// </summary>
        public static List<StatType> MissingRows()
        {
            var missing = new List<StatType>();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                if (stat != StatType.None && !ByType.ContainsKey(stat))
                {
                    missing.Add(stat);
                }
            }
            return missing;
        }

        /// <summary>
        /// The definition for a stat. Throws for an uncatalogued stat rather than returning a
        /// placeholder: a missing row is a development mistake that should surface at once, and
        /// <c>StatCatalogTests</c> already guarantees it cannot reach a build.
        /// </summary>
        public static StatDefinition Of(StatType stat)
        {
            StatDefinition definition;
            if (ByType.TryGetValue(stat, out definition))
            {
                return definition;
            }
            throw new KeyNotFoundException(
                "StatType." + stat + " has no StatCatalog entry. Add one in StatCatalog.Definitions; "
                + "short names, weights and authoring defaults all read from it.");
        }

        /// <summary>Short label, safe for <see cref="StatType.None"/> (returns "-").</summary>
        public static string ShortName(StatType stat)
        {
            return stat == StatType.None ? "-" : Of(stat).ShortName;
        }

        /// <summary>Full label, safe for <see cref="StatType.None"/> (returns "None").</summary>
        public static string DisplayName(StatType stat)
        {
            return stat == StatType.None ? "None" : Of(stat).DisplayName;
        }

        /// <summary>Stats that can be a source of power - everything that is not a pool.</summary>
        public static bool CanScalePower(StatType stat)
        {
            return stat != StatType.None && !Of(stat).IsPool;
        }
    }
}
