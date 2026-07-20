using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Hub "Forge" panel that spends Essence to permanently upgrade cards.
    /// Upgrades are per card type (all copies of a card benefit), matching how
    /// the combat layer identifies cards by key. Rows are cloned at runtime from
    /// an inactive template, mirroring DeckManagementUI's spawn pattern.
    /// </summary>
    public class CardUpgradeUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Currency Display")]
        [SerializeField] private TextMeshProUGUI _essenceLabel;

        [Header("Card List")]
        [SerializeField] private Transform _rowParent;
        [SerializeField] private GameObject _rowTemplate;
        [SerializeField] private TextMeshProUGUI _emptyLabel;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        public event Action OnClosed;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        private void Start()
        {
            if (_rootPanel != null)
            {
                _rootPanel.SetActive(false);
            }
            if (_rowTemplate != null)
            {
                _rowTemplate.SetActive(false);
            }
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
        }

        public void Show()
        {
            if (!CardCollectionManager.HasInstance)
            {
                Debug.LogWarning("CardCollectionManager not found. Cannot open card upgrades.");
                return;
            }

            if (_rootPanel != null)
            {
                _rootPanel.SetActive(true);
            }
            Refresh();
        }

        public void Hide()
        {
            if (_rootPanel != null)
            {
                _rootPanel.SetActive(false);
            }
            ClearRows();
            OnClosed?.Invoke();
        }

        private void Refresh()
        {
            ClearRows();

            if (_essenceLabel != null)
            {
                _essenceLabel.text = $"Essence: {MetaProgressManager.Instance.Essence}";
            }

            var distinctKeys = CardCollectionManager.Instance.GetAllCards()
                .Select(c => c.CardKey)
                .Distinct()
                .ToList();

            if (_emptyLabel != null)
            {
                _emptyLabel.gameObject.SetActive(distinctKeys.Count == 0);
            }

            if (_rowTemplate == null || _rowParent == null)
            {
                return;
            }

            foreach (var key in distinctKeys)
            {
                var cardSO = CardCollectionManager.Instance.GetCardSO(key);
                if (cardSO == null)
                {
                    continue;
                }
                SpawnRow(key, cardSO);
            }
        }

        private void SpawnRow(string cardKey, CardSO cardSO)
        {
            var row = Instantiate(_rowTemplate, _rowParent);
            row.SetActive(true);

            int level = MetaProgressManager.Instance.GetCardUpgradeLevel(cardKey);
            int cost = MetaProgressManager.Instance.GetCardUpgradeCost(cardKey);
            bool maxed = level >= MetaProgressManager.MaxCardUpgradeLevel;

            SetChildText(row, "NameLabel", cardSO.DisplayName);

            if (maxed)
            {
                SetChildText(row, "InfoLabel", $"Lv {level} (MAX)  +{MetaProgressManager.CardPowerBonusForLevel(level)} power");
            }
            else
            {
                SetChildText(row, "InfoLabel",
                    $"Lv {level}/{MetaProgressManager.MaxCardUpgradeLevel}  +{MetaProgressManager.CardPowerBonusForLevel(level)} power  —  next: {cost} essence");
            }

            var button = FindChild(row, "UpgradeButton")?.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = !maxed && MetaProgressManager.Instance.CanUpgradeCard(cardKey);
                SetChildText(button.gameObject, "Text", maxed ? "MAX" : "Upgrade");

                var capturedKey = cardKey;
                button.onClick.AddListener(() => OnUpgrade(capturedKey));
            }

            _spawnedRows.Add(row);
        }

        private void OnUpgrade(string cardKey)
        {
            if (MetaProgressManager.Instance.TryUpgradeCard(cardKey))
            {
                Refresh();
            }
        }

        private void ClearRows()
        {
            foreach (var row in _spawnedRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }
            _spawnedRows.Clear();
        }

        private static Transform FindChild(GameObject root, string name)
        {
            return root != null ? root.transform.Find(name) : null;
        }

        private static void SetChildText(GameObject root, string childName, string text)
        {
            var child = FindChild(root, childName);
            if (child != null)
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = text;
                }
            }
        }
    }
}
