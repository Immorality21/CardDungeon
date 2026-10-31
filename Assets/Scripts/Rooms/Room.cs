using Assets.Scripts.Enemies;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    public class Room : MonoBehaviour
    {
        public RoomSO RoomSO;
        public List<Door> Doors = new List<Door>();
        public Vector2Int GridPosition;

        /// <summary>
        /// Rows of north wall face drawn above the floor. Part of the room's footprint on the grid, never
        /// walkable: the floor is still <c>RoomSO.Width</c> x <c>RoomSO.Height</c> from <see cref="GridPosition"/>.
        /// </summary>
        public int WallRows;

        /// <summary>How many grid rows the room takes: its floor plus its wall face.</summary>
        public int FootprintHeight => RoomSO.Height + WallRows;

        public List<Enemy> Enemies = new List<Enemy>();

        /// <summary>
        /// A captive hero waiting in this room, placed by <c>DungeonManager</c> from
        /// <c>RunLevelEntry.RescueHero</c>. Non-null means they are freed automatically the moment
        /// the room is clear of enemies (<c>RoomActionUI.TryAutoRescue</c>), then cleared back to null.
        /// </summary>
        public Assets.Scripts.Heroes.HeroSO CaptiveHero;
        /// <summary>
        /// The stat-check event this room offers, assigned at placement by <c>DungeonManager</c> from
        /// the level's <c>LevelDefinitionSO.RoomEvents</c>, per instance.
        /// </summary>
        public Events.RoomEventSO RoomEvent;

        /// <summary>
        /// What this room is. Starts as the template's <see cref="RoomSO.Kind"/> and can be promoted at
        /// generation by <c>DungeonManager.PlaceRoomKinds</c> - per instance, so a template used three
        /// times in one level does not become three treasure caches.
        /// </summary>
        public RoomKind Kind = RoomKind.Combat;

        /// <summary>The stairs in an exit room, where walking up to them and pressing Enter descends.</summary>
        public Transform ExitMarker;

        /// <summary>The marker sprite drawn for a payload room, so it can be swapped once taken.</summary>
        public SpriteRenderer KindMarker;

        /// <summary>
        /// What <see cref="KindMarker"/> shows once the payload is gone - an opened chest, a dead
        /// fire. Null leaves the full marker up, which is still better than a greyed-out ghost.
        /// </summary>
        public Sprite KindMarkerSpent;

        public int RoomIndex { get; set; }
        public bool IsExplored { get; private set; }
        public bool IsExit { get; set; }

        /// <summary>
        /// Whether this room's event has been resolved. One-shot and persisted (see
        /// <c>RoomSaveData</c>): without it the player re-rolls a bad outcome by walking out and back
        /// in, or by quitting to the menu and resuming.
        /// </summary>
        public bool EventConsumed { get; private set; }

        /// <summary>Index into <c>RoomEvent.Options</c> that resolved it, or -1.</summary>
        public int EventOptionIndex { get; private set; } = -1;

        /// <summary>Index into that option's success or failure pool, or -1.</summary>
        public int EventOutcomeIndex { get; private set; } = -1;

        /// <summary>Which of the two pools <see cref="EventOutcomeIndex"/> indexes.</summary>
        public bool EventSucceeded { get; private set; }

        /// <summary>
        /// Whether this room's one-shot payload has been taken. Persisted for the same reason an event
        /// is: without it the player re-loots the cache by walking out and back in.
        /// </summary>
        public bool KindConsumed { get; private set; }

        /// <summary>Whether the room still has a cache or a refuge to offer.</summary>
        public bool HasPendingPayload
        {
            get { return Kind.HasPayload() && !KindConsumed; }
        }

        /// <summary>Records the payload as taken and shows its spent marker.</summary>
        public void MarkPayloadTaken()
        {
            KindConsumed = true;
            if (HostRoom != null)
            {
                HostRoom.SideRoomCleared = true;
            }
            ShowSpentKindMarker();
        }

        /// <summary>
        /// Swaps the payload marker for its spent state - the room still reads as a cache, just an
        /// emptied one. A spent <i>sprite</i> rather than a tint: the first version greyed the
        /// marker out, and a grey silhouette read as a ghost of something still there.
        /// </summary>
        public void ShowSpentKindMarker()
        {
            if (KindMarker != null && KindMarkerSpent != null)
            {
                KindMarker.sprite = KindMarkerSpent;
            }
        }

        /// <summary>
        /// The object standing in the room for its event, drawn from <c>RoomEventSO.WorldSprite</c>.
        /// Null when the event has no art: there is then nothing to walk up to, which
        /// <c>RoomEventObjectTests</c> guards against for every event a level lists.
        /// </summary>
        public SpriteRenderer EventMarker;

        /// <summary>An outcome's item, waiting for this room's fight to be won.</summary>
        [System.NonSerialized] public List<Assets.Scripts.Items.ItemSO> PendingEventLoot;

        /// <summary>Whether the event's guaranteed item has been handed over. Saved, so it pays once.</summary>
        public bool EventLootClaimed { get; set; }

        /// <summary>The room this one was reached from, or null for a room of the floor itself.</summary>
        public Room HostRoom { get; set; }

        /// <summary>What this side room was built from; null on a room of the floor itself.</summary>
        public Events.SideRoomSpec SideSpec { get; set; }

        /// <summary>This room's side room, once it has been built.</summary>
        public Room SideRoom { get; set; }

        /// <summary>Whether this room's side room has been emptied. Saved on this room.</summary>
        public bool SideRoomCleared { get; set; }

        public bool IsSideRoom => HostRoom != null;

        /// <summary>The way back out of a side room, walked up to like anything else.</summary>
        public SpriteRenderer ReturnMarker;

        /// <summary>Where the party stands on entering a side room: above its way out, in the middle.</summary>
        public Vector2Int SideEntryTile => GridPosition + new Vector2Int(RoomSO.Width / 2, 1);

        /// <summary>Whether the room still has an event to offer.</summary>
        public bool HasPendingEvent
        {
            get { return RoomEvent != null && !EventConsumed; }
        }

        /// <summary>
        /// Records that the event resolved. The coordinates are kept (rather than a manifest of what
        /// happened) because the outcome is still in the asset on reload, so re-spawning whatever it
        /// woke up only needs to know which outcome it was.
        /// </summary>
        public void MarkEventResolved(int optionIndex, int outcomeIndex, bool succeeded)
        {
            EventConsumed = true;
            EventOptionIndex = optionIndex;
            EventOutcomeIndex = outcomeIndex;
            EventSucceeded = succeeded;
            if (EventMarker != null && RoomEvent != null && RoomEvent.SpentSprite != null)
            {
                EventMarker.sprite = RoomEvent.SpentSprite;
            }
        }

        public void Reveal()
        {
            IsExplored = true;
            SetChildRenderersEnabled(true);
            SetEnemyRenderersEnabled(true);

            foreach (var door in Doors)
            {
                SetDoorRenderersEnabled(door, true);
            }
        }

        public void Hide()
        {
            SetChildRenderersEnabled(false);
            SetEnemyRenderersEnabled(false);

            foreach (var door in Doors)
            {
                var otherRoom = door.GetOtherRoom(this);
                if (otherRoom == null || !otherRoom.IsExplored)
                {
                    SetDoorRenderersEnabled(door, false);
                }
            }
        }

        private void SetChildRenderersEnabled(bool enabled)
        {
            // Renderer, not SpriteRenderer: a biome-drawn room is painted on Tilemaps.
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = enabled;
            }
        }

        private void SetEnemyRenderersEnabled(bool enabled)
        {
            foreach (var enemy in Enemies
                .Where(x => x))
            {
                var sr = enemy.GetComponent<SpriteRenderer>();

                if (sr != null)
                {
                    sr.enabled = enabled;
                }
            }
        }

        private void SetDoorRenderersEnabled(Door door, bool enabled)
        {
            var sr = door.GetComponent<SpriteRenderer>();

            if (sr != null)
            {
                sr.enabled = enabled;
            }
        }

        public void SetDoorsEnabled(Door excludeDoor)
        {
            foreach (var door in Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = excludeDoor == null || door == excludeDoor;
                }
            }
        }

        public void EnableAllDoors()
        {
            foreach (var door in Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }

        /// <summary>Disables every door — used for boss rooms so the fight can't be fled.</summary>
        public void DisableAllDoors()
        {
            foreach (var door in Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }

        public Vector3 GetCenter()
        {
            return new Vector3(
                GridPosition.x + RoomSO.Width / 2f - 0.5f,
                GridPosition.y + RoomSO.Height / 2f - 0.5f,
                -1f);
        }

        /// <summary>
        /// Claims a floor tile for <paramref name="owner"/> (an enemy, a marker) and returns it at
        /// depth <paramref name="z"/>. The tile keeps clear of the doors, of where the party stands
        /// when it walks in, and of everything claimed before - see <see cref="RoomSpotOps"/>.
        /// Claiming again for the same owner returns its existing spot.
        /// </summary>
        public Vector3 ClaimSpot(object owner, float z)
        {
            if (owner != null && _spots.TryGetValue(owner, out var existing))
            {
                return new Vector3(existing.x, existing.y, z);
            }

            var center = (Vector2)GetCenter();
            var avoid = new List<Vector2>(_spots.Values);
            if (IsSideRoom)
            {
                avoid.Add(SideEntryTile);
            }
            foreach (var door in Doors)
            {
                if (door == null)
                {
                    continue;
                }
                var doorTile = door.GetPositionInRoom(this);
                avoid.Add(doorTile);
                avoid.Add(RoomSpotOps.EntrySpot(doorTile, center));
            }

            var tiles = RoomSpotOps.Tiles(GridPosition, RoomSO.Width, RoomSO.Height);
            if (PropCells.Count > 0)
            {
                // Keep off the props while there is floor enough; a crowded room still uses them.
                var clear = tiles.FindAll(t => !PropCells.Contains(Vector2Int.RoundToInt(t)));
                if (clear.Count > _spots.Count)
                {
                    tiles = clear;
                }
            }

            var spot = RoomSpotOps.Choose(tiles, avoid, center);
            if (owner != null)
            {
                _spots[owner] = spot;
            }
            return new Vector3(spot.x, spot.y, z);
        }

        /// <summary>
        /// The wall tile nearest the middle of the north wall with no door in it and its floor tile
        /// free, so it can be walked up to. Null when the wall has none. The same answer until
        /// something else claims a tile, so a resumed floor puts the object back where it was.
        /// </summary>
        public Vector2Int? FreeNorthWallTile()
        {
            int topRow = GridPosition.y + RoomSO.Height - 1;
            var taken = new List<int>();
            foreach (var door in Doors)
            {
                if (door != null && door.DirectionFrom(this) == Vector2Int.up)
                {
                    taken.Add(Mathf.RoundToInt(door.GetPositionInRoom(this).x));
                }
            }
            foreach (var spot in _spots.Values)
            {
                if (Mathf.RoundToInt(spot.y) == topRow)
                {
                    taken.Add(Mathf.RoundToInt(spot.x));
                }
            }

            int middle = GridPosition.x + RoomSO.Width / 2;
            for (int offset = 0; offset < RoomSO.Width; offset++)
            {
                foreach (int x in new[] { middle - offset, middle + offset })
                {
                    if (x >= GridPosition.x && x < GridPosition.x + RoomSO.Width && !taken.Contains(x))
                    {
                        return new Vector2Int(x, topRow + 1);
                    }
                }
            }
            return null;
        }

        /// <summary>Claims one exact tile, for what has its own place in a side room.</summary>
        public Vector3 ClaimTile(object owner, Vector2Int tile, float z)
        {
            _spots[owner] = tile;
            return new Vector3(tile.x, tile.y, z);
        }

        /// <summary>
        /// Floor cells a biome prop lies on (<see cref="RoomTilePainter"/>). <see cref="ClaimSpot"/> keeps
        /// occupants off them while the room has other free tiles, so a skull is not drawn under a foe.
        /// </summary>
        public readonly HashSet<Vector2Int> PropCells = new HashSet<Vector2Int>();

        /// <summary>Frees the tile <paramref name="owner"/> claimed, so later occupants may use it.</summary>
        public void ReleaseSpot(object owner)
        {
            if (owner != null)
            {
                _spots.Remove(owner);
            }
        }

        /// <summary>
        /// The tiles a walking party may not step on, standing on <paramref name="partyTile"/>: everything
        /// claimed but the dead, except where blocking it would cut off a door or something the party
        /// has to walk up to (<see cref="TileWalkOps.Solid"/>).
        /// </summary>
        public HashSet<Vector2Int> SolidTiles(Vector2Int partyTile)
        {
            var occupied = new List<Vector2Int>();
            foreach (var spot in _spots)
            {
                if (spot.Key is Enemy enemy && (enemy == null || !enemy.IsAlive))
                {
                    continue;
                }
                occupied.Add(Vector2Int.RoundToInt(spot.Value));
            }

            var mustReach = new List<Vector2Int> { partyTile };
            foreach (var door in Doors)
            {
                if (door != null)
                {
                    mustReach.Add(Vector2Int.RoundToInt(door.GetPositionInRoom(this)));
                }
            }
            var floor = new RectInt(GridPosition, new Vector2Int(RoomSO.Width, RoomSO.Height));
            return TileWalkOps.Solid(floor, occupied, mustReach, UsableTiles());
        }

        /// <summary>Whether what stands on a tile is something the party uses, spent or not.</summary>
        public bool HasUsableAt(Vector2Int tile)
        {
            return UsableTiles().Contains(tile);
        }

        /// <summary>Where the room's stairs, chest or campfire, event object and way back stand.</summary>
        private List<Vector2Int> UsableTiles()
        {
            var tiles = new List<Vector2Int>();
            foreach (var marker in new Component[] { ExitMarker, KindMarker, EventMarker, ReturnMarker })
            {
                if (marker != null)
                {
                    tiles.Add(Vector2Int.RoundToInt(marker.transform.position));
                }
            }
            return tiles;
        }

        /// <summary>Whether anything has claimed a tile in this room.</summary>
        public bool HasClaimedSpots => _spots.Count > 0;

        private readonly Dictionary<object, Vector2> _spots = new Dictionary<object, Vector2>();
    }
}
