using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public event Action OnClosed;

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
            OnClosed?.Invoke();
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

            // Group by CardKey
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
            ClearSpawned(_spawnedAvailableCards);

            var unassigned = CardCollectionManager.Instance.GetUnassignedCards();

            // Group by CardKey
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

            // Set icon
            var icon = cardObj.transform.Find("Icon");
            if (icon != null)
            {
                var img = icon.GetComponent<Image>();
                if (img != null && cardSO.Icon != null)
                {
                    img.sprite = cardSO.Icon;
                }
            }

            // Set name with count
            var nameLabel = cardObj.transform.Find("NameLabel");
            if (nameLabel != null)
            {
                var tmp = nameLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = count > 1 ? $"{cardSO.DisplayName} ({count})" : cardSO.DisplayName;
                }
            }

            // Fallback: try the generic Label child (old prefab compat)
            if (nameLabel == null)
            {
                var label = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = count > 1 ? $"{cardSO.DisplayName} ({count})" : cardSO.DisplayName;
                }
            }

            // Set description
            var descLabel = cardObj.transform.Find("DescriptionLabel");
            if (descLabel != null)
            {
                var tmp = descLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = cardSO.Description ?? "";
                }
            }

            // Set effects summary
            var effectsLabel = cardObj.transform.Find("EffectsLabel");
            if (effectsLabel != null)
            {
                var tmp = effectsLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = GetEffectsSummary(cardSO);
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

        private string GetEffectsSummary(CardSO cardSO)
        {
            if (cardSO.Effects == null || cardSO.Effects.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < cardSO.Effects.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                var effect = cardSO.Effects[i];
                switch (effect.EffectType)
                {
                    case CardEffectType.Damage:
                        sb.Append($"DMG {effect.Power}");
                        if (effect.DamageType != Combat.DamageType.Normal)
                        {
                            sb.Append($" {effect.DamageType}");
                        }
                        break;
                    case CardEffectType.Heal:
                        sb.Append($"Heal {effect.Power}");
                        break;
                    case CardEffectType.Buff:
                        sb.Append($"+{effect.BuffType}");
                        break;
                    case CardEffectType.Debuff:
                        sb.Append($"-{effect.BuffType}");
                        break;
                }
            }

            return sb.ToString();
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
