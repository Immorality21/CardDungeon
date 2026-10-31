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
    /// <summary>
    /// Room + combat action UI, built on UI Toolkit. Presents non-combat actions
    /// (Examine/Action), the combat start bar (Fight/Flee), the hero-turn command
    /// window (Attack/Magic/Item/Inspect/Skip), and centered dialogs (options, results, death).
    /// Driven by GameManager (Show) and CombatManager events.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class RoomActionUI : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;

        private VisualElement _root;
        private VisualElement _combatBar;
        private VisualElement _heroBar;
        private VisualElement _detailWindow;
        private VisualElement _eventWindow;
        private VisualElement _partyStatus;
        private VisualElement _partyStatusRows;
        private VisualElement _pauseWindow;
        private Button _pauseResume;
        private Button _pauseQuit;
        private Label _pauseNote;
        private Button _pauseMap;
        private MainMenu.AudioOptionsUI _pauseAudio;
        private KeyboardNavigator _pauseNav;
        private bool _quitArmed;

        private VisualElement _mapWindow;
        private Label _mapTitle;
        private Label _mapStatus;
        private Button _mapBack;
        private Label _mapHint;
        private UI.DungeonMapView _mapView;
        private UI.UnitNameplates _nameplates;
        private VisualElement _nameplateLayer;
        private Cards.UI.MagicSelectionUI _magicSelection;
        /// <summary>Whether the map was opened from the pause overlay, so Back goes back there.</summary>
        private bool _mapFromPause;

        private readonly Dictionary<Heroes.Hero, VisualElement> _partyRows = new Dictionary<Heroes.Hero, VisualElement>();
        private readonly Dictionary<Heroes.Hero, Label> _partyHpLabels = new Dictionary<Heroes.Hero, Label>();
        private readonly Dictionary<Heroes.Hero, VisualElement> _partyHpFills = new Dictionary<Heroes.Hero, VisualElement>();
        private readonly Dictionary<Heroes.Hero, VisualElement> _partyAfflictions = new Dictionary<Heroes.Hero, VisualElement>();
        private readonly Dictionary<Heroes.Hero, VisualElement> _partyGaugeFills = new Dictionary<Heroes.Hero, VisualElement>();
        private Label _fightFoes;
        private VisualElement _dialogScrim;
        private VisualElement _deathBackdrop;
        private Label _detailChip;
        private VisualElement _detailRows;

        private Label _heroTitle;
        private Label _detailTitle;
        private Label _detailMessage;

        private Label _eventTitle;
        private Label _eventPrompt;
        private Label _eventOdds;
        private ScrollView _eventOptions;
        private Button _eventBack;
        /// <summary>The open event's own walk-away option, if it has one - what Escape presses.</summary>
        private Button _eventDecline;

        private Button _fightBtn;
        private Button _fleeBtn;
        private Button _detailOk;
        private Button _detailCancel;

        private VisualElement _bossBanner;
        private Label _bossBannerName;
        private VisualElement _summonBanner;
        private Label _summonBannerName;
        private VisualElement _abilityBanner;
        private Label _abilityBannerName;
        // Bumped per banner, so the timer of an older one cannot hide a newer one.
        private int _abilityBannerShown;

        private VisualElement _commandList;
        private VisualElement _turnOrder;
        private VisualElement _turnOrderList;

        private Label _navHint;

        private VisualElement _victoryWindow;
        private Label _victoryTitle;
        private VisualElement _victoryRewards;
        private Button _victoryContinue;

        private VisualElement _hud;
        private Label _hudLevel;
        private Label _hudRun;
        private Label _hudGold;
        private Label _hudExplore;
        private int _hudGoldShown = -1;

        private VisualElement _levelWindow;
        private Label _levelTitle;
        private Label _levelSubtitle;
        private ScrollView _levelBody;
        private VisualElement _levelJoined;
        private VisualElement _levelTallies;
        private VisualElement _levelXp;
        private Label _levelXpHeading;
        private VisualElement _levelLoot;
        private Label _levelNext;
        private Button _levelContinue;
        private Action _levelContinueAction;
        // Straight to the run's next level. Null when the summary offers only the town.
        private Button _levelNextLevel;
        private Action _levelNextLevelAction;
        // Which of the two the keyboard is on: true = Continue.
        private bool _levelChoiceIsNext;

        private bool _refsReady;
        private Action _detailOkAction;
        private Action _detailCancelAction;

        private ICombatUnit _currentHeroTurn;
        private Room _currentRoom;
        private Door _entryDoor;

        // Room-event state, live only while the event window is up.
        private readonly Events.RoomEventRunner _eventRunner = new Events.RoomEventRunner();
        private Events.RoomEventSO _currentEvent;

        // Where the party stood when it left for a side room, and the choice a mandatory one waits on:
        // that event only resolves when its fight is won.
        private Vector2Int _sideReturnTile;
        private int _arenaOption = -1;
        private int _arenaOutcome = -1;
        private bool _arenaSucceeded;
        private Heroes.Hero _eventActingHero;
        private float _eventChance;

        // Cursor-driven command menu (FFX-style selection list).
        // SummonAbility and Dismiss belong to a party-replacing summon's own menu (§4b), which is the
        // summon's, not a hero's - the hero menu still does not grow past Summon.
        private enum HeroCommand { Attack, Magic, Summon, Item, Inspect, Skip, SummonAbility, Dismiss, Ultra }

        private struct CommandEntry
        {
            public HeroCommand Command;
            public string Label;
            public bool Enabled;

            /// <summary>Why a disabled row is disabled, shown dim on its right ("No charges",
            /// "Silenced"). Empty on an enabled row.</summary>
            public string Reason;

            /// <summary>SummonAbility only: the ability this row uses.</summary>
            public Cards.MagicSO Ability;
        }

        private readonly List<CommandEntry> _commands = new List<CommandEntry>();
        private readonly List<VisualElement> _commandRows = new List<VisualElement>();
        private readonly List<Label> _commandCursors = new List<Label>();
        private int _selectedCommand;

        // Walking the dungeon from the keyboard: arrows/WASD walk the party, Enter uses what it faces.
        private readonly List<Door> _navDoors = new List<Door>();
        private bool _doorsLive;
        private PartyWalk _walk;
        private Door _walkGoalDoor;
        private Room _walkGoalRoom;
        private Vector2Int _walkGoalTile;
        private Vector2Int? _walkUseTile;
        private KeyboardNavigator _combatNav;
        private KeyboardNavigator _eventNav;
        private KeyboardNavigator _detailNav;

        private bool EnsureRefs()
        {
            if (_refsReady)
            {
                return true;
            }

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                return false;
            }

            // UXML texts are keys (#ui...); turn them into words once, as the tree is first bound.

            global::ImmoralityGaming.Localization.Loc.Localize(root);

            global::ImmoralityGaming.Menu.HoverTips.Attach(root);
            _root = root;

            _combatBar = root.Q<VisualElement>("combat-bar");
            _fightFoes = root.Q<Label>("fight-foes");
            _dialogScrim = root.Q<VisualElement>("dialog-scrim");
            _deathBackdrop = root.Q<VisualElement>("death-bg");
            _detailChip = root.Q<Label>("detail-chip");
            _detailRows = root.Q<VisualElement>("detail-rows");
            _heroBar = root.Q<VisualElement>("hero-bar");
            _detailWindow = root.Q<VisualElement>("detail-window");
            _eventWindow = root.Q<VisualElement>("event-window");
            _partyStatus = root.Q<VisualElement>("party-status");
            _partyStatusRows = root.Q<VisualElement>("party-status-rows");
            _pauseWindow = root.Q<VisualElement>("pause-window");
            _pauseResume = root.Q<Button>("pause-resume");
            _pauseQuit = root.Q<Button>("pause-quit");
            _pauseNote = root.Q<Label>("pause-note");
            _pauseMap = root.Q<Button>("pause-map");
            _mapWindow = root.Q<VisualElement>("map-window");
            _mapTitle = root.Q<Label>("map-title");
            _mapStatus = root.Q<Label>("map-status");
            _mapBack = root.Q<Button>("map-back");
            _mapHint = root.Q<Label>("map-hint");
            _commandList = root.Q<VisualElement>("command-list");
            _turnOrder = root.Q<VisualElement>("turn-order");
            _turnOrderList = root.Q<VisualElement>("turn-order-list");
            _victoryWindow = root.Q<VisualElement>("victory-window");
            _victoryTitle = root.Q<Label>("victory-title");
            _victoryRewards = root.Q<VisualElement>("victory-rewards");
            _victoryContinue = root.Q<Button>("victory-continue");
            _hud = root.Q<VisualElement>("dungeon-hud");
            _hudLevel = root.Q<Label>("hud-level");
            _hudRun = root.Q<Label>("hud-run");
            _hudGold = root.Q<Label>("hud-gold");
            _hudExplore = root.Q<Label>("hud-explore");
            _levelWindow = root.Q<VisualElement>("level-clear-window");
            _levelTitle = root.Q<Label>("level-clear-title");
            _levelSubtitle = root.Q<Label>("level-clear-subtitle");
            _levelBody = root.Q<ScrollView>("level-clear-body");
            _levelJoined = root.Q<VisualElement>("level-clear-joined");
            _levelTallies = root.Q<VisualElement>("level-clear-tallies");
            _levelXp = root.Q<VisualElement>("level-clear-xp");
            _levelXpHeading = root.Q<Label>("level-clear-xp-heading");
            _levelLoot = root.Q<VisualElement>("level-clear-loot");
            _levelNext = root.Q<Label>("level-clear-next");
            _levelContinue = root.Q<Button>("level-clear-continue");
            _levelNextLevel = root.Q<Button>("level-clear-next-level");
            _navHint = root.Q<Label>("nav-hint");

            _heroTitle = root.Q<Label>("hero-title");
            _detailTitle = root.Q<Label>("detail-title");
            _detailMessage = root.Q<Label>("detail-message");

            _eventTitle = root.Q<Label>("event-title");
            _eventPrompt = root.Q<Label>("event-prompt");
            _eventOdds = root.Q<Label>("event-odds");
            _eventOptions = root.Q<ScrollView>("event-options");
            _eventBack = root.Q<Button>("event-back");

            _fightBtn = root.Q<Button>("fight-btn");
            _fleeBtn = root.Q<Button>("flee-btn");
            _detailOk = root.Q<Button>("detail-ok");
            _detailCancel = root.Q<Button>("detail-cancel");

            _bossBanner = root.Q<VisualElement>("boss-banner");
            _bossBannerName = root.Q<Label>("boss-banner-name");
            _summonBanner = root.Q<VisualElement>("summon-banner");
            _summonBannerName = root.Q<Label>("summon-banner-name");
            _abilityBanner = root.Q<VisualElement>("ability-banner");
            _abilityBannerName = root.Q<Label>("ability-banner-name");

            _fightBtn.clicked += OnFight;
            _fleeBtn.clicked += OnFlee;
            if (_eventBack != null)
            {
                _eventBack.clicked += OnEventBack;
            }
            _detailOk.clicked += () => _detailOkAction?.Invoke();
            if (_detailCancel != null)
            {
                _detailCancel.clicked += () => _detailCancelAction?.Invoke();
            }
            _victoryContinue.clicked += OnVictoryContinue;
            if (_levelContinue != null)
            {
                _levelContinue.clicked += OnLevelClearContinue;
            }
            if (_levelNextLevel != null)
            {
                _levelNextLevel.clicked += OnLevelClearNextLevel;
            }

            // Strip focusability from every focusable descendant so UI Toolkit's arrow-key
            // navigation has nowhere to move focus — keyboard focus stays on the root and our
            // cursor nav keeps receiving keys. (Buttons stay clickable + hotkey-driven.)
            foreach (var focusable in new Focusable[] { _fightBtn, _fleeBtn, _detailOk, _detailCancel, _victoryContinue, _levelContinue, _levelNextLevel, _levelBody, _victoryRewards, _eventBack, _eventOptions })
            {
                if (focusable != null)
                {
                    focusable.focusable = false;
                }
            }

            // Keyboard hotkeys for the combat bars: the start bar and the hero command cursor.
            // Registered on the root so the panel routes key presses here; each hotkey runs the
            // same handler as its button, and only fires when the matching bar is visible.
            root.RegisterCallback<KeyDownEvent>(OnCombatHotkey);
            root.focusable = true;

            // Fight/Flee is a bar like any other. It kept its F/R letters, but a player who has just
            // learnt that arrows-and-Enter drive the room, the command menu and the whole hub should
            // not have to discover that this one bar is different.
            _combatNav = new KeyboardNavigator(_combatBar);

            // The event window builds its options at runtime, so its cursor navigates whatever is on
            // it rather than a fixed list. Escape leaves by its own Back button, so the keyboard route
            // out runs the same teardown a click does.
            // The pause overlay reuses the hub's audio dials wholesale: AudioOptionsUI queries its
            // root by element name, and the UXML gives it the same names, so there is one
            // implementation of "what does the Master dial do" rather than two that can disagree.
            if (_pauseWindow != null)
            {
                _pauseAudio = new MainMenu.AudioOptionsUI(_pauseWindow);
                _pauseNav = new KeyboardNavigator(_pauseWindow);
                _pauseNav.Cancelled += ClosePause;
            }
            if (_pauseResume != null)
            {
                _pauseResume.clicked += ClosePause;
            }
            if (_pauseQuit != null)
            {
                _pauseQuit.clicked += OnQuitToTitle;
            }
            if (_pauseMap != null)
            {
                _pauseMap.clicked += () => OpenMap(fromPause: true);
            }

            // The map canvas is authored in the UXML; the painter is added into it the same way the
            // hub hosts the sphere grid. Nav is a single Back button, so the window needs no cursor -
            // Escape, Backspace and M all leave through CloseMap.
            // Enemy names + HP numbers on the battle stage. The host is authored first in the root,
            // so every window draws over the plates.
            var nameplateHost = root.Q<VisualElement>("nameplate-layer");
            _nameplateLayer = nameplateHost;
            if (nameplateHost != null)
            {
                _nameplates = new UI.UnitNameplates();
                nameplateHost.Add(_nameplates);
            }

            var mapHost = root.Q<VisualElement>("map-canvas");
            if (mapHost != null)
            {
                _mapView = new UI.DungeonMapView();
                _mapView.RoomChosen += TravelTo;
                mapHost.Add(_mapView);
            }
            if (_mapBack != null)
            {
                _mapBack.clicked += CloseMap;
            }

            _eventNav = new KeyboardNavigator(_eventWindow);
            _eventNav.Cancelled += () =>
            {
                if (_eventDecline != null)
                {
                    KeyboardNavigator.Press(_eventDecline);
                }
                else if (IsShown(_eventBack))
                {
                    KeyboardNavigator.Press(_eventBack);
                }
            };

            // The detail dialog's buttons carry the cursor from the moment it opens, like Fight/Flee:
            // a question or a statement wants Enter to answer it, and the player should see which
            // button that is. Escape is handled in HandleDialogKey, which knows whether Cancel exists.
            _detailNav = new KeyboardNavigator(_detailWindow);

            // While the command menu is up, swallow UI Toolkit's built-in navigation so it can't
            // blur/steal keyboard focus off the root after the first arrow (our cursor nav owns it).
            root.RegisterCallback<NavigationMoveEvent>(evt => { if (OwnsNavigationKeys()) { evt.StopPropagation(); } });
            root.RegisterCallback<NavigationSubmitEvent>(evt => { if (OwnsNavigationKeys()) { evt.StopPropagation(); } });
            root.RegisterCallback<NavigationCancelEvent>(evt => { if (OwnsNavigationKeys()) { evt.StopPropagation(); } });

            HideAll();
            _refsReady = true;
            return true;
        }

        public void Show(Room room, Door entryDoor = null)
        {
            if (!EnsureRefs())
            {
                return;
            }

            UnsubscribeDoors();

            _currentRoom = room;
            _entryDoor = entryDoor;
            SetShown(_detailWindow, false);
            SetShown(_eventWindow, false);

            // Health is a *level*-scoped resource - it only refills on a fresh floor or in a refuge -
            // so the decisions the player makes while walking (take this fight? spend the refuge?
            // drink now?) are exactly the ones that need it, and until 2026-09-06 every one of them
            // was made blind. Rebuilt per room rather than once, because a rescued hero joins
            // mid-level.
            ShowPartyStatusOutOfCombat();
            RefreshHud();

            bool hasEnemy = room.Enemies.Any(e => e != null && e.IsAlive);
            SetShown(_combatBar, hasEnemy);
            if (_fightFoes != null)
            {
                _fightFoes.text = hasEnemy ? DescribeFoes(room) : string.Empty;
            }
            _combatNav?.Reset();

            // A captive is only reachable once the room is clear - guards first.
            if (!hasEnemy)
            {
                SettleRoom();
            }

            // Boss rooms: announce the boss and remove Flee — the climax can't be skipped.
            var boss = room.Enemies.FirstOrDefault(e => e != null && e.IsAlive && e.IsBoss);
            bool isBossRoom = hasEnemy && boss != null;
            // A side room has no door to flee through.
            SetShown(_fleeBtn, hasEnemy && !isBossRoom && !room.IsSideRoom);
            SetShown(_bossBanner, isBossRoom);
            if (isBossRoom && _bossBannerName != null)
            {
                _bossBannerName.text = boss.DisplayName;
            }

            if (hasEnemy)
            {
                if (isBossRoom)
                {
                    // Seal the room — no door flee — so the boss must be fought.
                    room.DisableAllDoors();
                }
                else
                {
                    room.SetDoorsEnabled(entryDoor);
                    if (_entryDoor != null)
                    {
                        _entryDoor.OnDoorClicked += OnEntryDoorFlee;
                    }
                }
                FocusRoot();
            }
            else
            {
                room.EnableAllDoors();
                SubscribeDoors();
                FocusRoot();
            }
        }

        public void Hide()
        {
            if (EnsureRefs())
            {
                HideAll();
            }
            UnsubscribeDoors();
        }

        private void HideAll()
        {
            SetShown(_combatBar, false);
            SetShown(_bossBanner, false);
            SetShown(_heroBar, false);
            SetShown(_detailWindow, false);
            SetShown(_eventWindow, false);
            SetShown(_partyStatus, false);
            SetShown(_turnOrder, false);
            SetShown(_victoryWindow, false);
            SetShown(_levelWindow, false);
            // Cleared first: CloseMap would otherwise put the pause overlay back up on its way out.
            _mapFromPause = false;
            CloseMap();
            ClosePause();
        }

        /// <summary>
        /// Puts the party window up outside combat, rebuilt from the live party. Safe to call when
        /// there is no party yet - the room is shown before combat exists, and a window with no rows
        /// is worse than no window.
        /// </summary>
        private void ShowPartyStatusOutOfCombat()
        {
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party == null || party.Heroes == null || party.Heroes.Count == 0)
            {
                SetShown(_partyStatus, false);
                return;
            }

            BuildPartyStatus(party);
            HighlightActiveHero(null);
            SetShown(_partyStatus, true);
        }

        // ============================================================
        //  EXAMINE / ACTION FLOWS
        // ============================================================

        /// <summary>
        /// Opens the room's event, from walking up to its object, once the room is clear. There is no
        /// button for it: the object in the room is the way in.
        /// </summary>
        private bool TryUseEvent()
        {
            if (_currentRoom.Enemies.Any(e => e != null && e.IsAlive))
            {
                return false;
            }

            var roomEvent = PendingEvent();
            if (roomEvent == null)
            {
                // What the event opened: its object is the way in now.
                var spec = DungeonManager.Instance.ResolvedOutcomeOf(_currentRoom)?.SideRoom;
                if (spec == null || !spec.IsSet || spec.Mandatory)
                {
                    return false;
                }
                EnterSideRoom(_currentRoom, spec);
                return true;
            }

            ShowRoomEvent(roomEvent);
            return true;
        }

        /// <summary>Takes the party to <paramref name="host"/>'s side room, remembering where it stood.</summary>
        private void EnterSideRoom(Room host, Events.SideRoomSpec spec, List<ItemSO> pendingLoot = null)
        {
            var party = GameManager.Instance.Party;
            var side = DungeonManager.Instance.SideRoomFor(host, spec, pendingLoot);
            _sideReturnTile = party.Tile;
            party.PlaceOnTile(side, side.SideEntryTile);
            side.Reveal();
            GameManager.Instance.SnapCamera();
            Show(side);
        }

        private void LeaveSideRoom()
        {
            var host = _currentRoom.HostRoom;
            var party = GameManager.Instance.Party;
            party.PlaceOnTile(host, _sideReturnTile);
            GameManager.Instance.SnapCamera();
            Show(host);
        }

        /// <summary>
        /// Pays the item an event held back until its fight was won, as one statement once the room is
        /// clear. Winning a mandatory side room is what resolves its event, and takes the party back.
        /// </summary>
        private bool TryGrantEventLoot()
        {
            if (_currentRoom == null || _currentRoom.PendingEventLoot == null
                || _currentRoom.Enemies.Any(e => e != null && e.IsAlive))
            {
                return false;
            }

            var item = Events.RoomEventRunner.GrantOneOf(_currentRoom.PendingEventLoot);
            _currentRoom.PendingEventLoot = null;
            var owner = _currentRoom.IsSideRoom ? _currentRoom.HostRoom : _currentRoom;
            owner.EventLootClaimed = true;
            bool arena = _currentRoom.IsSideRoom && _currentRoom.SideSpec.Mandatory;
            if (arena && _arenaOption >= 0)
            {
                owner.MarkEventResolved(_arenaOption, _arenaOutcome, _arenaSucceeded);
                _arenaOption = -1;
            }
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.Save(_currentRoom);
            }

            ShowDetail(owner.RoomEvent != null ? owner.RoomEvent.Title : string.Empty,
                item != null ? Loc.Format("event-text.report.found", ("item", item.DisplayName)) : string.Empty);
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                if (arena)
                {
                    LeaveSideRoom();
                }
                else
                {
                    SettleRoom();
                }
            };
            return true;
        }


        /// <summary>
        /// Who is in the room, as the Fight / Flee bar's heading (<see cref="FoeLine"/>): met enemies
        /// by name, grouped by kind, and the unmet folded into one count - no stats, so the decision
        /// is informed without the bar becoming the Inspect page.
        /// </summary>
        private static string DescribeFoes(Room room)
        {
            var foes = new List<FoeEntry>();
            // The boss leads the line and is always named: its banner already says who it is, so
            // "Floating Eye + 1 unknown" under "Abyssal Warden" hid nothing and read as a mistake
            // (playtest 2 finding 3).
            foreach (var enemy in room.Enemies.Where(e => e != null).OrderBy(e => e.IsBoss ? 0 : 1))
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }
                bool seen = enemy.IsBoss || (enemy.Definition != null && MetaProgressManager.HasInstance
                    && MetaProgressManager.Instance.IsEnemySeen(enemy.Definition.SaveKey));
                string kind = enemy.Definition != null ? enemy.Definition.SaveKey : enemy.DisplayName;
                foes.Add(new FoeEntry(kind, enemy.DisplayName, seen));
            }
            return FoeLine.Describe(foes);
        }

        // ============================================================
        //  ROOM EVENTS
        // ============================================================

        /// <summary>The room's unresolved event, or null when there is nothing left to do here.</summary>
        private Events.RoomEventSO PendingEvent()
        {
            return _currentRoom != null && _currentRoom.HasPendingEvent ? _currentRoom.RoomEvent : null;
        }

        /// <summary>
        /// Opens the event: the fiction, how well the party can read the risk, and the choices. The
        /// check resolves against the party's <i>best</i> hero at the governing stat, and that hero
        /// is named - the point of a specialist is being able to see them earn their slot.
        /// </summary>
        private void ShowRoomEvent(Events.RoomEventSO roomEvent)
        {
            var party = GameManager.Instance.Party;
            var units = new List<ICombatUnit>();
            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null)
                    {
                        units.Add(hero);
                    }
                }
            }

            _currentEvent = roomEvent;
            _eventActingHero = Events.RoomEventResolver.BestFor(units, roomEvent.GoverningStat) as Heroes.Hero;

            int statValue = _eventActingHero != null
                ? _eventActingHero.GetEffectiveStat(roomEvent.GoverningStat)
                : 0;
            _eventChance = Events.RoomEventResolver.SuccessChance(statValue, roomEvent.Difficulty);

            var band = Events.RoomEventResolver.BandFor(_eventChance);
            var clarity = Events.RoomEventResolver.ClarityFor(statValue, roomEvent.Difficulty);

            _eventTitle.text = roomEvent.Title;
            _eventPrompt.text = roomEvent.Prompt;
            // Only events that actually gamble get an odds line, and it says "anything you chance"
            // rather than "this": an event can mix a sure thing with a gamble (take the loose coin,
            // or open the chest), and a bare "this looks dangerous" over the whole window would be
            // claiming the safe option is risky.
            bool hasCheck = false;
            foreach (var option in roomEvent.Options)
            {
                if (option != null && option.Kind == Events.RoomEventOptionKind.StatCheck)
                {
                    hasCheck = true;
                    break;
                }
            }
            SetShown(_eventOdds, hasCheck);
            _eventOdds.text = hasCheck ? BuildOddsLine(roomEvent, band, clarity) : string.Empty;

            _eventOptions.Clear();
            _eventDecline = null;
            for (int i = 0; i < roomEvent.Options.Count; i++)
            {
                var option = roomEvent.Options[i];
                if (option == null || string.IsNullOrEmpty(option.Label))
                {
                    continue;
                }

                int captured = i;
                var btn = new Button(() => OnEventOptionChosen(captured));
                btn.AddToClassList("cd-list-button");
                btn.AddToClassList("cd-event-option");
                btn.focusable = false;
                // The label and its tag share a row, so a long choice wraps instead of running
                // under the tag (playtest 2 finding 5).
                var text = new Label(option.Label) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("cd-event-option__text");
                btn.Add(text);
                // Which choices are gambles, on the row itself rather than in prose above the list.
                var block = BlockOf(option);
                string tagText = OptionTag(option, roomEvent, band, clarity, out string tone);
                if (block != Events.OptionBlock.None)
                {
                    btn.SetEnabled(false);
                    tagText = BlockedTag(option, block);
                    tone = "bad";
                }
                var tag = new Label(tagText) { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("cd-event-tag");
                tag.AddToClassList("cd-event-tag--" + tone);
                btn.Add(tag);
                _eventOptions.Add(btn);
                if (option.Kind == Events.RoomEventOptionKind.Decline && _eventDecline == null)
                {
                    _eventDecline = btn;
                }
            }

            // An event's own Decline already walks away, in the event's words - a Back beside it is
            // the same exit twice. Back stays only for an event authored without one, so no event
            // window can trap the player.
            SetShown(_eventBack, _eventDecline == null);

            _eventNav?.Reset();
            SetShown(_eventWindow, true);
        }

        private static Events.OptionBlock BlockOf(Events.RoomEventOption option)
        {
            var inventory = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            return Events.RoomEventResolver.BlockOf(option,
                inventory != null && inventory.Holds(option.RequiredItem),
                inventory != null && inventory.HasAnyConsumable(),
                MetaProgressManager.HasInstance ? MetaProgressManager.Instance.PendingRunGold : 0);
        }

        /// <summary>What a greyed-out choice is waiting for, in its right-hand tag.</summary>
        private static string BlockedTag(Events.RoomEventOption option, Events.OptionBlock block)
        {
            switch (block)
            {
                case Events.OptionBlock.NeedsItem:
                    return Loc.Format("room.event.tag.needs-item", ("item", option.RequiredItem.DisplayName));
                case Events.OptionBlock.NeedsConsumable:
                    return Loc.Get("room.event.tag.needs-consumable");
                default:
                    return Loc.Format("room.event.tag.needs-gold", ("amount", option.GoldCost));
            }
        }

        /// <summary>
        /// A choice's right-hand tag: what a sure thing really holds ("safe", "price", "danger") or
        /// walking away, otherwise the stat it
        /// turns on and the odds as far as the party can read them ("LCK · even", "INT · risky",
        /// "WIS · ?"). <paramref name="tone"/> is the colour class suffix.
        /// </summary>
        private static string OptionTag(Events.RoomEventOption option, Events.RoomEventSO roomEvent,
            Events.OddsBand band, Events.OddsClarity clarity, out string tone)
        {
            if (option.Kind != Events.RoomEventOptionKind.StatCheck)
            {
                if (option.Kind == Events.RoomEventOptionKind.Decline)
                {
                    tone = "safe";
                    return Loc.Get("room.event.tag.leave");
                }
                switch (Events.RoomEventResolver.RiskOf(option))
                {
                    case Events.OptionRisk.Danger:
                        tone = "bad";
                        return Loc.Get("room.event.tag.danger");
                    case Events.OptionRisk.Price:
                        tone = "even";
                        return Loc.Get("room.event.tag.price");
                    default:
                        tone = "safe";
                        return Loc.Get("room.event.tag.safe");
                }
            }
            string stat = StatCatalog.ShortName(roomEvent.GoverningStat);
            if (clarity == Events.OddsClarity.Unknown)
            {
                tone = "unknown";
                return Loc.Format("room.event.tag.unknown", ("stat", stat));
            }
            bool good = band >= Events.OddsBand.VeryLikely;
            bool even = band == Events.OddsBand.EvenOdds;
            tone = good ? "good" : even ? "even" : "bad";
            if (clarity == Events.OddsClarity.Vague)
            {
                return Loc.Format(good ? "room.event.tag.vague.good" : even ? "room.event.tag.vague.even" : "room.event.tag.vague.bad", ("stat", stat));
            }
            switch (band)
            {
                case Events.OddsBand.AlmostCertain:
                    return Loc.Format("room.event.tag.certain", ("stat", stat));
                case Events.OddsBand.VeryLikely:
                    return Loc.Format("room.event.tag.likely", ("stat", stat));
                case Events.OddsBand.EvenOdds:
                    return Loc.Format("room.event.tag.even", ("stat", stat));
                case Events.OddsBand.SlightChance:
                    return Loc.Format("room.event.tag.slim", ("stat", stat));
                default:
                    return Loc.Format("room.event.tag.hopeless", ("stat", stat));
            }
        }

        private string BuildOddsLine(
            Events.RoomEventSO roomEvent,
            Events.OddsBand band,
            Events.OddsClarity clarity)
        {
            string reading = Events.RoomEventResolver.DescribeOdds(band, clarity);
            string statName = StatCatalog.DisplayName(roomEvent.GoverningStat);

            if (_eventActingHero == null)
            {
                return Loc.Format("room.event.odds", ("stat", statName), ("reading", reading));
            }

            return Loc.Format("room.event.odds-hero", ("stat", statName), ("hero", _eventActingHero.DisplayName), ("reading", reading));
        }

        /// <summary>
        /// Resolves the chosen option and applies whatever came of it. The resolution is written to
        /// the room <b>and</b> saved at once: without that the player re-rolls a bad outcome by
        /// walking out and back in, or by quitting to the menu and resuming.
        /// </summary>
        private void OnEventOptionChosen(int optionIndex)
        {
            if (_currentEvent == null || optionIndex < 0 || optionIndex >= _currentEvent.Options.Count)
            {
                return;
            }

            var option = _currentEvent.Options[optionIndex];
            if (BlockOf(option) != Events.OptionBlock.None)
            {
                return;
            }

            SetShown(_eventWindow, false);
            _eventDecline = null;

            if (option.Kind == Events.RoomEventOptionKind.Decline)
            {
                // Walking away leaves the event unconsumed - the choice is deferred, not spent.
                string declineText = string.IsNullOrEmpty(option.DeclineText)
                    ? Loc.Get("room.event.decline.default")
                    : option.DeclineText;
                // Say so, or the object still standing there reads as a bug rather than an offer
                // still open (revisit playtest finding 11).
                ShowDetail(_currentEvent.Title, Loc.Format("room.event.decline.message", ("text", declineText)));
                _detailOkAction = CloseEventResult;
                return;
            }

            bool succeeded = option.Kind == Events.RoomEventOptionKind.Guaranteed
                || Events.RoomEventResolver.Passes(_eventChance, UnityEngine.Random.Range(0f, 1f));

            var pool = succeeded ? option.Success : option.Failure;

            // The acting hero bends the pool - it is their hand in the chest. Party-best would mean
            // the Scout's charm helping while the Warrior forces a door.
            int outcomeIndex = Events.RoomEventResolver.PickOutcomeIndex(
                pool, UnityEngine.Random.Range(0f, 1f), _eventActingHero);
            var outcome = outcomeIndex >= 0 ? pool[outcomeIndex] : null;

            var paid = _eventRunner.PayFor(option);
            var report = _eventRunner.Apply(
                outcome,
                succeeded,
                _currentRoom,
                GameManager.Instance.Party,
                _eventActingHero,
                DungeonManager.HasInstance ? DungeonManager.Instance.Afflictions : null);
            report.Lines.InsertRange(0, paid);

            // The party window reports what the outcome did at once, not after the dialog is dismissed.
            RefreshPartyStatus();

            bool arena = outcome != null && outcome.SideRoom != null && outcome.SideRoom.IsSet && outcome.SideRoom.Mandatory;
            if (arena)
            {
                _arenaOption = optionIndex;
                _arenaOutcome = outcomeIndex;
                _arenaSucceeded = succeeded;
            }
            else
            {
                _currentRoom.MarkEventResolved(optionIndex, outcomeIndex, succeeded);
                if (DungeonSaveManager.HasInstance)
                {
                    DungeonSaveManager.Instance.Save(_currentRoom);
                }
            }

            // A room the outcome opened is walked into at once; afterwards the object is the way back in.
            bool opensRoom = outcome != null && outcome.SideRoom != null && outcome.SideRoom.IsSet;
            ShowEventResult(report, option.Kind == Events.RoomEventOptionKind.StatCheck ? (bool?)succeeded : null,
                opensRoom ? outcome : null);
        }

        private void ShowEventResult(Events.RoomEventOutcomeReport report, bool? checkPassed = null, Events.RoomEventOutcome arena = null)
        {
            string message = string.IsNullOrEmpty(report.Text) ? Loc.Get("room.event.result.nothing") : report.Text;
            if (report.Lines.Count > 0)
            {
                message += "\n\n" + string.Join("\n", report.Lines);
            }

            ShowDetail(_currentEvent.Title, message);
            // Whether the gamble came off, before the flavour text: a result read as fiction alone
            // left the player to infer it from the consequences.
            if (checkPassed.HasValue)
            {
                SetDetailChip(checkPassed.Value ? Loc.Get("room.event.result.success") : Loc.Get("room.event.result.failed"), checkPassed.Value);
            }

            // A woken room is a different room: re-show it so the Fight/Flee bar replaces the
            // Examine/Action one, rather than leaving the player among live enemies with
            // non-combat buttons.
            bool woken = report.SpawnedEnemies;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                _currentEvent = null;
                _eventActingHero = null;

                if (arena != null)
                {
                    EnterSideRoom(_currentRoom, arena.SideRoom, arena.SideRoom.Mandatory ? arena.LootOneOf : null);
                }
                else if (woken)
                {
                    Show(_currentRoom, _entryDoor);
                }
                else
                {
                    SettleRoom();
                }
            };
        }

        /// <summary>
        /// A question: Ok runs <paramref name="onConfirm"/>, Cancel puts the player back in the room
        /// having changed nothing. Anything irreversible - freeing a captive, leaving a level behind -
        /// should ask this way rather than through <see cref="ShowDetail"/>, where the only button is
        /// consent.
        /// </summary>
        private void ShowConfirm(string title, string message, string confirmLabel, Action onConfirm)
        {
            ShowDetail(title, message);
            _detailOk.text = confirmLabel;
            _detailOkAction = () =>
            {
                _detailOk.text = Loc.Get("room.detail.ok");
                SetShown(_detailWindow, false);
                onConfirm();
            };
            _detailCancelAction = () =>
            {
                _detailOk.text = Loc.Get("room.detail.ok");
                SetShown(_detailWindow, false);
                SettleRoom();
            };
            SetShown(_detailCancel, true);
        }

        private void CloseEventResult()
        {
            SetShown(_detailWindow, false);
            _currentEvent = null;
            _eventActingHero = null;
            SettleRoom();
        }

        private void OnEventBack()
        {
            _eventNav?.Reset();
            SetShown(_eventWindow, false);
            _eventOptions.Clear();
            _eventDecline = null;
            _currentEvent = null;
            _eventActingHero = null;
            SettleRoom();
        }

        /// <summary>
        /// A statement: one button, and dismissing it returns to the room. Callers that need a second
        /// beat overwrite <c>_detailOkAction</c> after calling this.
        /// </summary>
        private void ShowDetail(string title, string message)
        {
            _detailNav?.Reset();
            SetShown(_detailCancel, false);
            _detailCancelAction = null;
            _detailTitle.text = title;
            _detailMessage.text = message;
            SetShown(_detailChip, false);
            _detailRows?.Clear();
            SetShown(_detailRows, false);
            // Default: dismissing returns to the room. Callers that need a second beat (the rescue
            // flow, an event outcome) overwrite _detailOkAction after calling this.
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                SettleRoom();
            };
            SetShown(_detailWindow, true);
        }

        private void SetDetailChip(string text, bool good)
        {
            if (_detailChip == null)
            {
                return;
            }
            _detailChip.text = text;
            _detailChip.EnableInClassList("cd-detail-chip--good", good);
            _detailChip.EnableInClassList("cd-detail-chip--bad", !good);
            SetShown(_detailChip, true);
        }

        /// <summary>One reward row in a dialog, in the victory window's style: icon, rarity-coloured
        /// name, count on the right.</summary>
        private void AddDetailRow(Sprite icon, string name, int quantity, ItemSO rarityOf)
        {
            if (_detailRows == null)
            {
                return;
            }
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("cd-victory-loot");
            var tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("cd-victory-loot__icon");
            if (icon != null)
            {
                tile.style.backgroundImage = new StyleBackground(icon);
            }
            row.Add(tile);
            var label = new Label(quantity > 1 ? Loc.Format("room.reward.counted", ("name", name), ("count", quantity)) : name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("cd-victory-loot__name");
            if (rarityOf != null)
            {
                label.AddToClassList(ItemPresenter.RarityClass(rarityOf.Rarity));
            }
            row.Add(label);
            _detailRows.Add(row);
            SetShown(_detailRows, true);
        }

        /// <summary>
        /// Takes the stairs: this is the one place a level is completed. Confirmed first, because it
        /// ends the level and abandons anything still unspent on it.
        /// </summary>
        private void OnDescend()
        {
            if (_currentRoom == null || !_currentRoom.IsExit)
            {
                return;
            }

            // Warn only about what is really left, and say what - "anything still unfound stays here"
            // on a floor with nothing left read as a trap.
            var dungeon = DungeonManager.HasInstance ? DungeonManager.Instance : null;
            var model = DungeonMapOps.Build(BuildMapInputs(dungeon != null ? dungeon.CurrentRooms : null),
                _currentRoom.RoomIndex);
            string left = model.FrontierCount > 0
                ? (model.FrontierCount == 1
                    ? Loc.Format("room.descend.unexplored.one", ("count", model.FrontierCount))
                    : Loc.Format("room.descend.unexplored.many", ("count", model.FrontierCount)))
                : string.Empty;
            // The last floor has nothing below it: there the stairs lead home (revisit playtest
            // finding 11).
            bool lastFloor = dungeon != null && DungeonManager.ActiveRun != null
                && DungeonManager.RunLevelIndex >= DungeonManager.ActiveRun.Levels.Count - 1;
            ShowConfirm(lastFloor ? Loc.Get("room.descend.title.last") : Loc.Get("room.descend.title"),
                (lastFloor
                    ? Loc.Get("room.descend.message.last")
                    : Loc.Get("room.descend.message")) + left,
                lastFloor ? Loc.Get("room.descend.confirm.last") : Loc.Get("room.descend.confirm"),
                () => CombatManager.Instance.NotifyDungeonCleared());
        }

        /// <summary>
        /// Frees the captive in a cleared room - automatically, because leaving them bound was never
        /// a real choice (playtest 2026-09-28): the only decision the old Rescue button posed was
        /// whether to press it. One statement says who joined, and the party window is rebuilt at
        /// once so the newcomer's row is there before the player walks on. Returns false when there
        /// was nobody to free, so the caller can move on.
        /// </summary>
        private bool TryAutoRescue()
        {
            var captive = _currentRoom != null ? _currentRoom.CaptiveHero : null;
            if (captive == null || _currentRoom.Enemies.Any(e => e != null && e.IsAlive)
                || !DungeonManager.HasInstance || !DungeonManager.Instance.TryRescueCaptive(_currentRoom))
            {
                return false;
            }

            // Rebuilt, not refreshed: RefreshPartyStatus only updates the rows that already exist,
            // which is why the newcomer used to be missing until the next room.
            ShowPartyStatusOutOfCombat();

            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    GameManager.Instance.Party.transform.position,
                    Loc.Format("room.captive.joined-float", ("hero", captive.DisplayName)),
                    Color.cyan);
            }

            string blurb = string.IsNullOrEmpty(captive.Blurb) ? "" : captive.Blurb + "\n\n";
            ShowDetail(Loc.Format("room.captive.title", ("hero", captive.DisplayName)),
                Loc.Format("room.captive.message", ("hero", captive.DisplayName), ("blurb", blurb)));
            return true;
        }

        /// <summary>
        /// Empties a cache: gold, plus at most one depth-rolled item. Not confirmed, because nothing
        /// is spent and nothing is risked - the decision a cache poses is only whether to walk to it.
        /// </summary>
        private void OnSearch()
        {
            if (_currentRoom == null || !_currentRoom.HasPendingPayload
                || _currentRoom.Kind != RoomKind.Treasure)
            {
                return;
            }


            int gold = RoomKindRewards.TreasureGold(DungeonManager.RunLevelIndex);
            if (gold > 0)
            {
                // Pending, not banked: cache gold is forfeited on death exactly like a kill's, so
                // carrying it out of the level is the reward.
                MetaProgressManager.Instance.AddPendingGold(gold, EconomySource.Cache);
            }

            var item = RollTreasureItem();
            if (item != null && InventoryManager.HasInstance)
            {
                InventoryManager.Instance.AddItem(item, 1, EconomySource.Cache);
            }

            // A side room's chest can hold better than a cache's depth roll.
            var sideSpec = _currentRoom.SideSpec;
            if (sideSpec != null && UnityEngine.Random.Range(0f, 100f) < sideSpec.ChestLootChancePercent)
            {
                var rare = Events.RoomEventRunner.GrantOneOf(sideSpec.ChestLoot);
                item = rare != null ? rare : item;
            }

            // The floor's own raw stuff, rolled separately from the gear slot above - see
            // RoomKindRewards.TreasureMaterials.
            var materials = RollTreasureMaterials();
            foreach (var award in materials)
            {
                InventoryManager.Instance.AddItem(award, EconomySource.Cache);
            }

            TakePayload();

            if (FloatingTextHandler.HasInstance && GameManager.Instance.Party != null)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    GameManager.Instance.Party.transform.position, Loc.Format("room.cache.gold-float", ("amount", gold)), new Color(1f, 0.85f, 0.25f));
            }

            ShowDetail(Loc.Get("room.cache.title"), gold > 0
                ? Loc.Format("room.cache.message-gold", ("amount", gold))
                : Loc.Get("room.cache.message"));
            // What was found, as reward rows (the victory window's style) rather than sentences.
            if (item != null)
            {
                AddDetailRow(item.Icon, item.DisplayName, 1, item);
            }
            foreach (var award in materials)
            {
                AddDetailRow(award.Item.Icon, award.Item.DisplayName, award.Quantity, award.Item);
            }
            if (item == null && materials.Count == 0)
            {
                _detailMessage.text += Loc.Get("room.cache.nothing-else");
            }
            _detailOkAction = ClosePayloadResult;
        }

        /// <summary>
        /// One item from the whole catalog, rolled through the same rarity and depth rules a kill drop
        /// follows. Shuffled first, so *which* item varies; capped at one, so a cache is a find rather
        /// than a shop.
        /// </summary>
        private ItemSO RollTreasureItem()
        {
            if (!InventoryManager.HasInstance)
            {
                return null;
            }

            var candidates = InventoryManager.Instance.AllItems().Where(i => i != null).ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var swap = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swap;
            }

            return RoomKindRewards.PickTreasureItem(
                candidates, DungeonManager.RunLevelIndex, () => UnityEngine.Random.Range(0f, 1f));
        }

        /// <summary>
        /// The raw materials this floor's caches yield, off <c>LevelDefinitionSO.MaterialTable</c>.
        /// Empty when the level authors no table, which is the same as it authoring no materials.
        /// </summary>
        private List<LootAward> RollTreasureMaterials()
        {
            var level = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentLevel : null;
            if (level == null || !InventoryManager.HasInstance)
            {
                return new List<LootAward>();
            }

            var materials = RoomKindRewards.TreasureMaterials(
                level.MaterialTable, DungeonManager.RunLevelIndex, () => UnityEngine.Random.Range(0f, 1f));
            // A revisit below its best fear does not pay the scarce materials (docs/plans/REVISITS.md).
            // A recipe already known teaches nothing, so a cache does not hand it over again.
            materials.RemoveAll(a => a.Item != null
                && (RunFear.Current.Withholds(a.Item) || InventoryManager.Instance.AlreadyKnown(a.Item)));
            return materials;
        }

        /// <summary>
        /// Takes the refuge: heals every hero a share of their bar, once. Confirmed, because resting
        /// at full health throws the whole thing away - that timing is the decision the room poses,
        /// and the party's current state is in the prompt so the player can make it.
        /// </summary>
        private void OnRest()
        {
            if (_currentRoom == null || !_currentRoom.HasPendingPayload
                || _currentRoom.Kind != RoomKind.Rest)
            {
                return;
            }

            var party = GameManager.Instance.Party;
            int missing = 0;
            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null && hero.Stats != null)
                    {
                        missing += Mathf.Max(0, hero.GetEffectiveMaxHealth() - hero.Stats.Health);
                    }
                }
            }


            string state = missing > 0
                ? Loc.Format("room.refuge.state.hurt", ("missing", missing))
                : Loc.Get("room.refuge.state.whole");

            ShowConfirm(Loc.Get("room.refuge.title"),
                Loc.Format("room.refuge.prompt", ("state", state),
                    ("percent", Mathf.RoundToInt(RoomKindRewards.RestHealFraction * 100f))),
                Loc.Get("room.refuge.confirm"),
                ApplyRest);
        }

        private void ApplyRest()
        {
            var party = GameManager.Instance.Party;
            var lines = new List<string>();

            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero == null || hero.Stats == null || !hero.IsAlive)
                    {
                        continue;
                    }

                    int max = hero.GetEffectiveMaxHealth();
                    int healed = HealthOps.Heal(hero,
                        RunFear.Current.ScaleHeroHealing(RoomKindRewards.RestHealAmount(max)),
                        new HealthSource(null, HealthCause.Rest), null).Healed;
                    lines.Add(healed > 0
                        ? Loc.Format("room.refuge.healed", ("hero", hero.DisplayName), ("amount", healed))
                        : Loc.Format("room.refuge.already-whole", ("hero", hero.DisplayName)));
                }
            }

            // A refuge is the only in-run refill of a spell charge. Draw used to be that refill -
            // spend a charge, take the magic again - and removing it (2026-09-04) left magic as a
            // strictly finite run allowance with no way back. Hanging the top-up on the refuge keeps
            // it a place the player has to find and spend rather than a rule about levels, and puts
            // it in direct competition with the heal: the same one-shot room pays both, so resting
            // early for charges is resting early for health too.
            if (DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                DungeonManager.Instance.MagicState.RefillCharges();
                lines.Add(Loc.Get("room.refuge.ability-charges"));
            }
            if (DungeonManager.HasInstance && DungeonManager.Instance.Summons != null
                && DungeonManager.Instance.Summons.GetSaveData().Count > 0)
            {
                DungeonManager.Instance.Summons.RefillCharges();
                lines.Add(Loc.Get("room.refuge.summon-charges"));
            }

            TakePayload();

            if (FloatingTextHandler.HasInstance && party != null)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    party.transform.position, Loc.Get("room.refuge.rested-float"), Color.green);
            }

            ShowDetail(Loc.Get("room.refuge.title"), Loc.Format("room.refuge.result", ("lines", string.Join("\n", lines))));
            _detailOkAction = ClosePayloadResult;
            RefreshPartyStatus();
        }

        /// <summary>
        /// Marks the room's payload spent and saves at once - the same rule an event follows, and for
        /// the same reason: without it the player re-loots the cache by walking out and back in, or by
        /// quitting to the menu and resuming.
        /// </summary>
        private void TakePayload()
        {
            _currentRoom.MarkPayloadTaken();
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.Save(_currentRoom);
            }
        }

        private void ClosePayloadResult()
        {
            SetShown(_detailWindow, false);
            SettleRoom();
        }

        // ============================================================
        //  COMBAT
        // ============================================================

        private void OnFight()
        {
            var party = GameManager.Instance.Party;

            HideAll();

            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted += OnHeroTurnStarted;
            CombatManager.Instance.OnSummonStarted += OnSummonStarted;
            CombatManager.Instance.OnEnemyAbilityUsed += OnEnemyAbilityUsed;
            CombatManager.Instance.OnSummonEnded += OnSummonEnded;
            CombatManager.Instance.OnTurnExecuted += OnTurnExecuted;
            CombatManager.Instance.OnTurnOrderChanged += OnTurnOrderChanged;

            BuildPartyStatus(party);
            SetShown(_partyStatus, true);
            SetShown(_turnOrder, true);

            CombatManager.Instance.StartCombat(party, _currentRoom);
        }

        private void OnHeroTurnStarted(ICombatUnit hero)
        {
            _currentHeroTurn = hero;
            _heroTitle.text = Loc.Format("room.combat.turn-title", ("hero", hero.DisplayName));
            HighlightActiveHero(hero);
            RefreshPartyStatus();

            BuildCommandMenu(hero);

            SetShown(_heroBar, true);
            FocusRoot();
        }

        private void OnHeroAttack()
        {
            SetShown(_heroBar, false);

            var enemies = CombatManager.Instance.GetAliveEnemies();
            if (enemies.Count <= 1)
            {
                CombatManager.Instance.SubmitAttackAction(enemies.Count == 1 ? enemies[0] : null);
                return;
            }

            CombatManager.Instance.RequestAttackTargets(_currentHeroTurn, enemies);
        }

        private void OnHeroMagic()
        {
            SetShown(_heroBar, false);

            var heroComponent = _currentHeroTurn as Heroes.Hero;
            if (heroComponent == null || !DungeonManager.HasInstance || DungeonManager.Instance.MagicState == null)
            {
                SetShown(_heroBar, true);
                return;
            }

            var slots = new List<Cards.MagicSlot>(DungeonManager.Instance.MagicState.GetSlots(heroComponent.HeroKey));
            // An Ultra's form adds its abilities after the real slots - appended, so a real slot's
            // index (what a cast spends a charge from) never moves. Casting one spends nothing:
            // EquippedMagicState.TryCast refuses an index past the hero's slots.
            foreach (var ability in CombatManager.Instance.FormAbilities(_currentHeroTurn))
            {
                slots.Add(new Cards.MagicSlot { Magic = ability, Charges = 1, MaxCharges = 1, Unlimited = true });
            }
            CombatManager.Instance.RequestMagicSlots(_currentHeroTurn, slots);
        }

        /// <summary>The hero's Ultras, always as a list - even one - as FFX's Overdrive menu does, so
        /// the player reads what it does before spending a full gauge on it.</summary>
        private void OnHeroUltra()
        {
            var ultras = CombatManager.Instance.KnownUltras(_currentHeroTurn);
            if (ultras.Count == 0 || !CombatManager.Instance.CanUseUltra(_currentHeroTurn))
            {
                return;
            }
            SetShown(_heroBar, false);
            CombatManager.Instance.RequestUltraList(_currentHeroTurn, ultras);
        }

        /// <summary>
        /// Summons have no target picker - they land on a whole side, or replace the party. The
        /// command always opens the summon list, even for one (the owner's call, like FFX): it shows
        /// every summon the hero knows, spent ones greyed, and the footer says what it does before a
        /// run's charge is spent.
        /// </summary>
        private void OnHeroSummon()
        {
            var heroComponent = _currentHeroTurn as Heroes.Hero;
            var summons = DungeonManager.HasInstance ? DungeonManager.Instance.Summons : null;
            var known = heroComponent != null && summons != null
                ? summons.GetSummons(heroComponent.HeroKey)
                : new List<Cards.SummonSlot>();
            var usable = known.FindAll(s => s.CanUse);
            if (usable.Count == 0)
            {
                return;
            }
            SetShown(_heroBar, false);
            CombatManager.Instance.RequestSummonList(_currentHeroTurn, known);
        }

        /// <summary>
        /// One of a party-replacing summon's abilities. A single-enemy ability with more than one
        /// enemy standing goes through the target picker; everything else knows its targets.
        /// </summary>
        private void OnSummonAbility(Cards.MagicSO ability)
        {
            var summon = _currentHeroTurn as SummonUnit;
            if (summon == null || ability == null)
            {
                return;
            }
            SetShown(_heroBar, false);
            var targets = CombatManager.Instance.SummonAbilityTargets(ability, summon);
            if (ability.TargetType == Cards.MagicTargetType.SingleEnemy && targets.Count > 1)
            {
                CombatManager.Instance.RequestSummonAbilityTarget(summon, ability, targets);
                return;
            }
            // A revive asks whom, even of one - the list says who is down.
            if (ability.TargetType == Cards.MagicTargetType.FallenAlly && targets.Count > 0)
            {
                CombatManager.Instance.RequestSummonAbilityTarget(summon, ability, targets);
                return;
            }
            CombatManager.Instance.SubmitSummonAbility(ability, targets);
        }

        private void OnSummonDismiss()
        {
            SetShown(_heroBar, false);
            CombatManager.Instance.SubmitDismiss();
        }

        private void OnSummonStarted(ICombatUnit caster, Cards.SummonSO summon)
        {
            if (_summonBannerName != null)
            {
                _summonBannerName.text = summon != null ? summon.Label : "";
            }
            SetShown(_summonBanner, true);
        }

        private void OnSummonEnded()
        {
            SetShown(_summonBanner, false);
        }

        // Long enough to read a two-word name while the projectile flies; gone before the next turn.
        private const long AbilityBannerMs = 1600;

        /// <summary>
        /// Names what an enemy uses, FFX-style (playtest 3 finding 12): the name alone, no flavour -
        /// combat text stays minimal.
        /// </summary>
        private void OnEnemyAbilityUsed(ICombatUnit enemy, string name)
        {
            if (_abilityBanner == null || string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_abilityBannerName != null)
            {
                _abilityBannerName.text = name;
            }
            SetShown(_abilityBanner, true);
            int shown = ++_abilityBannerShown;
            _abilityBanner.schedule.Execute(() =>
            {
                if (shown == _abilityBannerShown)
                {
                    SetShown(_abilityBanner, false);
                }
            }).StartingIn(AbilityBannerMs);
        }

        private void OnHeroItem()
        {
            SetShown(_heroBar, false);

            if (!InventoryManager.HasInstance)
            {
                SetShown(_heroBar, true);
                return;
            }

            var consumables = InventoryManager.Instance.GetConsumables();
            if (consumables.Count == 0)
            {
                SetShown(_heroBar, true);
                return;
            }

            CombatManager.Instance.RequestItemList(_currentHeroTurn, consumables);
        }

        /// <summary>
        /// Opens the enemy knowledge page. The only hero command that costs nothing: it submits no
        /// action, so <c>MagicSelectionUI</c> hands the turn back through
        /// <see cref="ReturnToHeroActions"/> when the page closes. Reading what the party already
        /// learned is not a move.
        /// </summary>
        private void OnHeroInspect()
        {
            SetShown(_heroBar, false);

            var enemies = CombatManager.Instance.GetAliveEnemies();
            if (enemies.Count == 0)
            {
                SetShown(_heroBar, true);
                return;
            }

            CombatManager.Instance.RequestInspectTargets(_currentHeroTurn, enemies);
        }

        /// <summary>Called by the selection UI to return to the Attack/Magic/Item/Inspect/Skip window.</summary>
        public void ReturnToHeroActions()
        {
            if (EnsureRefs())
            {
                SetShown(_heroBar, true);
                FocusRoot(); // reclaim keyboard focus from the (now closed) selection picker
            }
        }

        private void OnHeroSkip()
        {
            SetShown(_heroBar, false);
            CombatManager.Instance.SubmitHeroAction(HeroAction.Skip);
        }

        // ============================================================
        //  COMMAND MENU (FFX-style cursor selection list)
        // ============================================================

        private void BuildCommandMenu(ICombatUnit hero)
        {
            _commands.Clear();
            _commandRows.Clear();
            _commandCursors.Clear();
            _commandList?.Clear();
            if (_commandList == null)
            {
                return;
            }

            if (hero is SummonUnit summonUnit)
            {
                AddSummonCommands(summonUnit);
            }
            else
            {
                AddHeroCommands(hero);
            }

            for (int i = 0; i < _commands.Count; i++)
            {
                var entry = _commands[i];
                var row = new VisualElement();
                row.AddToClassList("cd-cmd-row");
                if (!entry.Enabled)
                {
                    row.AddToClassList("cd-cmd-row--disabled");
                }

                var cursor = new Label(string.Empty);
                cursor.AddToClassList("cd-cmd-row__cursor");
                var label = new Label(entry.Label);
                label.AddToClassList("cd-cmd-row__label");
                row.Add(cursor);
                row.Add(label);
                if (!entry.Enabled && !string.IsNullOrEmpty(entry.Reason))
                {
                    var reason = new Label(entry.Reason);
                    reason.AddToClassList("cd-cmd-row__reason");
                    row.Add(reason);
                }

                int idx = i;
                row.RegisterCallback<ClickEvent>(_ => OnCommandClicked(idx));
                row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (_commands[idx].Enabled)
                    {
                        SetSelectedCommand(idx);
                    }
                });

                _commandList.Add(row);
                _commandRows.Add(row);
                _commandCursors.Add(cursor);
            }

            _selectedCommand = FirstEnabledCommand();
            RenderCommandCursor();
        }

        /// <summary>
        /// A party-replacing summon's own menu (§4b): Attack, its actions, its Signature once per
        /// summoning, Dismiss, and the free Inspect. No Item - the belt is the party's - and no Skip,
        /// because Dismiss covers what Skip was for (a Sacrifice horror gets Skip instead of Dismiss).
        /// Silence closes its abilities and nothing else, the same rule a hero lives under.
        /// </summary>
        private void AddSummonCommands(SummonUnit summon)
        {
            bool silenced = CombatManager.Instance.BuffTracker != null
                && CombatManager.Instance.BuffTracker.HasStatusEffect(summon, BuffType.Silenced);

            // A summon with its own Attack (Exatrix's Rend) still offers it as "Attack": it is the
            // basic attack, so Silence leaves it open like a hero's.
            if (summon.AttackAbility != null)
            {
                _commands.Add(new CommandEntry
                {
                    // A rotating summon's row is this turn's rite, by name - "Attack" would be a lie.
                    Command = HeroCommand.SummonAbility,
                    Label = summon.Summon != null && summon.Summon.RotateActions ? summon.AttackAbility.DisplayName : Loc.Get("room.command.attack"),
                    Enabled = true,
                    Ability = summon.AttackAbility
                });
            }
            else
            {
                _commands.Add(new CommandEntry { Command = HeroCommand.Attack, Label = Loc.Get("room.command.attack"), Enabled = true });
            }
            if (summon.Summon != null && summon.Summon.Actions != null)
            {
                foreach (var action in summon.Summon.Actions)
                {
                    if (action == null)
                    {
                        continue;
                    }
                    _commands.Add(new CommandEntry
                    {
                        Command = HeroCommand.SummonAbility, Label = action.DisplayName,
                        Enabled = !silenced, Ability = action
                    });
                }
            }
            if (summon.Summon != null && summon.Summon.Signature != null)
            {
                // The star says "once": after it is spent the row stays, greyed, so the player can
                // see what they have already used rather than wondering where it went.
                _commands.Add(new CommandEntry
                {
                    Command = HeroCommand.SummonAbility, Label = Loc.Format("room.command.signature", ("ability", summon.Summon.Signature.DisplayName)),
                    Enabled = !silenced && summon.Stay.CanUseSignature, Ability = summon.Summon.Signature
                });
            }
            _commands.Add(new CommandEntry { Command = HeroCommand.Inspect, Label = Loc.Get("room.command.inspect"), Enabled = true });
            // A stand-in (a Sacrifice horror) is what is left of a hero, not a guest: there is nothing to
            // send it home to, so it gets Skip where a summon gets Dismiss.
            if (!CombatManager.Instance.CanDismiss(summon))
            {
                _commands.Add(new CommandEntry { Command = HeroCommand.Skip, Label = Loc.Get("room.command.skip"), Enabled = true });
            }
            else
            {
                _commands.Add(new CommandEntry { Command = HeroCommand.Dismiss, Label = Loc.Get("room.command.dismiss"), Enabled = true });
            }
        }

        private void AddHeroCommands(ICombatUnit hero)
        {
            bool hasMagic = false;
            bool carriesAny = false;
            var heroComponent = hero as Heroes.Hero;
            if (heroComponent != null && DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                hasMagic = DungeonManager.Instance.MagicState.HasAnyCastable(heroComponent.HeroKey);
                foreach (var slot in DungeonManager.Instance.MagicState.GetSlots(heroComponent.HeroKey))
                {
                    if (slot != null && slot.Magic != null)
                    {
                        carriesAny = true;
                    }
                }
            }

            // Silence gates casting and nothing else - Attack, Item and Inspect stay open, so a
            // silenced hero still has a turn worth taking rather than three turns of Skip.
            bool silenced = CombatManager.Instance.BuffTracker != null
                && CombatManager.Instance.BuffTracker.HasStatusEffect(hero, BuffType.Silenced);
            if (silenced)
            {
                hasMagic = false;
            }

            // An Ultra's form brings abilities of its own, so a hero with no charges left still has some.
            if (!silenced && CombatManager.Instance.FormAbilities(hero).Count > 0)
            {
                hasMagic = true;
            }

            bool hasItem = InventoryManager.HasInstance && InventoryManager.Instance.HasAnyConsumable();

            // Summon is the one command the menu grew by (§4b, 2026-09-28): shown only to a hero
            // who knows a summon, greyed when every charge is spent - and, like casting, by Silence.
            var summons = DungeonManager.HasInstance ? DungeonManager.Instance.Summons : null;
            bool knowsSummon = heroComponent != null && summons != null && summons.Knows(heroComponent.HeroKey);
            bool canSummon = knowsSummon && !silenced && summons.HasAnyUsable(heroComponent.HeroKey);

            _commands.Add(new CommandEntry { Command = HeroCommand.Attack, Label = Loc.Get("room.command.attack"), Enabled = true });
            // A greyed command says why on its own row - "Ability" alone, dimmed, read as a bug.
            _commands.Add(new CommandEntry
            {
                Command = HeroCommand.Magic, Label = Loc.Get("room.command.ability"), Enabled = hasMagic,
                Reason = silenced ? Loc.Get("room.command.reason.silenced") : carriesAny ? Loc.Get("room.command.reason.no-charges") : Loc.Get("room.command.reason.none-carried")
            });
            if (knowsSummon)
            {
                _commands.Add(new CommandEntry
                {
                    Command = HeroCommand.Summon, Label = Loc.Get("room.command.summon"), Enabled = canSummon,
                    Reason = silenced ? Loc.Get("room.command.reason.silenced") : Loc.Get("room.command.reason.spent")
                });
            }
            // Ultra (COMBAT_DEPTH §13): shown to a hero who knows one, open once the gauge is full.
            if (CombatManager.Instance.KnownUltras(hero).Count > 0)
            {
                bool transformed = CombatManager.Instance.IsTransformed(hero);
                _commands.Add(new CommandEntry
                {
                    Command = HeroCommand.Ultra, Label = Loc.Get("room.command.ultra"), Enabled = CombatManager.Instance.CanUseUltra(hero),
                    Reason = transformed ? Loc.Get("room.command.reason.active") : UltraWaitReason(hero)
                });
            }
            _commands.Add(new CommandEntry { Command = HeroCommand.Item, Label = Loc.Get("room.command.item"), Enabled = hasItem, Reason = Loc.Get("room.command.reason.none") });
            // Inspect is free - it opens a page and hands the turn straight back - so it sits above
            // Skip rather than among the actions that spend the turn.
            _commands.Add(new CommandEntry { Command = HeroCommand.Inspect, Label = Loc.Get("room.command.inspect"), Enabled = true });
            _commands.Add(new CommandEntry { Command = HeroCommand.Skip, Label = Loc.Get("room.command.skip"), Enabled = true });
        }

        /// <summary>What a closed Ultra command waits for: the gauge's fill, or Focus for a hero whose only
        /// Ultras are MultiCasts (the Mage).</summary>
        private static string UltraWaitReason(ICombatUnit hero)
        {
            var known = CombatManager.Instance.KnownUltras(hero);
            if (known.Count > 0 && known.TrueForAll(Cards.UltraOps.IsMultiCast))
            {
                int focus = CombatManager.Instance.FocusOf(hero);
                return focus > 0 ? Loc.Format("room.command.reason.focus", ("focus", focus)) : Loc.Get("room.command.reason.no-focus");
            }
            return Loc.Format("room.command.reason.gauge", ("percent", CombatManager.Instance.UltraGauge(hero)));
        }

        private int FirstEnabledCommand()
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].Enabled)
                {
                    return i;
                }
            }
            return 0;
        }

        private void RenderCommandCursor()
        {
            for (int i = 0; i < _commandRows.Count; i++)
            {
                bool selected = i == _selectedCommand;
                _commandRows[i].EnableInClassList("cd-cmd-row--selected", selected);
                _commandCursors[i].text = selected ? "▸" : string.Empty;
            }
        }

        private void SetSelectedCommand(int index)
        {
            if (index < 0 || index >= _commands.Count)
            {
                return;
            }
            _selectedCommand = index;
            RenderCommandCursor();
        }

        private void MoveCommandCursor(int delta)
        {
            if (_commands.Count == 0)
            {
                return;
            }
            int i = _selectedCommand;
            for (int step = 0; step < _commands.Count; step++)
            {
                i = (i + delta + _commands.Count) % _commands.Count;
                if (_commands[i].Enabled)
                {
                    SetSelectedCommand(i);
                    CombatAudio.Play(CombatSound.CursorMove);
                    return;
                }
            }
        }

        private void ConfirmCommand()
        {
            if (_selectedCommand < 0 || _selectedCommand >= _commands.Count || !_commands[_selectedCommand].Enabled)
            {
                return;
            }
            CombatAudio.Play(CombatSound.Confirm);
            InvokeCommand(_commands[_selectedCommand]);
        }

        private void OnCommandClicked(int index)
        {
            if (index < 0 || index >= _commands.Count || !_commands[index].Enabled)
            {
                return;
            }
            SetSelectedCommand(index);
            ConfirmCommand();
        }

        /// <summary>Runs one row of the command menu. Callers check it is enabled first.</summary>
        private void InvokeCommand(CommandEntry entry)
        {
            switch (entry.Command)
            {
                case HeroCommand.SummonAbility:
                    OnSummonAbility(entry.Ability);
                    break;
                case HeroCommand.Dismiss:
                    OnSummonDismiss();
                    break;
                case HeroCommand.Attack:
                    OnHeroAttack();
                    break;
                case HeroCommand.Magic:
                    OnHeroMagic();
                    break;
                case HeroCommand.Summon:
                    OnHeroSummon();
                    break;
                case HeroCommand.Ultra:
                    OnHeroUltra();
                    break;
                case HeroCommand.Item:
                    OnHeroItem();
                    break;
                case HeroCommand.Inspect:
                    OnHeroInspect();
                    break;
                case HeroCommand.Skip:
                    OnHeroSkip();
                    break;
            }
        }

        // ============================================================
        //  TURN-ORDER LIST (FFX-style, right side)
        // ============================================================

        /// <summary>How many upcoming turns the portrait column shows.</summary>
        private const int TurnOrderShown = 10;

        private void OnTurnOrderChanged(List<ICombatUnit> order)
        {
            if (_turnOrderList == null)
            {
                return;
            }
            _turnOrderList.Clear();
            if (order == null)
            {
                return;
            }

            // Portraits only, FFX-style (2026-09-30): the names were cut off ("Abyssal Warde") and made
            // the panel wide enough to sit on a boss. Side is the frame colour, the actor is the big one.
            int shown = 0;
            for (int i = 0; i < order.Count && shown < TurnOrderShown; i++)
            {
                var unit = order[i];
                if (unit == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("cd-turn-row");
                if (shown == 0)
                {
                    row.AddToClassList("cd-turn-row--current");
                    // "Now" on the acting tile, so the column reads as a turn order at a glance (playtest
                    // 2026-10-30: the tester never noticed it).
                    var now = new Label(Loc.Get("room.turn-order.now")) { pickingMode = PickingMode.Ignore };
                    now.AddToClassList("cd-turn-row__now");
                    row.Add(now);
                }
                row.EnableInClassList("cd-turn-row--enemy", unit is Enemies.Enemy);
                row.tooltip = unit.DisplayName;

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("cd-turn-row__icon");
                if (unit.Icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(unit.Icon);
                }

                row.Add(icon);
                _turnOrderList.Add(row);
                shown++;
            }
        }

        /// <summary>
        /// The whole keyboard for this panel, dispatched by which bar is up: Fight/Flee on the combat
        /// bar, the hero command cursor and its letter shortcuts on the command bar, and otherwise the
        /// dungeon keys - arrows to walk, Enter to use. A key only ever acts when its bar is
        /// visible, and only invokes actions whose buttons are currently shown (Magic and Item
        /// appear conditionally), so a hotkey can never do something the on-screen menu cannot.
        /// </summary>
        private void OnCombatHotkey(KeyDownEvent evt)
        {
            // The map is checked before pause because it can be opened *from* pause, and the two are
            // never up together - opening the map closes the overlay and Back puts it back.
            if (IsShown(_mapWindow))
            {
                HandleMapKey(evt);
                evt.StopPropagation();
                return;
            }

            // Pause is above everything else, including a dialog it can never be opened over: while
            // it is up it is the only thing the keyboard reaches.
            if (IsShown(_pauseWindow))
            {
                if (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Backspace || evt.keyCode == KeyCode.X || evt.keyCode == KeyCode.C)
                {
                    ClosePause();
                }
                else
                {
                    _pauseNav?.HandleKey(evt);
                }
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.M && CanOpenMap())
            {
                OpenMap(fromPause: false);
                evt.StopPropagation();
                return;
            }

            // C is the RPG Maker menu key (playtest 2026-10-30), beside Escape.
            if ((evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.C) && CanOpenPause())
            {
                OpenPause();
                evt.StopPropagation();
                return;
            }

            // Dialogs first: whatever is stacked over the room owns the keyboard while it is up.
            if (HandleDialogKey(evt))
            {
                evt.StopPropagation();
                return;
            }
            if (IsDialogUp())
            {
                // A dialog is up but did not want this key - it must not fall through to the room.
                return;
            }

            if (IsShown(_combatBar))
            {
                // Stepping back through the door the party came in by is Flee; nothing walks further in.
                if (IsShown(_fleeBtn) && _entryDoor != null
                    && PartyWalk.DirectionOf(evt.keyCode) == _entryDoor.DirectionFrom(_currentRoom))
                {
                    evt.StopPropagation();
                    OnFlee();
                    return;
                }

                // Arrows/Tab move along the bar, Enter presses. Flee is hidden in a boss room, so the
                // cursor cannot reach a way out the fight does not offer.
                if (_combatNav != null && _combatNav.HandleKey(evt))
                {
                    evt.StopPropagation();
                }
                return;
            }

            if (IsShown(_heroBar))
            {
                switch (evt.keyCode)
                {
                    // Cursor navigation (controller-friendly).
                    case KeyCode.UpArrow:
                        MoveCommandCursor(-1);
                        evt.StopPropagation();
                        break;
                    case KeyCode.DownArrow:
                        MoveCommandCursor(1);
                        evt.StopPropagation();
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Z:
                    case KeyCode.Space:
                        ConfirmCommand();
                        evt.StopPropagation();
                        break;
                }
                return;
            }

            if (DoorNavActive() && HandleDungeonKey(evt))
            {
                evt.StopPropagation();
            }
        }

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
            if (_pauseWindow == null || IsShown(_pauseWindow) || IsShown(_mapWindow) || IsDialogUp())
            {
                return false;
            }
            return DoorNavActive() || IsShown(_combatBar) || IsShown(_heroBar);
        }

        private void OpenPause()
        {
            if (_pauseWindow == null)
            {
                return;
            }

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
            if (_mapWindow == null || IsShown(_mapWindow) || IsDialogUp() || (_currentRoom != null && _currentRoom.IsSideRoom))
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

            // The scrim follows the dialogs rather than being set at each of the many places one
            // opens, so no path can leave the room dimmed or a dialog undimmed.
            bool dialogUp = IsShown(_eventWindow) || IsShown(_detailWindow) || IsShown(_mapWindow) || IsShown(_pauseWindow);
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
                return Loc.Get("room.keys.combat-bar");
            }

            if (IsShown(_heroBar))
            {
                return Loc.Get("room.keys.hero-bar");
            }

            if (DoorNavActive())
            {
                return Loc.Get("room.keys.doors");
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
            return IsShown(_pauseWindow) || IsShown(_mapWindow) || IsShown(_heroBar)
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
                && !IsShown(_mapWindow) && !IsShown(_detailWindow) && !IsShown(_eventWindow)
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
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party == null || _currentRoom == null || _currentRoom.Enemies.Any(e => e != null && e.IsAlive))
            {
                return false;
            }

            var facing = party.Tile + party.Facing;
            if (IsAtHand(_currentRoom.ReturnMarker, facing, party.Tile))
            {
                LeaveSideRoom();
                return true;
            }
            if (IsAtHand(_currentRoom.EventMarker, facing, party.Tile) && TryUseEvent())
            {
                return true;
            }
            if (IsAtHand(_currentRoom.KindMarker, facing, party.Tile) && _currentRoom.HasPendingPayload)
            {
                if (_currentRoom.Kind == RoomKind.Treasure)
                {
                    OnSearch();
                }
                else
                {
                    OnRest();
                }
                return true;
            }
            if (_currentRoom.IsExit && IsAtHand(_currentRoom.ExitMarker, facing, party.Tile))
            {
                OnDescend();
                return true;
            }
            return false;
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
                _walk = new PartyWalk(OnDoorSelected);
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
                _walk = new PartyWalk(OnDoorSelected);
            }
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            return DoorNavActive() && _walk.TryStep(party, _currentRoom, direction);
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
        //  PARTY STATUS WINDOW (FF-style, bottom-left)
        // ============================================================

        /// <summary>
        /// One row per hero: portrait, name, HP as a number and a slim bar, and any level affliction
        /// as a chip ("STR -2") - an event's lasting penalty used to vanish with its dialog.
        /// </summary>
        private void BuildPartyStatus(Heroes.Party party)
        {
            _partyStatusRows?.Clear();
            _partyRows.Clear();
            _partyHpLabels.Clear();
            _partyHpFills.Clear();
            _partyAfflictions.Clear();
            _partyGaugeFills.Clear();
            if (party == null || _partyStatusRows == null)
            {
                return;
            }

            foreach (var hero in party.Heroes)
            {
                if (hero == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("cd-party-row");

                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("cd-party-row__portrait");
                if (hero.Icon != null)
                {
                    portrait.style.backgroundImage = new StyleBackground(hero.Icon);
                }
                row.Add(portrait);

                var main = new VisualElement { pickingMode = PickingMode.Ignore };
                main.AddToClassList("cd-party-row__main");
                var top = new VisualElement { pickingMode = PickingMode.Ignore };
                top.AddToClassList("cd-party-row__top");
                var name = new Label(hero.DisplayName);
                name.AddToClassList("cd-party-row__name");
                var hp = new Label();
                hp.AddToClassList("cd-party-row__hp");
                top.Add(name);
                top.Add(hp);
                main.Add(top);

                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("cd-party-row__bar");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("cd-party-row__fill");
                bar.Add(fill);
                main.Add(bar);

                // The Ultra gauge, under the health bar, only for a hero who knows an Ultra.
                if (hero.KnownUltras.Count > 0)
                {
                    var gauge = new VisualElement { pickingMode = PickingMode.Ignore };
                    gauge.AddToClassList("cd-party-row__gauge");
                    var gaugeFill = new VisualElement { pickingMode = PickingMode.Ignore };
                    gaugeFill.AddToClassList("cd-party-row__gauge-fill");
                    gauge.Add(gaugeFill);
                    main.Add(gauge);
                    _partyGaugeFills[hero] = gaugeFill;
                }

                var afflictions = new VisualElement { pickingMode = PickingMode.Ignore };
                afflictions.AddToClassList("cd-party-row__afflictions");
                main.Add(afflictions);
                row.Add(main);

                _partyStatusRows.Add(row);
                _partyRows[hero] = row;
                _partyHpLabels[hero] = hp;
                _partyHpFills[hero] = fill;
                _partyAfflictions[hero] = afflictions;
            }

            RefreshPartyStatus();
        }

        private void RefreshPartyStatus()
        {
            var tracker = DungeonManager.HasInstance ? DungeonManager.Instance.Afflictions : null;
            foreach (var pair in _partyHpLabels)
            {
                var hero = pair.Key;
                var hp = pair.Value;
                if (hero == null || hp == null)
                {
                    continue;
                }
                int max = Mathf.Max(1, hero.GetEffectiveMaxHealth());
                // Overkill leaves Health below zero; the row reads 0, as the plate above the hero does.
                hp.text = Loc.Format("room.party.hp", ("current", Mathf.Max(0, hero.Stats.Health)), ("max", max));
                if (_partyRows.TryGetValue(hero, out var row) && row != null)
                {
                    row.EnableInClassList("cd-party-row--dead", !hero.IsAlive);
                }
                if (_partyHpFills.TryGetValue(hero, out var fill) && fill != null)
                {
                    float ratio = Mathf.Clamp01((float)hero.Stats.Health / max);
                    fill.style.width = Length.Percent(ratio * 100f);
                    fill.EnableInClassList("cd-party-row__fill--mid", ratio <= 0.5f && ratio > 0.25f);
                    fill.EnableInClassList("cd-party-row__fill--low", ratio <= 0.25f);
                }
                if (_partyGaugeFills.TryGetValue(hero, out var gaugeFill) && gaugeFill != null)
                {
                    // Outside a fight there is no gauge (it starts every fight empty), so it reads empty.
                    var combat = CombatManager.HasInstance ? CombatManager.Instance : null;
                    int gauge = combat != null && combat.InCombat ? combat.UltraGauge(hero) : 0;
                    // A hero who builds Focus (the Mage) spends that on his Ultra, so the bar shows it instead.
                    var focusRule = hero.HeroSO != null ? hero.HeroSO.Focus : null;
                    if (focusRule != null && focusRule.IsActive)
                    {
                        int focus = combat != null && combat.InCombat ? combat.FocusOf(hero) : 0;
                        gauge = Mathf.RoundToInt(focus * (float)Cards.UltraOps.Max / focusRule.Max);
                    }
                    gaugeFill.style.width = Length.Percent(gauge * 100f / Cards.UltraOps.Max);
                    gaugeFill.EnableInClassList("cd-party-row__gauge-fill--full", Cards.UltraOps.IsFull(gauge));
                    gaugeFill.EnableInClassList("cd-party-row__gauge-fill--active",
                        combat != null && combat.InCombat && combat.IsTransformed(hero));
                }
                if (_partyAfflictions.TryGetValue(hero, out var chips) && chips != null)
                {
                    chips.Clear();
                    if (tracker != null)
                    {
                        foreach (var affliction in tracker.For(hero.HeroKey))
                        {
                            var handler = Cards.Buffs.BuffHandlerRegistry.Get(affliction.Buff);
                            string text = handler != null
                                ? handler.GetDisplayText(affliction.Amount).TrimEnd('!')
                                : affliction.Buff.ToString();
                            var chip = new Label(text) { pickingMode = PickingMode.Ignore, tooltip = Loc.Format("room.party.affliction-tooltip", ("effect", text)) };
                            chip.AddToClassList("cd-party-row__affliction");
                            chips.Add(chip);
                        }
                    }
                }
            }
        }

        private void HighlightActiveHero(ICombatUnit active)
        {
            foreach (var pair in _partyRows)
            {
                if (pair.Value == null)
                {
                    continue;
                }
                pair.Value.EnableInClassList("cd-party-row--active", ReferenceEquals(pair.Key, active));
            }
        }

        private void OnTurnExecuted()
        {
            // The turn is over: the row stops claiming it, or the hero stayed lit through the
            // enemy's turn that follows. The next hero turn lights its own row.
            HighlightActiveHero(null);
            RefreshPartyStatus();
        }

        private void OnCombatEnded(CombatResult result)
        {
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted -= OnHeroTurnStarted;
            CombatManager.Instance.OnSummonStarted -= OnSummonStarted;
            CombatManager.Instance.OnEnemyAbilityUsed -= OnEnemyAbilityUsed;
            SetShown(_abilityBanner, false);
            CombatManager.Instance.OnSummonEnded -= OnSummonEnded;
            SetShown(_summonBanner, false);
            CombatManager.Instance.OnTurnExecuted -= OnTurnExecuted;
            CombatManager.Instance.OnTurnOrderChanged -= OnTurnOrderChanged;
            SetShown(_heroBar, false);
            SetShown(_turnOrder, false);
            // The party window survives the fight - it is the exploring HUD now, not a combat-only
            // panel - so it keeps its rows and only drops the whose-turn-is-it highlight.
            HighlightActiveHero(null);
            RefreshPartyStatus();
            _turnOrderList?.Clear();
            _commandList?.Clear();

            switch (result.Outcome)
            {
                case CombatOutcome.Victory:
                    ShowVictory(result);
                    break;
                case CombatOutcome.PlayerDied:
                    ShowDeathScreen(result.Log);
                    break;
            }
        }

        private void ShowDeathScreen(string log)
        {
            SetShown(_combatBar, false);
            // What dying costs and what it keeps, as rows (2026-09-30) - it used to be the generic
            // detail dialog with the raw combat log dumped in it.
            // A painted scene rather than the dungeon behind a dimmed dialog: the fire is going out
            // and the gear lies where it fell. The dialog docks left so it stays in view; nothing
            // follows this screen but the hub, so neither change is ever undone.
            SetShown(_deathBackdrop, true);
            Assets.Scripts.Hub.UI.BackdropAmbienceView.AttachTo(_deathBackdrop)?.Show("cd-bg--death");
            _detailWindow?.AddToClassList("cd-window--death");
            ShowDetail(Loc.Get("room.death.title"), DefeatSummary() + FirstDeathLesson() + ReplayHint());
            _detailOk.text = Loc.Get("room.death.return");
            AddDefeatRows();
            _detailOkAction = () =>
            {
                if (DungeonManager.HasInstance)
                {
                    DungeonManager.Instance.HandlePartyDeath();
                }
                // A wipe returns to the hub like any other way out of a run - death costs the
                // floor's gains, not your place in the game.
                SceneManager.LoadScene("HubScene");
            };
            SetShown(_detailWindow, true);
        }

        /// <summary>
        /// The first wipe of a save says that dying is expected (a playtest finding: a new player read
        /// it as failure). Read before <c>HandlePartyDeath</c> counts this one, so 0 means first.
        /// </summary>
        private static string FirstDeathLesson()
        {
            if (!MetaProgressManager.HasInstance || MetaProgressManager.Instance.PartyDeaths > 0)
            {
                return string.Empty;
            }
            var tutorial = UnityEngine.Resources.Load<Tutorial.TutorialSO>(Tutorial.TutorialSO.ResourcePath);
            string text = tutorial != null ? tutorial.TextFor(Tutorial.TutorialCue.FirstDeath) : string.Empty;
            return string.IsNullOrEmpty(text) ? string.Empty : "\n\n" + text;
        }

        /// <summary>
        /// The first wipe in a run built as a wall to come back to (<c>RunDefinitionSO.ReplayHintOnWipe</c>, the
        /// Catacombs) says how to get past it: replay earlier runs for XP and gear. Once per save.
        /// </summary>
        private static string ReplayHint()
        {
            var run = DungeonManager.ActiveRun;
            // Only once the tutorial is over: until then there is no earlier run to replay.
            if (run == null || !run.ReplayHintOnWipe || !MetaProgressManager.HasInstance
                || !MetaProgressManager.Instance.TutorialFinished
                || MetaProgressManager.Instance.ReplayHintShown)
            {
                return string.Empty;
            }
            var tutorial = UnityEngine.Resources.Load<Tutorial.TutorialSO>(Tutorial.TutorialSO.ResourcePath);
            string text = tutorial != null ? tutorial.TextFor(Tutorial.TutorialCue.ReplayToGetStronger) : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            MetaProgressManager.Instance.MarkReplayHintShown();
            return "\n\n" + text;
        }

        private static string DefeatSummary()
        {
            var entry = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentLevelEntry : null;
            string where = entry != null && !string.IsNullOrEmpty(entry.LevelName) ? entry.LevelName : Loc.Get("room.death.where-default");
            return DungeonManager.ActiveRun != null
                ? Loc.Format("room.death.summary-run", ("where", where))
                : Loc.Format("room.death.summary", ("where", where));
        }

        /// <summary>Kept and lost, in the same rows the victory window uses.</summary>
        private void AddDefeatRows()
        {
            if (_detailRows == null)
            {
                return;
            }
            _detailRows.Clear();
            AddRunStoryRows();
            if (DungeonManager.ActiveRun != null)
            {
                int levels = DungeonManager.RunLevelIndex + 1;
                _detailRows.Add(MakeVictoryRow(Loc.Format("room.death.row.level-gold", ("level", levels)),
                    Loc.Format("room.reward.plus", ("amount", MetaProgressManager.GoldPerLevelOnDeath * levels))));
            }
            _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.earlier-floors"), Loc.Get("room.death.kept")));
            int pending = MetaProgressManager.HasInstance ? MetaProgressManager.Instance.PendingRunGold : 0;
            if (pending > 0)
            {
                _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.floor-gold"), Loc.Get("room.death.lost")));
            }
            _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.floor-gains"), Loc.Get("room.death.lost")));

            // Point at the way to get stronger. Nothing after a wipe used to link losing to the
            // sphere grid (revisit playtest finding 5). The XP is read off disk: saves are deferred,
            // so disk holds what survives this wipe. Worded "XP Gained", never "banked" - the owner
            // wants that word out of the game (playtest 3 finding 17).
            int unspent = UnspentPartyXp();
            _detailRows.Add(MakeVictoryRow(unspent > 0 ? Loc.Format("room.death.row.xp", ("xp", unspent)) : Loc.Get("room.death.row.grow"),
                Loc.Get("room.death.row.xp-where")));
            SetShown(_detailRows, true);
        }

        /// <summary>
        /// Where the run ended (docs/plans/POLISH_CONTENT.md §15). The "Fell to" foes and the run's
        /// kill/fight tally were cut (playtest 3 finding 17): the owner does not want them, and the
        /// extra rows pushed the Return button off the bottom of the screen.
        /// </summary>
        private void AddRunStoryRows()
        {
            if (DungeonManager.ActiveRun == null)
            {
                return;
            }
            _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.fell-on"),
                Loc.Format("room.death.row.floor", ("floor", DungeonManager.RunLevelIndex + 1), ("total", DungeonManager.ActiveRun.Levels.Count))));
        }

        /// <summary>The fielded heroes' unspent XP as it stands on disk - what a wipe leaves them.</summary>
        private static int UnspentPartyXp()
        {
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party == null)
            {
                return 0;
            }
            int total = 0;
            foreach (var hero in party.Heroes)
            {
                if (hero != null && hero.HeroSO != null)
                {
                    total += Heroes.HeroRoster.GetHeroSave(hero.HeroSO).CurrentXp;
                }
            }
            return total;
        }

        private void OnFlee()
        {
            var party = GameManager.Instance.Party;

            if (!CombatManager.Instance.CanFlee(party))
            {
                SetShown(_combatBar, false);
                ShowCombatResult(Loc.Get("room.flee.title"), Loc.Get("room.flee.nowhere"), showNormalAfter: false, returnToCombat: true);
                return;
            }

            SetShown(_combatBar, false);
            UnsubscribeDoors();

            CombatManager.Instance.Flee(party, _entryDoor, _currentRoom);
        }

        private void OnEntryDoorFlee(Door door)
        {
            OnFlee();
        }

        private void ShowCombatResult(string title, string message, bool showNormalAfter, bool returnToCombat = false)
        {
            SetShown(_combatBar, false);
            _detailNav?.Reset();
            _detailTitle.text = title;
            _detailMessage.text = message;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                if (showNormalAfter)
                {
                    _currentRoom.EnableAllDoors();
                    SettleRoom();
                    SubscribeDoors();
                }
                else if (returnToCombat)
                {
                    SetShown(_combatBar, true);
                }
            };
            SetShown(_detailWindow, true);
        }

        // ============================================================
        //  VICTORY SUMMARY (loot / XP / gold, over the battle stage)
        // ============================================================

        private void ShowVictory(CombatResult result)
        {
            SetShown(_combatBar, false);
            SetShown(_detailWindow, false);

            // Escalate the copy toward the climax: run complete > boss slain > victory. Clearing the
            // exit room is not "Level Cleared!" - the level ends on the stairs, and the level-clear
            // window says so then (playtest 2 finding 6).
            if (result.RunCompleted)
            {
                _victoryTitle.text = Loc.Get("room.victory.title.run");
            }
            else if (result.BossDefeated)
            {
                _victoryTitle.text = Loc.Get("room.victory.title.boss");
            }
            else
            {
                _victoryTitle.text = Loc.Get("room.victory.title");
            }
            _victoryRewards.Clear();

            // Gold, then XP per hero, then the loot (menu review, 2026-09-28): the two numbers every
            // fight pays first, and the drops - the part worth reading - last and at full size,
            // instead of a "Loot" header above everything with the currencies trailing under it.
            // No "(banked)" on the exit fight: its gold joins the floor's pending pool like any
            // other, banked on the stairs, which is what the HUD says (playtest 2 finding 6).
            _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.gold"), Loc.Format("room.reward.plus", ("amount", result.GoldGained))));

            // The per-hero split only when there is something to split: "shared" over a party of
            // one reads as a mistake.
            if (result.XpByHero != null && result.XpByHero.Count > 1)
            {
                _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.xp"), Loc.Format("room.victory.xp-shared", ("amount", result.XpGained))));
                foreach (var share in result.XpByHero)
                {
                    var row = MakeVictoryRow(share.Key, Loc.Format("room.reward.plus", ("amount", share.Value)));
                    row.AddToClassList("cd-victory-row--sub");
                    _victoryRewards.Add(row);
                }
            }
            else
            {
                _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.xp"), Loc.Format("room.reward.plus", ("amount", result.XpGained))));
            }

            bool anyLoot = false;
            if (result.Loot != null)
            {
                foreach (var award in result.Loot)
                {
                    if (award.IsEmpty)
                    {
                        continue;
                    }
                    if (!anyLoot)
                    {
                        _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.loot"), string.Empty));
                        anyLoot = true;
                    }
                    var loot = new VisualElement();
                    loot.AddToClassList("cd-victory-loot");
                    loot.AddToClassList("cd-victory-loot--large");
                    var icon = new VisualElement();
                    icon.AddToClassList("cd-victory-loot__icon");
                    if (award.Item.Icon != null)
                    {
                        icon.style.backgroundImage = new StyleBackground(award.Item.Icon);
                    }
                    var name = new Label(award.Quantity > 1
                        ? Loc.Format("room.reward.counted", ("name", award.Item.DisplayName), ("count", award.Quantity))
                        : award.Item.DisplayName);
                    name.AddToClassList("cd-victory-loot__name");
                    name.AddToClassList(ItemPresenter.RarityClass(award.Item.Rarity));
                    loot.Add(icon);
                    loot.Add(name);
                    _victoryRewards.Add(loot);
                }
            }
            if (!anyLoot)
            {
                _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.loot"), Loc.Get("room.victory.loot-none")));
            }

            if (result.LevelCleared)
            {
                // After the run's last boss there is no further floor - the stairs lead out.
                _victoryRewards.Add(MakeVictoryRow(result.RunCompleted ? Loc.Get("room.victory.way-out") : Loc.Get("room.victory.way-down"), Loc.Get("room.victory.way-open")));
            }

            if (_victoryRewards is ScrollView rewardsScroll)
            {
                rewardsScroll.scrollOffset = Vector2.zero;
            }
            SetShown(_victoryWindow, true);
        }

        // ============================================================
        //  DUNGEON HUD (top-left, while walking the floor)
        // ============================================================

        /// <summary>
        /// Where the party is and what the floor has paid so far: the level's name, the run and
        /// "level N of M", the gold found on this floor, and the map's explored/frontier line.
        ///
        /// <para>Added 2026-09-28 (playtest finding 7): the room floated in a black screen with no
        /// level name and no counter, so gold from an event had nowhere to show up. The gold shown is
        /// the floor's <b>pending</b> pool and says so - it is only banked on the stairs and a wipe
        /// forfeits it, which is the thing the player needs to know about it. The exploration line is
        /// <see cref="DungeonMapOps.StatusLine"/>, the floor map's own, so the HUD can never reveal
        /// more than the map (no total room count).</para>
        ///
        /// <para>Names and the exploration line change only when the party changes room, so they are
        /// rebuilt from <see cref="Show"/>; gold can change on any click (a cache, an event), so
        /// <see cref="TickHud"/> polls it and rewrites the label only when it moved.</para>
        /// </summary>
        private void RefreshHud()
        {
            if (_hud == null)
            {
                return;
            }

            var dungeon = DungeonManager.HasInstance ? DungeonManager.Instance : null;
            var entry = dungeon != null ? dungeon.CurrentLevelEntry : null;
            var run = DungeonManager.ActiveRun;

            string levelName = entry != null && !string.IsNullOrEmpty(entry.LevelName)
                ? entry.LevelName
                : dungeon != null && dungeon.CurrentLevel != null ? SpacedName(dungeon.CurrentLevel.name) : Loc.Get("room.hud.level-default");
            SetText(_hudLevel, levelName);

            string runLine = string.Empty;
            if (run != null && run.Levels.Count > 0)
            {
                string runName = !string.IsNullOrEmpty(run.DisplayName) ? run.DisplayName : run.name;
                runLine = Loc.Format("room.hud.run-line", ("run", runName), ("level", DungeonManager.RunLevelIndex + 1), ("total", run.Levels.Count));
            }
            SetText(_hudRun, runLine);
            SetShown(_hudRun, !string.IsNullOrEmpty(runLine));

            var rooms = dungeon != null ? dungeon.CurrentRooms : null;
            var model = DungeonMapOps.Build(BuildMapInputs(rooms), _currentRoom != null ? _currentRoom.RoomIndex : -1);
            string explore = DungeonMapOps.StatusLine(model);
            SetText(_hudExplore, string.IsNullOrEmpty(explore) ? Loc.Get("room.hud.map-key") : Loc.Format("room.hud.explore", ("status", explore)));

            _hudGoldShown = -1; // force the gold label to redraw
            TickHud();
        }

        /// <summary>
        /// "CollapsedCaverns" -> "Collapsed Caverns": a level with no run entry (a sandbox, a test
        /// floor) has only its asset name to show.
        /// </summary>
        private static string SpacedName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(assetName.Length + 4);
            for (int i = 0; i < assetName.Length; i++)
            {
                char c = assetName[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && char.IsLower(assetName[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Every panel the camera should keep the party's room out from under, most important first,
        /// in viewport space: the Fight/Flee bar, the HUD and the party window - only
        /// the ones that are up (playtest 2 finding 8: the bar covered a door and an enemy).
        /// </summary>
        public void GetCameraSafePanels(List<Rect> panels)
        {
            panels.Clear();
            foreach (var element in new[] { _combatBar, _hud, _partyStatus })
            {
                if (TryGetViewportRect(element, out Rect rect))
                {
                    panels.Add(rect);
                }
            }
        }

        /// <summary>
        /// An element's screen rectangle in viewport space. UI Toolkit's origin is top-left, the
        /// viewport's bottom-left. False while it is hidden or not laid out yet.
        /// </summary>
        private static bool TryGetViewportRect(VisualElement element, out Rect rect)
        {
            rect = default;
            if (element == null || element.panel == null || !IsShown(element))
            {
                return false;
            }

            Rect screen = element.panel.visualTree.worldBound;
            Rect box = element.worldBound;
            if (screen.width <= 0f || screen.height <= 0f || box.width <= 0f || float.IsNaN(box.width))
            {
                return false;
            }

            rect = Rect.MinMaxRect(
                box.xMin / screen.width,
                1f - box.yMax / screen.height,
                box.xMax / screen.width,
                1f - box.yMin / screen.height);
            return true;
        }

        /// <summary>Per frame: the HUD is up only while walking, and its gold follows the pending pool.</summary>
        private void TickHud()
        {
            if (_hud == null)
            {
                return;
            }

            bool inCombat = CombatManager.HasInstance && CombatManager.Instance.InCombat;
            bool visible = _currentRoom != null && !inCombat
                && !IsShown(_victoryWindow) && !IsShown(_levelWindow);
            if (IsShown(_hud) != visible)
            {
                SetShown(_hud, visible);
            }
            if (!visible)
            {
                return;
            }

            int gold = MetaProgressManager.HasInstance ? MetaProgressManager.Instance.PendingRunGold : 0;
            if (gold != _hudGoldShown)
            {
                _hudGoldShown = gold;
                // The "banked when you take the stairs" lesson only while the tutorial runs: on every
                // frame of every floor it was teaching text that never went away.
                bool teaching = MetaProgressManager.HasInstance && !MetaProgressManager.Instance.TutorialFinished;
                SetText(_hudGold, teaching
                    ? Loc.Format("room.hud.gold-teaching", ("amount", gold))
                    : Loc.Format("room.hud.gold", ("amount", gold)));
            }
        }

        // ============================================================
        //  LEVEL CLEARED
        // ============================================================

        /// <summary>
        /// The moment a level ends: what the floor banked, shown before the hub loads. Everything in
        /// it is already committed by <c>DungeonManager.OnDungeonCleared</c>, so it is a statement -
        /// one button, and <paramref name="onContinue"/> is the scene change.
        ///
        /// <para>Built to make every number the hub is about to show traceable (playtest finding 6):
        /// gold is split into what the floor paid and the clear bonus, because the bonus was the
        /// +30 nobody could account for, and Essence says where it is spent.</para>
        /// </summary>
        /// <param name="onContinue">Back to Town. Null hides the button: mid-run the town is not a way
        /// on (owner, 2026-10-30) - only the run's end goes home.</param>
        /// <param name="onNextLevel">Continue: straight down to the next level. Null hides the button,
        /// leaving the town as the only way on (the run's last level).</param>
        public void ShowLevelCleared(LevelClearSummary summary, Action onContinue, Action onNextLevel = null)
        {
            if (!EnsureRefs() || _levelWindow == null || summary == null)
            {
                (onNextLevel ?? onContinue)?.Invoke();
                return;
            }

            // Nothing else is live now: the level is over, and a door clicked behind the window
            // would walk a party that no longer has a floor.
            UnsubscribeDoors();
            SetShown(_combatBar, false);
            SetShown(_detailWindow, false);
            SetShown(_eventWindow, false);
            SetShown(_victoryWindow, false);
            SetShown(_bossBanner, false);

            _levelContinueAction = onContinue;
            _levelNextLevelAction = onNextLevel;
            SetShown(_levelNextLevel, onNextLevel != null);
            SetShown(_levelContinue, onContinue != null);
            // Continue is the default when it is offered: a player clearing a run level by level
            // should not have to reach for the arrows every time.
            SetLevelChoice(onNextLevel != null);
            _levelTitle.text = summary.RunCompleted ? Loc.Get("room.level-clear.title.run") : Loc.Get("room.level-clear.title");
            SetText(_levelSubtitle, LevelClearSubtitle(summary));

            FillLevelClearBody(summary);

            SetText(_levelNext, summary.RunCompleted
                ? Loc.Get("room.level-clear.next.done")
                : string.IsNullOrEmpty(summary.NextLevelName)
                    ? Loc.Get("room.level-clear.next.story-map")
                    : onNextLevel != null
                        ? Loc.Format("room.level-clear.next.continue", ("level", summary.NextLevelName))
                        : Loc.Format("room.level-clear.next.from-map", ("level", summary.NextLevelName)));

            _levelBody.scrollOffset = Vector2.zero;
            SetShown(_levelWindow, true);
            FocusRoot();
        }

        private static string LevelClearSubtitle(LevelClearSummary summary)
        {
            if (summary.LevelCount <= 0)
            {
                return summary.LevelName ?? string.Empty;
            }
            string level = string.IsNullOrEmpty(summary.LevelName)
                ? Loc.Format("room.level-clear.subtitle.level", ("number", summary.LevelNumber), ("count", summary.LevelCount))
                : Loc.Format("room.level-clear.subtitle.named", ("name", summary.LevelName), ("number", summary.LevelNumber), ("count", summary.LevelCount));
            return string.IsNullOrEmpty(summary.RunName) ? level : Loc.Format("room.level-clear.subtitle.run", ("run", summary.RunName), ("level", level));
        }

        /// <summary>
        /// The window's body, most telling first (2026-10-22 redesign): a newcomer, then the totals as
        /// tiles, then each hero's XP with a portrait, then the finds as an icon grid. The single list
        /// it replaced pushed XP and newcomers below the scroll fold on any floor that dropped much.
        /// </summary>
        private void FillLevelClearBody(LevelClearSummary summary)
        {
            _levelJoined?.Clear();
            foreach (var hero in summary.Joined)
            {
                var row = new VisualElement();
                row.AddToClassList("cd-lc-joined__row");
                row.Add(Portrait(hero.Icon, "cd-lc-joined__portrait"));
                var text = new Label(Loc.Format("room.level-clear.joined", ("hero", hero.Label)));
                text.AddToClassList("cd-lc-joined__text");
                row.Add(text);
                _levelJoined?.Add(row);
            }

            int xpTotal = 0;
            foreach (var line in summary.Xp)
            {
                xpTotal += line.Amount;
            }
            _levelTallies?.Clear();
            AddTally("cd-lc-tally--gold", Loc.Format("room.reward.plus", ("amount", summary.GoldTotal)), Loc.Get("room.level-clear.gold"),
                Loc.Format("room.level-clear.gold-note", ("found", summary.GoldFound), ("bonus", summary.GoldBonus)));
            AddTally("cd-lc-tally--xp", Loc.Format("room.reward.plus", ("amount", xpTotal)), Loc.Get("room.level-clear.xp"), Loc.Get("room.level-clear.xp-note"));
            // Only a revisit pays Essence; a "+0" tile on every first clear would only confuse.
            if (summary.Essence > 0)
            {
                AddTally("cd-lc-tally--essence", Loc.Format("room.reward.plus", ("amount", summary.Essence)), Loc.Get("room.level-clear.essence"), Loc.Get("room.level-clear.essence-note"));
            }

            _levelXp?.Clear();
            SetShown(_levelXpHeading, summary.Xp.Count > 0);
            foreach (var line in summary.Xp)
            {
                var card = new VisualElement();
                card.AddToClassList("cd-lc-hero");
                card.Add(Portrait(line.Icon, "cd-lc-hero__portrait"));
                var text = new VisualElement();
                text.AddToClassList("cd-lc-hero__text");
                var name = new Label(line.Label);
                name.AddToClassList("cd-lc-hero__name");
                text.Add(name);
                var xp = new Label(Loc.Format("room.level-clear.hero-xp", ("amount", line.Amount)));
                xp.AddToClassList("cd-lc-hero__xp");
                text.Add(xp);
                card.Add(text);
                _levelXp?.Add(card);
            }

            // Materials first: they gate buildings, and they are what a floor is for finding.
            _levelLoot?.Clear();
            AddLootCells(summary.Materials);
            AddLootCells(summary.Items);
            if (summary.Materials.Count == 0 && summary.Items.Count == 0)
            {
                var none = new Label(Loc.Get("room.level-clear.no-loot"));
                none.AddToClassList("cd-lc-empty");
                _levelLoot?.Add(none);
            }
        }

        private void AddTally(string kindClass, string value, string label, string note)
        {
            var tile = new VisualElement();
            tile.AddToClassList("cd-lc-tally");
            tile.AddToClassList(kindClass);
            var v = new Label(value);
            v.AddToClassList("cd-lc-tally__value");
            tile.Add(v);
            var l = new Label(label);
            l.AddToClassList("cd-lc-tally__label");
            tile.Add(l);
            var n = new Label(note);
            n.AddToClassList("cd-lc-tally__note");
            tile.Add(n);
            _levelTallies?.Add(tile);
        }

        private void AddLootCells(List<LevelClearSummary.Line> lines)
        {
            foreach (var line in lines)
            {
                var cell = new VisualElement();
                cell.AddToClassList("cd-lc-item");
                cell.Add(Portrait(line.Icon, "cd-lc-item__icon"));
                var name = new Label(line.Label);
                name.AddToClassList("cd-lc-item__name");
                if (line.Item != null)
                {
                    name.AddToClassList(ItemPresenter.RarityClass(line.Item.Rarity));
                }
                cell.Add(name);
                var count = new Label(Loc.Format("room.level-clear.loot-count", ("count", line.Amount)));
                count.AddToClassList("cd-lc-item__count");
                cell.Add(count);
                _levelLoot?.Add(cell);
            }
        }

        private static VisualElement Portrait(Sprite sprite, string className)
        {
            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList(className);
            if (sprite != null)
            {
                image.style.backgroundImage = new StyleBackground(sprite);
            }
            return image;
        }

        private void OnLevelClearContinue()
        {
            var action = _levelContinueAction;
            _levelContinueAction = null;
            _levelNextLevelAction = null;
            SetShown(_levelWindow, false);
            action?.Invoke();
        }

        private void OnLevelClearNextLevel()
        {
            var action = _levelNextLevelAction;
            if (action == null)
            {
                return;
            }
            _levelContinueAction = null;
            _levelNextLevelAction = null;
            SetShown(_levelWindow, false);
            action.Invoke();
        }

        /// <summary>Moves the keyboard highlight between Back to Town and Continue.</summary>
        private void SetLevelChoice(bool next)
        {
            _levelChoiceIsNext = next;
            _levelNextLevel?.EnableInClassList("cd-nav--selected", next);
            _levelContinue?.EnableInClassList("cd-nav--selected", !next);
        }

        private VisualElement MakeVictoryRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-victory-row");
            var l = new Label(label);
            l.AddToClassList("cd-victory-row__label");
            var v = new Label(value);
            v.AddToClassList("cd-victory-row__value");
            row.Add(l);
            row.Add(v);
            return row;
        }

        private void OnVictoryContinue()
        {
            SetShown(_victoryWindow, false);
            CombatManager.Instance.FinishVictory();
            ShowPartyStatusOutOfCombat();
            // Always back to the room, even after clearing the exit: the level completes when the
            // player takes the stairs.
            SettleRoom();
            SubscribeDoors();
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
            // so it is also the state in which the arrow keys point at them.
            _doorsLive = _navDoors.Count > 0;
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

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
