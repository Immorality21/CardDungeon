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
        public List<Enemy> Enemies = new List<Enemy>();

        /// <summary>
        /// A captive hero waiting in this room, placed by <c>DungeonManager</c> from
        /// <c>RunLevelEntry.RescueHero</c>. Non-null means the room offers a Rescue action once the
        /// room is clear of enemies; cleared back to null the moment they are freed.
        /// </summary>
        public Assets.Scripts.Heroes.HeroSO CaptiveHero;
        /// <summary>
        /// The stat-check event this room offers, assigned at placement by <c>DungeonManager</c> from
        /// the room template's <c>RoomSO.PossibleEvents</c>. Lives on the instance, not the template,
        /// so a template used three times in one level does not offer the same event three times.
        /// </summary>
        public Events.RoomEventSO RoomEvent;

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
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.enabled = enabled;
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

        public Vector3 GetRandomWalkablePosition(List<Vector3> avoidWorldPositions, float minDistance)
        {
            float minX = GridPosition.x + 1;
            float maxX = GridPosition.x + RoomSO.Width - 2;
            float minY = GridPosition.y + 1;
            float maxY = GridPosition.y + RoomSO.Height - 2;

            if (minX > maxX || minY > maxY)
            {
                return GetCenter();
            }

            Vector3 bestPos = Vector3.zero;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                bestPos = new Vector3(
                    Random.Range(minX, maxX + 1),
                    Random.Range(minY, maxY + 1),
                    -1f);

                bool overlaps = false;
                if (avoidWorldPositions != null)
                {
                    foreach (var existing in avoidWorldPositions)
                    {
                        if (Vector3.Distance(existing, bestPos) < minDistance)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                }

                if (!overlaps)
                {
                    return bestPos;
                }
            }

            return bestPos;
        }
    }
}
