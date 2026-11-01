using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// A <see cref="BuffType"/>'s player-facing name, as the HP bar, the Bestiary and the ability texts
    /// show it. The handlers' <c>GetDisplayText</c> is the combat popup, with its numbers and its "!";
    /// this is the bare name. <c>LocFollowUpTests</c> fails on a member with no words of its own.
    /// </summary>
    public static class BuffNames
    {
        public static string Of(BuffType type)
        {
            switch (type)
            {
                case BuffType.FireResistance:
                    return Resistance(DamageType.Fire);
                case BuffType.IceResistance:
                    return Resistance(DamageType.Ice);
                case BuffType.LightningResistance:
                    return Resistance(DamageType.Lightning);
                case BuffType.HolyResistance:
                    return Resistance(DamageType.Holy);
                case BuffType.ShadowResistance:
                    return Resistance(DamageType.Shadow);
                case BuffType.Frozen:
                    return Loc.Get("bestiary.status.frozen");
                case BuffType.Slow:
                    return Loc.Get("bestiary.status.slow");
                case BuffType.Haste:
                    return Loc.Get("bestiary.status.haste");
                case BuffType.Burning:
                    return Loc.Get("bestiary.status.burn");
                case BuffType.Poisoned:
                    return Loc.Get("bestiary.status.poison");
                case BuffType.Bleeding:
                    return Loc.Get("bestiary.status.bleed");
                case BuffType.Regenerating:
                    return Loc.Get("bestiary.status.regen");
                case BuffType.Silenced:
                    return Loc.Get("bestiary.status.silence");
                case BuffType.Blinded:
                    return Loc.Get("bestiary.status.blind");
                case BuffType.Guarded:
                    return Loc.Get("bestiary.status.guard");
                case BuffType.Barrier:
                    return Loc.Get("bestiary.status.barrier");
                case BuffType.Leeched:
                    return Loc.Get("bestiary.status.leech");
                case BuffType.Confused:
                    return Loc.Get("bestiary.status.confuse");
                case BuffType.Marked:
                    return Loc.Get("bestiary.status.mark");
                case BuffType.Hidden:
                    return Loc.Get("bestiary.status.hidden");
                case BuffType.Evasive:
                    return Loc.Get("bestiary.status.evasive");
                case BuffType.DeathMarked:
                    return Loc.Get("bestiary.status.death-mark");
                case BuffType.DivineProtection:
                    return Loc.Get("bestiary.status.divine-protection");
            }
            return BuffHandlerRegistry.Get(type) is StatBuffHandler stat
                ? StatCatalog.DisplayName(stat.Stat)
                : type.ToString();
        }

        /// <summary>The status as it reads after "vs" in an ability's bonus line ("double vs marked").</summary>
        public static string Versus(BuffType type)
        {
            return type == BuffType.Marked ? Loc.Get("abilities.describe.versus-marked") : Of(type);
        }

        private static string Resistance(DamageType element)
        {
            return Loc.Format("bestiary.status.resistance", ("element", DamageTypeNames.Of(element)));
        }
    }
}
