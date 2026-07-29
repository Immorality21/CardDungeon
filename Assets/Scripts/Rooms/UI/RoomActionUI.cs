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

        private VisualElement _mainBar;
        private VisualElement _combatBar;
        private VisualElement _heroBar;
        private VisualElement _optionWindow;
        private VisualElement _detailWindow;

        private Label _heroTitle;
        private Label _optionTitle;
        private Label _detailTitle;
        private Label _detailMessage;
        private ScrollView _optionScroll;

        private Button _examineBtn;
        private Button _actionBtn;
        private Button _fightBtn;
        private Button _fleeBtn;
        private Button _attackBtn;
        private Button _magicBtn;
        private Button _drawBtn;
        private Button _skipBtn;
        private Button _optionBack;
        private Button _detailOk;

        private bool _refsReady;
        private Action _detailOkAction;

        private ICombatUnit _currentHeroTurn;
        private Room _currentRoom;
        private Door _entryDoor;

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

            _mainBar = root.Q<VisualElement>("main-bar");
            _combatBar = root.Q<VisualElement>("combat-bar");
            _heroBar = root.Q<VisualElement>("hero-bar");
            _optionWindow = root.Q<VisualElement>("option-window");
            _detailWindow = root.Q<VisualElement>("detail-window");

            _heroTitle = root.Q<Label>("hero-title");
            _optionTitle = root.Q<Label>("option-title");
            _detailTitle = root.Q<Label>("detail-title");
            _detailMessage = root.Q<Label>("detail-message");
            _optionScroll = root.Q<ScrollView>("option-scroll");

            _examineBtn = root.Q<Button>("examine-btn");
            _actionBtn = root.Q<Button>("action-btn");
            _fightBtn = root.Q<Button>("fight-btn");
            _fleeBtn = root.Q<Button>("flee-btn");
            _attackBtn = root.Q<Button>("attack-btn");
            _magicBtn = root.Q<Button>("magic-btn");
            _drawBtn = root.Q<Button>("draw-btn");
            _skipBtn = root.Q<Button>("skip-btn");
            _optionBack = root.Q<Button>("option-back");
            _detailOk = root.Q<Button>("detail-ok");

            _examineBtn.clicked += OnExamine;
            _actionBtn.clicked += OnAction;
            _fightBtn.clicked += OnFight;
            _fleeBtn.clicked += OnFlee;
            _attackBtn.clicked += OnHeroAttack;
            _magicBtn.clicked += OnHeroMagic;
            _drawBtn.clicked += OnHeroDraw;
            _skipBtn.clicked += OnHeroSkip;
            _optionBack.clicked += OnBack;
            _detailOk.clicked += () => _detailOkAction?.Invoke();

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
            CombatManager.Instance.StartCombat(party, _currentRoom);
        }

        private void OnHeroTurnStarted(ICombatUnit hero)
        {
            _currentHeroTurn = hero;
            _heroTitle.text = $"{hero.DisplayName}'s Turn";

            // Show Magic only when this hero has a charged slot.
            bool hasMagic = false;
            var heroComponent = hero as Heroes.Hero;
            if (heroComponent != null && DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                hasMagic = DungeonManager.Instance.MagicState.HasAnyCastable(heroComponent.HeroKey);
            }
            SetShown(_magicBtn, hasMagic);

            // Show Draw only when an enemy has magic to draw.
            bool hasDrawable = CombatManager.Instance.GetDrawableEnemies().Count > 0;
            SetShown(_drawBtn, hasDrawable);

            SetShown(_heroBar, true);
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
            }
        }

        private void OnHeroSkip()
        {
            SetShown(_heroBar, false);
            CombatManager.Instance.SubmitHeroAction(HeroAction.Skip);
        }

        private void OnCombatEnded(CombatResult result)
        {
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted -= OnHeroTurnStarted;
            SetShown(_heroBar, false);

            switch (result.Outcome)
            {
                case CombatOutcome.Victory:
                    ShowCombatResult("Victory!", result.Log, showNormalAfter: true);
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
