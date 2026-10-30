using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Balance;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Items.UI;
using Assets.Scripts.Progression;
using Assets.Scripts.Resources;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;
using Assets.Scripts.Events;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// Hub merchant (UI Toolkit view-controller). A Gold sink: buy gear from a rotating (persisted,
    /// paid-restock) stock, sell un-equipped gear back at a loss, and enlarge the potion belt.
    /// Operates on a VisualElement subtree owned by the menu's UIDocument — not a MonoBehaviour.
    ///
    /// <para><b>On the inventory frame since 2026-09-30</b> (menu review): Buy / Sell /
    /// Potion Belt tabs, a hero strip that picks who an item is compared against, the list, and a
    /// detail column with the item's grants, every stat now → if worn, the price and the action.
    /// It used to be two 70px scrollers of "Simple Sword (Common)" with nothing to judge a purchase by.
    /// A row only selects; the detail column's button buys or sells, and says why when it cannot.</para>
    /// </summary>
    public class MerchantUI
    {
        private const int StockSize = 4;
        private const int RestockCost = 25;
        private const string PotionItemKey = "HealingPotion";

        private enum Tab
        {
            Buy,
            Sell,
            Belt,
        }

        /// <summary>One row of the list and everything its detail and action need.</summary>
        private class Entry
        {
            public ItemSO Item;
            public string Key;
            public int Price;
            public Button Row;

            // Potion Belt tab only: what is on offer, for which potion type, and its next step
            // (null once that ladder is all bought).
            public BeltOffer Offer;
            public BeltPotionType Type;
            public BeltPrice Upgrade;
        }

        private enum BeltOffer
        {
            Slot,
            Recipe,
            Capacity,
            Potency,
        }

        private readonly VisualElement _root;
        private readonly PartyRosterSO _roster;
        private readonly Label _goldLabel;
        private readonly Label _feedbackLabel;
        private readonly Button _tabBuy;
        private readonly Button _tabSell;
        private readonly Button _tabBelt;
        private readonly VisualElement _heroesRow;
        private readonly Label _listTitle;
        private readonly ScrollView _list;
        private readonly Label _emptyLabel;
        private readonly Button _restockButton;
        private readonly VisualElement _detailIcon;
        private readonly Label _detailName;
        private readonly Label _detailSub;
        private readonly Button _actionButton;
        private readonly Label _reasonLabel;
        private readonly ScrollView _detailBody;
        private readonly Button _closeButton;

        private readonly List<Entry> _entries = new List<Entry>();
        private Tab _tab = Tab.Buy;
        private int _index;
        private string _compareHeroKey;

        public event Action OnClosed;

        public MerchantUI(VisualElement root, PartyRosterSO roster)
        {
            _root = root;
            _roster = roster;
            _goldLabel = root.Q<Label>("merchant-gold");
            _feedbackLabel = root.Q<Label>("merchant-feedback");
            _tabBuy = root.Q<Button>("merchant-tab-buy");
            _tabSell = root.Q<Button>("merchant-tab-sell");
            _tabBelt = root.Q<Button>("merchant-tab-belt");
            _heroesRow = root.Q<VisualElement>("merchant-heroes");
            _listTitle = root.Q<Label>("merchant-list-title");
            _list = root.Q<ScrollView>("merchant-list");
            _emptyLabel = root.Q<Label>("merchant-empty");
            _restockButton = root.Q<Button>("restock-btn");
            _detailIcon = root.Q<VisualElement>("merchant-detail-icon");
            _detailName = root.Q<Label>("merchant-detail-name");
            _detailSub = root.Q<Label>("merchant-detail-sub");
            _actionButton = root.Q<Button>("merchant-action");
            _reasonLabel = root.Q<Label>("merchant-reason");
            _detailBody = root.Q<ScrollView>("merchant-detail-body");
            _closeButton = root.Q<Button>("merchant-close");

            WireTab(_tabBuy, Tab.Buy);
            WireTab(_tabSell, Tab.Sell);
            WireTab(_tabBelt, Tab.Belt);
            if (_restockButton != null)
            {
                _restockButton.clicked += OnRestock;
            }
            if (_actionButton != null)
            {
                _actionButton.clicked += OnAction;
            }
            _closeButton.clicked += Hide;

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            SetFeedback(string.Empty);
            EnsureStock();
            _tab = Tab.Buy;
            _index = 0;
            var fielded = _roster != null
                ? HeroRoster.GetSelectedKeys(_roster, MetaProgressManager.Instance.GetPartyCap())
                : new List<string>();
            _compareHeroKey = fielded.Count > 0 ? fielded[0] : null;
            Refresh();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        private void WireTab(Button tab, Tab which)
        {
            if (tab == null)
            {
                return;
            }
            tab.clicked += () =>
            {
                _tab = which;
                _index = 0;
                SetFeedback(string.Empty);
                Refresh();
            };
        }

        /// <summary>Whether the bags and the purse both cover <paramref name="price"/>.</summary>
        private static bool CanPay(BeltPrice price)
        {
            return price != null && Gold >= price.Gold && InventoryManager.Instance.CanAfford(price.Materials);
        }

        /// <summary>A healing potion's share of max health with the potency bought, as a whole percent.</summary>
        private static int PotionPercent(ItemSO potion, float bonus)
        {
            return Mathf.RoundToInt(((potion != null ? potion.ConsumablePercent : 0f) + bonus) * 100f);
        }

        private static int Gold => MetaProgressManager.Instance.Gold;

        // --- Refresh -----------------------------------------------------------

        private void Refresh()
        {
            // Only the currency this screen spends - essence has never bought anything here.
            SetText(_goldLabel, Loc.Format("hub.merchant.gold", ("gold", Gold)));
            _tabBuy?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Buy);
            _tabSell?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Sell);
            _tabBelt?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Belt);

            RefreshHeroes();
            BuildList();
            RefreshDetail();
        }

        /// <summary>
        /// Who a piece of gear is compared against. Muted, never hidden, on the belt tab - hiding it
        /// would move the window's regions, and the frame exists so nothing moves.
        /// </summary>
        private void RefreshHeroes()
        {
            if (_heroesRow == null)
            {
                return;
            }
            _heroesRow.Clear();
            bool active = _tab != Tab.Belt;
            _heroesRow.EnableInClassList("cd-inv-heroes--muted", !active);

            var heroes = _roster != null ? HeroRoster.GetOwnedHeroes(_roster) : new List<HeroSO>();
            foreach (var hero in heroes)
            {
                if (hero == null)
                {
                    continue;
                }
                string key = hero.SaveKey;
                var card = new Button(() => CompareAgainst(key)) { text = string.Empty, tooltip = hero.DisplayName };
                card.AddToClassList("cd-inv-hero");
                card.EnableInClassList("cd-inv-hero--active", active && key == _compareHeroKey);
                card.SetEnabled(active);

                var sprite = new VisualElement { pickingMode = PickingMode.Ignore };
                sprite.AddToClassList("cd-inv-hero__sprite");
                if (hero.Sprite != null)
                {
                    sprite.style.backgroundImage = new StyleBackground(hero.Sprite);
                }
                card.Add(sprite);
                card.Add(MakeLabel(hero.DisplayName, "cd-inv-hero__name"));
                _heroesRow.Add(card);
            }

            var summary = new VisualElement { pickingMode = PickingMode.Ignore };
            summary.AddToClassList("cd-inv-heroes__summary");
            var compare = FindHero(_compareHeroKey);
            if (active && compare != null)
            {
                summary.Add(MakeLabel(Loc.Format("hub.merchant.compared-on", ("hero", compare.DisplayName)), "cd-inv-heroes__summary-line", "cd-inv-heroes__summary-name"));
                summary.Add(MakeLabel(Loc.Get("hub.merchant.pick-hero"), "cd-inv-heroes__summary-line"));
            }
            _heroesRow.Add(summary);
        }

        /// <summary>Repaints only the strip's highlight and the detail, so the keyboard cursor stays
        /// on the card it just pressed.</summary>
        private void CompareAgainst(string heroKey)
        {
            _compareHeroKey = heroKey;
            var hero = FindHero(heroKey);
            _heroesRow?.Query<Button>(className: "cd-inv-hero").ForEach(card =>
                card.EnableInClassList("cd-inv-hero--active", hero != null && card.tooltip == hero.DisplayName));
            var line = _heroesRow?.Q<Label>(className: "cd-inv-heroes__summary-name");
            if (line != null && hero != null)
            {
                line.text = Loc.Format("hub.merchant.compared-on", ("hero", hero.DisplayName));
            }
            RefreshDetail();
        }

        private void BuildList()
        {
            _list?.Clear();
            _entries.Clear();
            var inv = InventoryManager.Instance;

            SetShown(_restockButton, _tab == Tab.Buy);
            if (_restockButton != null)
            {
                _restockButton.text = Loc.Format("hub.merchant.restock", ("cost", RestockCost));
                _restockButton.SetEnabled(Gold >= RestockCost);
            }

            string empty = string.Empty;
            switch (_tab)
            {
                case Tab.Buy:
                    SetText(_listTitle, Loc.Get("hub.merchant.tab.wares"));
                    foreach (var key in MetaProgressManager.Instance.GetShopStock())
                    {
                        var so = inv.GetItemSO(key);
                        if (so != null)
                        {
                            AddEntry(new Entry { Item = so, Key = key, Price = ShopPricing.BuyPrice(so) });
                        }
                    }
                    empty = Loc.Get("hub.merchant.empty.sold-out");
                    break;

                case Tab.Sell:
                    SetText(_listTitle, Loc.Get("hub.merchant.tab.spare-gear"));
                    foreach (var saved in inv.GetBagEquipment())
                    {
                        var so = inv.GetItemSO(saved.ItemKey);
                        if (so != null)
                        {
                            AddEntry(new Entry { Item = so, Key = saved.ItemKey, Price = ShopPricing.SellPrice(so) });
                        }
                    }
                    empty = Loc.Get("hub.merchant.empty.no-spare");
                    break;

                case Tab.Belt:
                    SetText(_listTitle, Loc.Get("hub.merchant.tab.belt"));
                    AddBeltEntries(inv.GetItemSO(PotionItemKey));
                    empty = Loc.Get("hub.merchant.empty.no-belt");
                    break;
            }

            bool none = _entries.Count == 0;
            SetShown(_emptyLabel, none);
            SetText(_emptyLabel, none ? empty : string.Empty);
            _index = none ? -1 : Mathf.Clamp(_index, 0, _entries.Count - 1);
            PaintSelection();
        }

        private void AddEntry(Entry entry)
        {
            int index = _entries.Count;
            var row = new Button(() => Select(index)) { text = string.Empty };
            row.AddToClassList("cd-inv-row");
            row.AddToClassList("cd-camp-row");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("cd-inv-row__icon");
            InventoryHubUI.SetIcon(icon, entry.Item != null ? entry.Item.Icon : null, _tab == Tab.Belt ? null : entry.Item);
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            var name = MakeLabel(RowName(entry), "cd-inv-row__name");
            if (_tab != Tab.Belt)
            {
                InventoryHubUI.ApplyRarity(name, entry.Item);
            }
            text.Add(name);
            text.Add(MakeLabel(RowCaption(entry), "cd-inv-row__caption"));
            row.Add(text);

            // Buying: the price, red when it is out of reach. Selling: what it fetches. A belt step
            // is red when its materials are short too; a ladder bought to the top says so.
            bool beltMaxed = _tab == Tab.Belt && entry.Upgrade == null;
            var price = MakeLabel(beltMaxed ? Loc.Get("hub.merchant.price-max") : Loc.Format("hub.merchant.price", ("price", entry.Price)), "cd-inv-row__meta", "cd-shop-price");
            bool short_ = _tab == Tab.Belt ? !beltMaxed && !CanPay(entry.Upgrade) : _tab != Tab.Sell && Gold < entry.Price;
            price.EnableInClassList("cd-shop-price--short", short_);
            row.Add(price);

            entry.Row = row;
            _entries.Add(entry);
            _list?.Add(row);
        }

        private string RowName(Entry entry)
        {
            if (_tab == Tab.Belt)
            {
                switch (entry.Offer)
                {
                    case BeltOffer.Slot:
                        return Loc.Get("hub.merchant.belt.another-slot");
                    case BeltOffer.Recipe:
                        return Loc.Format("hub.merchant.belt.learn", ("potion", entry.Type.Potion.DisplayName));
                    case BeltOffer.Capacity:
                        return Loc.Format("hub.merchant.belt.carry-more", ("potion", entry.Type.DisplayName));
                    default:
                        return Loc.Format("hub.merchant.belt.stronger", ("potion", entry.Type.DisplayName));
                }
            }
            return entry.Item.DisplayName;
        }

        private string RowCaption(Entry entry)
        {
            if (_tab == Tab.Belt)
            {
                return BeltChange(entry);
            }
            return $"{ItemPresenter.SlotLabel(entry.Item.SlotType)} · {ItemPresenter.RarityLabel(entry.Item.Rarity)}";
        }

        private void Select(int index)
        {
            _index = index;
            PaintSelection();
            RefreshDetail();
        }

        private void PaintSelection()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Row.EnableInClassList("cd-inv-row--selected", i == _index);
            }
        }

        // --- Detail ------------------------------------------------------------

        private Entry Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

        private void RefreshDetail()
        {
            _detailBody?.Clear();
            var entry = Current;
            if (entry == null)
            {
                InventoryHubUI.SetIcon(_detailIcon, null, null);
                SetText(_detailName, string.Empty);
                SetText(_detailSub, string.Empty);
                SetText(_reasonLabel, string.Empty);
                SetShown(_actionButton, false);
                return;
            }
            SetShown(_actionButton, true);

            if (_tab == Tab.Belt)
            {
                ShowBeltDetail(entry);
                return;
            }

            var item = entry.Item;
            InventoryHubUI.SetIcon(_detailIcon, item.Icon, item);
            SetText(_detailName, item.DisplayName);
            InventoryHubUI.ApplyRarity(_detailName, item);
            SetText(_detailSub, $"{ItemPresenter.SlotLabel(item.SlotType)} · {ItemPresenter.RarityLabel(item.Rarity)}");

            if (_tab == Tab.Buy)
            {
                SetAction(Loc.Format("hub.merchant.buy", ("price", entry.Price)), Gold >= entry.Price,
                    Loc.Format("hub.merchant.need-gold", ("price", entry.Price), ("gold", Gold)));
            }
            else
            {
                SetAction(Loc.Format("hub.merchant.sell", ("price", entry.Price)), true, string.Empty);
            }

            if (_detailBody == null)
            {
                return;
            }
            InventoryHubUI.AddItemLines(_detailBody, item);
            AddComparison(item);
        }

        /// <summary>
        /// Every stat of the compared hero now against wearing this - the inventory's "Stats if
        /// equipped" block, so buying reads the same as equipping.
        /// </summary>
        private void AddComparison(ItemSO item)
        {
            var hero = FindHero(_compareHeroKey);
            if (hero == null)
            {
                return;
            }

            var save = HeroRoster.GetHeroSave(hero);
            var nodes = save != null && save.ActivatedNodes != null ? save.ActivatedNodes : new List<string>();
            var baseStats = HeroStatCalculator.BaseStatsForNodes(hero, nodes);
            var worn = InventoryManager.Instance.GetEquippedGear(hero.SaveKey);
            var now = HeroStatCalculator.WithGear(baseStats, worn);
            // A shop sells fresh copies: the candidate has counted nothing.
            var then = HeroStatCalculator.WithGear(baseStats, ItemPresenter.SwapIn(worn, new GearPiece(item, null)));

            var inSlot = GearPiece.ItemsOf(worn).FirstOrDefault(w => w.SlotType == item.SlotType);
            _detailBody.Add(MakeLabel(inSlot != null
                    ? Loc.Format("hub.merchant.wears-now", ("hero", hero.DisplayName), ("item", inSlot.DisplayName))
                    : Loc.Format("hub.merchant.slot-empty", ("hero", hero.DisplayName)),
                "cd-inv-detail__note"));

            _detailBody.Add(BestiaryLineView.Section(Loc.Format("hub.merchant.if-wore", ("hero", hero.DisplayName))));
            var grid = new VisualElement();
            grid.AddToClassList("cd-inv-stats");
            foreach (var stat in StatCatalog.Types)
            {
                bool changes = then[stat] != now[stat];
                grid.Add(InventoryHubUI.StatCell(StatCatalog.ShortName(stat),
                    changes ? $"{now[stat]} → {then[stat]}" : now[stat].ToString(),
                    changes ? (then[stat] > now[stat] ? 1 : -1) : 0,
                    StatCatalog.DisplayName(stat)));
            }
            _detailBody.Add(grid);
        }

        /// <summary>
        /// One row per belt offer: another slot, then per potion type its capacity and potency
        /// ladders once known, or its recipe once on sale. A recipe still gated behind a run is not
        /// listed at all - the merchant does not advertise what the player has not reached.
        /// </summary>
        private void AddBeltEntries(ItemSO healingPotion)
        {
            var belt = PotionBeltSO.Load();
            if (belt == null)
            {
                return;
            }
            AddBelt(BeltOffer.Slot, null, PotionBeltOps.NextSlot(belt, PotionBeltState.Slots(belt)), healingPotion);
            foreach (var type in belt.Types)
            {
                if (type == null || type.Potion == null)
                {
                    continue;
                }
                if (PotionBeltState.Unlocked(type))
                {
                    if (type.CapacityTiers.Count > 0)
                    {
                        AddBelt(BeltOffer.Capacity, type, PotionBeltOps.NextCapacity(type, PotionBeltState.Capacity(type)), type.Potion);
                    }
                    if (type.PotencyTiers.Count > 0)
                    {
                        AddBelt(BeltOffer.Potency, type, PotionBeltOps.NextPotency(type, PotionBeltState.Potency(type)), type.Potion);
                    }
                }
                else if (PotionBeltState.OnSale(type))
                {
                    AddBelt(BeltOffer.Recipe, type, type.Unlock, type.Potion);
                }
            }
        }

        private void AddBelt(BeltOffer offer, BeltPotionType type, BeltPrice step, ItemSO icon)
        {
            AddEntry(new Entry
            {
                Item = icon, Key = PotionItemKey, Offer = offer, Type = type, Upgrade = step,
                Price = step != null ? step.Gold : 0,
            });
        }

        /// <summary>Whether <paramref name="type"/>'s potency is charges (Energy) rather than healing.</summary>
        private static bool PotencyIsCharges(BeltPotionType type)
        {
            return type.PotencyTiers.Exists(t => t != null && t.Charges > 0);
        }

        private static int ChargesAt(BeltPotionType type, int bought)
        {
            return Mathf.Max(1, type.Potion.ConsumableCharges + PotionBeltOps.ChargeBonus(type, bought));
        }

        /// <summary>What a belt step changes ("2 → 3 slots", "1 → 2 per run"), or where its ladder tops out.</summary>
        private string BeltChange(Entry entry)
        {
            var belt = PotionBeltSO.Load();
            var type = entry.Type;
            switch (entry.Offer)
            {
                case BeltOffer.Slot:
                    int slots = PotionBeltState.Slots(belt);
                    return entry.Upgrade is BeltSlotTier more
                        ? Loc.Format("hub.merchant.belt.slots-change", ("now", slots), ("next", more.Slots))
                        : Loc.Format("hub.merchant.belt.slots-max", ("now", slots));
                case BeltOffer.Recipe:
                    return Loc.Get("hub.merchant.belt.new-potion");
                case BeltOffer.Capacity:
                    int cap = PotionBeltState.Capacity(type);
                    return entry.Upgrade is BeltCapacityTier size
                        ? Loc.Format("hub.merchant.belt.capacity-change", ("now", cap), ("next", size.Capacity))
                        : Loc.Format("hub.merchant.belt.capacity-max", ("now", cap));
                default:
                    int bought = PotionBeltState.Potency(type);
                    if (PotencyIsCharges(type))
                    {
                        int now = ChargesAt(type, bought);
                        return entry.Upgrade != null
                            ? Loc.Format("hub.merchant.belt.charges-change", ("now", now), ("next", ChargesAt(type, bought + 1)))
                            : Loc.Format("hub.merchant.belt.charges-max", ("now", now));
                    }
                    int pct = PotionPercent(type.Potion, PotionBeltOps.HealBonus(type, bought));
                    return entry.Upgrade is BeltPotencyTier brew
                        ? Loc.Format("hub.merchant.belt.heal-change", ("now", pct), ("next", PotionPercent(type.Potion, brew.HealPercent)))
                        : Loc.Format("hub.merchant.belt.heal-max", ("now", pct));
            }
        }

        private void ShowBeltDetail(Entry entry)
        {
            InventoryHubUI.SetIcon(_detailIcon, entry.Item != null ? entry.Item.Icon : null, null);
            SetText(_detailName, RowName(entry));
            InventoryHubUI.ApplyRarity(_detailName, null);
            SetText(_detailSub, BeltChange(entry));

            var step = entry.Upgrade;
            if (step == null)
            {
                SetAction(Loc.Get("hub.merchant.fully-upgraded"), false, string.Empty);
            }
            else
            {
                string reason = Gold < step.Gold
                    ? Loc.Format("hub.merchant.need-gold", ("price", step.Gold), ("gold", Gold))
                    : Loc.Get("hub.merchant.not-enough-materials"); // which ones: the coloured line below
                SetAction(Loc.Format("hub.merchant.buy", ("price", step.Gold)), CanPay(step), reason);
            }

            string desc;
            switch (entry.Offer)
            {
                case BeltOffer.Slot:
                    desc = Loc.Get("hub.merchant.belt.slot-desc");
                    break;
                case BeltOffer.Recipe:
                    desc = ItemPresenter.ConsumableEffectLine(entry.Type.Potion) + " " + Loc.Get("hub.merchant.belt.recipe-desc");
                    break;
                case BeltOffer.Capacity:
                    desc = Loc.Format("hub.merchant.belt.capacity-desc", ("potion", entry.Type.Potion.DisplayName));
                    break;
                default:
                    desc = Loc.Format("hub.merchant.belt.potency-desc", ("potion", entry.Type.Potion.DisplayName));
                    break;
            }
            _detailBody?.Add(MakeLabel(desc, "cd-inv-detail__desc"));
            if (step != null && step.Materials != null && step.Materials.Count > 0)
            {
                _detailBody?.Add(MakeLabel(Loc.Format("hub.merchant.materials", ("cost", MaterialCostText.Describe(step.Materials))), "cd-inv-detail__note"));
                AddRevisitHints(step);
            }
        }

        /// <summary>
        /// The belt's tutorial hint (playtest 3 finding 5): a step short of a revisit-only material
        /// says where it drops, since nothing else in the game does. Only when short - a player who
        /// has the material already found it.
        /// </summary>
        private void AddRevisitHints(BeltPrice step)
        {
            foreach (var line in step.Materials)
            {
                var material = line != null ? line.Material : null;
                if (material == null || material.RevisitFear < 0 || string.IsNullOrEmpty(material.RevisitRun)
                    || InventoryManager.Instance.GetMaterialQuantity(material.Key) >= line.Amount)
                {
                    continue;
                }
                _detailBody?.Add(MakeLabel(
                    Loc.Format("hub.merchant.revisit-hint", ("material", material.DisplayName), ("run", material.RevisitRun), ("fear", Mathf.Max(1, material.RevisitFear))),
                    "cd-inv-detail__note", "cd-belt-hint"));
            }
        }

        private void SetAction(string text, bool enabled, string reasonWhenDisabled)
        {
            if (_actionButton != null)
            {
                _actionButton.text = text;
                _actionButton.SetEnabled(enabled);
            }
            SetText(_reasonLabel, enabled ? string.Empty : reasonWhenDisabled);
        }

        // --- Actions -----------------------------------------------------------

        private void OnAction()
        {
            var entry = Current;
            if (entry == null)
            {
                return;
            }
            switch (_tab)
            {
                case Tab.Buy:
                    OnBuy(entry.Key, entry.Item, entry.Price);
                    break;
                case Tab.Sell:
                    OnSell(entry.Key, entry.Item, entry.Price);
                    break;
                case Tab.Belt:
                    OnBuyPotionBelt(entry);
                    break;
            }
        }

        private void OnBuy(string key, ItemSO so, int price)
        {
            if (!MetaProgressManager.Instance.TrySpendGold(price, EconomySource.Merchant))
            {
                SetFeedback(Loc.Get("hub.merchant.feedback.not-enough-gold"));
                return;
            }

            InventoryManager.Instance.AddItem(so, 1, EconomySource.Merchant);
            MetaProgressManager.Instance.RemoveFromShopStock(key);
            SetFeedback(Loc.Format("hub.merchant.feedback.bought", ("item", so.DisplayName)));
            Refresh();
        }

        private void OnRestock()
        {
            if (!MetaProgressManager.Instance.TrySpendGold(RestockCost, EconomySource.Merchant))
            {
                SetFeedback(Loc.Get("hub.merchant.feedback.no-restock-gold"));
                return;
            }

            MetaProgressManager.Instance.SetShopStock(GenerateStock(StockSize));
            SetFeedback(Loc.Get("hub.merchant.feedback.restocked"));
            _index = 0;
            Refresh();
        }

        private void OnSell(string key, ItemSO so, int price)
        {
            // Only ever sell an un-equipped (bag) copy, and only pay out if one was actually removed.
            if (!InventoryManager.Instance.RemoveBagEquipment(key, ItemRemovalReason.Sold))
            {
                SetFeedback(Loc.Get("hub.merchant.feedback.nothing-to-sell"));
                return;
            }
            MetaProgressManager.Instance.AddGold(price, EconomySource.Merchant);
            SetFeedback(Loc.Format("hub.merchant.feedback.sold", ("item", so.DisplayName), ("price", price)));
            Refresh();
        }

        private void OnBuyPotionBelt(Entry entry)
        {
            var step = entry.Upgrade;
            // Checked whole first, so the gold is never taken for a step the bags cannot finish paying.
            if (!CanPay(step))
            {
                SetFeedback(step == null ? Loc.Get("hub.merchant.feedback.fully-upgraded") : Loc.Get("hub.merchant.feedback.cannot-pay"));
                return;
            }
            if (!MetaProgressManager.Instance.TrySpendGold(step.Gold, EconomySource.Merchant)
                || !InventoryManager.Instance.SpendMaterials(step.Materials))
            {
                SetFeedback(Loc.Get("hub.merchant.feedback.cannot-pay"));
                return;
            }

            var type = entry.Type;
            switch (entry.Offer)
            {
                case BeltOffer.Slot:
                    int slots = ((BeltSlotTier)step).Slots;
                    PotionBeltState.SetSlots(slots);
                    SetFeedback(Loc.Format("hub.merchant.feedback.belt-slots", ("slots", slots)));
                    break;
                case BeltOffer.Recipe:
                    PotionBeltState.Unlock(type);
                    SetFeedback(Loc.Format("hub.merchant.feedback.learned", ("potion", type.Potion.DisplayName)));
                    break;
                case BeltOffer.Capacity:
                    int cap = ((BeltCapacityTier)step).Capacity;
                    PotionBeltState.SetCapacity(type, cap);
                    SetFeedback(Loc.Format("hub.merchant.feedback.capacity", ("potion", type.DisplayName), ("cap", cap)));
                    break;
                default:
                    PotionBeltState.AddPotency(type);
                    SetFeedback(Loc.Format("hub.merchant.feedback.stronger", ("potion", type.Potion.DisplayName)));
                    break;
            }
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
                ? catalog.Items.Where(i => i != null && i.Category == ItemCategory.Equipment && !i.CraftOnly && !string.IsNullOrEmpty(i.Key)).ToList()
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

        // --- Helpers -----------------------------------------------------------

        private HeroSO FindHero(string key)
        {
            if (_roster == null || string.IsNullOrEmpty(key))
            {
                return null;
            }
            return _roster.Find(key);
        }

        private static Label MakeLabel(string text, string className, string extraClass = null)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            if (extraClass != null)
            {
                label.AddToClassList(extraClass);
            }
            return label;
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetFeedback(string message)
        {
            SetText(_feedbackLabel, message);
        }
    }
}
