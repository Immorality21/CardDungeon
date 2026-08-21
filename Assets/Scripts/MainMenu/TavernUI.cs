using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// Hub tavern (UI Toolkit view-controller). The reliable, paid route to a bigger roster, and the
    /// game's largest Gold sink: a rotating, persisted, paid-restock offer of heroes the player does
    /// not own yet. The unreliable route is rescuing a captive mid-dungeon — free, but you do not get
    /// to choose who.
    ///
    /// Stock lives in <see cref="MetaProgressSaveData.TavernStock"/> for the same reason the
    /// merchant's does: reopening the screen must not re-roll the offer for free. Operates on a
    /// VisualElement subtree owned by the menu's UIDocument — not a MonoBehaviour.
    /// </summary>
    public class TavernUI
    {
        private const int StockSize = 3;
        private const int RestockCost = 40;

        private readonly VisualElement _root;
        private readonly PartyRosterSO _catalog;
        private readonly Label _goldLabel;
        private readonly Label _rosterLabel;
        private readonly Label _feedbackLabel;
        private readonly ScrollView _stockList;
        private readonly Button _restockButton;
        private readonly Button _closeButton;

        public event Action OnClosed;

        public TavernUI(VisualElement root, PartyRosterSO catalog)
        {
            _root = root;
            _catalog = catalog;

            _goldLabel = root.Q<Label>("tavern-gold");
            _rosterLabel = root.Q<Label>("tavern-roster");
            _feedbackLabel = root.Q<Label>("tavern-feedback");
            _stockList = root.Q<ScrollView>("tavern-list");
            _restockButton = root.Q<Button>("tavern-restock");
            _closeButton = root.Q<Button>("tavern-close");

            if (_restockButton != null)
            {
                _restockButton.clicked += OnRestock;
            }
            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
            }

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

        private void Refresh()
        {
            int gold = MetaProgressManager.Instance.Gold;
            _goldLabel.text = $"Gold: {gold}";

            var owned = HeroRoster.GetOwnedHeroes(_catalog);
            _rosterLabel.text = owned.Count == 0
                ? "No heroes."
                : $"Roster ({owned.Count}): {string.Join(", ", owned.ConvertAll(h => h.DisplayName))}";

            if (_restockButton != null)
            {
                bool anyLeft = HeroRoster.GetRecruitable(_catalog).Count > 0;
                _restockButton.text = anyLeft
                    ? $"New Arrivals — {RestockCost} gold"
                    : "Nobody else is looking for work";
                _restockButton.SetEnabled(anyLeft && gold >= RestockCost);
            }

            BuildStockList(gold);
        }

        private void BuildStockList(int gold)
        {
            if (_stockList == null)
            {
                return;
            }
            _stockList.Clear();

            var stock = ResolveStock();
            if (stock.Count == 0)
            {
                bool anyLeft = HeroRoster.GetRecruitable(_catalog).Count > 0;
                _stockList.Add(MakeEmptyLabel(anyLeft
                    ? "The tavern is empty tonight — buy a round to draw a crowd."
                    : "Every hero in the land already answers to you."));
                return;
            }

            foreach (var hero in stock)
            {
                int price = ShopPricing.RecruitPrice(hero);
                var captured = hero;
                _stockList.Add(MakeRow(hero, price, gold >= price, () => OnRecruit(captured, price)));
            }
        }

        private void OnRecruit(HeroSO hero, int price)
        {
            // Ownership first: TryAddOwned is the authority on "already recruited", so a double
            // click cannot charge twice.
            if (!HeroRoster.TryAddOwned(_catalog, hero))
            {
                SetFeedback($"{hero.DisplayName} already rides with you.");
                MetaProgressManager.Instance.RemoveFromTavernStock(hero.SaveKey);
                Refresh();
                return;
            }

            if (!MetaProgressManager.Instance.TrySpendGold(price))
            {
                // Roll the recruitment back rather than handing out a free hero.
                HeroRoster.RemoveOwned(_catalog, hero);
                SetFeedback("Not enough gold.");
                return;
            }

            MetaProgressManager.Instance.RemoveFromTavernStock(hero.SaveKey);
            SetFeedback($"{hero.DisplayName} joins your roster.");
            Refresh();
        }

        private void OnRestock()
        {
            if (!MetaProgressManager.Instance.TrySpendGold(RestockCost))
            {
                SetFeedback("Not enough gold for a round.");
                return;
            }

            MetaProgressManager.Instance.SetTavernStock(GenerateStock(StockSize));
            SetFeedback("Word gets around — new faces at the bar.");
            Refresh();
        }

        // --- Stock -------------------------------------------------------------

        private void EnsureStock()
        {
            if (ResolveStock().Count == 0 && HeroRoster.GetRecruitable(_catalog).Count > 0)
            {
                MetaProgressManager.Instance.SetTavernStock(GenerateStock(StockSize));
            }
        }

        /// <summary>
        /// Persisted stock as definitions, dropping anything the player has since acquired (a
        /// captive rescued in a dungeon can be sitting in the tavern's stored offer).
        /// </summary>
        private List<HeroSO> ResolveStock()
        {
            var result = new List<HeroSO>();
            var recruitable = HeroRoster.GetRecruitable(_catalog);
            foreach (var key in MetaProgressManager.Instance.GetTavernStock())
            {
                var hero = _catalog != null ? _catalog.Find(key) : null;
                if (hero != null && recruitable.Contains(hero) && !result.Contains(hero))
                {
                    result.Add(hero);
                }
            }
            return result;
        }

        private List<string> GenerateStock(int count)
        {
            var pool = HeroRoster.GetRecruitable(_catalog);
            var picked = new List<string>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                picked.Add(pool[index].SaveKey);
                pool.RemoveAt(index); // no duplicate entries in one offer
            }
            return picked;
        }

        // --- Rows --------------------------------------------------------------

        private static VisualElement MakeRow(HeroSO hero, int price, bool affordable, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-shop-row");

            var icon = new VisualElement();
            icon.AddToClassList("cd-shop-row__icon");
            if (hero.Sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(hero.Sprite);
            }

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1f;

            // Show every stat the hero actually has. Listing four by hand hid the caster stats,
            // so the Acolyte read as a weak fighter rather than the party's only real caster.
            var statParts = new List<string>();
            foreach (var stat in StatCatalog.Types)
            {
                int value = hero.BaseStats[stat];
                if (value != 0)
                {
                    statParts.Add(StatCatalog.ShortName(stat) + " " + value);
                }
            }

            var name = new Label(hero.DisplayName + "   " + string.Join(" · ", statParts));
            name.AddToClassList("cd-shop-row__name");
            textCol.Add(name);

            if (!string.IsNullOrEmpty(hero.Blurb))
            {
                var blurb = new Label(hero.Blurb);
                blurb.AddToClassList("cd-shop-row__sub");
                textCol.Add(blurb);
            }

            var button = new Button(() => onClick()) { text = $"Recruit — {price}g" };
            button.AddToClassList("cd-button");
            button.AddToClassList("cd-shop-row__btn");
            button.SetEnabled(affordable);

            row.Add(icon);
            row.Add(textCol);
            row.Add(button);
            return row;
        }

        private static Label MakeEmptyLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("cd-shop-empty");
            return label;
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
