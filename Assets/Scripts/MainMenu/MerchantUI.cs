using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// Hub merchant (UI Toolkit view-controller). A Gold sink: enlarge the potion belt, buy gear
    /// from a rotating (persisted, paid-restock) stock, and sell un-equipped gear back at a loss.
    /// Operates on a VisualElement subtree owned by the menu's UIDocument — not a MonoBehaviour.
    /// </summary>
    public class MerchantUI
    {
        private const int PotionCostPerCurrentMax = 25;
        private const int StockSize = 4;
        private const int RestockCost = 25;

        private readonly VisualElement _root;
        private readonly Label _goldLabel;
        private readonly Label _essenceLabel;
        private readonly Label _feedbackLabel;
        private readonly Button _potionButton;
        private readonly Button _restockButton;
        private readonly ScrollView _shopList;
        private readonly ScrollView _sellList;
        private readonly Button _closeButton;

        public event Action OnClosed;

        public MerchantUI(VisualElement root)
        {
            _root = root;
            _goldLabel = root.Q<Label>("merchant-gold");
            _essenceLabel = root.Q<Label>("merchant-essence");
            _feedbackLabel = root.Q<Label>("merchant-feedback");
            _potionButton = root.Q<Button>("potion-btn");
            _restockButton = root.Q<Button>("restock-btn");
            _shopList = root.Q<ScrollView>("shop-list");
            _sellList = root.Q<ScrollView>("sell-list");
            _closeButton = root.Q<Button>("merchant-close");

            _potionButton.clicked += OnBuyPotionBelt;
            if (_restockButton != null)
            {
                _restockButton.clicked += OnRestock;
            }
            _closeButton.clicked += Hide;

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            SetFeedback(string.Empty);
            EnsureStock();
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

            if (_restockButton != null)
            {
                _restockButton.text = $"Restock Wares — {RestockCost} gold";
                _restockButton.SetEnabled(gold >= RestockCost);
            }

            BuildShopList(gold);
            BuildSellList();
        }

        // --- Buy ---------------------------------------------------------------

        private void BuildShopList(int gold)
        {
            if (_shopList == null)
            {
                return;
            }
            _shopList.Clear();

            var inv = InventoryManager.Instance;
            var stock = MetaProgressManager.Instance.GetShopStock();
            if (stock.Count == 0)
            {
                _shopList.Add(MakeEmptyLabel("Sold out — restock for more."));
                return;
            }

            foreach (var key in stock)
            {
                var so = inv.GetItemSO(key);
                if (so == null)
                {
                    continue;
                }
                int price = ShopPricing.BuyPrice(so);
                string captured = key;
                var soCaptured = so;
                _shopList.Add(MakeRow(so, $"{so.DisplayName}  ({so.Rarity})", $"Buy — {price}g",
                    gold >= price, () => OnBuy(captured, soCaptured, price)));
            }
        }

        private void OnBuy(string key, ItemSO so, int price)
        {
            if (!MetaProgressManager.Instance.TrySpendGold(price))
            {
                SetFeedback("Not enough gold.");
                return;
            }

            InventoryManager.Instance.AddItem(so);
            MetaProgressManager.Instance.RemoveFromShopStock(key);
            SetFeedback($"Bought {so.DisplayName}.");
            Refresh();
        }

        private void OnRestock()
        {
            if (!MetaProgressManager.Instance.TrySpendGold(RestockCost))
            {
                SetFeedback("Not enough gold to restock.");
                return;
            }

            MetaProgressManager.Instance.SetShopStock(GenerateStock(StockSize));
            SetFeedback("The merchant lays out fresh wares.");
            Refresh();
        }

        // --- Sell --------------------------------------------------------------

        private void BuildSellList()
        {
            if (_sellList == null)
            {
                return;
            }
            _sellList.Clear();

            var inv = InventoryManager.Instance;
            var bag = inv.GetBagEquipment();
            if (bag.Count == 0)
            {
                _sellList.Add(MakeEmptyLabel("No spare gear to sell."));
                return;
            }

            foreach (var entry in bag)
            {
                var so = inv.GetItemSO(entry.ItemKey);
                if (so == null)
                {
                    continue;
                }
                int price = ShopPricing.SellPrice(so);
                string captured = entry.ItemKey;
                var soCaptured = so;
                _sellList.Add(MakeRow(so, $"{so.DisplayName}  ({so.Rarity})", $"Sell — {price}g",
                    true, () => OnSell(captured, soCaptured, price)));
            }
        }

        private void OnSell(string key, ItemSO so, int price)
        {
            // Only ever sell an un-equipped (bag) copy, and only pay out if one was actually removed.
            if (!InventoryManager.Instance.RemoveBagEquipment(key))
            {
                SetFeedback("Nothing to sell.");
                return;
            }
            MetaProgressManager.Instance.AddGold(price);
            SetFeedback($"Sold {so.DisplayName} for {price}g.");
            Refresh();
        }

        // --- Stock generation --------------------------------------------------

        private void EnsureStock()
        {
            if (MetaProgressManager.Instance.GetShopStock().Count == 0)
            {
                MetaProgressManager.Instance.SetShopStock(GenerateStock(StockSize));
            }
        }

        private List<string> GenerateStock(int count)
        {
            var catalog = UnityEngine.Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            var pool = catalog != null
                ? catalog.Items.Where(i => i != null && i.Category == ItemCategory.Equipment && !string.IsNullOrEmpty(i.Key)).ToList()
                : new List<ItemSO>();

            var picked = new List<string>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var item = WeightedPick(pool);
                picked.Add(item.Key);
                pool.Remove(item); // no duplicate stock entries
            }
            return picked;
        }

        private static ItemSO WeightedPick(List<ItemSO> pool)
        {
            float total = pool.Sum(RarityWeight);
            float roll = UnityEngine.Random.Range(0f, total);
            foreach (var it in pool)
            {
                roll -= RarityWeight(it);
                if (roll <= 0f)
                {
                    return it;
                }
            }
            return pool[pool.Count - 1];
        }

        private static float RarityWeight(ItemSO item)
        {
            switch (item.Rarity)
            {
                case ItemRarity.Common:
                    return 1.0f;
                case ItemRarity.Uncommon:
                    return 0.6f;
                case ItemRarity.Rare:
                    return 0.3f;
                case ItemRarity.Epic:
                    return 0.15f;
                case ItemRarity.Legendary:
                    return 0.06f;
                default:
                    return 1.0f;
            }
        }

        // --- Row helpers -------------------------------------------------------

        private static VisualElement MakeRow(ItemSO so, string label, string buttonText, bool enabled, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-shop-row");

            var icon = new VisualElement();
            icon.AddToClassList("cd-shop-row__icon");
            if (so.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(so.Icon);
            }

            var name = new Label(label);
            name.AddToClassList("cd-shop-row__name");

            var button = new Button(() => onClick()) { text = buttonText };
            button.AddToClassList("cd-button");
            button.AddToClassList("cd-shop-row__btn");
            button.SetEnabled(enabled);

            row.Add(icon);
            row.Add(name);
            row.Add(button);
            return row;
        }

        private static Label MakeEmptyLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("cd-shop-empty");
            return label;
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
