using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Walks the party a tile at a time with the arrows or WASD, the way an SNES Final Fantasy hero
    /// walks a dungeon (owner, 2026-10-30). Holding a key keeps walking; when two are held the one
    /// pressed last wins, so turning a corner does not need the first key let go. A tap made while a
    /// step is still drawn waits its turn (<see cref="WalkInput"/>, up to three).
    ///
    /// <para>Where a step may go is <see cref="TileWalkOps"/>. A step through a door is the same walk
    /// a click on that door takes (<c>RoomActionUI.OnDoorSelected</c>): the party is placed, the room
    /// revealed and saved, and Fight / Flee raised if anything is waiting. Nothing about entering a
    /// room is decided here.</para>
    ///
    /// <para>It reads the keyboard through legacy <c>Input</c> rather than UI Toolkit key events,
    /// because a held key in UI Toolkit only repeats at the OS repeat rate - half a second before the
    /// second step. The room panel still swallows the arrow keys so they cannot move UI focus.</para>
    /// </summary>
    public class PartyWalk
    {
        /// <summary>Seconds per tile: about seven tiles a second, close to an SNES walk.</summary>
        public const float StepSeconds = 0.14f;

        // How long after a step the walk cycle keeps running, so the feet do not stop for the one
        // frame between two steps of a held key.
        private const float WalkCycleGrace = 0.06f;

        /// <summary>The way a walking key points, or zero for any other key.</summary>
        public static Vector2Int DirectionOf(KeyCode keyCode)
        {
            foreach (var key in Keys)
            {
                if (key.Arrow == keyCode || key.Letter == keyCode)
                {
                    return key.Direction;
                }
            }
            return Vector2Int.zero;
        }

        private static readonly (Vector2Int Direction, KeyCode Arrow, KeyCode Letter)[] Keys =
        {
            (Vector2Int.up, KeyCode.UpArrow, KeyCode.W),
            (Vector2Int.down, KeyCode.DownArrow, KeyCode.S),
            (Vector2Int.left, KeyCode.LeftArrow, KeyCode.A),
            (Vector2Int.right, KeyCode.RightArrow, KeyCode.D),
        };

        private readonly Action<Door> _enterDoor;
        private readonly WalkInput _input = new WalkInput();
        private readonly List<RoomDoorway> _doorways = new List<RoomDoorway>();
        private readonly List<bool> _open = new List<bool>();
        private readonly Queue<Vector2Int> _path = new Queue<Vector2Int>();

        private Party _party;
        private Vector3 _from;
        private Vector3 _to;
        private float _elapsed;
        private float _duration;
        private float _doorWait;
        private float _sinceStep;
        private Door _throughDoor;
        private bool _keyStep;
        private bool _stepByKey;

        /// <summary>A step the walking keys took has landed on this tile (not a click's path, not a door).</summary>
        public event Action<Vector2Int> KeyStepEnded;

        /// <summary>A walking key pushed the party from the first tile toward the second and the step was refused.</summary>
        public event Action<Vector2Int, Vector2Int> KeyBumped;

        /// <param name="enterDoor">Walks through a door: what a click on it does.</param>
        public PartyWalk(Action<Door> enterDoor)
        {
            _enterDoor = enterDoor;
        }

        /// <summary>Whether a step is being drawn. Nothing else should move the party meanwhile.</summary>
        public bool IsStepping => _party != null;

        /// <summary>Whether a queued walk (a click) still has steps to take.</summary>
        public bool HasPath => _path.Count > 0;

        /// <summary>Whether a direction key is down or a tap is queued: the player has taken over from any click.</summary>
        public bool KeyHeld => _input.AnyActive;

        /// <summary>
        /// Ignores every walking key now down until it is let go: for a key that has just done
        /// something else, like the arrow that fled a fight (playtest 4 finding 33).
        /// </summary>
        public void HoldUntilReleased()
        {
            foreach (var key in Keys)
            {
                if (Input.GetKey(key.Arrow) || Input.GetKey(key.Letter))
                {
                    _input.Latch(key.Direction);
                }
            }
            _input.ClearQueue();
        }

        /// <summary>Queues steps to take one after another, in place of any already queued.</summary>
        public void Follow(IEnumerable<Vector2Int> steps)
        {
            _path.Clear();
            foreach (var step in steps)
            {
                _path.Enqueue(step);
            }
        }

        /// <summary>
        /// Advances the step being drawn, then starts the next one if a key is held and
        /// <paramref name="canWalk"/> - the room panel's "the doors are live and nothing is over the
        /// room".
        /// </summary>
        public void Tick(Party party, Room room, bool canWalk, float deltaTime)
        {
            ReadKeys();
            if (IsStepping)
            {
                _sinceStep = 0f;
                Advance(deltaTime);
                return;
            }
            if (NextStep(party, room, canWalk))
            {
                return;
            }
            _sinceStep += deltaTime;
            if (party != null && _sinceStep >= WalkCycleGrace)
            {
                party.SetWalking(false);
            }
        }

        private bool NextStep(Party party, Room room, bool canWalk)
        {
            if (!canWalk || party == null || room == null)
            {
                _path.Clear();
                _input.ClearQueue();
                return false;
            }
            if (_input.TryNext(out var direction))
            {
                _path.Clear();
                _keyStep = true;
                try
                {
                    return TryStep(party, room, direction);
                }
                finally
                {
                    _keyStep = false;
                }
            }
            if (_path.Count == 0)
            {
                return false;
            }
            if (TryStep(party, room, _path.Dequeue()))
            {
                return true;
            }
            _path.Clear();
            return false;
        }

        /// <summary>
        /// Starts one step, as a held key would. Public so a script can walk the party without a
        /// keyboard - which tests this path, not the key reading (see <c>docs/GAMEPLAY_VALIDATION.md</c>).
        /// </summary>
        public bool TryStep(Party party, Room room, Vector2Int direction)
        {
            if (IsStepping || party == null || room == null || room.RoomSO == null)
            {
                return false;
            }

            party.Face(direction);
            var floor = new RectInt(room.GridPosition, new Vector2Int(room.RoomSO.Width, room.RoomSO.Height));
            var from = TileWalkOps.NearestFloorTile(floor, party.transform.position);

            _doorways.Clear();
            _open.Clear();
            var doors = new List<Door>();
            foreach (var door in room.Doors)
            {
                if (door == null)
                {
                    continue;
                }
                doors.Add(door);
                _doorways.Add(new RoomDoorway(Vector2Int.RoundToInt(door.GetPositionInRoom(room)), door.DirectionFrom(room)));
                // The same gate a click passes: a door is usable while its collider is on
                // (Room.SetDoorsEnabled / DisableAllDoors switch it off in a sealed room).
                var collider = door.GetComponent<Collider2D>();
                _open.Add(collider == null || collider.enabled);
            }

            var step = TileWalkOps.Step(from, direction, floor, _doorways, _open, room.SolidTiles(from));
            switch (step.Kind)
            {
                case WalkStepKind.Move:
                    Begin(party, new Vector3(step.To.x, step.To.y, -1f), 1f, null);
                    _stepByKey = _keyStep;
                    return true;
                case WalkStepKind.Door:
                    var door = doors[step.DoorIndex];
                    var landing = door.GetPositionInRoom(door.GetOtherRoom(room));
                    var target = new Vector3(landing.x, landing.y, -1f);
                    // A north door crosses the wall face too: two tiles, at the same pace.
                    float tiles = Mathf.Max(1f, Vector2.Distance(party.transform.position, target));
                    Begin(party, target, tiles, door);
                    // The party waits for the door: a step is too short to see one open during it.
                    _doorWait = door.Swing(direction);
                    _stepByKey = false;
                    return true;
                default:
                    if (_keyStep)
                    {
                        KeyBumped?.Invoke(from, from + direction);
                    }
                    return false;
            }
        }

        /// <summary>Drops a step in progress where it is going - for a room change from elsewhere.</summary>
        public void Cancel()
        {
            if (_party != null)
            {
                _party.transform.position = _to;
            }
            _party = null;
            _throughDoor = null;
            _stepByKey = false;
            _doorWait = 0f;
            _path.Clear();
            _input.ClearQueue();
        }

        private void Begin(Party party, Vector3 target, float tiles, Door throughDoor)
        {
            party.SetWalking(true);
            _sinceStep = 0f;
            _party = party;
            _from = party.transform.position;
            _to = target;
            _elapsed = 0f;
            _duration = StepSeconds * tiles;
            _throughDoor = throughDoor;
        }

        private void Advance(float deltaTime)
        {
            if (_doorWait > 0f)
            {
                _doorWait -= deltaTime;
                return;
            }

            _elapsed += deltaTime;
            float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            _party.transform.position = Vector3.Lerp(_from, _to, t);
            if (t < 1f)
            {
                return;
            }

            var door = _throughDoor;
            var landed = Vector2Int.RoundToInt(_to);
            bool byKey = _stepByKey;
            _party = null;
            _throughDoor = null;
            _stepByKey = false;
            if (door != null)
            {
                _enterDoor(door);
            }
            else if (byKey)
            {
                KeyStepEnded?.Invoke(landed);
            }
        }

        private void ReadKeys()
        {
            // A tap during a step is queued rather than lost when it is let go before the step ends
            // (playtest 4 finding 57); WalkInput holds the rules.
            foreach (var key in Keys)
            {
                bool down = Input.GetKey(key.Arrow) || Input.GetKey(key.Letter);
                bool pressed = Input.GetKeyDown(key.Arrow) || Input.GetKeyDown(key.Letter);
                _input.Read(key.Direction, down, pressed, IsStepping);
            }
        }
    }
}
