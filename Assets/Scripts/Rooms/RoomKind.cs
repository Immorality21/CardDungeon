namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// What a room <i>is</i>. Rooms used to be interchangeable boxes with a spawn table, so a level
    /// was a hallway; a kind is what makes it a sequence of decisions instead.
    ///
    /// <para>Serialized by ordinal on `RoomSO` assets and in `RoomSaveData`, so <b>append only</b>.
    /// Members are added when they do something: an enum entry no code acts on is exactly the dead
    /// content this project keeps finding, so Elite / Merchant / Boss are deliberately absent until
    /// they have behaviour. (The boss room is already expressed by `RunLevelEntry.BossEnemy` claiming
    /// the exit room.)</para>
    /// </summary>
    public enum RoomKind
    {
        /// <summary>A room that may hold enemies. The default, and what every room was before kinds.</summary>
        Combat = 0,

        /// <summary>A hallway: no enemies, no events, no payload. Was <c>RoomSO.IsConnectorRoom</c>.</summary>
        Connector = 1,

        /// <summary>Holds a one-shot cache: gold plus a depth-rolled item. No enemies.</summary>
        Treasure = 2,

        /// <summary>Holds a one-shot rest: heals every hero a share of their bar. No enemies.</summary>
        Rest = 3
    }

    public static class RoomKinds
    {
        /// <summary>
        /// Whether <c>EnemyManager</c> populates a room of this kind. A treasure cache guarded by
        /// three imps is just another fight, and the point of a non-combat room is that it is not one.
        /// </summary>
        public static bool HoldsEnemies(this RoomKind kind)
        {
            return kind == RoomKind.Combat;
        }

        /// <summary>
        /// Whether the room offers a one-shot payload through the room bar (Search / Rest). The two
        /// kinds that do are also the two that can be *promoted* onto an ordinary room at generation.
        /// </summary>
        public static bool HasPayload(this RoomKind kind)
        {
            return kind == RoomKind.Treasure || kind == RoomKind.Rest;
        }

        /// <summary>
        /// Whether a captive, a stat-check event or another special can share the room. False for
        /// anything with a payload: a room offers one thing, so its button means one thing.
        /// </summary>
        public static bool AcceptsOtherSpecials(this RoomKind kind)
        {
            return kind == RoomKind.Combat;
        }

        public static string DisplayName(this RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Connector:
                    return "Passage";
                case RoomKind.Treasure:
                    return "Cache";
                case RoomKind.Rest:
                    return "Refuge";
                default:
                    return "Chamber";
            }
        }
    }
}
