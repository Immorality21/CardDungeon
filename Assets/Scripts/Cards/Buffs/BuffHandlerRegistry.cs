using System.Collections.Generic;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards.Buffs
{
    public static class BuffHandlerRegistry
    {
        private static readonly Dictionary<BuffType, IBuffHandler> Handlers = new Dictionary<BuffType, IBuffHandler>
        {
            { BuffType.Strength, new StatBuffHandler(StatType.Strength, "Attack") },
            { BuffType.Endurance, new StatBuffHandler(StatType.Endurance, "Defense") },
            { BuffType.Agility, new StatBuffHandler(StatType.Agility, "Agility") },
            { BuffType.FireResistance, new ResistanceBuffHandler("FireResistance") },
            { BuffType.IceResistance, new ResistanceBuffHandler("IceResistance") },
            { BuffType.LightningResistance, new ResistanceBuffHandler("LightningResistance") },
            { BuffType.HolyResistance, new ResistanceBuffHandler("HolyResistance") },
            { BuffType.ShadowResistance, new ResistanceBuffHandler("ShadowResistance") },
            { BuffType.Frozen, new FrozenBuffHandler() },
            { BuffType.Slow, new SlowBuffHandler() },
            { BuffType.Haste, new HasteBuffHandler() }
        };

        public static IBuffHandler Get(BuffType type)
        {
            return Handlers[type];
        }
    }
}
