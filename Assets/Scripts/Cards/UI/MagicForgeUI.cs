using System;
using System.Collections.Generic;
using Assets.Scripts.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Hub "Forge" panel that spends Essence to permanently upgrade magic types.
    /// Upgrades are per magic key — whenever the player draws that magic during a run,
    /// it carries the upgraded power. Rows are cloned at runtime from an inactive template.
    /// </summary>
    public class MagicForgeUI : MonoBehaviour
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
            if (!MagicCatalog.HasInstance)
            {
                Debug.LogWarning("MagicCatalog not found. Cannot open magic upgrades.");
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

            var catalog = MagicCatalog.Instance.AllMagic;

            if (_emptyLabel != null)
            {
                _emptyLabel.gameObject.SetActive(catalog.Count == 0);
            }

            if (_rowTemplate == null || _rowParent == null)
            {
                return;
            }

            foreach (var magic in catalog)
            {
                if (magic == null || string.IsNullOrEmpty(magic.Key))
                {
                    continue;
                }
                SpawnRow(magic.Key, magic);
            }
        }

        private void SpawnRow(string magicKey, MagicSO magic)
        {
            var row = Instantiate(_rowTemplate, _rowParent);
            row.SetActive(true);

            int level = MetaProgressManager.Instance.GetMagicUpgradeLevel(magicKey);
            int cost = MetaProgressManager.Instance.GetMagicUpgradeCost(magicKey);
            bool maxed = level >= MetaProgressManager.MaxMagicUpgradeLevel;

            SetChildText(row, "NameLabel", magic.DisplayName);

            if (maxed)
            {
                SetChildText(row, "InfoLabel", $"Lv {level} (MAX)  +{MetaProgressManager.MagicPowerBonusForLevel(level)} power");
            }
            else
            {
                SetChildText(row, "InfoLabel",
                    $"Lv {level}/{MetaProgressManager.MaxMagicUpgradeLevel}  +{MetaProgressManager.MagicPowerBonusForLevel(level)} power  —  next: {cost} essence");
            }

            var button = FindChild(row, "UpgradeButton")?.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = !maxed && MetaProgressManager.Instance.CanUpgradeMagic(magicKey);
                SetChildText(button.gameObject, "Text", maxed ? "MAX" : "Upgrade");

                var capturedKey = magicKey;
                button.onClick.AddListener(() => OnUpgrade(capturedKey));
            }

            _spawnedRows.Add(row);
        }

        private void OnUpgrade(string magicKey)
        {
            if (MetaProgressManager.Instance.TryUpgradeMagic(magicKey))
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
