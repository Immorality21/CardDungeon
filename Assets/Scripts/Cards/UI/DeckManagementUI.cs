using System;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Hero Select Panel")]
        [SerializeField] private GameObject _heroSelectPanel;
        [SerializeField] private Transform _heroSelectParent;
        [SerializeField] private GameObject _heroSelectPrefab;
        [SerializeField] private List<HeroSO> _heroes;

        [Header("Deck Edit Panel")]
        [SerializeField] private GameObject _deckEditPanel;
        [SerializeField] private Button _deckBackButton;
        [SerializeField] private TextMeshProUGUI _heroNameLabel;
        [SerializeField] private TextMeshProUGUI _deckCountLabel;
        [SerializeField] private Transform _deckCardParent;
        [SerializeField] private GameObject _deckCardPrefab;

        [Header("Available Cards")]
        [SerializeField] private Transform _availableCardParent;
        [SerializeField] private GameObject _availableCardPrefab;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        public event Action OnClosed;

        private string _selectedHeroKey;
        private List<GameObject> _spawnedHeroEntries = new List<GameObject>();
        private List<GameObject> _spawnedDeckCards = new List<GameObject>();
        private List<GameObject> _spawnedAvailableCards = new List<GameObject>();

        private void Start()
        {
            _rootPanel.SetActive(false);
            _closeButton.onClick.AddListener(Hide);
            _deckBackButton.onClick.AddListener(ShowHeroSelect);
        }

        public void Show()
        {
            if (!CardCollectionManager.HasInstance)
            {
                Debug.LogWarning("CardCollectionManager not found. Cannot open deck management.");
                return;
            }

            _rootPanel.SetActive(true);
            ShowHeroSelect();
        }

        public void Hide()
        {
            _rootPanel.SetActive(false);
            _spawnedHeroEntries.DestroyAndClear();
            _spawnedDeckCards.DestroyAndClear();
            _spawnedAvailableCards.DestroyAndClear();
            OnClosed?.Invoke();
        }

        private void ShowHeroSelect()
        {
            _spawnedDeckCards.DestroyAndClear();
            _spawnedAvailableCards.DestroyAndClear();

            _heroSelectPanel.SetActive(true);
            _deckEditPanel.SetActive(false);

            BuildHeroEntries();
        }

        private void ShowDeckEdit(string heroKey)
        {
            _spawnedHeroEntries.DestroyAndClear();

            _selectedHeroKey = heroKey;
            _heroSelectPanel.SetActive(false);
            _deckEditPanel.SetActive(true);

            _heroNameLabel.text = heroKey;
            RefreshDeck();
            RefreshAvailable();
        }

        private void BuildHeroEntries()
        {
            _spawnedHeroEntries.DestroyAndClear();

            foreach (var hero in _heroes)
            {
                var entry = Instantiate(_heroSelectPrefab, _heroSelectParent);
                entry.SetActive(true);

                var icon = entry.transform.Find("Icon");
                if (icon != null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null && hero.Sprite != null)
                    {
                        img.sprite = hero.Sprite;
                    }
                }

                var nameLabel = entry.transform.Find("NameLabel");
                if (nameLabel != null)
                {
                    var tmp = nameLabel.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = hero.Label;
                    }
                }

                var descLabel = entry.transform.Find("DescriptionLabel");
                if (descLabel != null)
                {
                    var tmp = descLabel.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = string.Empty;
                    }
                }

                int deckCount = CardCollectionManager.Instance.GetCardsForHero(hero.Label).Count;
                var effectsLabel = entry.transform.Find("EffectsLabel");
                if (effectsLabel != null)
                {
                    var tmp = effectsLabel.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = $"Deck {deckCount} / {CardCollectionManager.MaxDeckSize}";
                    }
                }

                var captured = hero.Label;
                var btn = entry.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => ShowDeckEdit(captured));
                }

                _spawnedHeroEntries.Add(entry);
            }
        }

        private void RefreshDeck()
        {
            _spawnedDeckCards.DestroyAndClear();

            var assigned = CardCollectionManager.Instance.GetCardsForHero(_selectedHeroKey);
            _deckCountLabel.text = $"{assigned.Count} / {CardCollectionManager.MaxDeckSize}";

            var groups = assigned.GroupBy(c => c.CardKey);

            foreach (var group in groups)
            {
                var cardSO = CardCollectionManager.Instance.GetCardSO(group.Key);
                if (cardSO == null)
                {
                    continue;
                }

                int count = group.Count();
                var cardObj = SpawnCardEntry(_deckCardPrefab, _deckCardParent, cardSO, count);

                var capturedKey = group.Key;
                var btn = cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnUnassignCard(capturedKey));
                }

                _spawnedDeckCards.Add(cardObj);
            }
        }

        private void RefreshAvailable()
        {
            _spawnedAvailableCards.DestroyAndClear();

            var unassigned = CardCollectionManager.Instance.GetUnassignedCards();

            var groups = unassigned.GroupBy(c => c.CardKey);

            foreach (var group in groups)
            {
                var cardSO = CardCollectionManager.Instance.GetCardSO(group.Key);
                if (cardSO == null)
                {
                    continue;
                }

                int count = group.Count();
                var cardObj = SpawnCardEntry(_availableCardPrefab, _availableCardParent, cardSO, count);

                var capturedKey = group.Key;
                var btn = cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnAssignCard(capturedKey));
                }

                _spawnedAvailableCards.Add(cardObj);
            }
        }

        private GameObject SpawnCardEntry(GameObject prefab, Transform parent, CardSO cardSO, int count)
        {
            var cardObj = Instantiate(prefab, parent);
            cardObj.SetActive(true);

            var icon = cardObj.transform.Find("Icon");
            if (icon != null)
            {
                var img = icon.GetComponent<Image>();
                if (img != null && cardSO.Icon != null)
                {
                    img.sprite = cardSO.Icon;
                }
            }

            var nameLabel = cardObj.transform.Find("NameLabel");
            if (nameLabel != null)
            {
                var tmp = nameLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = count > 1 ? $"{cardSO.DisplayName} ({count})" : cardSO.DisplayName;
                }
            }

            if (nameLabel == null)
            {
                var label = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = count > 1 ? $"{cardSO.DisplayName} ({count})" : cardSO.DisplayName;
                }
            }

            var descLabel = cardObj.transform.Find("DescriptionLabel");
            if (descLabel != null)
            {
                var tmp = descLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = cardSO.Description ?? "";
                }
            }

            var effectsLabel = cardObj.transform.Find("EffectsLabel");
            if (effectsLabel != null)
            {
                var tmp = effectsLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = cardSO.GetEffectsSummary();
                }
            }

            return cardObj;
        }

        private void OnAssignCard(string cardKey)
        {
            var unassigned = CardCollectionManager.Instance.GetUnassignedCards();
            var cardData = unassigned.FirstOrDefault(c => c.CardKey == cardKey);
            if (cardData != null)
            {
                if (CardCollectionManager.Instance.AssignCardToHero(cardData, _selectedHeroKey))
                {
                    RefreshDeck();
                    RefreshAvailable();
                }
            }
        }

        private void OnUnassignCard(string cardKey)
        {
            var assigned = CardCollectionManager.Instance.GetCardsForHero(_selectedHeroKey);
            var cardData = assigned.FirstOrDefault(c => c.CardKey == cardKey);
            if (cardData != null)
            {
                CardCollectionManager.Instance.UnassignCard(cardData);
                RefreshDeck();
                RefreshAvailable();
            }
        }

    }
}
