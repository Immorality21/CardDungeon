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
        private VisualElement _mainBar;
        private VisualElement _combatBar;
        private VisualElement _heroBar;
        private VisualElement _detailWindow;
        private VisualElement _eventWindow;
        private VisualElement _partyStatus;
        private VisualElement _partyStatusRows;

        private readonly Dictionary<Heroes.Hero, VisualElement> _partyRows = new Dictionary<Heroes.Hero, VisualElement>();
        private readonly Dictionary<Heroes.Hero, Label> _partyHpLabels = new Dictionary<Heroes.Hero, Label>();

        private Label _heroTitle;
        private Label _detailTitle;
        private Label _detailMessage;

        private Label _eventTitle;
        private Label _eventPrompt;
        private Label _eventOdds;
        private ScrollView _eventOptions;
        private Button _eventBack;

        private Button _actionBtn;
        private Button _searchBtn;
        private Button _restBtn;
        private Button _rescueBtn;
        private Button _descendBtn;
        private Button _fightBtn;
        private Button _fleeBtn;
        private Button _detailOk;
        private Button _detailCancel;

        private VisualElement _bossBanner;
        private Label _bossBannerName;

        private VisualElement _commandList;
        private VisualElement _turnOrder;
        private VisualElement _turnOrderList;

        private Label _navHint;

        private VisualElement _victoryWindow;
        private Label _victoryTitle;
        private VisualElement _victoryRewards;
        private Button _victoryContinue;

        private bool _refsReady;
        private Action _detailOkAction;
        private Action _detailCancelAction;

        private ICombatUnit _currentHeroTurn;
        private Room _currentRoom;
        private Door _entryDoor;

        // Room-event state, live only while the event window is up.
        private readonly Events.RoomEventRunner _eventRunner = new Events.RoomEventRunner();
        private Events.RoomEventSO _currentEvent;
        private Heroes.Hero _eventActingHero;
        private float _eventChance;

        // Cursor-driven command menu (FFX-style selection list).
        private enum HeroCommand { Attack, Magic, Item, Inspect, Skip }

        private struct CommandEntry
        {
            public HeroCommand Command;
            public string Label;
            public bool Enabled;
        }

        private readonly List<CommandEntry> _commands = new List<CommandEntry>();
        private readonly List<VisualElement> _commandRows = new List<VisualElement>();
        private readonly List<Label> _commandCursors = new List<Label>();
        private int _selectedCommand;

        // Walking the dungeon from the keyboard: the arrows point at doors, Tab reaches the room bar.
        private readonly List<Door> _navDoors = new List<Door>();
        private readonly List<Vector2> _navDoorPoints = new List<Vector2>();
        private Door _selectedDoor;
        private bool _doorsLive;
        private KeyboardNavigator _barNav;
        private KeyboardNavigator _combatNav;
        private KeyboardNavigator _eventNav;

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
            _root = root;

            _mainBar = root.Q<VisualElement>("main-bar");
            _combatBar = root.Q<VisualElement>("combat-bar");
            _heroBar = root.Q<VisualElement>("hero-bar");
            _detailWindow = root.Q<VisualElement>("detail-window");
            _eventWindow = root.Q<VisualElement>("event-window");
            _partyStatus = root.Q<VisualElement>("party-status");
            _partyStatusRows = root.Q<VisualElement>("party-status-rows");
            _commandList = root.Q<VisualElement>("command-list");
            _turnOrder = root.Q<VisualElement>("turn-order");
            _turnOrderList = root.Q<VisualElement>("turn-order-list");
            _victoryWindow = root.Q<VisualElement>("victory-window");
            _victoryTitle = root.Q<Label>("victory-title");
            _victoryRewards = root.Q<VisualElement>("victory-rewards");
            _victoryContinue = root.Q<Button>("victory-continue");
            _navHint = root.Q<Label>("nav-hint");

            _heroTitle = root.Q<Label>("hero-title");
            _detailTitle = root.Q<Label>("detail-title");
            _detailMessage = root.Q<Label>("detail-message");

            _eventTitle = root.Q<Label>("event-title");
            _eventPrompt = root.Q<Label>("event-prompt");
            _eventOdds = root.Q<Label>("event-odds");
            _eventOptions = root.Q<ScrollView>("event-options");
            _eventBack = root.Q<Button>("event-back");

            _actionBtn = root.Q<Button>("action-btn");
            _searchBtn = root.Q<Button>("search-btn");
            _restBtn = root.Q<Button>("rest-btn");
            _rescueBtn = root.Q<Button>("rescue-btn");
            _descendBtn = root.Q<Button>("descend-btn");
            _fightBtn = root.Q<Button>("fight-btn");
            _fleeBtn = root.Q<Button>("flee-btn");
            _detailOk = root.Q<Button>("detail-ok");
            _detailCancel = root.Q<Button>("detail-cancel");

            _bossBanner = root.Q<VisualElement>("boss-banner");
            _bossBannerName = root.Q<Label>("boss-banner-name");

            _actionBtn.clicked += OnAction;
            if (_searchBtn != null)
            {
                _searchBtn.clicked += OnSearch;
            }
            if (_restBtn != null)
            {
                _restBtn.clicked += OnRest;
            }
            if (_rescueBtn != null)
            {
                _rescueBtn.clicked += OnRescue;
            }
            if (_descendBtn != null)
            {
                _descendBtn.clicked += OnDescend;
            }
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

            // Strip focusability from every focusable descendant so UI Toolkit's arrow-key
            // navigation has nowhere to move focus — keyboard focus stays on the root and our
            // cursor nav keeps receiving keys. (Buttons stay clickable + hotkey-driven.)
            foreach (var focusable in new Focusable[] { _actionBtn, _searchBtn, _restBtn, _rescueBtn, _descendBtn, _fightBtn, _fleeBtn, _detailOk, _detailCancel, _victoryContinue, _eventBack, _eventOptions })
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

            // Tab-driven cursor for the room bar, so Search/Rest/Rescue/Descend are reachable without
            // the mouse while the arrow keys stay free for the doors. Scoped to the bar itself: it
            // must never wander onto a combat button.
            _barNav = new KeyboardNavigator(_mainBar);

            // Fight/Flee is a bar like any other. It kept its F/R letters, but a player who has just
            // learnt that arrows-and-Enter drive the room, the command menu and the whole hub should
            // not have to discover that this one bar is different.
            _combatNav = new KeyboardNavigator(_combatBar);

            // The event window builds its options at runtime, so its cursor navigates whatever is on
            // it rather than a fixed list. Escape leaves by its own Back button, so the keyboard route
            // out runs the same teardown a click does.
            _eventNav = new KeyboardNavigator(_eventWindow);
            _eventNav.Cancelled += () =>
            {
                if (IsShown(_eventBack))
                {
                    KeyboardNavigator.Press(_eventBack);
                }
            };

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

            bool hasEnemy = room.Enemies.Any(e => e != null && e.IsAlive);
            SetShown(_combatBar, hasEnemy);
            _combatNav?.Reset();

            // A captive is only reachable once the room is clear - guards first. With enemies up the
            // room bar is irrelevant anyway; without them, ShowMainBar decides whether there is
            // anything to show.
            if (hasEnemy)
            {
                SetShown(_mainBar, false);
                RefreshRescueButton();
                RefreshActionButton();
                RefreshPayloadButtons();
                RefreshDescendButton();
            }
            else
            {
                ShowMainBar();
            }

            // Boss rooms: announce the boss and remove Flee — the climax can't be skipped.
            var boss = room.Enemies.FirstOrDefault(e => e != null && e.IsAlive && e.IsBoss);
            bool isBossRoom = hasEnemy && boss != null;
            SetShown(_fleeBtn, hasEnemy && !isBossRoom);
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
            SetShown(_mainBar, false);
            SetShown(_combatBar, false);
            SetShown(_bossBanner, false);
            SetShown(_heroBar, false);
            SetShown(_detailWindow, false);
            SetShown(_eventWindow, false);
            SetShown(_partyStatus, false);
            SetShown(_turnOrder, false);
            SetShown(_victoryWindow, false);
        }

        // ============================================================
        //  EXAMINE / ACTION FLOWS
        // ============================================================

        /// <summary>
        /// Action is the verb that costs something, so it only exists when there is something to
        /// spend it on: the room's event. <see cref="RefreshActionButton"/> hides it otherwise
        /// rather than offering a dead affordance.
        /// </summary>
        private void OnAction()
        {
            var roomEvent = PendingEvent();
            if (roomEvent == null)
            {
                RefreshActionButton();
                return;
            }

            SetShown(_mainBar, false);
            ShowRoomEvent(roomEvent);
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
            for (int i = 0; i < roomEvent.Options.Count; i++)
            {
                var option = roomEvent.Options[i];
                if (option == null || string.IsNullOrEmpty(option.Label))
                {
                    continue;
                }

                int captured = i;
                var btn = new Button(() => OnEventOptionChosen(captured)) { text = option.Label };
                btn.AddToClassList("cd-list-button");
                btn.focusable = false;
                _eventOptions.Add(btn);
            }

            _eventNav?.Reset();
            SetShown(_eventWindow, true);
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
                return $"Anything you chance here turns on {statName}. {reading}";
            }

            return $"Anything you chance here turns on {statName} - "
                   + $"{_eventActingHero.DisplayName} has the best of it. {reading}";
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
            SetShown(_eventWindow, false);

            if (option.Kind == Events.RoomEventOptionKind.Decline)
            {
                // Walking away leaves the event unconsumed - the choice is deferred, not spent.
                string declineText = string.IsNullOrEmpty(option.DeclineText)
                    ? "You leave it be."
                    : option.DeclineText;
                ShowDetail(_currentEvent.Title, declineText);
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

            var report = _eventRunner.Apply(
                outcome,
                succeeded,
                _currentRoom,
                GameManager.Instance.Party,
                _eventActingHero,
                DungeonManager.HasInstance ? DungeonManager.Instance.Afflictions : null);

            _currentRoom.MarkEventResolved(optionIndex, outcomeIndex, succeeded);
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.Save(_currentRoom);
            }

            ShowEventResult(report);
        }

        private void ShowEventResult(Events.RoomEventOutcomeReport report)
        {
            string message = string.IsNullOrEmpty(report.Text) ? "Nothing comes of it." : report.Text;
            if (report.Lines.Count > 0)
            {
                message += "\n\n" + string.Join("\n", report.Lines);
            }

            ShowDetail(_currentEvent.Title, message);

            // A woken room is a different room: re-show it so the Fight/Flee bar replaces the
            // Examine/Action one, rather than leaving the player among live enemies with
            // non-combat buttons.
            bool woken = report.SpawnedEnemies;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                _currentEvent = null;
                _eventActingHero = null;

                if (woken)
                {
                    Show(_currentRoom, _entryDoor);
                }
                else
                {
                    ShowMainBar();
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
                _detailOk.text = "Ok";
                SetShown(_detailWindow, false);
                onConfirm();
            };
            _detailCancelAction = () =>
            {
                _detailOk.text = "Ok";
                SetShown(_detailWindow, false);
                ShowMainBar();
            };
            SetShown(_detailCancel, true);
        }

        private void CloseEventResult()
        {
            SetShown(_detailWindow, false);
            _currentEvent = null;
            _eventActingHero = null;
            ShowMainBar();
        }

        private void OnEventBack()
        {
            _eventNav?.Reset();
            SetShown(_eventWindow, false);
            _eventOptions.Clear();
            _currentEvent = null;
            _eventActingHero = null;
            ShowMainBar();
        }

        /// <summary>
        /// A statement: one button, and dismissing it returns to the room. Callers that need a second
        /// beat overwrite <c>_detailOkAction</c> after calling this.
        /// </summary>
        private void ShowDetail(string title, string message)
        {
            SetShown(_detailCancel, false);
            _detailCancelAction = null;
            _detailTitle.text = title;
            _detailMessage.text = message;
            // Default: dismissing returns to the room. Callers that need a second beat (the rescue
            // flow, an event outcome) overwrite _detailOkAction after calling this.
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                ShowMainBar();
            };
            SetShown(_detailWindow, true);
        }

        /// <summary>
        /// Takes the stairs: this is the one place a level is completed. Confirmed first, because it
        /// ends the level and abandons anything still unspent on it.
        /// </summary>
        private void OnDescend()
        {
            if (_currentRoom == null || !_currentRoom.IsExit)
            {
                RefreshDescendButton();
                return;
            }

            SetShown(_mainBar, false);
            ShowConfirm("The Way Down",
                "The stairs drop away into the dark below.\n\nAnything still unfound on this level "
                + "stays here.",
                "Descend",
                () => CombatManager.Instance.NotifyDungeonCleared());
        }

        /// <summary>
        /// Frees the captive in this room. Two beats on purpose: the first panel introduces who they
        /// are (the player has only seen a tinted portrait), the second confirms they have joined -
        /// so a permanent reward is not a single unread popup.
        /// </summary>
        private void OnRescue()
        {
            var captive = _currentRoom != null ? _currentRoom.CaptiveHero : null;
            if (captive == null)
            {
                SetShown(_rescueBtn, false);
                return;
            }

            SetShown(_mainBar, false);

            string blurb = string.IsNullOrEmpty(captive.Blurb)
                ? "They look ready to fight."
                : captive.Blurb;

            ShowConfirm("A Prisoner",
                $"{captive.DisplayName} is bound here. {blurb}",
                "Free them",
                () =>
            {
                if (!DungeonManager.Instance.TryRescueCaptive(_currentRoom))
                {
                    ShowMainBar();
                    SetShown(_rescueBtn, false);
                    return;
                }

                SetShown(_rescueBtn, false);
                if (FloatingTextHandler.HasInstance)
                {
                    FloatingTextHandler.Instance.CreateFloatingText(
                        GameManager.Instance.Party.transform.position,
                        $"{captive.DisplayName} joined!",
                        Color.cyan);
                }

                ShowDetail($"{captive.DisplayName} joins you",
                    $"{captive.DisplayName} takes up arms alongside the party.\n\n"
                    + "They are yours for good once this level is cleared - fall here and they are "
                    + "lost with the rest of the run.");
                _detailOkAction = () =>
                {
                    SetShown(_detailWindow, false);
                    ShowMainBar();
                    RefreshPartyStatus();
                };
            });
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
                RefreshPayloadButtons();
                return;
            }

            SetShown(_mainBar, false);

            var lines = new List<string>();

            int gold = RoomKindRewards.TreasureGold(DungeonManager.RunLevelIndex);
            if (gold > 0)
            {
                // Pending, not banked: cache gold is forfeited on death exactly like a kill's, so
                // carrying it out of the level is the reward.
                MetaProgressManager.Instance.AddPendingGold(gold);
                lines.Add($"+{gold} gold.");
            }

            var item = RollTreasureItem();
            if (item != null && InventoryManager.HasInstance)
            {
                InventoryManager.Instance.AddItem(item);
                lines.Add($"Found: {item.DisplayName}.");
            }
            else
            {
                lines.Add("Nothing else worth carrying.");
            }

            TakePayload();

            if (FloatingTextHandler.HasInstance && GameManager.Instance.Party != null)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    GameManager.Instance.Party.transform.position, $"+{gold} gold", new Color(1f, 0.85f, 0.25f));
            }

            ShowDetail("A Cache", "Coin and oddments, stashed here and forgotten.\n\n"
                + string.Join("\n", lines));
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
        /// Takes the refuge: heals every hero a share of their bar, once. Confirmed, because resting
        /// at full health throws the whole thing away - that timing is the decision the room poses,
        /// and the party's current state is in the prompt so the player can make it.
        /// </summary>
        private void OnRest()
        {
            if (_currentRoom == null || !_currentRoom.HasPendingPayload
                || _currentRoom.Kind != RoomKind.Rest)
            {
                RefreshPayloadButtons();
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

            SetShown(_mainBar, false);

            string state = missing > 0
                ? $"The party is down {missing} health between them."
                : "Nobody here is hurt - resting now wastes the place.";

            ShowConfirm("A Refuge",
                $"Dry ground, and nothing watching. {state}\n\n"
                + $"Resting restores {Mathf.RoundToInt(RoomKindRewards.RestHealFraction * 100f)}% of "
                + "each hero's health and refills every spell charge. It can only be done once.",
                "Rest",
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
                    int healed = Mathf.Min(
                        RoomKindRewards.RestHealAmount(max), Mathf.Max(0, max - hero.Stats.Health));
                    hero.Stats.Health += healed;
                    lines.Add(healed > 0
                        ? $"{hero.DisplayName} recovers {healed} health."
                        : $"{hero.DisplayName} was already whole.");
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
                lines.Add("Spell charges are restored.");
            }

            TakePayload();

            if (FloatingTextHandler.HasInstance && party != null)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    party.transform.position, "Rested", Color.green);
            }

            ShowDetail("A Refuge", "You make camp long enough to bind what can be bound.\n\n"
                + string.Join("\n", lines));
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
            ShowMainBar();
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
            _heroTitle.text = $"{hero.DisplayName}'s Turn";
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

            var slots = DungeonManager.Instance.MagicState.GetSlots(heroComponent.HeroKey);
            CombatManager.Instance.RequestMagicSlots(_currentHeroTurn, slots);
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

            bool hasMagic = false;
            var heroComponent = hero as Heroes.Hero;
            if (heroComponent != null && DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                hasMagic = DungeonManager.Instance.MagicState.HasAnyCastable(heroComponent.HeroKey);
            }

            // Silence gates casting and nothing else - Attack, Item and Inspect stay open, so a
            // silenced hero still has a turn worth taking rather than three turns of Skip.
            bool silenced = CombatManager.Instance.BuffTracker != null
                && CombatManager.Instance.BuffTracker.HasStatusEffect(hero, BuffType.Silenced);
            if (silenced)
            {
                hasMagic = false;
            }

            bool hasItem = InventoryManager.HasInstance && InventoryManager.Instance.HasAnyConsumable();

            _commands.Add(new CommandEntry { Command = HeroCommand.Attack, Label = "Attack", Enabled = true });
            _commands.Add(new CommandEntry { Command = HeroCommand.Magic, Label = "Magic", Enabled = hasMagic });
            _commands.Add(new CommandEntry { Command = HeroCommand.Item, Label = "Item", Enabled = hasItem });
            // Inspect is free - it opens a page and hands the turn straight back - so it sits above
            // Skip rather than among the actions that spend the turn.
            _commands.Add(new CommandEntry { Command = HeroCommand.Inspect, Label = "Inspect", Enabled = true });
            _commands.Add(new CommandEntry { Command = HeroCommand.Skip, Label = "Skip", Enabled = true });

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
            InvokeCommand(_commands[_selectedCommand].Command);
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

        /// <summary>Direct letter-key shortcut: acts only if that command is currently enabled.</summary>
        private void InvokeCommand(HeroCommand command)
        {
            switch (command)
            {
                case HeroCommand.Attack:
                    OnHeroAttack();
                    break;
                case HeroCommand.Magic:
                    OnHeroMagic();
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

            int shown = 0;
            for (int i = 0; i < order.Count && shown < 8; i++)
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
                }

                var icon = new VisualElement();
                icon.AddToClassList("cd-turn-row__icon");
                if (unit.Icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(unit.Icon);
                }

                var name = new Label(unit.DisplayName);
                name.AddToClassList("cd-turn-row__name");

                row.Add(icon);
                row.Add(name);
                _turnOrderList.Add(row);
                shown++;
            }
        }

        /// <summary>
        /// The whole keyboard for this panel, dispatched by which bar is up: Fight/Flee on the combat
        /// bar, the hero command cursor and its letter shortcuts on the command bar, and otherwise the
        /// dungeon keys - arrows for doors, Tab for the room bar. A key only ever acts when its bar is
        /// visible, and only invokes actions whose buttons are currently shown (Magic and Item
        /// appear conditionally), so a hotkey can never do something the on-screen menu cannot.
        /// </summary>
        private void OnCombatHotkey(KeyDownEvent evt)
        {
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

        /// <summary>Whether a window is stacked over the room, dialog-style.</summary>
        private bool IsDialogUp()
        {
            return IsShown(_victoryWindow) || IsShown(_eventWindow) || IsShown(_detailWindow);
        }

        /// <summary>
        /// The keyboard for the windows that stack over the room. Without these a player who searched
        /// a cache or opened an event with the keyboard would have to reach for the mouse to get out
        /// of the dialog, which defeats the point of the rest of it.
        /// </summary>
        private bool HandleDialogKey(KeyDownEvent evt)
        {
            if (IsShown(_victoryWindow))
            {
                // Nothing to choose on a spoils screen, so every confirm or cancel key dismisses it.
                switch (evt.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Space:
                    case KeyCode.Escape:
                    case KeyCode.Backspace:
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
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Space:
                        _detailOkAction?.Invoke();
                        return true;
                    case KeyCode.Escape:
                    case KeyCode.Backspace:
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
        private void Update()
        {
            if (!_refsReady)
            {
                return;
            }

            // Clicking anything that is not UI - a door, a wall, the floor - clears the EventSystem's
            // selection and with it the keyboard. Re-claiming here costs a null check and means the
            // arrows cannot go dead halfway across a floor the player is walking with the mouse.
            PanelKeyboard.Claim();

            // Fight/Flee is a question, so it carries its cursor from the moment it appears - the
            // command menu already does, and Enter should not need an arrow press to wake it up.
            // Armed here rather than where the bar is shown because on that frame the bar's resolved
            // style is still stale and there would be nothing to select.
            if (IsShown(_combatBar))
            {
                _combatNav?.SelectFirst();
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
                return "← → choose · Enter confirm";
            }

            if (IsShown(_heroBar))
            {
                return "↑↓ choose · Enter confirm";
            }

            if (DoorNavActive())
            {
                return "Arrows pick a door · Enter walk through · Tab room actions";
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
            return IsShown(_heroBar) || IsShown(_combatBar) || IsDialogUp() || DoorNavActive();
        }

        /// <summary>
        /// Whether the arrow keys currently mean "pick a door". Doors being subscribed is the same
        /// condition as being able to walk through one, but a dialog on top of the room owns the
        /// keyboard while it is up - an arrow key must not move the party out from under an open
        /// event window.
        /// </summary>
        private bool DoorNavActive()
        {
            return _doorsLive
                && !IsShown(_combatBar) && !IsShown(_heroBar)
                && !IsShown(_detailWindow) && !IsShown(_eventWindow) && !IsShown(_victoryWindow);
        }

        /// <summary>
        /// The out-of-combat keyboard: arrows point at a door, Enter walks through it, and Tab reaches
        /// the room bar instead. Two cursors share Enter, and the split is unambiguous because it is
        /// the last thing the player touched that decides - an arrow key always hands Enter back to
        /// the doors.
        /// </summary>
        private bool HandleDungeonKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveDoorCursor(new Vector2(0f, 1f));
                    return true;
                case KeyCode.DownArrow:
                    MoveDoorCursor(new Vector2(0f, -1f));
                    return true;
                case KeyCode.LeftArrow:
                    MoveDoorCursor(new Vector2(-1f, 0f));
                    return true;
                case KeyCode.RightArrow:
                    MoveDoorCursor(new Vector2(1f, 0f));
                    return true;
                case KeyCode.Tab:
                    return _barNav != null && _barNav.HandleKey(evt);
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    if (_barNav != null && _barNav.HasSelection)
                    {
                        return _barNav.HandleKey(evt);
                    }
                    return ConfirmDoor();
            }

            return false;
        }

        /// <summary>
        /// Moves the door cursor. World space, so "up" is +y - unlike the UI cursors, which run in UI
        /// Toolkit's y-down space.
        ///
        /// <para>With no door picked yet the arrow is measured from where the party is standing rather
        /// than from the room's centre, so the first press means "the door that way from us". If
        /// nothing lies that way at all, the nearest door is taken instead: a first arrow press that
        /// appears to do nothing reads as the feature being missing.</para>
        /// </summary>
        private void MoveDoorCursor(Vector2 direction)
        {
            _barNav?.Reset();

            _navDoorPoints.Clear();
            int from = -1;
            for (int i = 0; i < _navDoors.Count; i++)
            {
                _navDoorPoints.Add(_navDoors[i].transform.position);
                if (_navDoors[i] == _selectedDoor)
                {
                    from = i;
                }
            }
            if (_navDoorPoints.Count == 0)
            {
                return;
            }

            if (from >= 0)
            {
                int next = DirectionalNav.PickInDirection(_navDoorPoints, from, direction);
                if (next >= 0)
                {
                    SetDoorCursor(_navDoors[next]);
                }
                return;
            }

            var origin = PartyPosition();
            int target = DirectionalNav.PickInDirection(_navDoorPoints, origin, direction);
            SetDoorCursor(_navDoors[target >= 0 ? target : NearestDoorIndex(origin)]);
        }

        private Vector2 PartyPosition()
        {
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party != null)
            {
                return party.transform.position;
            }
            return _currentRoom != null ? (Vector2)_currentRoom.GetCenter() : Vector2.zero;
        }

        private int NearestDoorIndex(Vector2 origin)
        {
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < _navDoorPoints.Count; i++)
            {
                float distance = (_navDoorPoints[i] - origin).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        private void SetDoorCursor(Door door)
        {
            if (_selectedDoor == door)
            {
                return;
            }
            if (_selectedDoor != null)
            {
                _selectedDoor.SetHighlighted(false);
            }
            _selectedDoor = door;
            if (_selectedDoor != null)
            {
                _selectedDoor.SetHighlighted(true);
            }
        }

        private void ClearDoorCursor()
        {
            SetDoorCursor(null);
        }

        /// <summary>Walks through the door under the cursor - the same path a click on it takes.</summary>
        private bool ConfirmDoor()
        {
            var door = _selectedDoor;
            if (door == null)
            {
                return false;
            }

            ClearDoorCursor();
            OnDoorSelected(door);
            return true;
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

        private void BuildPartyStatus(Heroes.Party party)
        {
            _partyStatusRows?.Clear();
            _partyRows.Clear();
            _partyHpLabels.Clear();
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

                var name = new Label(hero.DisplayName);
                name.AddToClassList("cd-party-row__name");
                var hp = new Label();
                hp.AddToClassList("cd-party-row__hp");

                row.Add(name);
                row.Add(hp);
                _partyStatusRows.Add(row);
                _partyRows[hero] = row;
                _partyHpLabels[hero] = hp;
            }

            RefreshPartyStatus();
        }

        private void RefreshPartyStatus()
        {
            foreach (var pair in _partyHpLabels)
            {
                var hero = pair.Key;
                var hp = pair.Value;
                if (hero == null || hp == null)
                {
                    continue;
                }
                hp.text = $"HP {hero.Stats.Health}/{hero.GetEffectiveMaxHealth()}";
                if (_partyRows.TryGetValue(hero, out var row) && row != null)
                {
                    row.EnableInClassList("cd-party-row--dead", !hero.IsAlive);
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

        private void OnTurnExecuted(string log)
        {
            RefreshPartyStatus();
        }

        private void OnCombatEnded(CombatResult result)
        {
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted -= OnHeroTurnStarted;
            CombatManager.Instance.OnTurnExecuted -= OnTurnExecuted;
            CombatManager.Instance.OnTurnOrderChanged -= OnTurnOrderChanged;
            SetShown(_heroBar, false);
            SetShown(_partyStatus, false);
            SetShown(_turnOrder, false);
            _partyStatusRows?.Clear();
            _partyRows.Clear();
            _partyHpLabels.Clear();
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
            SetShown(_mainBar, false);
            SetShown(_combatBar, false);
            _detailTitle.text = "Your Party Has Fallen...";
            _detailMessage.text = log;
            _detailOkAction = () =>
            {
                if (DungeonManager.HasInstance)
                {
                    DungeonManager.Instance.HandlePartyDeath();
                }
                SceneManager.LoadScene("MenuScene");
            };
            SetShown(_detailWindow, true);
        }

        private void OnFlee()
        {
            var party = GameManager.Instance.Party;

            if (!CombatManager.Instance.CanFlee(party))
            {
                SetShown(_combatBar, false);
                ShowCombatResult("Flee", "Nowhere to flee!", showNormalAfter: false, returnToCombat: true);
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
            SetShown(_mainBar, false);
            SetShown(_combatBar, false);
            _detailTitle.text = title;
            _detailMessage.text = message;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                if (showNormalAfter)
                {
                    _currentRoom.EnableAllDoors();
                    ShowMainBar();
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
            SetShown(_mainBar, false);
            SetShown(_combatBar, false);
            SetShown(_detailWindow, false);

            // Escalate the copy toward the climax: run complete > boss slain > level cleared > victory.
            if (result.RunCompleted)
            {
                _victoryTitle.text = "Dungeon Conquered!";
            }
            else if (result.BossDefeated)
            {
                _victoryTitle.text = "Boss Slain!";
            }
            else if (result.LevelCleared)
            {
                _victoryTitle.text = "Level Cleared!";
            }
            else
            {
                _victoryTitle.text = "Victory!";
            }
            _victoryRewards.Clear();

            // Loot — a header row, then an icon+name line per item.
            if (result.Loot != null && result.Loot.Count > 0)
            {
                _victoryRewards.Add(MakeVictoryRow("Loot", result.Loot.Count > 1 ? $"x{result.Loot.Count}" : string.Empty));
                foreach (var item in result.Loot)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    var loot = new VisualElement();
                    loot.AddToClassList("cd-victory-loot");
                    var icon = new VisualElement();
                    icon.AddToClassList("cd-victory-loot__icon");
                    if (item.Icon != null)
                    {
                        icon.style.backgroundImage = new StyleBackground(item.Icon);
                    }
                    var name = new Label(item.DisplayName);
                    name.AddToClassList("cd-victory-loot__name");
                    loot.Add(icon);
                    loot.Add(name);
                    _victoryRewards.Add(loot);
                }
            }
            else
            {
                _victoryRewards.Add(MakeVictoryRow("Loot", "None"));
            }

            _victoryRewards.Add(MakeVictoryRow("XP", "+" + result.XpGained));

            string gold = "+" + result.GoldGained;
            if (result.LevelCleared)
            {
                gold += "  (banked)";
            }
            _victoryRewards.Add(MakeVictoryRow("Gold", gold));

            if (result.LevelCleared)
            {
                _victoryRewards.Add(MakeVictoryRow("The way down", "open"));
            }

            SetShown(_victoryWindow, true);
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
            // Always back to the room, even after clearing the exit: the level completes when the
            // player takes the stairs, which ShowMainBar surfaces as the Descend button.
            ShowMainBar();
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
                door.OnDoorClicked += OnDoorSelected;
                _navDoors.Add(door);
            }
            // The doors being subscribed is exactly the state in which walking through one is legal,
            // so it is also the state in which the arrow keys point at them.
            _doorsLive = _navDoors.Count > 0;
        }

        private void UnsubscribeDoors()
        {
            ClearDoorCursor();
            _doorsLive = false;
            _navDoors.Clear();

            if (_currentRoom == null)
            {
                return;
            }
            foreach (var door in _currentRoom.Doors)
            {
                door.OnDoorClicked -= OnDoorSelected;
            }
            if (_entryDoor != null)
            {
                _entryDoor.OnDoorClicked -= OnEntryDoorFlee;
            }
        }

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
        /// Shows the non-combat action bar and re-evaluates what it can offer. Always use this rather
        /// than showing <c>_mainBar</c> directly: whether a Rescue is available depends on room state
        /// that changes *after* <see cref="Show"/> ran - clearing the room's guards is exactly what
        /// makes a captive reachable - so a bare SetShown would leave Rescue hidden for good in any
        /// room that had enemies in it.
        /// </summary>
        /// <summary>
        /// Shows the room bar, or nothing at all. Every button on it is conditional now, so an
        /// ordinary cleared room has no bar rather than an empty frame docked at the bottom.
        /// </summary>
        private void ShowMainBar()
        {
            // The bar is rebuilt button-by-button below; whatever the Tab cursor was on may be about
            // to be hidden, so it starts over.
            _barNav?.Reset();
            RefreshRescueButton();
            RefreshActionButton();
            RefreshPayloadButtons();
            RefreshDescendButton();
            SetShown(_mainBar, HasRoomActions());
        }

        private bool HasRoomActions()
        {
            return IsShown(_actionBtn) || IsShown(_searchBtn) || IsShown(_restBtn)
                || IsShown(_rescueBtn) || IsShown(_descendBtn);
        }

        /// <summary>
        /// Search and Rest exist only in a room of that kind whose payload is still unspent, and only
        /// once the room is clear - same rule as Rescue. Conditional like every other button on the
        /// bar: a Search that reports "nothing here" teaches the player to stop pressing it.
        /// </summary>
        private void RefreshPayloadButtons()
        {
            bool clear = _currentRoom != null && !_currentRoom.Enemies.Any(e => e != null && e.IsAlive);
            bool pending = clear && _currentRoom.HasPendingPayload;

            SetShown(_searchBtn, pending && _currentRoom.Kind == RoomKind.Treasure);
            SetShown(_restBtn, pending && _currentRoom.Kind == RoomKind.Rest);
        }

        /// <summary>Rescue is offered only in a room that still holds a captive and has no living enemies.</summary>
        private void RefreshRescueButton()
        {
            bool clear = _currentRoom != null && !_currentRoom.Enemies.Any(e => e != null && e.IsAlive);
            SetShown(_rescueBtn, clear && _currentRoom.CaptiveHero != null);
        }

        /// <summary>
        /// The stairs, in a cleared exit room. Shown rather than fired automatically so finishing a
        /// level is the player's decision - they may want to sweep the rooms they skipped, or spend
        /// an event they walked past, before leaving.
        /// </summary>
        private void RefreshDescendButton()
        {
            bool clear = _currentRoom != null && !_currentRoom.Enemies.Any(e => e != null && e.IsAlive);
            SetShown(_descendBtn, clear && _currentRoom != null && _currentRoom.IsExit);
        }

        /// <summary>
        /// Action exists only when the room has an unresolved event. Hiding it is the point: a
        /// button that reports "there is nothing here" teaches the player to stop pressing it, and
        /// then they miss the rooms where it mattered. Examine always stays - looking is free.
        /// </summary>
        private void RefreshActionButton()
        {
            SetShown(_actionBtn, PendingEvent() != null);
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
