using System.Collections.Generic;
using Assets.Scripts.Items;

namespace Assets.Scripts.Cards.Buffs
{
    public static class BuffHandlerRegistry
    {
        private static readonly Dictionary<BuffType, IBuffHandler> Handlers = new Dictionary<BuffType, IBuffHandler>
        {
            { BuffType.Attack, new StatBuffHandler(StatType.Attack, "Attack") },
            { BuffType.Defense, new StatBuffHandler(StatType.Defense, "Defense") },
            { BuffType.Agility, new StatBuffHandler(StatType.Agility, "Agility") },
            { BuffType.FireResistance, new ResistanceBuffHandler("FireResistance") },
            { BuffType.IceResistance, new ResistanceBuffHandler("IceResistance") },
            { BuffType.LightningResistance, new ResistanceBuffHandler("LightningResistance") },
            { BuffType.HolyResistance, new ResistanceBuffHandler("HolyResistance") },
            { BuffType.ShadowResistance, new ResistanceBuffHandler("ShadowResistance") },
            { BuffType.Frozen, new FrozenBuffHandler() }
        };

        public static IBuffHandler Get(BuffType type)
        {
            return Handlers[type];
        }
    }
}
