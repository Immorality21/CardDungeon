using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.Cards.UI
{
    public class CardSelectionUI : MonoBehaviour
    {
        [Header("Card List Panel")]
        [SerializeField] private GameObject _cardListPanel;
        [SerializeField] private Transform _cardListParent;
        [SerializeField] private GameObject _cardButtonPrefab;
        [SerializeField] private Button _backButton;

        [Header("Target Selection Panel")]
        [SerializeField] private GameObject _targetPanel;
        [SerializeField] private Transform _targetListParent;
        [SerializeField] private GameObject _targetButtonPrefab;
        [SerializeField] private Button _targetBackButton;
        [SerializeField] private TextMeshProUGUI _targetPromptLabel;

        private ICombatUnit _currentHero;
        private CardSO _selectedCard;
        private List<GameObject> _spawnedCardButtons = new List<GameObject>();
        private List<GameObject> _spawnedTargetButtons = new List<GameObject>();

        private void OnEnable()
        {
            CombatManager.Instance.OnCardDeckRequested += ShowCardList;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }

        private void OnDisable()
        {
            if (CombatManager.HasInstance)
            {
                CombatManager.Instance.OnCardDeckRequested -= ShowCardList;
                CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            }
        }

        private void Start()
        {
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);

            _backButton.onClick.AddListener(OnBackToActions);
            _targetBackButton.onClick.AddListener(OnBackToCardList);
        }

        private void ShowCardList(ICombatUnit hero, List<CardSO> availableCards)
        {
            _currentHero = hero;
            _selectedCard = null;

            ClearSpawned(_spawnedCardButtons);

            foreach (var card in availableCards)
            {
                var btnObj = Instantiate(_cardButtonPrefab, _cardListParent);
                btnObj.SetActive(true);

                var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"{card.DisplayName} ({card.Power})";
                }

                var icon = btnObj.transform.Find("Icon");
                if (icon != null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null && card.Icon != null)
                    {
                        img.sprite = card.Icon;
                    }
                }

                var captured = card;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnCardSelected(captured));
                }

                _spawnedCardButtons.Add(btnObj);
            }

            _cardListPanel.SetActive(true);
            _targetPanel.SetActive(false);
        }

        private void OnCardSelected(CardSO card)
        {
            _selectedCard = card;

            // Self/AllAllies/AllEnemies don't need target picking
            if (card.TargetType == CardTargetType.Self)
            {
                SubmitCard(new List<ICombatUnit> { _currentHero });
                return;
            }

            if (card.TargetType == CardTargetType.AllEnemies)
            {
                SubmitCard(CombatManager.Instance.GetAliveEnemies());
                return;
            }

            if (card.TargetType == CardTargetType.AllAllies)
            {
                var party = GameManager.Instance.Party;
                SubmitCard(CombatManager.Instance.GetAliveHeroes(party));
                return;
            }

            // SingleEnemy or SingleAlly needs target selection
            ShowTargetSelection(card.TargetType);
        }

        private void ShowTargetSelection(CardTargetType targetType)
        {
            ClearSpawned(_spawnedTargetButtons);

            List<ICombatUnit> targets;
            if (targetType == CardTargetType.SingleEnemy)
            {
                targets = CombatManager.Instance.GetAliveEnemies();
                _targetPromptLabel.text = "Select Enemy Target";
            }
            else
            {
                var party = GameManager.Instance.Party;
                targets = CombatManager.Instance.GetAliveHeroes(party);
                _targetPromptLabel.text = "Select Ally Target";
            }

            foreach (var target in targets)
            {
                var btnObj = Instantiate(_targetButtonPrefab, _targetListParent);
                btnObj.SetActive(true);

                var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"{target.DisplayName} (HP: {target.Stats.Health})";
                }

                var icon = btnObj.transform.Find("Icon");
                if (icon != null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null && target.Icon != null)
                    {
                        img.sprite = target.Icon;
                    }
                }

                var captured = target;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnTargetSelected(captured));
                }

                _spawnedTargetButtons.Add(btnObj);
            }

            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(true);
        }

        private void OnTargetSelected(ICombatUnit target)
        {
            SubmitCard(new List<ICombatUnit> { target });
        }

        private void SubmitCard(List<ICombatUnit> targets)
        {
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);

            CombatManager.Instance.SubmitCardAction(_selectedCard, _currentHero, targets);
        }

        private void OnBackToActions()
        {
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);

            // Return to the hero action panel (Attack/Cards/Skip)
            var roomActionUI = FindObjectOfType<RoomActionUI>();
            if (roomActionUI != null)
            {
                roomActionUI.CancelCardSelection();
            }
        }

        private void OnBackToCardList()
        {
            _targetPanel.SetActive(false);

            // Re-show the card list with the same hero
            var heroComponent = _currentHero as Hero;
            if (heroComponent != null && CardCollectionManager.HasInstance &&
                DungeonManager.HasInstance && DungeonManager.Instance.DeckState != null)
            {
                var available = DungeonManager.Instance.DeckState.GetAvailableCards(
                    heroComponent.HeroKey, CardCollectionManager.Instance);
                ShowCardList(_currentHero, available);
            }
        }

        private void OnCombatEnded(CombatResult result)
        {
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);
            ClearSpawned(_spawnedCardButtons);
            ClearSpawned(_spawnedTargetButtons);
        }

        private void ClearSpawned(List<GameObject> list)
        {
            foreach (var obj in list)
            {
                Destroy(obj);
            }
            list.Clear();
        }
    }
}
