using ImmoralityGaming.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Assets.Scripts.Events;

namespace Assets.Scripts.Rooms
{
    /// <summary>The room's screen, by job: the pause overlay, the floor map, the potion window and the dialogs.</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  PAUSE OVERLAY
        // ============================================================

        /// <summary>
        /// Whether Escape should open the pause menu right now. Only from the three places the room
        /// panel actually owns the keyboard - walking the floor, the Fight/Flee prompt, and a hero's
        /// command menu. Anything stacked over the room (an event, a dialog, the spoils screen) uses
        /// Escape for its own way out, and the ability and target pickers live on their own panel and
        /// take focus while they are up, so pause never has to fight another window for the key.
        ///
        /// <para>Those three states are also the ones in which nothing is ticking - the floor waits
        /// for a door, and a CTB turn waits for the command - which is what makes the word "Paused"
        /// honest without stopping any clocks. There is deliberately no way in during an enemy's
        /// turn; it resolves in under a second.</para>
        /// </summary>
        private bool CanOpenPause()
        {
            if (_pauseWindow == null || IsShown(_pauseWindow) || IsShown(_mapWindow) || PotionsOpen || HintsOpen || IsDialogUp())
            {
                return false;
            }
            return DoorNavActive() || IsShown(_combatBar) || IsShown(_heroBar);
        }

        private bool PotionsOpen => _potions != null && _potions.IsOpen;

        /// <summary>Only while the party walks: in a fight a potion costs a turn, which is the Item command.</summary>
        private bool CanOpenPotions()
        {
            return _potions != null && DoorNavActive() && UI.PotionWindow.AnyPotion();
        }

        private void OpenPotions(bool fromPause)
        {
            _potionsFromPause = fromPause;
            if (fromPause)
            {
                ClosePause();
            }
            _potions.Open(GameManager.Instance.Party);
            FocusRoot();
        }

        private void OnPotionsClosed()
        {
            bool backToPause = _potionsFromPause;
            _potionsFromPause = false;
            if (backToPause)
            {
                OpenPause();
                return;
            }
            FocusRoot();
        }

        /// <summary>Saved at once, like a room event: otherwise quitting would give the potion back
        /// and take the health away, or the other way round.</summary>
        private void OnPotionDrunk()
        {
            ShowPartyStatusOutOfCombat();
            if (DungeonSaveManager.HasInstance && _currentRoom != null)
            {
                DungeonSaveManager.Instance.Save(_currentRoom);
            }
        }

        private void OpenPause()
        {
            if (_pauseWindow == null)
            {
                return;
            }
            _pausePotions?.SetEnabled(CanOpenPotions());

            // Arming is per-visit: a player who opened the menu, thought better of quitting and
            // resumed must not find the button still primed the next time they pause.
            _quitArmed = false;
            RefreshQuitButton();

            // AudioOptionsUI owns this element's display and refreshes the readouts as it shows, so
            // the dials always read the live values rather than whatever they said last time.
            _pauseAudio?.Show();
            SetShown(_pauseWindow, true);
            _pauseWindow.parent?.AddToClassList("cd-root--paused");
            _pauseNav?.Reset();
            FocusRoot();
        }

        private void ClosePause()
        {
            if (_pauseWindow == null || !IsShown(_pauseWindow))
            {
                return;
            }

            _quitArmed = false;
            _pauseAudio?.Hide();
            SetShown(_pauseWindow, false);
            _pauseWindow.parent?.RemoveFromClassList("cd-root--paused");
            _pauseNav?.Reset();
            FocusRoot();
        }

        /// <summary>
        /// Whether M should open the map right now - the same three states pause opens from
        /// (<see cref="CanOpenPause"/>), for the same reason: those are the moments the room panel
        /// owns the keyboard, so M cannot be stolen from a window that needs it. The pause overlay's
        /// own Map button bypasses this, because pause being up is already one of those states.
        /// </summary>
        private bool CanOpenMap()
        {
            // A side room is not on the floor's map.
            if (_mapWindow == null || IsShown(_mapWindow) || PotionsOpen || HintsOpen || IsDialogUp() || (_currentRoom != null && _currentRoom.IsSideRoom))
            {
                return false;
            }
            return DoorNavActive() || IsShown(_combatBar) || IsShown(_heroBar);
        }

        /// <summary>
        /// Shows the floor as the party knows it. Rebuilt on every open rather than kept in sync:
        /// the map is a snapshot of a graph that only changes while it is closed.
        /// </summary>
        private void OpenMap(bool fromPause)
        {
            if (_mapWindow == null)
            {
                return;
            }

            _mapFromPause = fromPause;
            if (fromPause)
            {
                ClosePause();
            }

            RefreshMap();
            SetShown(_mapWindow, true);
            FocusRoot();
        }

        private void CloseMap()
        {
            if (_mapWindow == null || !IsShown(_mapWindow))
            {
                return;
            }

            SetShown(_mapWindow, false);
            bool backToPause = _mapFromPause;
            _mapFromPause = false;

            // Back means back: a map reached through the overlay returns to it rather than dropping
            // the player into the room two menus deep.
            if (backToPause)
            {
                OpenPause();
                return;
            }
            FocusRoot();
        }

        private void RefreshMap()
        {
            var rooms = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentRooms : null;
            var model = DungeonMapOps.Build(
                BuildMapInputs(rooms),
                _currentRoom != null ? _currentRoom.RoomIndex : -1);

            _mapView?.SetModel(model);
            // Not StatusLine: the HUD beside the map already says it.
            SetText(_mapStatus, DungeonMapOps.WaitingLine(model));
            SetText(_mapHint, DungeonMapOps.TravelHint(model));

            var entry = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentLevelEntry : null;
            SetText(_mapTitle, entry != null && !string.IsNullOrEmpty(entry.LevelName)
                ? entry.LevelName
                : Loc.Get("room.map.title"));
        }

        /// <summary>
        /// The map's keyboard. Arrows move a cursor over the rooms travel can reach - the same
        /// <c>DirectionalNav</c> maths the sphere grid uses - and Enter travels to it.
        /// Escape, Backspace and M all leave, so the key that opened it also closes it.
        /// </summary>
        private void HandleMapKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                case KeyCode.Backspace:
                case KeyCode.X:
                case KeyCode.M:
                    CloseMap();
                    return;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Z:
                case KeyCode.Space:
                    // With no room picked yet, Enter is the way out rather than a no-op: the map is
                    // read far more often than it is travelled with.
                    if (_mapView != null && _mapView.SelectedRoom >= 0)
                    {
                        TravelTo(_mapView.SelectedRoom);
                        return;
                    }
                    CloseMap();
                    return;
            }

            var direction = ArrowDirection(evt.keyCode);
            if (direction == Vector2.zero || _mapView == null)
            {
                return;
            }

            // Nothing selected yet: the first arrow is measured from the party's own room, so the
            // cursor lands where the player is looking rather than at whichever room was authored
            // first. Same rule as the door cursor.
            int next = _mapView.RoomInDirection(direction);
            if (next < 0 && _mapView.SelectedRoom < 0)
            {
                next = _mapView.FirstTravellableRoom();
            }
            if (next >= 0)
            {
                _mapView.SelectRoom(next);
            }
        }

        /// <summary>Screen-space direction for an arrow key. UI space is y-down.</summary>
        private static Vector2 ArrowDirection(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.UpArrow:
                    return new Vector2(0f, -1f);
                case KeyCode.DownArrow:
                    return new Vector2(0f, 1f);
                case KeyCode.LeftArrow:
                    return new Vector2(-1f, 0f);
                case KeyCode.RightArrow:
                    return new Vector2(1f, 0f);
                default:
                    return Vector2.zero;
            }
        }

        /// <summary>
        /// Fast travel: puts the party in an already-explored room without walking the rooms in
        /// between. What it saves is the trudge back through rooms already dealt with; what it must
        /// never do is buy something a walk could not, which is why
        /// <see cref="DungeonMapOps.TravellableRooms"/> refuses to leave a room live enemies hold
        /// (that would be a free Flee) and refuses to route *through* one (walking cannot - the room
        /// seals as the party enters it).
        ///
        /// <para>The arrival runs the ordinary <see cref="OnDoorSelected"/> steps, through the
        /// <b>last door on the route</b>, so the party ends up in exactly the state a walk would have
        /// left: placed just inside the door, that door recorded as the entry door, and
        /// <c>Party.PreviousRoom</c> pointing at the room behind it - which is what Flee reads. The
        /// two-step placement is what buys that last part: <c>PlaceInRoom</c> moves the party to the
        /// second-to-last room first, so <c>PlaceAtDoor</c> then records *that* as where they came
        /// from rather than the far side of the floor.</para>
        /// </summary>
        private void TravelTo(int roomIndex)
        {
            var rooms = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentRooms : null;
            if (rooms == null || _currentRoom == null || roomIndex == _currentRoom.RoomIndex)
            {
                return;
            }

            var route = DungeonMapOps.RouteTo(BuildMapInputs(rooms), _currentRoom.RoomIndex, roomIndex);
            if (route.Count < 2)
            {
                return;
            }
            // A step still being drawn would otherwise keep moving the party after it has arrived.
            _walk?.Cancel();

            Room Lookup(int index)
            {
                foreach (var room in rooms)
                {
                    if (room != null && room.RoomIndex == index)
                    {
                        return room;
                    }
                }
                return null;
            }

            var penultimate = Lookup(route[route.Count - 2]);
            var destination = Lookup(route[route.Count - 1]);
            if (penultimate == null || destination == null)
            {
                return;
            }

            Door lastDoor = null;
            foreach (var door in penultimate.Doors)
            {
                if (door != null && door.GetOtherRoom(penultimate) == destination)
                {
                    lastDoor = door;
                    break;
                }
            }
            if (lastDoor == null)
            {
                return;
            }

            // Travel is not a way back into the pause overlay.
            _mapFromPause = false;
            CloseMap();

            UnsubscribeDoors();

            var party = GameManager.Instance.Party;
            var leaving = _currentRoom;
            leaving.EnableAllDoors();

            party.PlaceInRoom(penultimate);
            party.PlaceAtDoor(lastDoor, penultimate);

            // The camera follows the party by lerp, which across a floor would read as a long sweep
            // rather than as arriving. Snap it so the map closes onto the destination.
            GameManager.Instance.SnapCamera();

            GameManager.Instance.EnterRoom(destination, lastDoor);
        }

        /// <summary>
        /// Click-to-walk (playtest 2026-10-29, walked rather than jumped since 2026-10-30): a click on
        /// a floor walks the party to that tile, tile by tile, through <see cref="PartyWalk"/> - in this
        /// room, or in another when the way is safe (<see cref="DungeonMapOps.WalkRoute"/>, the map's
        /// fast-travel rules plus one step into a room not yet entered). Only while the doors are live
        /// (<see cref="DoorNavActive"/>), so never in a fight, under a dialog or over the map.
        ///
        /// <para>A door under the pointer is left to its own click (<c>Door.OnMouseDown</c>, which
        /// runs before this), and so is anything on the UI panel. A click on an explored room with no
        /// safe way there floats a line saying so; a click on darkness says nothing, so it cannot tell
        /// the player a hidden room is there.</para>
        /// </summary>
        private void TickRoomClick()
        {
            if (Input.GetMouseButtonDown(0) && IsShown(_combatBar) && !IsDialogUp())
            {
                TryClickFlee();
                return;
            }
            if (!Input.GetMouseButtonDown(0) || _currentRoom == null || !DoorNavActive())
            {
                return;
            }
            var cam = Camera.main;
            if (cam == null || PointerOverUi(Input.mousePosition))
            {
                return;
            }

            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(world);
            if (hit != null && hit.GetComponentInParent<Door>() != null)
            {
                return;
            }

            if (TryWalkToWallEvent(world))
            {
                return;
            }

            if (_currentRoom != null && _currentRoom.IsSideRoom)
            {
                SetWalkGoal(null, _currentRoom, TileWalkOps.NearestFloorTile(FloorOf(_currentRoom), world));
                return;
            }

            var rooms = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentRooms : null;
            int target = DungeonMapOps.RoomAt(BuildMapInputs(rooms), world);
            if (target < 0)
            {
                return;
            }

            var destination = RoomByIndex(rooms, target);
            if (!TryWalkTo(target, world) && destination != null && destination.IsExplored && FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    new Vector3(world.x, world.y, -2f), Loc.Get("room.map.no-safe-way"), new Color(1f, 0.75f, 0.45f));
            }
        }

        /// <summary>
        /// On the Fight bar, a click in the room the party came from flees, like the arrow pointing back
        /// out (there is no Flee button since playtest 4, owner 2026-11-10). A click on the entry door
        /// itself flees through <c>OnEntryDoorFlee</c>.
        /// </summary>
        private void TryClickFlee()
        {
            var cam = Camera.main;
            if (!_canFlee || _entryDoor == null || _currentRoom == null || cam == null || PointerOverUi(Input.mousePosition))
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            var rooms = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentRooms : null;
            int target = DungeonMapOps.RoomAt(BuildMapInputs(rooms), world);
            if (target < 0)
            {
                return;
            }
            var clicked = RoomByIndex(rooms, target);
            if (clicked != null && clicked == _entryDoor.GetOtherRoom(_currentRoom))
            {
                OnFlee();
            }
        }

        /// <summary>A click on an object on the wall walks to the floor tile below it and uses it.</summary>
        private bool TryWalkToWallEvent(Vector2 world)
        {
            var marker = _currentRoom != null ? _currentRoom.EventMarker : null;
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (marker == null || party == null || _currentRoom.RoomEvent == null
                || _currentRoom.RoomEvent.Placement != Events.RoomEventPlacement.NorthWall)
            {
                return false;
            }

            var tile = Vector2Int.RoundToInt(marker.transform.position);
            if (Vector2Int.RoundToInt(world) != tile)
            {
                return false;
            }

            var below = tile + Vector2Int.down;
            var path = TileWalkOps.Path(party.Tile, below, FloorOf(_currentRoom), _currentRoom.SolidTiles(party.Tile));
            if (path == null)
            {
                return false;
            }
            _walkUseTile = tile;
            _walk.Follow(path);
            return true;
        }

        /// <summary>
        /// Sets the party walking to <paramref name="roomIndex"/> by <see cref="DungeonMapOps.WalkRoute"/>,
        /// as a click on that room does, to the floor tile nearest <paramref name="point"/> (the room's
        /// centre when null). The party's own room needs no route. False, and nothing moves, when the
        /// way is not safe or the doors are not live. Public so a scripted check can drive the walk the
        /// mouse would.
        /// </summary>
        public bool TryWalkTo(int roomIndex, Vector2? point = null)
        {
            if (_currentRoom == null || !DoorNavActive())
            {
                return false;
            }
            var rooms = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentRooms : null;
            var destination = RoomByIndex(rooms, roomIndex);
            if (destination == null)
            {
                return false;
            }
            if (destination != _currentRoom
                && DungeonMapOps.WalkRoute(BuildMapInputs(rooms), _currentRoom.RoomIndex, roomIndex).Count < 2)
            {
                return false;
            }

            var tile = TileWalkOps.NearestFloorTile(FloorOf(destination), point ?? (Vector2)destination.GetCenter());
            SetWalkGoal(null, destination, tile);
            return true;
        }

        /// <summary>A door of the room the party is in, which a step could go through.</summary>
        private bool IsUsableDoor(Door door)
        {
            if (!_currentRoom.Doors.Contains(door))
            {
                return false;
            }
            var collider = door.GetComponent<Collider2D>();
            return collider == null || collider.enabled;
        }

        private static RectInt FloorOf(Room room)
        {
            return new RectInt(room.GridPosition, new Vector2Int(room.RoomSO.Width, room.RoomSO.Height));
        }

        /// <summary>Where a click sent the party: a door to go through, or a tile in a room.</summary>
        private void SetWalkGoal(Door door, Room room, Vector2Int tile)
        {
            _walk?.Follow(System.Array.Empty<Vector2Int>());
            _walkUseTile = null;
            _walkGoalDoor = door;
            _walkGoalRoom = room;
            _walkGoalTile = tile;
        }

        private void ClearWalkGoal()
        {
            _walkGoalDoor = null;
            _walkGoalRoom = null;
        }

        /// <summary>
        /// Plans the next leg of a click's walk: up to the door out of this room and through it, or to
        /// the tile once in the right room. One leg at a time and re-derived on every arrival, so
        /// whatever a room turns out to hold stops the walk (the doors are no longer live) and the route
        /// can never go stale. A held key takes over from it.
        /// </summary>
        private void ContinueWalkGoal()
        {
            if (_walkGoalDoor == null && _walkGoalRoom == null)
            {
                return;
            }
            if (_walk.IsStepping || _walk.HasPath)
            {
                return;
            }
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party == null || _currentRoom == null || _walk.KeyHeld || !DoorNavActive())
            {
                ClearWalkGoal();
                return;
            }

            var door = _walkGoalDoor;
            if (door != null && !IsUsableDoor(door))
            {
                // Clicked while a step through another door was landing, or sealed since.
                ClearWalkGoal();
                return;
            }
            var floor = FloorOf(_currentRoom);
            var solid = _currentRoom.SolidTiles(party.Tile);
            if (door == null && _walkGoalRoom == _currentRoom)
            {
                // Something solid is walked up to and used, something left walkable walked onto
                // and used; anywhere else is just walked to.
                bool isSolid = solid.Contains(_walkGoalTile);
                bool useIt = isSolid || _currentRoom.HasUsableAt(_walkGoalTile);
                var path = isSolid
                    ? TileWalkOps.PathNextTo(party.Tile, _walkGoalTile, floor, solid)
                    : TileWalkOps.Path(party.Tile, _walkGoalTile, floor, solid);
                _walkUseTile = useIt && path != null ? _walkGoalTile : (Vector2Int?)null;
                ClearWalkGoal();
                if (path != null)
                {
                    _walk.Follow(path);
                }
                return;
            }
            if (door == null)
            {
                var rooms = DungeonManager.Instance.CurrentRooms;
                var route = DungeonMapOps.WalkRoute(BuildMapInputs(rooms), _currentRoom.RoomIndex, _walkGoalRoom.RoomIndex);
                door = route.Count >= 2 ? DoorBetween(_currentRoom, RoomByIndex(rooms, route[1])) : null;
                if (door == null || !IsUsableDoor(door))
                {
                    ClearWalkGoal();
                    return;
                }
            }

            _walkGoalDoor = null;
            var steps = TileWalkOps.Path(party.Tile, Vector2Int.RoundToInt(door.GetPositionInRoom(_currentRoom)), floor, solid);
            if (steps == null)
            {
                ClearWalkGoal();
                return;
            }
            steps.Add(door.DirectionFrom(_currentRoom));
            _walk.Follow(steps);
        }

        /// <summary>The last of a click on something: turn to it, now the party is beside it or on it, and use it.</summary>
        private void FinishWalkUse()
        {
            if (_walkUseTile == null || _walk.IsStepping || _walk.HasPath)
            {
                return;
            }
            var tile = _walkUseTile.Value;
            _walkUseTile = null;
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party == null || _walk.KeyHeld || !DoorNavActive())
            {
                return;
            }
            var offset = tile - party.Tile;
            int distance = Mathf.Abs(offset.x) + Mathf.Abs(offset.y);
            if (distance > 1)
            {
                return;
            }
            if (distance == 1)
            {
                party.Face(offset);
            }
            UseFacing();
        }

        /// <summary>Whether the pointer is over something on the UI panel (its root does not pick).</summary>
        private bool PointerOverUi(Vector2 screen)
        {
            var panel = _root != null ? _root.panel : null;
            if (panel == null)
            {
                return false;
            }
            var local = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(local) != null;
        }

        private static Room RoomByIndex(IReadOnlyList<Room> rooms, int index)
        {
            if (rooms == null)
            {
                return null;
            }
            foreach (var room in rooms)
            {
                if (room != null && room.RoomIndex == index)
                {
                    return room;
                }
            }
            return null;
        }

        private static Door DoorBetween(Room from, Room to)
        {
            foreach (var door in from.Doors)
            {
                if (door != null && door.GetOtherRoom(from) == to)
                {
                    return door;
                }
            }
            return null;
        }

        /// <summary>
        /// Reads the live rooms into the plain inputs <see cref="DungeonMapOps"/> works on. The
        /// adjacency comes off the doors rather than the generator's <c>RoomNode.connections</c>,
        /// because a door is what the player can actually walk through - a connection with no door
        /// placed is not a route, and drawing it would put a corridor on the map that does not exist.
        /// </summary>
        private static List<MapRoomInput> BuildMapInputs(IReadOnlyList<Room> rooms)
        {
            var inputs = new List<MapRoomInput>();
            if (rooms == null)
            {
                return inputs;
            }

            foreach (var room in rooms)
            {
                if (room == null || room.RoomSO == null)
                {
                    continue;
                }

                var neighbours = new List<int>();
                foreach (var door in room.Doors)
                {
                    if (door == null)
                    {
                        continue;
                    }

                    var other = door.GetOtherRoom(room);
                    if (other != null && other != room)
                    {
                        neighbours.Add(other.RoomIndex);
                    }
                }

                bool pending = room.HasPendingPayload;
                inputs.Add(new MapRoomInput
                {
                    Index = room.RoomIndex,
                    Bounds = new RectInt(
                        room.GridPosition.x, room.GridPosition.y, room.RoomSO.Width, room.FootprintHeight),
                    IsExplored = room.IsExplored,
                    IsExit = room.IsExit,
                    HasEnemies = room.Enemies.Any(e => e != null && e.IsAlive),
                    HasCaptive = room.CaptiveHero != null,
                    HasPendingEvent = room.HasPendingEvent,
                    HasPendingCache = pending && room.Kind == RoomKind.Treasure,
                    HasPendingRefuge = pending && room.Kind == RoomKind.Rest,
                    Neighbours = neighbours,
                });
            }

            return inputs;
        }

        /// <summary>
        /// Leaving mid-run, in two presses. The first arms the button and says what it costs, because
        /// the cost is real and none of it is on screen: the floor restarts from its entrance with
        /// every enemy back on its feet, and this floor's un-banked XP and kill-gold are forfeited
        /// exactly as a wipe forfeits them - while the party's health, charges and wounds come back
        /// untouched, so it can never be used to undo a bad fight.
        /// </summary>
        private void OnQuitToTitle()
        {
            if (!_quitArmed)
            {
                _quitArmed = true;
                RefreshQuitButton();
                return;
            }

            if (DungeonManager.HasInstance)
            {
                DungeonManager.Instance.HandleQuitToTitle();
            }
            // The title screen, never the hub: the town is reached only at a run's end (owner,
            // 2026-10-30). Continue there sends the party straight back down (HubManager.Start).
            SceneManager.LoadScene("MenuScene");
        }

        private void RefreshQuitButton()
        {
            if (_pauseQuit != null)
            {
                _pauseQuit.text = _quitArmed ? Loc.Get("room.pause.leave-armed") : Loc.Get("room.pause.leave");
            }
            SetText(_pauseNote, _quitArmed
                ? Loc.Get("room.pause.note-armed")
                : Loc.Get("room.pause.note"));
        }

        /// <summary>Whether a window is stacked over the room, dialog-style.</summary>
        private bool IsDialogUp()
        {
            return IsShown(_victoryWindow) || IsShown(_levelWindow) || IsShown(_eventWindow) || IsShown(_detailWindow);
        }

        /// <summary>
        /// The keyboard for the windows that stack over the room. Without these a player who searched
        /// a cache or opened an event with the keyboard would have to reach for the mouse to get out
        /// of the dialog, which defeats the point of the rest of it.
        /// </summary>
        private bool HandleDialogKey(KeyDownEvent evt)
        {
            if (IsShown(_levelWindow))
            {
                // A statement like the spoils screen: confirm leaves. Escape is not accepted here -
                // the next thing is a scene load, and a player backing out of a menu should not
                // find themselves in the hub by accident.
                switch (evt.keyCode)
                {
                    case KeyCode.LeftArrow:
                    case KeyCode.RightArrow:
                    case KeyCode.Tab:
                        // Only a window offering both ways on has anything to switch between.
                        if (_levelNextLevelAction != null && _levelContinueAction != null)
                        {
                            SetLevelChoice(!_levelChoiceIsNext);
                        }
                        return true;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Z:
                    case KeyCode.Space:
                        if (_levelChoiceIsNext && _levelNextLevelAction != null)
                        {
                            OnLevelClearNextLevel();
                        }
                        else
                        {
                            OnLevelClearContinue();
                        }
                        return true;
                }
                return false;
            }

            if (IsShown(_victoryWindow))
            {
                // Nothing to choose on a spoils screen, so every confirm or cancel key dismisses it.
                switch (evt.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Z:
                    case KeyCode.Space:
                    case KeyCode.Escape:
                    case KeyCode.Backspace:
                    case KeyCode.X:
                        OnVictoryContinue();
                        return true;
                }
                return false;
            }

            if (IsShown(_eventWindow))
            {
                return _eventNav != null && _eventNav.HandleKey(evt);
            }

            if (IsShown(_detailWindow))
            {
                switch (evt.keyCode)
                {
                    case KeyCode.UpArrow:
                    case KeyCode.DownArrow:
                    case KeyCode.LeftArrow:
                    case KeyCode.RightArrow:
                    case KeyCode.Tab:
                        return _detailNav != null && _detailNav.HandleKey(evt);
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Z:
                    case KeyCode.Space:
                        // The highlighted button answers; with no cursor yet (the frame it opened) Ok does.
                        if (_detailNav == null || !_detailNav.HandleKey(evt))
                        {
                            _detailOkAction?.Invoke();
                        }
                        return true;
                    case KeyCode.Escape:
                    case KeyCode.Backspace:
                    case KeyCode.X:
                        // Only a dialog that is actually offering a way out has one; a statement with a
                        // single OK is dismissed by Escape too rather than trapping the player.
                        if (_detailCancel != null && IsShown(_detailCancel))
                        {
                            _detailCancelAction?.Invoke();
                        }
                        else
                        {
                            _detailOkAction?.Invoke();
                        }
                        return true;
                }
                return false;
            }

            return false;
        }
    }
}
