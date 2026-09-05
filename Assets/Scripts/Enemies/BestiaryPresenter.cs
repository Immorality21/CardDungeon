using System;
using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Combat;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Enemies
{
    /// <summary>
    /// How a line reads to a player who is <b>attacking</b> this enemy - which is the only frame the
    /// bestiary is useful in. "Good" means exploit it, "Bad" means avoid it. Colour is applied by
    /// the two views (the in-combat Inspect window and the hub bestiary) from this, so they cannot
    /// drift apart.
    /// </summary>
    public enum BestiaryTone
    {
        Unknown,   // not yet observed
        Neutral,   // known, and unremarkable
        Good,      // known, and in the player's favour (a weakness)
        Bad        // known, and against the player (resisted / immune / absorbed)
    }

    /// <summary>One labelled row of enemy knowledge, already resolved to display text.</summary>
    public readonly struct BestiaryLine
    {
        public readonly string Label;
        public readonly string Value;
        public readonly BestiaryTone Tone;

        public BestiaryLine(string label, string value, BestiaryTone tone)
        {
            Label = label;
            Value = value;
            Tone = tone;
        }

        public bool IsKnown
        {
            get { return Tone != BestiaryTone.Unknown; }
        }
    }

    /// <summary>
    /// Turns an <see cref="EnemySO"/> plus what the player has learned about it into display rows.
    ///
    /// <para>Pure and view-free on purpose: the in-combat Inspect window and the hub bestiary render
    /// the same knowledge, and duplicating "how do I phrase a 120% fire resistance" in two UI
    /// controllers is how two screens start disagreeing.</para>
    ///
    /// <para>It never reaches for <c>MetaProgressManager</c> - the caller passes the
    /// <see cref="BestiaryEntry"/> (or null for an enemy never met), which is what makes every rule
    /// here unit-testable.</para>
    /// </summary>
    public static class BestiaryPresenter
    {
        /// <summary>Shown wherever the player has not earned the answer yet.</summary>
        public const string Unknown = "???";

        /// <summary>
        /// The elements a bestiary page lists, in a fixed order so a page never reshuffles between
        /// two enemies. Includes <see cref="DamageType.Normal"/> - an enemy that shrugs off physical
        /// blows is exactly the kind of thing the player needs told.
        /// </summary>
        public static readonly DamageType[] DisplayedTypes =
        {
            DamageType.Normal,
            DamageType.Fire,
            DamageType.Ice,
            DamageType.Lightning,
            DamageType.Holy,
            DamageType.Shadow
        };

        /// <summary><see cref="DamageType.Normal"/> reads as "Physical"; every other type is its own name.</summary>
        public static string DamageTypeLabel(DamageType type)
        {
            return type == DamageType.Normal ? "Physical" : type.ToString();
        }

        // ============================================================
        //  RESISTANCES
        // ============================================================

        /// <summary>
        /// One row per element: what it does to this enemy, or <see cref="Unknown"/> until the
        /// player has landed a hit of that type on it.
        /// </summary>
        public static List<BestiaryLine> ResistanceLines(EnemySO definition, BestiaryEntry known)
        {
            var lines = new List<BestiaryLine>(DisplayedTypes.Length);
            foreach (var type in DisplayedTypes)
            {
                lines.Add(ResistanceLine(definition, known, type));
            }
            return lines;
        }

        public static BestiaryLine ResistanceLine(EnemySO definition, BestiaryEntry known, DamageType type)
        {
            string label = DamageTypeLabel(type);
            if (definition == null || !BestiaryOps.KnowsDamageType(known, type))
            {
                return new BestiaryLine(label, Unknown, BestiaryTone.Unknown);
            }

            float percent = DamageCalculator.GetResistance(type, definition.Resistances);
            var effectiveness = DamageCalculator.Classify(type, definition.Resistances);
            return new BestiaryLine(label, ResistanceText(effectiveness, percent), ToneOf(effectiveness));
        }

        /// <summary>
        /// The player-facing phrasing of one resistance. The classification comes from
        /// <see cref="DamageCalculator.Classify"/> rather than a second set of thresholds here, so
        /// the word in the bestiary is always the word the combat popup will use.
        /// </summary>
        public static string ResistanceText(DamageEffectiveness effectiveness, float percent)
        {
            switch (effectiveness)
            {
                case DamageEffectiveness.Absorbed:
                    return "Absorbs " + Percent(percent);
                case DamageEffectiveness.Immune:
                    return "Immune";
                case DamageEffectiveness.Resisted:
                    return "Resists " + Percent(percent);
                case DamageEffectiveness.Weak:
                    return "Weak " + Percent(percent);
                default:
                    return "-";
            }
        }

        public static BestiaryTone ToneOf(DamageEffectiveness effectiveness)
        {
            switch (effectiveness)
            {
                case DamageEffectiveness.Weak:
                    return BestiaryTone.Good;
                case DamageEffectiveness.Resisted:
                case DamageEffectiveness.Immune:
                case DamageEffectiveness.Absorbed:
                    return BestiaryTone.Bad;
                default:
                    return BestiaryTone.Neutral;
            }
        }

        // ============================================================
        //  ATTACK ELEMENT
        // ============================================================

        /// <summary>
        /// What this enemy hits <i>with</i> - the defensive half of the elemental layer, and what
        /// tells the player whether a cloak is worth its health cost. Learned by being attacked
        /// rather than by attacking, so it has its own flag instead of riding on the observed-damage
        /// list.
        /// </summary>
        public static BestiaryLine AttackLine(EnemySO definition, BestiaryEntry known)
        {
            if (definition == null || known == null || !known.AttackTypeKnown)
            {
                return new BestiaryLine("Attacks with", Unknown, BestiaryTone.Unknown);
            }

            var type = definition.AttackDamageType;
            return new BestiaryLine(
                "Attacks with",
                DamageTypeLabel(type),
                type == DamageType.Normal ? BestiaryTone.Neutral : BestiaryTone.Bad);
        }

        // ============================================================
        //  LOOT
        // ============================================================

        /// <summary>
        /// What this enemy drops - one row per line of its table, named only once that line has
        /// actually been seen to drop. A drop is rolled per kill, so knowing a table is genuinely
        /// earned, and the rows of it are earned one at a time.
        ///
        /// <para>Unseen lines are listed but unnamed rather than hidden, the same bargain
        /// <see cref="SpellLines"/> makes: the count of what you have not seen is itself worth
        /// knowing, and it is what tells a player farming a material that there is more here. An
        /// enemy that carries nothing at all gets a single "Nothing" row once met, rather than
        /// staying <see cref="Unknown"/> forever.</para>
        /// </summary>
        public static List<BestiaryLine> LootLines(EnemySO definition, BestiaryEntry known)
        {
            var lines = new List<BestiaryLine>();
            if (definition == null || known == null)
            {
                lines.Add(new BestiaryLine("Drops", Unknown, BestiaryTone.Unknown));
                return lines;
            }

            bool any = false;
            if (definition.LootTable != null)
            {
                foreach (var drop in definition.LootTable)
                {
                    if (drop == null || drop.Item == null)
                    {
                        continue;
                    }

                    any = true;
                    bool seen = BestiaryOps.KnowsLoot(known, drop.Item.Key);
                    lines.Add(new BestiaryLine(
                        lines.Count == 0 ? "Drops" : "",
                        seen ? drop.Item.DisplayName : Unknown,
                        seen ? BestiaryTone.Good : BestiaryTone.Unknown));
                }
            }

            if (!any)
            {
                lines.Add(new BestiaryLine("Drops", "Nothing", BestiaryTone.Neutral));
            }
            return lines;
        }

        // ============================================================
        //  STATS / TALLY / SPELLS
        // ============================================================

        /// <summary>
        /// Whether a stat earns a row on a knowledge page.
        ///
        /// <para>A non-zero value always does. A <b>zero</b> one does only if the stat is one every
        /// unit is authored with (<see cref="StatDefinition.AuthoringDefault"/> above zero) - which
        /// today means Strength, Endurance and Agility. That is the difference between "this thing
        /// has no armour", which is a finding worth acting on, and a wall of INT 0 / SPR 0 / LCK 0
        /// on every melee enemy in the game. Reading it off the catalog rather than a hard-coded
        /// list means a stat added later sorts itself.</para>
        /// </summary>
        public static bool IsWorthShowing(StatType stat, int value)
        {
            if (stat == StatType.None || stat == StatType.MaxHealth)
            {
                return false;
            }
            return value != 0 || StatCatalog.Of(stat).AuthoringDefault > 0;
        }

        /// <summary>
        /// The enemy's base stat line, in <see cref="StatCatalog"/> order. Only meaningful once the
        /// enemy has been met; an unmet enemy gets a row of <see cref="Unknown"/> so the page keeps
        /// its shape. MaxHealth is skipped - the health line above states it, and in combat the live
        /// value is the one that matters.
        /// </summary>
        public static List<BestiaryLine> StatLines(EnemySO definition, BestiaryEntry known)
        {
            var lines = new List<BestiaryLine>();
            bool seen = definition != null && known != null;

            foreach (var stat in StatCatalog.Types)
            {
                if (stat == StatType.MaxHealth)
                {
                    continue;
                }

                string label = StatCatalog.ShortName(stat);
                if (!seen)
                {
                    // An unmet enemy has no values to filter on, so every row is offered as a gap.
                    lines.Add(new BestiaryLine(label, Unknown, BestiaryTone.Unknown));
                    continue;
                }

                int value = definition.BaseStats[stat];
                if (!IsWorthShowing(stat, value))
                {
                    continue;
                }

                lines.Add(new BestiaryLine(label, value.ToString(), BestiaryTone.Neutral));
            }

            return lines;
        }

        /// <summary>Kill tally. Zero is a real answer once met - fled from, or survived.</summary>
        public static BestiaryLine KillsLine(BestiaryEntry known)
        {
            if (known == null)
            {
                return new BestiaryLine("Slain", Unknown, BestiaryTone.Unknown);
            }
            return new BestiaryLine("Slain", known.Kills.ToString(), BestiaryTone.Neutral);
        }

        /// <summary>
        /// What this enemy can throw, named only once the player has actually <b>seen it cast</b>
        /// (<see cref="BestiaryEntry.ObservedSpellKeys"/>).
        ///
        /// <para>Per enemy, not globally. Until 2026-09-04 this list was the enemy's Draw table and
        /// an entry was named once the magic had been drawn from <i>anywhere</i>, because drawing was
        /// the acquisition and one reveal was enough. With Draw gone the list is purely the
        /// monster's own repertoire, and "the Cinder Imp throws Fireball" is a fact about the Cinder
        /// Imp - learning it off a Dragon should not fill in the Imp's page.</para>
        ///
        /// <para>Unobserved entries are listed but unnamed rather than hidden, so the page still says
        /// <i>how many</i> spells the thing has. That is the same bargain the resistance rows make:
        /// the shape of what you do not know is itself information, and a page that silently omitted
        /// rows would read as complete when it is not.</para>
        /// </summary>
        public static List<BestiaryLine> SpellLines(EnemySO definition, BestiaryEntry known)
        {
            var lines = new List<BestiaryLine>();
            if (definition == null || definition.Spells == null)
            {
                return lines;
            }

            foreach (var entry in definition.Spells)
            {
                if (entry == null || entry.Magic == null)
                {
                    continue;
                }

                bool seen = BestiaryOps.KnowsSpell(known, entry.Magic.Key);
                lines.Add(new BestiaryLine(
                    seen ? entry.Magic.DisplayName : Unknown,
                    seen ? "seen" : "",
                    seen ? BestiaryTone.Neutral : BestiaryTone.Unknown));
            }
            return lines;
        }

        // ============================================================
        //  COLLECTION SUMMARY
        // ============================================================

        /// <summary>How many of <paramref name="catalog"/> the player has met, for the "N of M" header.</summary>
        public static int SeenCount(IList<EnemySO> catalog, List<BestiaryEntry> knowledge)
        {
            if (catalog == null)
            {
                return 0;
            }

            int seen = 0;
            foreach (var definition in catalog)
            {
                if (definition == null)
                {
                    continue;
                }
                if (BestiaryOps.Find(knowledge, definition.SaveKey) != null)
                {
                    seen++;
                }
            }
            return seen;
        }

        private static string Percent(float percent)
        {
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
    }
}
