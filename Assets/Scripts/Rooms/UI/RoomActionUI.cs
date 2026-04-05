using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Rooms
{
    public class RoomActionUI : MonoBehaviour
    {
        [Header("Main Action Panel")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private Button _examineButton;
        [SerializeField] private Button _actionButton;

        [Header("Combat Panel")]
        [SerializeField] private GameObject _combatPanel;
        [SerializeField] private Button _fightButton;
        [SerializeField] private Button _fleeButton;

        [Header("Hero Action Panel")]
        [SerializeField] private GameObject _heroActionPanel;
        [SerializeField] private TextMeshProUGUI _heroActionLabel;
        [SerializeField] private Button _attackButton;
        [SerializeField] private Button _cardsButton;
        [SerializeField] private Button _skipButton;

        [Header("Sub Panel")]
        [SerializeField] private GameObject _subPanel;
        [SerializeField] private Transform _optionListParent;
        [SerializeField] private Button _backButton;
        [SerializeField] private GameObject _optionButtonPrefab;

        [Header("Detail Panel")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private TextMeshProUGUI _detailMessage;
        [SerializeField] private Button _detailOkButton;

        private ICombatUnit _currentHeroTurn;
        private Room _currentRoom;
        private Door _entryDoor;
        private List<GameObject> _spawnedOptions = new List<GameObject>();

        private void Awake()
        {
            HideAll();

            _examineButton.onClick.AddListener(OnExamine);
            _actionButton.onClick.AddListener(OnAction);
            _fightButton.onClick.AddListener(OnFight);
            _fleeButton.onClick.AddListener(OnFlee);
            _attackButton.onClick.AddListener(OnHeroAttack);
            _cardsButton.onClick.AddListener(OnHeroCards);
            _skipButton.onClick.AddListener(OnHeroSkip);
            _backButton.onClick.AddListener(OnBack);
        }

        public void Show(Room room, Door entryDoor = null)
        {
            UnsubscribeDoors();

            _currentRoom = room;
            _entryDoor = entryDoor;
            _subPanel.SetActive(false);
            _detailPanel.SetActive(false);

            bool hasEnemy = room.Enemies.Any(e => e != null && e.IsAlive);
            _combatPanel.SetActive(hasEnemy);
            _mainPanel.SetActive(!hasEnemy);

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
            HideAll();
            UnsubscribeDoors();
        }

        private void HideAll()
        {
            _mainPanel.SetActive(false);
            _combatPanel.SetActive(false);
            _heroActionPanel.SetActive(false);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(false);
        }

        // ============================================================
        //  EXAMINE / ACTION FLOWS
        // ============================================================

        private void OnExamine()
        {
            ShowOptionList(
                _currentRoom.RoomSO.ExamineOptions,
                text => ShowDetail("Examine", text));
        }

        private void OnAction()
        {
            ShowOptionList(
                _currentRoom.RoomSO.ActionOptions,
                text => ShowDetail("Action", text));
        }

        private void ShowOptionList(List<string> options, Action<string> onSelect)
        {
            _mainPanel.SetActive(false);
            _subPanel.SetActive(true);
            _spawnedOptions.DestroyAndClear();

            if (options == null || options.Count == 0)
            {
                ShowDetail("Nothing", "There is nothing here.");
                return;
            }

            foreach (var option in options)
            {
                var btnObj = Instantiate(_optionButtonPrefab, _optionListParent);
                btnObj.SetActive(true);

                var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = option;
                }

                var captured = option;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => onSelect(captured));
                }

                _spawnedOptions.Add(btnObj);
            }
        }

        private void ShowDetail(string title, string message)
        {
            _subPanel.SetActive(false);
            _detailPanel.SetActive(true);
            _detailTitle.text = title;
            _detailMessage.text = message;

            _detailOkButton.onClick.RemoveAllListeners();
            _detailOkButton.onClick.AddListener(() =>
            {
                _detailPanel.SetActive(false);
                _subPanel.SetActive(true);
            });
        }


        private void OnBack()
        {
            _subPanel.SetActive(false);
            _spawnedOptions.DestroyAndClear();
            _mainPanel.SetActive(true);
        }

        // ============================================================
        //  COMBAT
        // ============================================================

        private void OnFight()
        {
            var party = GameManager.Instance.Party;

            // Hide all UI during combat
            HideAll();

            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted += OnHeroTurnStarted;
            CombatManager.Instance.StartCombat(party, _currentRoom);
        }

        private void OnHeroTurnStarted(ICombatUnit hero)
        {
            _currentHeroTurn = hero;
            _heroActionLabel.text = $"{hero.DisplayName}'s Turn";

            // Show/hide cards button based on available cards
            bool hasCards = false;
            if (CardCollectionManager.HasInstance && DungeonManager.HasInstance && DungeonManager.Instance.DeckState != null)
            {
                var heroComponent = hero as Heroes.Hero;
                if (heroComponent != null)
                {
                    var available = DungeonManager.Instance.DeckState.GetAvailableCards(
                        heroComponent.HeroKey, CardCollectionManager.Instance);
                    hasCards = available.Count > 0;
                }
            }
            _cardsButton.gameObject.SetActive(hasCards);

            _heroActionPanel.SetActive(true);
        }

        private void OnHeroAttack()
        {
            _heroActionPanel.SetActive(false);
            CombatManager.Instance.SubmitHeroAction(HeroAction.Attack);
        }

        private void OnHeroCards()
        {
            _heroActionPanel.SetActive(false);

            var heroComponent = _currentHeroTurn as Heroes.Hero;
            if (heroComponent == null)
            {
                return;
            }

            var available = DungeonManager.Instance.DeckState.GetAvailableCards(
                heroComponent.HeroKey, CardCollectionManager.Instance);

            CombatManager.Instance.RequestCardDeck(_currentHeroTurn, available);
        }

        public void CancelCardSelection()
        {
            _heroActionPanel.SetActive(true);
        }

        private void OnHeroSkip()
        {
            _heroActionPanel.SetActive(false);
            CombatManager.Instance.SubmitHeroAction(HeroAction.Skip);
        }

        private void OnCombatEnded(CombatResult result)
        {
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted -= OnHeroTurnStarted;
            _heroActionPanel.SetActive(false);

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
            _mainPanel.SetActive(false);
            _combatPanel.SetActive(false);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(true);
            _detailTitle.text = "Your Party Has Fallen...";
            _detailMessage.text = log;

            _detailOkButton.onClick.RemoveAllListeners();
            _detailOkButton.onClick.AddListener(() =>
            {
                // Wipe run and dungeon saves, return to menu
                if (DungeonManager.HasInstance)
                {
                    DungeonManager.Instance.HandlePartyDeath();
                }
                SceneManager.LoadScene("MenuScene");
            });
        }

        private void OnFlee()
        {
            var party = GameManager.Instance.Party;

            if (!CombatManager.Instance.CanFlee(party))
            {
                _combatPanel.SetActive(false);
                ShowCombatResult("Flee", "Nowhere to flee!", showNormalAfter: false, returnToCombat: true);
                return;
            }

            _combatPanel.SetActive(false);
            UnsubscribeDoors();

            CombatManager.Instance.Flee(party, _entryDoor, _currentRoom);
        }

        private void OnEntryDoorFlee(Door door)
        {
            OnFlee();
        }

        private void ShowCombatResult(string title, string message, bool showNormalAfter, bool returnToCombat = false)
        {
            _mainPanel.SetActive(false);
            _combatPanel.SetActive(false);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(true);
            _detailOkButton.gameObject.SetActive(true);
            _detailTitle.text = title;
            _detailMessage.text = message;

            _detailOkButton.onClick.RemoveAllListeners();
            _detailOkButton.onClick.AddListener(() =>
            {
                _detailPanel.SetActive(false);
                if (showNormalAfter)
                {
                    _currentRoom.EnableAllDoors();
                    _mainPanel.SetActive(true);
                    SubscribeDoors();
                }
                else if (returnToCombat)
                {
                    _combatPanel.SetActive(true);
                }
            });
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
    }
}
