namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Whether a dungeon save still describes the layout its level builds — the pure rule behind the
    /// resume path, so it can be tested without a scene.
    ///
    /// <para><b>Why this has to exist.</b> A dungeon save stores <b>room indices</b> into a layout that
    /// is rebuilt from the level asset rather than stored, so it is only valid while that asset's
    /// generation parameters are unchanged. Editing a level's <c>RoomsToGenerate</c> or its room pool
    /// silently invalidates every in-flight save of that level — and a balance pass does exactly that,
    /// routinely. Before this check the failure mode was an <c>ArgumentOutOfRangeException</c> deep in
    /// <c>DungeonManager.RestoreSavedState</c> when the player pressed Continue, which reads as a
    /// corrupt save rather than an out-of-date one.</para>
    ///
    /// <para><c>DungeonSaveData.LevelKey</c> was already being written for precisely this purpose and
    /// was never read back.</para>
    ///
    /// <para>Rejecting a save is not losing the run: which run, which floor, XP, gear and meta
    /// progress all live in other files. Only the current floor restarts.</para>
    /// </summary>
    public static class DungeonSaveCompatibility
    {
        /// <summary>Why a save cannot be resumed, or <see cref="Reason.Compatible"/>.</summary>
        public enum Reason
        {
            Compatible,
            NoSave,
            NoLayout,
            DifferentLevel,
            RoomCountChanged,
            CurrentRoomOutOfRange
        }

        /// <summary>
        /// Whether <paramref name="saveData"/> can be restored onto a freshly built layout of
        /// <paramref name="roomCount"/> rooms for the level keyed <paramref name="levelKey"/>.
        /// </summary>
        public static bool IsCompatible(DungeonSaveData saveData, string levelKey, int roomCount)
        {
            return Check(saveData, levelKey, roomCount) == Reason.Compatible;
        }

        /// <summary>The specific reason, so the warning can say something actionable.</summary>
        public static Reason Check(DungeonSaveData saveData, string levelKey, int roomCount)
        {
            if (saveData == null)
            {
                return Reason.NoSave;
            }

            if (roomCount <= 0)
            {
                return Reason.NoLayout;
            }

            // A save written before LevelKey existed has none; only judge it when both sides do.
            if (!string.IsNullOrEmpty(saveData.LevelKey)
                && !string.IsNullOrEmpty(levelKey)
                && saveData.LevelKey != levelKey)
            {
                return Reason.DifferentLevel;
            }

            // One saved entry per room, so a differing count means the layout changed shape.
            if (saveData.Rooms == null || saveData.Rooms.Count != roomCount)
            {
                return Reason.RoomCountChanged;
            }

            if (saveData.CurrentRoomIndex < 0 || saveData.CurrentRoomIndex >= roomCount)
            {
                return Reason.CurrentRoomOutOfRange;
            }

            return Reason.Compatible;
        }

        /// <summary>A one-line explanation for the log, naming the numbers that disagree.</summary>
        public static string Describe(DungeonSaveData saveData, string levelKey, int roomCount)
        {
            switch (Check(saveData, levelKey, roomCount))
            {
                case Reason.Compatible:
                    return "compatible";

                case Reason.NoSave:
                    return "no save";

                case Reason.NoLayout:
                    return "the level built no rooms";

                case Reason.DifferentLevel:
                    return $"saved level '{saveData.LevelKey}', now loading '{levelKey}'";

                case Reason.RoomCountChanged:
                {
                    int saved = saveData.Rooms != null ? saveData.Rooms.Count : 0;
                    return $"saved {saved} rooms, the level now builds {roomCount}";
                }

                case Reason.CurrentRoomOutOfRange:
                    return $"saved current room {saveData.CurrentRoomIndex} of {roomCount}";

                default:
                    return "incompatible";
            }
        }
    }
}
