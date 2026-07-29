using System;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// Hub merchant (UI Toolkit view-controller). Spends Gold to enlarge the potion belt
    /// (raises healing-potion max). Operates on a VisualElement subtree owned by the menu's
    /// UIDocument — not a MonoBehaviour. Cost scales so gold stays meaningful.
    /// </summary>
    public class MerchantUI
    {
        private const int PotionCostPerCurrentMax = 25;

        private readonly VisualElement _root;
        private readonly Label _goldLabel;
        private readonly Label _essenceLabel;
        private readonly Label _feedbackLabel;
        private readonly Button _potionButton;
        private readonly Button _closeButton;

        public event Action OnClosed;

        public MerchantUI(VisualElement root)
        {
            _root = root;
            _goldLabel = root.Q<Label>("merchant-gold");
            _essenceLabel = root.Q<Label>("merchant-essence");
            _feedbackLabel = root.Q<Label>("merchant-feedback");
            _potionButton = root.Q<Button>("potion-btn");
            _closeButton = root.Q<Button>("merchant-close");

            _potionButton.clicked += OnBuyPotionBelt;
            _closeButton.clicked += Hide;

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            SetFeedback(string.Empty);
            Refresh();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        private int PotionBeltCost()
        {
            int currentMax = PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion);
            return PotionCostPerCurrentMax * Mathf.Max(1, currentMax);
        }

        private void Refresh()
        {
            int gold = MetaProgressManager.Instance.Gold;
            int essence = MetaProgressManager.Instance.Essence;

            _goldLabel.text = $"Gold: {gold}";
            _essenceLabel.text = $"Essence: {essence}";

            int potionCost = PotionBeltCost();
            int currentMax = PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion);
            _potionButton.text = $"Enlarge Potion Belt ({currentMax} → {currentMax + 1}) — {potionCost} gold";
            _potionButton.SetEnabled(gold >= potionCost);
        }

        private void OnBuyPotionBelt()
        {
            int cost = PotionBeltCost();
            if (!MetaProgressManager.Instance.TrySpendGold(cost))
            {
                SetFeedback("Not enough gold.");
                return;
            }

            int newMax = PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion) + 1;
            PartyResourceManager.Instance.SetMax(PartyResourceType.HealingPotion, newMax);
            SetFeedback($"Potion belt enlarged to {newMax}!");
            Refresh();
        }

        private void SetFeedback(string message)
        {
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = message;
            }
        }
    }
}
