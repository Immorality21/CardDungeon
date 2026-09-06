using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.IO;
using Assets.Scripts.Rooms;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class DungeonSaveData : IWriteable
    {
        public int Seed;
        public string LevelKey;
        public int CurrentRoomIndex;

        /// <summary>
        /// Where this floor's entrance is, so a restart knows where to put the party back. Written
        /// every save rather than derived, because the start room is a fact about how the floor was
        /// built and nothing in the rebuilt layout marks it (only the exit is flagged).
        /// </summary>
        public int StartRoomIndex;

        /// <summary>
        /// Set when the player left from the pause menu: the floor is spent, so on the way back in
        /// the enemies stand up again and the party starts at the entrance.
        ///
        /// <para><b>What deliberately does not reset is the party</b> - health, ability charges,
        /// afflictions and the potions this floor has already drunk all still restore, so walking
        /// out of a fight you are losing and walking back in is not a heal, and a hero who went
        /// down stays down. That is the whole point of the flag: leaving costs you the floor, never
        /// your wounds.</para>
        ///
        /// <para><b>Nor does anything the floor already paid out</b> - a looted cache stays looted, a
        /// spent refuge stays spent, and a resolved room event stays resolved. Without that, a
        /// restart would put the refuge back and the loop "heal, quit, heal again" would be exactly
        /// the free healing the health carry-over exists to prevent.</para>
        /// </summary>
        public bool RestartAtEntrance;
        public List<RoomSaveData> Rooms = new List<RoomSaveData>();
        public List<MagicSlotSaveData> EquippedMagic = new List<MagicSlotSaveData>();

        /// <summary>
        /// Buffs and debuffs room events hung on the party for the rest of the level. Saved so that
        /// quitting to the menu is not a cure for a curse.
        /// </summary>
        public List<Rooms.Events.LevelAffliction> Afflictions = new List<Rooms.Events.LevelAffliction>();

        /// <summary>
        /// Current health per hero, for the same reason the afflictions are here: health refills only
        /// on entering a fresh dungeon, so it is a level-scoped resource and quitting to the menu was
        /// otherwise a free full heal - and a free undo of every room event's damage.
        /// See <see cref="PartyHealthSnapshot"/> for the restore rules.
        /// </summary>
        public List<HeroHealthSaveData> HeroHealth = new List<HeroHealthSaveData>();

        /// <summary>
        /// Consumables this level has spent, per item key. The other half of the sustain pool: the
        /// item collection is committed only on level clear, so without this a quit and resume handed
        /// back every potion drunk on the way here. A <b>delta</b>, not a snapshot of quantities - the
        /// hub is reachable while a run is paused, and restoring absolute counts would undo whatever
        /// was bought there. See <see cref="Items.ConsumableSpend"/>.
        /// </summary>
        public List<Items.ConsumableSpend> ConsumablesSpent = new List<Items.ConsumableSpend>();

        public string GetFileName()
        {
            return $"Dungeon_{Seed}";
        }
    }
}
