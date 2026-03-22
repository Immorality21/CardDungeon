using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.Cards.UI
{
    public class DeckManagementUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Hero Tabs")]
        [SerializeField] private Transform _heroTabParent;
        [SerializeField] private GameObject _heroTabPrefab;
        [SerializeField] private List<HeroSO> _heroes;

        [Header("Deck Panel")]
        [SerializeField] private TextMeshProUGUI _heroNameLabel;
        [SerializeField] private TextMeshProUGUI _deckCountLabel;
        [SerializeField] private Transform _deckCardParent;
        [SerializeField] private GameObject _deckCardPrefab;

        [Header("Available Cards")]
        [SerializeField] private Transform _availableCardParent;
        [SerializeField] private GameObject _availableCardPrefab;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        private string _selectedHeroKey;
        private List<GameObject> _spawnedHeroTabs = new List<GameObject>();
        private List<GameObject> _spawnedDeckCards = new List<GameObject>();
        private List<GameObject> _spawnedAvailableCards = new List<GameObject>();

        private void Start()
        {
            _rootPanel.SetActive(false);
            _closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            if (!CardCollectionManager.HasInstance)
            {
                Debug.LogWarning("CardCollectionManager not found. Cannot open deck management.");
                return;
            }

            _rootPanel.SetActive(true);
            BuildHeroTabs();

            if (_heroes.Count > 0)
            {
                SelectHero(_heroes[0].Label);
            }
        }

        public void Hide()
        {
            _rootPanel.SetActive(false);
            ClearSpawned(_spawnedHeroTabs);
            ClearSpawned(_spawnedDeckCards);
            ClearSpawned(_spawnedAvailableCards);
        }

        private void BuildHeroTabs()
        {
            ClearSpawned(_spawnedHeroTabs);

            foreach (var hero in _heroes)
            {
                var tabObj = Instantiate(_heroTabPrefab, _heroTabParent);
                tabObj.SetActive(true);

                var label = tabObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = hero.Label;
                }

                var icon = tabObj.transform.Find("Icon");
                if (icon != null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null && hero.Sprite != null)
                    {
                        img.sprite = hero.Sprite;
                    }
                }

                var captured = hero.Label;
                var btn = tabObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => SelectHero(captured));
                }

                _spawnedHeroTabs.Add(tabObj);
            }
        }

        private void SelectHero(string heroKey)
        {
            _selectedHeroKey = heroKey;
            _heroNameLabel.text = heroKey;
            RefreshDeck();
            RefreshAvailable();
        }

        private void RefreshDeck()
        {
            ClearSpawned(_spawnedDeckCards);

            var assigned = CardCollectionManager.Instance.GetCardsForHero(_selectedHeroKey);
            _deckCountLabel.text = $"{assigned.Count} / {CardCollectionManager.MaxDeckSize}";

            foreach (var cardData in assigned)
            {
                var cardSO = CardCollectionManager.Instance.GetCardSO(cardData.CardKey);
                if (cardSO == null)
                {
                    continue;
                }

                var btnObj = Instantiate(_deckCardPrefab, _deckCardParent);
                btnObj.SetActive(true);

                var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"{cardSO.DisplayName} ({cardSO.Power})";
                }

                var icon = btnObj.transform.Find("Icon");
                if (icon != null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null && cardSO.Icon != null)
                    {
                        img.sprite = cardSO.Icon;
                    }
                }

                var captured = cardData;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnUnassignCard(captured));
                }

                _spawnedDeckCards.Add(btnObj);
            }
        }

        private void RefreshAvailable()
        {
            ClearSpawned(_spawnedAvailableCards);

            var unassigned = CardCollectionManager.Instance.GetUnassignedCards();

            foreach (var cardData in unassigned)
            {
                var cardSO = CardCollectionManager.Instance.GetCardSO(cardData.CardKey);
                if (cardSO == null)
                {
                    continue;
                }

                var btnObj = Instantiate(_availableCardPrefab, _availableCardParent);
                btnObj.SetActive(true);

                var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"{cardSO.DisplayName} ({cardSO.Power})";
                }

                var icon = btnObj.transform.Find("Icon");
                if (icon != null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null && cardSO.Icon != null)
                    {
                        img.sprite = cardSO.Icon;
                    }
                }

                var captured = cardData;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnAssignCard(captured));
                }

                _spawnedAvailableCards.Add(btnObj);
            }
        }

        private void OnAssignCard(CardSaveData cardData)
        {
            if (CardCollectionManager.Instance.AssignCardToHero(cardData, _selectedHeroKey))
            {
                RefreshDeck();
                RefreshAvailable();
            }
        }

        private void OnUnassignCard(CardSaveData cardData)
        {
            CardCollectionManager.Instance.UnassignCard(cardData);
            RefreshDeck();
            RefreshAvailable();
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
