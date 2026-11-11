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
    /// <summary>The room's screen, by job: walking the floor, from the keyboard and by clicks.</summary>
    public partial class RoomActionUI
    {
        /// <summary>What using the thing at hand would do (<see cref="UseAt"/>).</summary>
        private enum RoomUse { None, GoBack, Event, SideRoom, Search, Rest, Stairs }

        // ============================================================
        //  WALKING THE DUNGEON FROM THE KEYBOARD
        // ============================================================

        /// <summary>
        /// Keeps the keyboard hint in step with what the keys currently do. Driven from Update rather
        /// than from the dozen places that swap a bar or open a window: the line is derived from what
        /// is on screen, and re-deriving it each frame cannot fall out of sync the way a dozen call
        /// sites can.
        /// </summary>
        /// <summary>
        /// Tells the world-space HP bars where the turn-order panel is, so a boss's bar can step
        /// out from under it (<see cref="Combat.CombatHudLayout"/>).
        /// </summary>
        /// <summary>
        /// The nameplates are hidden under the combat Inspect page. They are in this document, which
        /// draws over the picker's, and the page is docked left - over the heroes' plates - so that
        /// it leaves the enemy it describes in view. Visibility, not display, so layout is kept.
        /// </summary>
        private void TickNameplateLayer()
        {
            if (_nameplateLayer == null)
            {
                return;
            }
            if (_magicSelection == null)
            {
                _magicSelection = FindFirstObjectByType<Cards.UI.MagicSelectionUI>();
            }
            bool covered = _magicSelection != null && _magicSelection.IsInspectOpen;
            var wanted = covered ? Visibility.Hidden : Visibility.Visible;
            if (_nameplateLayer.style.visibility != wanted)
            {
                _nameplateLayer.style.visibility = wanted;
            }
        }

        private void PublishHudLayout()
        {
            Combat.CombatHudLayout.TurnOrderViewport = TryGetViewportRect(_turnOrder, out Rect box) ? box : Rect.zero;
        }

        private void Update()
        {
            if (!_refsReady)
            {
                return;
            }

            _nameplates?.Tick(Camera.main);
            TickNameplateLayer();
            TickHud();
            PublishHudLayout();

            // The band follows the stage rather than the fight: it stays up behind the victory window
            // and the party window goes with it, back to its corner for the walk. A picker lives in
            // another document, drawn under this one, and fills the band's left window itself, so the
            // band steps aside for it.
            bool stageUp = CombatStage.HasInstance && CombatStage.Instance.IsRaised;
            bool bandUp = stageUp && (_magicSelection == null || !_magicSelection.IsPickerOpen);
            if (IsShown(_combatBand) != bandUp)
            {
                SetShown(_combatBand, bandUp);
            }
            if (_partyStatus != null && _partyStatus.ClassListContains("cd-party-status--band") != stageUp)
            {
                _partyStatus.EnableInClassList("cd-party-status--band", stageUp);
                _navHint?.EnableInClassList("cd-nav-hint--band", stageUp);
            }

            // The scrim follows the dialogs rather than being set at each of the many places one
            // opens, so no path can leave the room dimmed or a dialog undimmed.
            bool dialogUp = IsShown(_eventWindow) || IsShown(_detailWindow) || IsShown(_mapWindow) || IsShown(_pauseWindow)
                || PotionsOpen || HintsOpen;
            if (IsShown(_dialogScrim) != dialogUp)
            {
                SetShown(_dialogScrim, dialogUp);
            }

            // Clicking anything that is not UI - a door, a wall, the floor - clears the EventSystem's
            // selection and with it the keyboard. Re-claiming here costs a null check and means the
            // arrows cannot go dead halfway across a floor the player is walking with the mouse.
            PanelKeyboard.Claim();

            TickRoomClick();
            TickWalk();
            TickUsableGlow();

            // Fight/Flee is a question, so it carries its cursor from the moment it appears - the
            // command menu already does, and Enter should not need an arrow press to wake it up.
            // Armed here rather than where the bar is shown because on that frame the bar's resolved
            // style is still stale and there would be nothing to select.
            if (IsShown(_combatBar))
            {
                _combatNav?.SelectFirst();
            }

            // The same for the detail dialog: Ok (first in the document) carries the cursor, so
            // Confirm/Cancel reads as a choice the arrows can change.
            if (IsShown(_detailWindow))
            {
                _detailNav?.SelectFirst();
            }

            // The Hints window's only button is Back, so Enter must find it without an arrow press.
            if (HintsOpen)
            {
                _hintsNav?.SelectFirst();
            }

            var text = NavHintText();
            SetShown(_navHint, text != null);
            if (text != null && _navHint != null && _navHint.text != text)
            {
                _navHint.text = text;
            }
        }

        /// <summary>
        /// What the keys do right now, or null when there is nothing worth saying. The dialogs are
        /// deliberately silent: Enter dismisses them and a line under a modal is noise.
        /// </summary>
        private string NavHintText()
        {
            if (IsDialogUp())
            {
                return null;
            }

            if (IsShown(_combatBar))
            {
                // A boss room refuses Flee, so its hint must not offer it.
                return _canFlee ? Loc.Get("room.keys.combat-bar") : Loc.Get("room.keys.combat-bar-no-flee");
            }

            if (DoorNavActive())
            {
                // Name what Enter would use right now, or leave Enter out (playtest 4 finding 36).
                string use = UseLabel(UseAtHand());
                return use != null
                    ? Loc.Format("room.keys.doors-use", ("use", use))
                    : Loc.Get("room.keys.doors");
            }

            return null;
        }

        /// <summary>
        /// Whether this panel is driving the arrow keys itself, and so must swallow UI Toolkit's own
        /// focus navigation - one arrow through that would move focus off the root and the next key
        /// would never arrive.
        /// </summary>
        private bool OwnsNavigationKeys()
        {
            return IsShown(_pauseWindow) || IsShown(_mapWindow) || PotionsOpen || HintsOpen || IsShown(_heroBar)
                || IsShown(_combatBar) || IsDialogUp() || DoorNavActive();
        }

        /// <summary>
        /// Whether the arrow keys currently walk the party. Doors being subscribed is the same
        /// condition as being able to walk through one, but a dialog on top of the room owns the
        /// keyboard while it is up - an arrow key must not move the party out from under an open
        /// event window.
        /// </summary>
        private bool DoorNavActive()
        {
            return _doorsLive
                && !IsShown(_combatBar) && !IsShown(_heroBar) && !IsShown(_pauseWindow)
                && !IsShown(_mapWindow) && !PotionsOpen && !HintsOpen && !IsShown(_detailWindow) && !IsShown(_eventWindow)
                && !IsShown(_victoryWindow) && !IsShown(_levelWindow);
        }

        /// <summary>
        /// The out-of-combat keyboard: the arrows (and WASD) walk the party - read by
        /// <see cref="TickWalk"/>, since a held key must keep walking - and Enter uses what it faces.
        /// The arrows and Tab are still swallowed here, or UI Toolkit would move focus off the panel
        /// with them.
        /// </summary>
        private bool HandleDungeonKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Z:
                case KeyCode.Space:
                    return UseFacing();
            }

            return false;
        }

        /// <summary>
        /// Uses what the party is standing against, the way pressing A at a chest does in FF: the only
        /// way into a room's contents, so a refuge still asks first and the stairs still warn. Nothing
        /// happens on a spent chest or in a guarded room. Standing on it counts too: an object that
        /// would shut something off is left walkable. Public so a scripted check can press what Enter
        /// presses.
        /// </summary>
        public bool UseFacing()
        {
            return Use(UseAtHand());
        }

        /// <summary>What Enter would use: the thing the party faces or stands on.</summary>
        private RoomUse UseAtHand()
        {
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            return party != null ? UseAt(party.Tile + party.Facing, party.Tile) : RoomUse.None;
        }

        /// <summary>
        /// The one rule for what is usable at a tile, shared by Enter, the walking hint, stepping onto
        /// the stairs or a refuge, and the glow on usable objects, so none of them can disagree. In this
        /// order: the way back out of a side room, the event's object, a cache or refuge, the stairs.
        /// </summary>
        private RoomUse UseAt(Vector2Int facing, Vector2Int standing)
        {
            if (_currentRoom == null || _currentRoom.Enemies.Any(e => e != null && e.IsAlive))
            {
                return RoomUse.None;
            }
            if (IsAtHand(_currentRoom.ReturnMarker, facing, standing))
            {
                return RoomUse.GoBack;
            }
            if (IsAtHand(_currentRoom.EventMarker, facing, standing))
            {
                if (PendingEvent() != null)
                {
                    return RoomUse.Event;
                }
                if (UsableSideRoom() != null)
                {
                    return RoomUse.SideRoom;
                }
            }
            if (IsAtHand(_currentRoom.KindMarker, facing, standing) && _currentRoom.HasPendingPayload)
            {
                return _currentRoom.Kind == RoomKind.Treasure ? RoomUse.Search : RoomUse.Rest;
            }
            if (_currentRoom.IsExit && IsAtHand(_currentRoom.ExitMarker, facing, standing))
            {
                return RoomUse.Stairs;
            }
            return RoomUse.None;
        }

        private bool Use(RoomUse use)
        {
            switch (use)
            {
                case RoomUse.GoBack:
                    LeaveSideRoom();
                    return true;
                case RoomUse.Event:
                case RoomUse.SideRoom:
                    return TryUseEvent();
                case RoomUse.Search:
                    OnSearch();
                    return true;
                case RoomUse.Rest:
                    OnRest();
                    return true;
                case RoomUse.Stairs:
                    OnDescend();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The words after "Enter:" in the walking hint, or null when nothing is at hand.</summary>
        private string UseLabel(RoomUse use)
        {
            switch (use)
            {
                case RoomUse.GoBack:
                    return Loc.Get("room.use.go-back");
                case RoomUse.Event:
                    var roomEvent = PendingEvent();
                    return roomEvent != null && !string.IsNullOrEmpty(roomEvent.Title)
                        ? Loc.Format("room.use.event", ("event", roomEvent.Title))
                        : Loc.Get("room.use.look");
                case RoomUse.SideRoom:
                    return Loc.Get("room.use.go-in");
                case RoomUse.Search:
                    return Loc.Get("room.use.search");
                case RoomUse.Rest:
                    return Loc.Get("room.use.rest");
                case RoomUse.Stairs:
                    return Loc.Get("room.use.stairs");
                default:
                    return null;
            }
        }

        /// <summary>
        /// Stepping onto (or into) the stairs or a refuge opens its prompt at once (playtest 4 findings
        /// 37 and 64). Only for a step the keys took, and only once per step: a declined prompt does
        /// not come back while the party stands there or keeps pushing the key - Enter still opens it.
        /// A click already uses what it walks to, so a click's walk is left alone.
        /// </summary>
        private void OnKeyStepEnded(Vector2Int tile)
        {
            _bumpedTile = null;
            PromptOnStep(tile);
        }

        private void OnKeyBumped(Vector2Int from, Vector2Int tile)
        {
            if (_bumpedTile == tile && _bumpedFrom == from)
            {
                return;
            }
            _bumpedTile = tile;
            _bumpedFrom = from;
            PromptOnStep(tile);
        }

        private void PromptOnStep(Vector2Int tile)
        {
            if (!DoorNavActive() || _walkUseTile != null)
            {
                return;
            }
            var use = UseAt(tile, tile);
            if (use == RoomUse.Stairs || use == RoomUse.Rest)
            {
                Use(use);
            }
        }

        private static bool IsAtHand(Component marker, Vector2Int facing, Vector2Int standing)
        {
            if (marker == null)
            {
                return false;
            }
            var tile = Vector2Int.RoundToInt(marker.transform.position);
            return tile == facing || tile == standing;
        }

        /// <summary>
        /// Walks the party while a direction is held and the doors are live (<see cref="DoorNavActive"/>:
        /// nothing over the room, no fight waiting). A step through a door is <see cref="OnDoorSelected"/>.
        /// </summary>
        private void TickWalk()
        {
            if (_walk == null)
            {
                CreateWalk();
            }
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            _walk.Tick(party, _currentRoom, DoorNavActive(), Time.deltaTime);
            ContinueWalkGoal();
            FinishWalkUse();
        }

        /// <summary>Takes one step, as a held key would. For scripts and tests: no keyboard involved.</summary>
        public bool WalkStep(Vector2Int direction)
        {
            if (_walk == null)
            {
                CreateWalk();
            }
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            return DoorNavActive() && _walk.TryStep(party, _currentRoom, direction);
        }

        // ============================================================
        //  WHAT CAN BE USED STANDS OUT
        // ============================================================

        // A soft halo behind each object in the room that Enter would use now, so it reads apart
        // from the biome's decoration (playtest 4 finding 10, the Warren Tunnels). Keyed by the
        // object's renderer; the halo is a child copy of its sprite, so the object itself is never
        // tinted and its spent sprite needs no undo.
        private readonly Dictionary<SpriteRenderer, SpriteRenderer> _glows = new Dictionary<SpriteRenderer, SpriteRenderer>();
        private readonly List<SpriteRenderer> _glowMarkers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _staleGlows = new List<SpriteRenderer>();

        private const float GlowScale = 1.22f;
        private const float GlowPulseSpeed = 2.6f;
        private const float GlowAlphaLow = 0.18f;
        private const float GlowAlphaHigh = 0.5f;
        private static readonly Color GlowColor = new Color(1f, 0.88f, 0.45f);

        /// <summary>
        /// Lights the usable, unused objects of the party's room and puts out every other halo. Usable
        /// is <see cref="UseAt"/> on the object's own tile, the rule Enter follows, so a guarded room,
        /// a spent chest or a resolved event shows nothing.
        /// </summary>
        private void TickUsableGlow()
        {
            _glowMarkers.Clear();
            if (_currentRoom != null && !(CombatStage.HasInstance && CombatStage.Instance.IsRaised))
            {
                AddGlowMarker(_currentRoom.ReturnMarker);
                AddGlowMarker(_currentRoom.EventMarker);
                AddGlowMarker(_currentRoom.KindMarker);
                AddGlowMarker(_currentRoom.ExitMarker != null ? _currentRoom.ExitMarker.GetComponent<SpriteRenderer>() : null);
            }

            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * GlowPulseSpeed);
            foreach (var marker in _glowMarkers)
            {
                var tile = Vector2Int.RoundToInt(marker.transform.position);
                bool usable = marker.enabled && UseAt(tile, tile) != RoomUse.None;
                var glow = usable ? GlowFor(marker) : (_glows.TryGetValue(marker, out var existing) ? existing : null);
                if (glow == null)
                {
                    continue;
                }
                if (glow.gameObject.activeSelf != usable)
                {
                    glow.gameObject.SetActive(usable);
                }
                if (usable)
                {
                    glow.sprite = marker.sprite;
                    glow.flipX = marker.flipX;
                    glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, Mathf.Lerp(GlowAlphaLow, GlowAlphaHigh, t));
                }
            }

            // Halos of other rooms (or of objects gone) go out.
            _staleGlows.Clear();
            foreach (var pair in _glows)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    _staleGlows.Add(pair.Key);
                    continue;
                }
                if (!_glowMarkers.Contains(pair.Key) && pair.Value.gameObject.activeSelf)
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
            foreach (var key in _staleGlows)
            {
                _glows.Remove(key);
            }
        }

        private void AddGlowMarker(SpriteRenderer marker)
        {
            if (marker != null && marker.sprite != null)
            {
                _glowMarkers.Add(marker);
            }
        }

        private SpriteRenderer GlowFor(SpriteRenderer marker)
        {
            if (_glows.TryGetValue(marker, out var glow) && glow != null)
            {
                return glow;
            }
            var obj = new GameObject("UsableGlow");
            obj.transform.SetParent(marker.transform, false);
            obj.transform.localScale = Vector3.one * GlowScale;
            // Behind the object, so only the rim of the larger copy shows: an outline, not a wash.
            obj.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            glow = obj.AddComponent<SpriteRenderer>();
            glow.sortingLayerID = marker.sortingLayerID;
            glow.sortingOrder = marker.sortingOrder - 1;
            obj.SetActive(false);
            _glows[marker] = glow;
            return glow;
        }

        private void CreateWalk()
        {
            _walk = new PartyWalk(OnDoorSelected);
            _walk.KeyStepEnded += OnKeyStepEnded;
            _walk.KeyBumped += OnKeyBumped;
        }

        private static bool IsShown(VisualElement element)
        {
            return element != null && element.resolvedStyle.display == DisplayStyle.Flex;
        }

        /// <summary>
        /// Gives the panel root keyboard focus so the combat hotkeys receive key events (UI
        /// Toolkit routes KeyDownEvents to the focused element). Called whenever a combat bar
        /// appears; harmless if already focused.
        /// </summary>
        private void FocusRoot()
        {
            if (_root != null && _root.panel != null)
            {
                _root.Focus();
            }
            // UI Toolkit focus alone does not make the OS keyboard arrive - see PanelKeyboard.
            PanelKeyboard.Claim();
        }

        // ============================================================
        //  DOOR CLICK
        // ============================================================

        private void SubscribeDoors()
        {
            if (_currentRoom == null)
            {
                return;
            }

            _navDoors.Clear();
            foreach (var door in _currentRoom.Doors)
            {
                if (door == null)
                {
                    continue;
                }
                door.OnDoorClicked += OnDoorClickedWalk;
                _navDoors.Add(door);
            }
            // The doors being subscribed is exactly the state in which walking through one is legal,
            // so it is also the state in which the arrow keys point at them. A side room has no doors
            // at all, and walking to its way back is the only way out of it.
            _doorsLive = _navDoors.Count > 0 || _currentRoom.IsSideRoom;
        }

        private void UnsubscribeDoors()
        {
            _doorsLive = false;
            _navDoors.Clear();

            if (_currentRoom == null)
            {
                return;
            }
            foreach (var door in _currentRoom.Doors)
            {
                door.OnDoorClicked -= OnDoorClickedWalk;
            }
            if (_entryDoor != null)
            {
                _entryDoor.OnDoorClicked -= OnEntryDoorFlee;
            }
        }

        /// <summary>A click on a door: walk to it and through it, from anywhere in the room.</summary>
        private void OnDoorClickedWalk(Door door)
        {
            SetWalkGoal(door, null, default);
        }

        /// <summary>Goes through <paramref name="door"/>: what the last step of a walk up to it does.</summary>
        private void OnDoorSelected(Door door)
        {
            UnsubscribeDoors();

            var party = GameManager.Instance.Party;
            var fromRoom = _currentRoom;
            party.PlaceAtDoor(door, fromRoom);

            fromRoom.EnableAllDoors();

            var destRoom = door.GetOtherRoom(fromRoom);
            GameManager.Instance.EnterRoom(destRoom, door);
        }

        /// <summary>
        /// What a room does by itself each time the party is back in it with nothing over it. Run it
        /// whenever a window closes: what is due changes *after* <see cref="Show"/> ran - clearing the
        /// room's guards is exactly what frees a captive.
        /// </summary>
        private void SettleRoom()
        {
            // A captive in a clear room is freed on the spot; its notice dismisses back through
            // here, by which time there is nobody left to free.
            if (!TryAutoRescue())
            {
                TryGrantEventLoot();
            }
        }
    }
}
