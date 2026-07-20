using System;
using Assets.Scripts.Cards;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// Hub merchant that spends Gold. Offers are fixed for now: buy a random card
    /// (feeds the collection) and enlarge the potion belt (raises healing-potion max).
    /// Both scale in cost so gold stays meaningful across a campaign.
    /// </summary>
    public class MerchantUI : MonoBehaviour
    {
        private const int CardPackCost = 40;
        private const int PotionCostPerCurrentMax = 25;

        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Currency Display")]
        [SerializeField] private TextMeshProUGUI _goldLabel;
        [SerializeField] private TextMeshProUGUI _essenceLabel;

        [Header("Card Pack Offer")]
        [SerializeField] private Button _cardPackButton;
        [SerializeField] private TextMeshProUGUI _cardPackLabel;

        [Header("Potion Belt Offer")]
        [SerializeField] private Button _potionButton;
        [SerializeField] private TextMeshProUGUI _potionLabel;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI _feedbackLabel;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        public event Action OnClosed;

        private void Start()
        {
            if (_rootPanel != null)
            {
                _rootPanel.SetActive(false);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
            if (_cardPackButton != null)
            {
                _cardPackButton.onClick.AddListener(OnBuyCardPack);
            }
            if (_potionButton != null)
            {
                _potionButton.onClick.AddListener(OnBuyPotionBelt);
            }
        }

        public void Show()
        {
            if (_rootPanel != null)
            {
                _rootPanel.SetActive(true);
            }
            SetFeedback(string.Empty);
            Refresh();
        }

        public void Hide()
        {
            if (_rootPanel != null)
            {
                _rootPanel.SetActive(false);
            }
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

            if (_goldLabel != null)
            {
                _goldLabel.text = $"Gold: {gold}";
            }
            if (_essenceLabel != null)
            {
                _essenceLabel.text = $"Essence: {essence}";
            }

            if (_cardPackLabel != null)
            {
                _cardPackLabel.text = $"Card Pack — {CardPackCost} gold";
            }
            if (_cardPackButton != null)
            {
                _cardPackButton.interactable = gold >= CardPackCost && CardCollectionManager.HasInstance;
            }

            int potionCost = PotionBeltCost();
            int currentMax = PartyResourceManager.Instance.GetMax(PartyResourceType.HealingPotion);
            if (_potionLabel != null)
            {
                _potionLabel.text = $"Enlarge Potion Belt ({currentMax} → {currentMax + 1}) — {potionCost} gold";
            }
            if (_potionButton != null)
            {
                _potionButton.interactable = gold >= potionCost;
            }
        }

        private void OnBuyCardPack()
        {
            if (!CardCollectionManager.HasInstance)
            {
                SetFeedback("No card catalog available.");
                return;
            }

            if (!MetaProgressManager.Instance.TrySpendGold(CardPackCost))
            {
                SetFeedback("Not enough gold.");
                return;
            }

            var card = CardCollectionManager.Instance.GetRandomCard();
            if (card == null)
            {
                // Refund if the catalog was unexpectedly empty
                MetaProgressManager.Instance.AddGold(CardPackCost);
                SetFeedback("The merchant is out of cards.");
                return;
            }

            CardCollectionManager.Instance.AddCard(card);
            SetFeedback($"You bought: {card.DisplayName}!");
            Refresh();
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
