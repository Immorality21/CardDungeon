using Assets.Scripts.Dungeon;
using Assets.Scripts.Rooms;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// A dungeon save stores room *indices* into a layout that is rebuilt from the level asset, so it
    /// is only valid while that asset's generation parameters are unchanged. These pin the rule that
    /// decides whether a save can still be resumed.
    ///
    /// <para>The bug that prompted them: <c>WeepingCauseway</c> went from 11 rooms to 6 in a balance
    /// pass, and an in-flight save of it held <c>CurrentRoomIndex 6</c>. Pressing Continue threw an
    /// <c>ArgumentOutOfRangeException</c> out of the restore, which reads as a corrupt save rather than
    /// an out-of-date one. <c>LevelKey</c> was already being written to catch exactly this and was
    /// never read back.</para>
    /// </summary>
    public class DungeonSaveCompatibilityTests
    {
        private static DungeonSaveData Save(string levelKey, int roomCount, int currentRoom)
        {
            var data = new DungeonSaveData
            {
                Seed = 1234,
                LevelKey = levelKey,
                CurrentRoomIndex = currentRoom
            };
            for (int i = 0; i < roomCount; i++)
            {
                data.Rooms.Add(new RoomSaveData { RoomIndex = i });
            }
            return data;
        }

        [Test]
        public void MatchingSave_IsResumable()
        {
            var save = Save("WeepingCauseway", roomCount: 6, currentRoom: 3);

            Assert.IsTrue(DungeonSaveCompatibility.IsCompatible(save, "WeepingCauseway", 6));
            Assert.AreEqual(DungeonSaveCompatibility.Reason.Compatible,
                DungeonSaveCompatibility.Check(save, "WeepingCauseway", 6));
        }

        [Test]
        public void RoomCountShrank_IsRejected()
        {
            // The real case: 11 rooms saved, the level now builds 6.
            var save = Save("WeepingCauseway", roomCount: 11, currentRoom: 6);

            Assert.IsFalse(DungeonSaveCompatibility.IsCompatible(save, "WeepingCauseway", 6));
            Assert.AreEqual(DungeonSaveCompatibility.Reason.RoomCountChanged,
                DungeonSaveCompatibility.Check(save, "WeepingCauseway", 6));
            Assert.AreEqual("saved 11 rooms, the level now builds 6",
                DungeonSaveCompatibility.Describe(save, "WeepingCauseway", 6));
        }

        [Test]
        public void RoomCountGrew_IsAlsoRejected()
        {
            // Growing is just as wrong: the indices would resolve, but to different rooms.
            var save = Save("WeepingCauseway", roomCount: 6, currentRoom: 3);

            Assert.IsFalse(DungeonSaveCompatibility.IsCompatible(save, "WeepingCauseway", 11));
        }

        [Test]
        public void DifferentLevel_IsRejected()
        {
            var save = Save("CollapsedCaverns", roomCount: 7, currentRoom: 1);

            Assert.AreEqual(DungeonSaveCompatibility.Reason.DifferentLevel,
                DungeonSaveCompatibility.Check(save, "WeepingCauseway", 7));
            Assert.AreEqual("saved level 'CollapsedCaverns', now loading 'WeepingCauseway'",
                DungeonSaveCompatibility.Describe(save, "WeepingCauseway", 7));
        }

        [Test]
        public void CurrentRoomOutOfRange_IsRejected()
        {
            // Same shape, but the current room does not exist — caught rather than thrown on.
            var save = Save("WeepingCauseway", roomCount: 6, currentRoom: 6);

            Assert.AreEqual(DungeonSaveCompatibility.Reason.CurrentRoomOutOfRange,
                DungeonSaveCompatibility.Check(save, "WeepingCauseway", 6));
        }

        [Test]
        public void SaveWithNoLevelKey_IsJudgedOnShapeAlone()
        {
            // Saves written before LevelKey existed have none. Rejecting those outright would throw
            // away resumable runs, so the key is only compared when both sides have one.
            var save = Save(null, roomCount: 6, currentRoom: 3);

            Assert.IsTrue(DungeonSaveCompatibility.IsCompatible(save, "WeepingCauseway", 6));
            Assert.IsFalse(DungeonSaveCompatibility.IsCompatible(save, "WeepingCauseway", 7));
        }

        [Test]
        public void NoSaveOrNoLayout_IsNotResumable()
        {
            Assert.IsFalse(DungeonSaveCompatibility.IsCompatible(null, "WeepingCauseway", 6));
            Assert.AreEqual(DungeonSaveCompatibility.Reason.NoSave,
                DungeonSaveCompatibility.Check(null, "WeepingCauseway", 6));

            var save = Save("WeepingCauseway", roomCount: 6, currentRoom: 0);
            Assert.AreEqual(DungeonSaveCompatibility.Reason.NoLayout,
                DungeonSaveCompatibility.Check(save, "WeepingCauseway", 0));
        }

        [Test]
        public void NegativeCurrentRoom_IsRejected()
        {
            var save = Save("WeepingCauseway", roomCount: 6, currentRoom: -1);

            Assert.AreEqual(DungeonSaveCompatibility.Reason.CurrentRoomOutOfRange,
                DungeonSaveCompatibility.Check(save, "WeepingCauseway", 6));
        }
    }
}
