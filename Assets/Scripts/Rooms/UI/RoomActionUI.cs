using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Room + combat action UI, built on UI Toolkit. Presents non-combat actions
    /// (Examine/Action), the combat start bar (Fight/Flee), the hero-turn command
    /// window (Attack/Magic/Draw/Skip), and centered dialogs (options, results, death).
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
        private VisualElement _optionWindow;
        private VisualElement _detailWindow;
        private VisualElement _partyStatus;
        private VisualElement _partyStatusRows;

        private readonly Dictionary<Heroes.Hero, VisualElement> _partyRows = new Dictionary<Heroes.Hero, VisualElement>();
        private readonly Dictionary<Heroes.Hero, Label> _partyHpLabels = new Dictionary<Heroes.Hero, Label>();

        private Label _heroTitle;
        private Label _optionTitle;
        private Label _detailTitle;
        private Label _detailMessage;
        private ScrollView _optionScroll;

        private Button _examineBtn;
        private Button _actionBtn;
        private Button _fightBtn;
        private Button _fleeBtn;
        private Button _optionBack;
        private Button _detailOk;

        private VisualElement _commandList;
        private VisualElement _turnOrder;
        private VisualElement _turnOrderList;

        private VisualElement _victoryWindow;
        private Label _victoryTitle;
        private VisualElement _victoryRewards;
        private Button _victoryContinue;
        private bool _pendingLevelCleared;

        private bool _refsReady;
        private Action _detailOkAction;

        private ICombatUnit _currentHeroTurn;
        private Room _currentRoom;
        private Door _entryDoor;

        // Cursor-driven command menu (FFX-style selection list).
        private enum HeroCommand { Attack, Magic, Draw, Skip }

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
            _optionWindow = root.Q<VisualElement>("option-window");
            _detailWindow = root.Q<VisualElement>("detail-window");
            _partyStatus = root.Q<VisualElement>("party-status");
            _partyStatusRows = root.Q<VisualElement>("party-status-rows");
            _commandList = root.Q<VisualElement>("command-list");
            _turnOrder = root.Q<VisualElement>("turn-order");
            _turnOrderList = root.Q<VisualElement>("turn-order-list");
            _victoryWindow = root.Q<VisualElement>("victory-window");
            _victoryTitle = root.Q<Label>("victory-title");
            _victoryRewards = root.Q<VisualElement>("victory-rewards");
            _victoryContinue = root.Q<Button>("victory-continue");

            _heroTitle = root.Q<Label>("hero-title");
            _optionTitle = root.Q<Label>("option-title");
            _detailTitle = root.Q<Label>("detail-title");
            _detailMessage = root.Q<Label>("detail-message");
            _optionScroll = root.Q<ScrollView>("option-scroll");

            _examineBtn = root.Q<Button>("examine-btn");
            _actionBtn = root.Q<Button>("action-btn");
            _fightBtn = root.Q<Button>("fight-btn");
            _fleeBtn = root.Q<Button>("flee-btn");
            _optionBack = root.Q<Button>("option-back");
            _detailOk = root.Q<Button>("detail-ok");

            _examineBtn.clicked += OnExamine;
            _actionBtn.clicked += OnAction;
            _fightBtn.clicked += OnFight;
            _fleeBtn.clicked += OnFlee;
            _optionBack.clicked += OnBack;
            _detailOk.clicked += () => _detailOkAction?.Invoke();
            _victoryContinue.clicked += OnVictoryContinue;

            // Strip focusability from every focusable descendant so UI Toolkit's arrow-key
            // navigation has nowhere to move focus — keyboard focus stays on the root and our
            // cursor nav keeps receiving keys. (Buttons stay clickable + hotkey-driven.)
            foreach (var focusable in new Focusable[] { _examineBtn, _actionBtn, _fightBtn, _fleeBtn, _optionBack, _detailOk, _victoryContinue, _optionScroll })
            {
                if (focusable != null)
                {
                    focusable.focusable = false;
                }
            }

            // Keyboard hotkeys for the combat bars: F/R start-bar, A/M/D/S hero command bar.
            // Registered on the root so the panel routes key presses here; each hotkey runs the
            // same handler as its button, and only fires when the matching bar is visible.
            root.RegisterCallback<KeyDownEvent>(OnCombatHotkey);
            root.focusable = true;

            // While the command menu is up, swallow UI Toolkit's built-in navigation so it can't
            // blur/steal keyboard focus off the root after the first arrow (our cursor nav owns it).
            root.RegisterCallback<NavigationMoveEvent>(evt => { if (IsShown(_heroBar)) { evt.StopPropagation(); } });
            root.RegisterCallback<NavigationSubmitEvent>(evt => { if (IsShown(_heroBar)) { evt.StopPropagation(); } });
            root.RegisterCallback<NavigationCancelEvent>(evt => { if (IsShown(_heroBar)) { evt.StopPropagation(); } });

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
            SetShown(_optionWindow, false);
            SetShown(_detailWindow, false);

            bool hasEnemy = room.Enemies.Any(e => e != null && e.IsAlive);
            SetShown(_combatBar, hasEnemy);
            SetShown(_mainBar, !hasEnemy);

            if (hasEnemy)
            {
                room.SetDoorsEnabled(entryDoor);
                if (_entryDoor != null)
                {
                    _entryDoor.OnDoorClicked += OnEntryDoorFlee;
                }
                FocusRoot();
            }
            else
            {
                room.EnableAllDoors();
                SubscribeDoors();
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
            SetShown(_heroBar, false);
            SetShown(_optionWindow, false);
            SetShown(_detailWindow, false);
            SetShown(_partyStatus, false);
            SetShown(_turnOrder, false);
            SetShown(_victoryWindow, false);
        }

        // ============================================================
        //  EXAMINE / ACTION FLOWS
        // ============================================================

        private void OnExamine()
        {
            ShowOptionList("Examine", _currentRoom.RoomSO.ExamineOptions,
                text => ShowDetail("Examine", text));
        }

        private void OnAction()
        {
            ShowOptionList("Action", _currentRoom.RoomSO.ActionOptions,
                text => ShowDetail("Action", text));
        }

        private void ShowOptionList(string title, List<string> options, Action<string> onSelect)
        {
            SetShown(_mainBar, false);
            _optionTitle.text = title;
            _optionScroll.Clear();

            if (options == null || options.Count == 0)
            {
                ShowDetail("Nothing", "There is nothing here.");
                return;
            }

            foreach (var option in options)
            {
                var captured = option;
                var btn = new Button(() => onSelect(captured)) { text = option };
                btn.AddToClassList("cd-list-button");
                _optionScroll.Add(btn);
            }

            SetShown(_optionWindow, true);
        }

        private void ShowDetail(string title, string message)
        {
            SetShown(_optionWindow, false);
            _detailTitle.text = title;
            _detailMessage.text = message;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                SetShown(_optionWindow, true);
            };
            SetShown(_detailWindow, true);
        }

        private void OnBack()
        {
            SetShown(_optionWindow, false);
            _optionScroll.Clear();
            SetShown(_mainBar, true);
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

        private void OnHeroDraw()
        {
            SetShown(_heroBar, false);

            var drawable = CombatManager.Instance.GetDrawableEnemies();
            if (drawable.Count == 0)
            {
                SetShown(_heroBar, true);
                return;
            }

            CombatManager.Instance.RequestDrawTargets(_currentHeroTurn, drawable);
        }

        /// <summary>Called by the selection UI to return to the Attack/Magic/Draw/Skip window.</summary>
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
            bool hasDrawable = CombatManager.Instance.GetDrawableEnemies().Count > 0;

            _commands.Add(new CommandEntry { Command = HeroCommand.Attack, Label = "Attack", Enabled = true });
            _commands.Add(new CommandEntry { Command = HeroCommand.Magic, Label = "Magic", Enabled = hasMagic });
            _commands.Add(new CommandEntry { Command = HeroCommand.Draw, Label = "Draw", Enabled = hasDrawable });
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
        private void InvokeCommandShortcut(HeroCommand command)
        {
            foreach (var entry in _commands)
            {
                if (entry.Command == command)
                {
                    if (entry.Enabled)
                    {
                        InvokeCommand(command);
                    }
                    return;
                }
            }
        }

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
                case HeroCommand.Draw:
                    OnHeroDraw();
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
        /// Keyboard shortcuts mirroring the combat buttons. Each key only acts when its bar is
        /// visible, and only invokes actions whose buttons are currently shown (e.g. Magic/Draw
        /// appear conditionally), so a hotkey can never do something the on-screen menu can't.
        /// </summary>
        private void OnCombatHotkey(KeyDownEvent evt)
        {
            if (IsShown(_combatBar))
            {
                if (evt.keyCode == KeyCode.F)
                {
                    OnFight();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.R)
                {
                    OnFlee();
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
                    // Direct letter shortcuts (act only if that command is enabled).
                    case KeyCode.A:
                        InvokeCommandShortcut(HeroCommand.Attack);
                        evt.StopPropagation();
                        break;
                    case KeyCode.M:
                        InvokeCommandShortcut(HeroCommand.Magic);
                        evt.StopPropagation();
                        break;
                    case KeyCode.D:
                        InvokeCommandShortcut(HeroCommand.Draw);
                        evt.StopPropagation();
                        break;
                    case KeyCode.S:
                        InvokeCommandShortcut(HeroCommand.Skip);
                        evt.StopPropagation();
                        break;
                }
            }
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
                hp.text = $"HP {hero.Stats.Health}/{hero.Stats.MaxHealth}";
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
            SetShown(_optionWindow, false);
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
            SetShown(_optionWindow, false);
            _detailTitle.text = title;
            _detailMessage.text = message;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                if (showNormalAfter)
                {
                    _currentRoom.EnableAllDoors();
                    SetShown(_mainBar, true);
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
            _pendingLevelCleared = result.LevelCleared;

            SetShown(_mainBar, false);
            SetShown(_combatBar, false);
            SetShown(_optionWindow, false);
            SetShown(_detailWindow, false);

            _victoryTitle.text = result.LevelCleared ? "Level Cleared!" : "Victory!";
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
            CombatManager.Instance.FinishVictory(_pendingLevelCleared);
            if (!_pendingLevelCleared)
            {
                SetShown(_mainBar, true);
                SubscribeDoors();
            }
            // If the level was cleared, FinishVictory raises OnDungeonCleared → scene loads to menu.
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
            foreach (var door in _currentRoom.Doors)
            {
                door.OnDoorClicked += OnDoorSelected;
            }
        }

        private void UnsubscribeDoors()
        {
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

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
