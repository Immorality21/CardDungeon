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
    public partial class RoomActionUI : MonoBehaviour
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
        private Button _pausePotions;
        private UI.PotionWindow _potions;
        private bool _potionsFromPause;
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
            _pausePotions = root.Q<Button>("pause-potions");
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
            if (_pausePotions != null)
            {
                _pausePotions.clicked += () => OpenPotions(fromPause: true);
            }
            var potionRoot = root.Q<VisualElement>("potion-window");
            if (potionRoot != null)
            {
                _potions = new UI.PotionWindow(potionRoot);
                _potions.Closed += OnPotionsClosed;
                _potions.Drank += OnPotionDrunk;
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
            _potions?.Close();
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
    }
}
