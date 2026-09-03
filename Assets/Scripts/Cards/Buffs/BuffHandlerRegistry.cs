using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// Maps a <see cref="BuffType"/> to the handler that applies it.
    ///
    /// <para>The stat handlers are <b>generated</b> from <see cref="StatCatalog.Types"/>: every stat that
    /// has a matching <see cref="BuffType"/> member gets a <see cref="StatBuffHandler"/> automatically.
    /// They used to be three hand-written entries, so Intelligence, Spirit and Luck buffs threw
    /// <c>KeyNotFoundException</c> even though the enum listed them.</para>
    /// </summary>
    public static class BuffHandlerRegistry
    {
        private static readonly Dictionary<BuffType, IBuffHandler> Handlers = Build();

        private static Dictionary<BuffType, IBuffHandler> Build()
        {
            var handlers = new Dictionary<BuffType, IBuffHandler>
            {
                { BuffType.FireResistance, new ResistanceBuffHandler(DamageType.Fire, "Fire Res") },
                { BuffType.IceResistance, new ResistanceBuffHandler(DamageType.Ice, "Ice Res") },
                { BuffType.LightningResistance, new ResistanceBuffHandler(DamageType.Lightning, "Lightning Res") },
                { BuffType.HolyResistance, new ResistanceBuffHandler(DamageType.Holy, "Holy Res") },
                { BuffType.ShadowResistance, new ResistanceBuffHandler(DamageType.Shadow, "Shadow Res") },
                { BuffType.Frozen, new FrozenBuffHandler() },
                { BuffType.Slow, new SlowBuffHandler() },
                { BuffType.Haste, new HasteBuffHandler() },
                { BuffType.Silenced, new SilencedBuffHandler() },

                // Over-time effects. Their differences are the whole point, so they are stated here
                // rather than buried per class:
                //
                //   Poison  - bypasses Endurance. The answer to a target the defense curve has made
                //             immune to flat damage, and the reason to cast something other than the
                //             biggest number in the kit.
                //   Burn    - honours Endurance, is dealt as Fire so resistances, weaknesses and
                //             absorption all apply, and is doused by Ice (mirroring Frozen/Fire).
                //   Bleed   - honours Endurance, dealt as Normal. Today it is the plain one.
                //
                // Poison and bleed are both Normal, so they are resisted identically; only
                // IgnoresDefense separates them. Nothing in the project authors a Normal resistance,
                // so neither is resistable in practice - but if one is ever added it will apply to
                // both, and this is the line to revisit (giving poison its own element is the fix).
                {
                    BuffType.Burning,
                    new OverTimeBuffHandler(
                        BuffType.Burning, "Burn", false, DamageType.Fire, false, DamageType.Ice)
                },
                {
                    BuffType.Poisoned,
                    new OverTimeBuffHandler(
                        BuffType.Poisoned, "Poison", false, DamageType.Normal, true)
                },
                {
                    BuffType.Bleeding,
                    new OverTimeBuffHandler(
                        BuffType.Bleeding, "Bleed", false, DamageType.Normal, false)
                },
                {
                    BuffType.Regenerating,
                    new OverTimeBuffHandler(
                        BuffType.Regenerating, "Regen", true, DamageType.Normal, false)
                }
            };

            // A stat is buffable when BuffType declares a member of the same name. Adding a stat
            // therefore costs one BuffType member, not a registry edit as well.
            foreach (var stat in StatCatalog.Types)
            {
                BuffType asBuff;
                if (!Enum.TryParse(stat.ToString(), out asBuff))
                {
                    continue;
                }
                if (!handlers.ContainsKey(asBuff))
                {
                    handlers[asBuff] = new StatBuffHandler(stat, StatCatalog.DisplayName(stat));
                }
            }

            return handlers;
        }

        /// <summary>
        /// The handler for a buff type, or null when nothing handles it. Returns null rather than
        /// throwing: a magic authored with a buff type that has no handler should be inert and
        /// reportable, not a crash mid-combat.
        /// </summary>
        public static IBuffHandler Get(BuffType type)
        {
            IBuffHandler handler;
            return Handlers.TryGetValue(type, out handler) ? handler : null;
        }

        /// <summary>
        /// The status effects a cure removes. Listed in one place rather than inferred, because
        /// "harmful" is a design judgement and not a property of the handler: <see cref="BuffType.Haste"/>
        /// and <see cref="BuffType.Regenerating"/> are status effects too, and a cure that stripped
        /// the party's own buffs would be a trap rather than a tool.
        /// </summary>
        private static readonly HashSet<BuffType> Curable = new HashSet<BuffType>
        {
            BuffType.Frozen,
            BuffType.Slow,
            BuffType.Silenced,
            BuffType.Burning,
            BuffType.Poisoned,
            BuffType.Bleeding
        };

        /// <summary>Whether a cure removes this status. See <see cref="Curable"/>.</summary>
        public static bool IsCurable(BuffType type)
        {
            return Curable.Contains(type);
        }

        /// <summary>Buff types with no handler — surfaced so the balance analyzer can report them.</summary>
        public static List<BuffType> Unhandled()
        {
            var missing = new List<BuffType>();
            foreach (BuffType type in Enum.GetValues(typeof(BuffType)))
            {
                if (type != BuffType.None && !Handlers.ContainsKey(type))
                {
                    missing.Add(type);
                }
            }
            return missing;
        }

        /// <summary>
        /// Stats that cannot be buffed because <see cref="BuffType"/> declares no member of the same
        /// name. The inverse of <see cref="Unhandled"/>, and the one that actually bites.
        ///
        /// <para><see cref="BuffType"/> is a second per-stat list, so a new stat is <b>not</b> quite
        /// "one enum member plus one catalog row" if it should be buffable — it needs a
        /// <see cref="BuffType"/> member too. The loop that builds the stat handlers silently skips a
        /// stat with no match, which means no buff, no debuff and no Haste-style effect can ever
        /// target it, and nothing throws to say so. <c>StatCatalogTests</c> asserts this list is
        /// empty for every non-pool stat, so the gap fails a test instead of shipping.</para>
        /// </summary>
        public static List<StatType> StatsWithNoBuffType()
        {
            var missing = new List<StatType>();
            foreach (var stat in StatCatalog.Types)
            {
                if (!StatCatalog.CanScalePower(stat))
                {
                    // Pools are excluded deliberately: a buff to the size of the health bar would
                    // have to decide what happens to current health, which no handler does.
                    continue;
                }

                BuffType asBuff;
                if (!Enum.TryParse(stat.ToString(), out asBuff) || !Handlers.ContainsKey(asBuff))
                {
                    missing.Add(stat);
                }
            }
            return missing;
        }
    }
}
