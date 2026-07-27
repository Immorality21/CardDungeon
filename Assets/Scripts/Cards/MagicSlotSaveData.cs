using System;
using System.Collections.Generic;

namespace Assets.Scripts.Cards
{
    /// <summary>One equipped magic slot's persisted state (empty slots have an empty MagicKey).</summary>
    [Serializable]
    public class MagicSlotEntry
    {
        public string MagicKey;
        public int Charges;
        public int MaxCharges;
    }

    /// <summary>A hero's full set of equipped magic slots, in slot order.</summary>
    [Serializable]
    public class MagicSlotSaveData
    {
        public string HeroKey;
        public List<MagicSlotEntry> Slots = new List<MagicSlotEntry>();
    }
}
