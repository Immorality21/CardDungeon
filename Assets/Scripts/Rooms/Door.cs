using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Rooms
{
    public class Door : MonoBehaviour
    {
        public Room RoomA;
        public Room RoomB;
        public Vector2 PositionInA;
        public Vector2 PositionInB;

        public event Action<Door> OnDoorClicked;

        // Seconds per frame of the swing.
        private const float SwingFrameSeconds = 0.05f;

        // Where this door is painted. A side door is painted by both rooms, each with tiles offset from
        // its own cell, so a swing is one set of frames per place. Empty on the old look, whose door is
        // the sprite.
        private readonly List<DoorPlace> _places = new List<DoorPlace>();
        private bool _opened;

        private class DoorPlace
        {
            public Tilemap Map;
            public Vector3Int Cell;
            public Dictionary<Vector2Int, IReadOnlyList<TileBase>> Swings;
        }

        /// <summary>
        /// Paints the shut door into a room's tilemap, with that room's frames for each way the party
        /// can walk through it, closed to open.
        /// </summary>
        public void AddPlace(Tilemap map, Vector3Int cell, TileBase shut, Dictionary<Vector2Int, IReadOnlyList<TileBase>> swings)
        {
            _places.Add(new DoorPlace { Map = map, Cell = cell, Swings = swings });
            map.SetTile(cell, shut);
        }

        /// <summary>
        /// Swings the door open for a party walking in <paramref name="direction"/>. It stays open, the
        /// way it swung (owner, 2026-10-30): walking back through is just walking. Returns how long the
        /// opening takes, so the party can wait for it: 0 when the door has no swing or is open already.
        /// </summary>
        public float Swing(Vector2Int direction)
        {
            int frames = 0;
            foreach (var place in _places)
            {
                if (place.Swings.TryGetValue(direction, out var tiles))
                {
                    frames = Mathf.Max(frames, tiles.Count);
                }
            }
            if (_opened || frames == 0 || !isActiveAndEnabled)
            {
                return 0f;
            }
            _opened = true;
            StartCoroutine(SwingRoutine(direction, frames));
            return frames * SwingFrameSeconds;
        }

        private IEnumerator SwingRoutine(Vector2Int direction, int frames)
        {
            var wait = new WaitForSeconds(SwingFrameSeconds);
            for (int i = 0; i < frames; i++)
            {
                foreach (var place in _places)
                {
                    if (place.Map != null && place.Swings.TryGetValue(direction, out var tiles) && i < tiles.Count)
                    {
                        place.Map.SetTile(place.Cell, tiles[i]);
                    }
                }
                yield return wait;
            }
        }

        public Room GetOtherRoom(Room current)
        {
            return current == RoomA ? RoomB : RoomA;
        }

        public Vector2 GetPositionInRoom(Room room)
        {
            return room == RoomA ? PositionInA : PositionInB;
        }

        /// <summary>
        /// The way this door leads out of <paramref name="room"/>, one step on the grid. Read off the
        /// two tiles either side, so it holds for a north door whose tiles are two apart (an archway
        /// through a wall face between them).
        /// </summary>
        public Vector2Int DirectionFrom(Room room)
        {
            var offset = GetPositionInRoom(GetOtherRoom(room)) - GetPositionInRoom(room);
            return Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)
                ? new Vector2Int((int)Mathf.Sign(offset.x), 0)
                : new Vector2Int(0, (int)Mathf.Sign(offset.y));
        }

        private void OnMouseDown()
        {
            OnDoorClicked?.Invoke(this);
        }
    }
}
