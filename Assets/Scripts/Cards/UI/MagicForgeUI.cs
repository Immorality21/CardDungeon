using System;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Hub "Forge" (UI Toolkit view-controller). Spends Essence to permanently upgrade
    /// magic types (per key). Lists every magic in the MagicCatalog as a row with its
    /// level/cost and an Upgrade button. Operates on a VisualElement subtree; not a MonoBehaviour.
    /// </summary>
    public class MagicForgeUI
    {
        private readonly VisualElement _root;
        private readonly Label _essenceLabel;
        private readonly Label _emptyLabel;
        private readonly ScrollView _scroll;
        private readonly Button _closeButton;

        public event Action OnClosed;

        public MagicForgeUI(VisualElement root)
        {
            _root = root;
            _essenceLabel = root.Q<Label>("forge-essence");
            _emptyLabel = root.Q<Label>("forge-empty");
            _scroll = root.Q<ScrollView>("forge-scroll");
            _closeButton = root.Q<Button>("forge-close");

            _closeButton.clicked += Hide;

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            if (!MagicCatalog.HasInstance)
            {
                Debug.LogWarning("MagicCatalog not found. Cannot open magic upgrades.");
                return;
            }

            _root.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _scroll.Clear();
            OnClosed?.Invoke();
        }

        private void Refresh()
        {
            _scroll.Clear();
            _essenceLabel.text = $"Essence: {MetaProgressManager.Instance.Essence}";

            var catalog = MagicCatalog.Instance.AllMagic;
            SetShown(_emptyLabel, catalog.Count == 0);

            foreach (var magic in catalog)
            {
                if (magic == null || string.IsNullOrEmpty(magic.Key))
                {
                    continue;
                }
                _scroll.Add(BuildRow(magic.Key, magic));
            }
        }

        private VisualElement BuildRow(string magicKey, MagicSO magic)
        {
            int level = MetaProgressManager.Instance.GetMagicUpgradeLevel(magicKey);
            int cost = MetaProgressManager.Instance.GetMagicUpgradeCost(magicKey);
            bool maxed = level >= MetaProgressManager.MaxMagicUpgradeLevel;

            var row = new VisualElement();
            row.AddToClassList("cd-forge-row");

            var textCol = new VisualElement();
            textCol.AddToClassList("cd-forge-row__text");
            textCol.pickingMode = PickingMode.Ignore;

            var nameLabel = new Label(magic.DisplayName);
            nameLabel.AddToClassList("cd-forge-row__name");
            textCol.Add(nameLabel);

            string info = maxed
                ? $"Lv {level} (MAX)  +{MetaProgressManager.MagicPowerBonusForLevel(level)} power"
                : $"Lv {level}/{MetaProgressManager.MaxMagicUpgradeLevel}  +{MetaProgressManager.MagicPowerBonusForLevel(level)} power  —  next: {cost} essence";
            var infoLabel = new Label(info);
            infoLabel.AddToClassList("cd-forge-row__info");
            textCol.Add(infoLabel);

            row.Add(textCol);

            var capturedKey = magicKey;
            var upgradeBtn = new Button(() => OnUpgrade(capturedKey)) { text = maxed ? "MAX" : "Upgrade" };
            upgradeBtn.AddToClassList("cd-forge-row__btn");
            upgradeBtn.SetEnabled(!maxed && MetaProgressManager.Instance.CanUpgradeMagic(magicKey));
            row.Add(upgradeBtn);

            return row;
        }

        private void OnUpgrade(string magicKey)
        {
            if (MetaProgressManager.Instance.TryUpgradeMagic(magicKey))
            {
                Refresh();
            }
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
