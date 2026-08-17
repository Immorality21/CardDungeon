using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Items.UI
{
    /// <summary>
    /// Hub inventory (UI Toolkit view-controller, mirrors <c>MagicForgeUI</c>). Manages equipment
    /// per hero and shows carried consumables — the only place gear is managed, since it's between
    /// runs. Operates on a VisualElement subtree owned by the menu's UIDocument (not a MonoBehaviour).
    /// Reads the roster from a <see cref="PartyRosterSO"/> because the hub has no live Party, and all
    /// item/equip state from the (scene-independent) <see cref="InventoryManager"/>.
    /// </summary>
    public class InventoryHubUI
    {
        private enum Tab { Equipment, Consumables }

        private readonly VisualElement _root;
        private readonly PartyRosterSO _roster;

        private readonly VisualElement _heroesRow;
        private readonly Label _statsLabel;
        private readonly VisualElement _tabsBar;
        private readonly Button _tabEquipment;
        private readonly Button _tabConsumables;
        private readonly ScrollView _scroll;
        private readonly Label _emptyLabel;
        private readonly Button _closeButton;

        private Tab _tab = Tab.Equipment;
        private string _selectedHeroKey;

        public event Action OnClosed;

        private static readonly Dictionary<ItemRarity, Color> RarityColors = new Dictionary<ItemRarity, Color>
        {
            { ItemRarity.Common, new Color(0.78f, 0.78f, 0.78f) },
            { ItemRarity.Uncommon, new Color(0.30f, 0.85f, 0.30f) },
            { ItemRarity.Rare, new Color(0.30f, 0.50f, 1.00f) },
            { ItemRarity.Epic, new Color(0.70f, 0.30f, 0.90f) },
            { ItemRarity.Legendary, new Color(1.00f, 0.65f, 0.00f) }
        };

        public InventoryHubUI(VisualElement root, PartyRosterSO roster)
        {
            _root = root;
            _roster = roster;

            _heroesRow = root.Q<VisualElement>("inventory-heroes");
            _statsLabel = root.Q<Label>("inventory-stats");
            _tabsBar = root.Q<VisualElement>("inventory-tabs");
            _tabEquipment = root.Q<Button>("tab-equipment");
            _tabConsumables = root.Q<Button>("tab-consumables");
            _scroll = root.Q<ScrollView>("inventory-scroll");
            _emptyLabel = root.Q<Label>("inventory-empty");
            _closeButton = root.Q<Button>("inventory-close");

            if (_tabEquipment != null)
            {
                _tabEquipment.clicked += () => SetTab(Tab.Equipment);
            }
            if (_tabConsumables != null)
            {
                _tabConsumables.clicked += () => SetTab(Tab.Consumables);
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
            _tab = Tab.Equipment;

            // The hub MenuScene has no wired Managers prefab; touch Instance so the manager
            // auto-creates and loads (in Awake) before we read equip/consumable state.
            _ = InventoryManager.Instance;

            // Default to the first roster hero.
            _selectedHeroKey = null;
            if (_roster != null && _roster.Heroes.Count > 0 && _roster.Heroes[0] != null)
            {
                _selectedHeroKey = _roster.Heroes[0].Label;
            }

            RefreshTabs();
            RefreshHeroes();
            RefreshStats();
            RefreshList();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            RefreshTabs();
            RefreshStats();
            RefreshList();
        }

        private void RefreshTabs()
        {
            _tabEquipment?.EnableInClassList("cd-tab--active", _tab == Tab.Equipment);
            _tabConsumables?.EnableInClassList("cd-tab--active", _tab == Tab.Consumables);
            // Stats + hero selector are only meaningful for equipment.
            SetShown(_statsLabel, _tab == Tab.Equipment);
            SetShown(_heroesRow, _tab == Tab.Equipment);
        }

        // ============================================================
        //  HERO SELECTOR
        // ============================================================

        private void RefreshHeroes()
        {
            if (_heroesRow == null)
            {
                return;
            }
            _heroesRow.Clear();
            if (_roster == null)
            {
                return;
            }

            foreach (var hero in _roster.Heroes)
            {
                if (hero == null)
                {
                    continue;
                }
                var key = hero.Label;
                var btn = new Button(() => SelectHero(key)) { text = hero.Label };
                btn.AddToClassList("cd-button");
                btn.AddToClassList("cd-button--narrow");
                btn.EnableInClassList("cd-tab--active", key == _selectedHeroKey);
                _heroesRow.Add(btn);
            }
        }

        private void SelectHero(string heroKey)
        {
            _selectedHeroKey = heroKey;
            RefreshHeroes();
            RefreshStats();
            RefreshList();
        }

        // ============================================================
        //  STATS PREVIEW (base HeroSO stats + equipment bonuses)
        // ============================================================

        private void RefreshStats()
        {
            if (_statsLabel == null)
            {
                return;
            }

            var hero = FindHero(_selectedHeroKey);
            if (hero == null || !InventoryManager.HasInstance)
            {
                _statsLabel.text = string.Empty;
                return;
            }

            var raw = InventoryManager.Instance.ComputeRawBonuses(_selectedHeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(_selectedHeroKey);

            _statsLabel.text =
                $"ATK {Effective(hero.BaseAttack, raw[StatType.Attack], pct[StatType.Attack])}   " +
                $"DEF {Effective(hero.BaseDefense, raw[StatType.Defense], pct[StatType.Defense])}   " +
                $"HP {Effective(hero.BaseHealth, raw[StatType.MaxHealth], pct[StatType.MaxHealth])}   " +
                $"AGI {Effective(hero.BaseAgility, raw[StatType.Agility], pct[StatType.Agility])}";
        }

        private static int Effective(int baseVal, float raw, float pct)
        {
            return Mathf.RoundToInt((baseVal + raw) * (1f + pct / 100f));
        }

        // ============================================================
        //  LIST (equipment slots + bag, or consumables)
        // ============================================================

        private void RefreshList()
        {
            if (_scroll == null)
            {
                return;
            }
            _scroll.Clear();

            if (!InventoryManager.HasInstance)
            {
                ShowEmpty("Inventory unavailable.");
                return;
            }

            if (_tab == Tab.Equipment)
            {
                RefreshEquipment();
            }
            else
            {
                RefreshConsumables();
            }
        }

        private void RefreshEquipment()
        {
            SetShown(_emptyLabel, false);

            // Equipped slots (click a filled slot to unequip).
            foreach (SlotType slot in Enum.GetValues(typeof(SlotType)))
            {
                var equipped = InventoryManager.Instance.GetEquipped(slot, _selectedHeroKey);
                if (equipped != null)
                {
                    var so = InventoryManager.Instance.GetItemSO(equipped.ItemKey);
                    string name = so != null ? so.DisplayName : equipped.ItemKey;
                    var capturedSlot = slot;
                    var row = BuildRow(so != null ? so.Icon : null, $"[{slot}] {name}", "Unequip", true,
                        () => Unequip(capturedSlot), so);
                    _scroll.Add(row);
                }
                else
                {
                    _scroll.Add(BuildRow(null, $"[{slot}] Empty", string.Empty, false, null, null));
                }
            }

            // Un-equipped equipment in the bag (click to equip on the selected hero).
            var bag = InventoryManager.Instance.GetBagEquipment();
            foreach (var item in bag)
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                string name = so != null ? so.DisplayName : item.ItemKey;
                var capturedItem = item;
                var capturedSo = so;
                _scroll.Add(BuildRow(so != null ? so.Icon : null, name, "Equip", capturedSo != null,
                    () => Equip(capturedItem, capturedSo), so));
            }

            if (bag.Count == 0)
            {
                var note = new Label("Bag has no equipment.");
                note.AddToClassList("cd-info-label");
                _scroll.Add(note);
            }
        }

        private void RefreshConsumables()
        {
            var consumables = InventoryManager.Instance.GetConsumables();
            if (consumables.Count == 0)
            {
                ShowEmpty("No consumables carried.");
                return;
            }

            SetShown(_emptyLabel, false);
            foreach (var item in consumables)
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                string name = so != null ? so.DisplayName : item.ItemKey;
                _scroll.Add(BuildRow(so != null ? so.Icon : null, name, $"x{item.Quantity}", false, null, so));
            }
        }

        private void Equip(ItemSaveData item, ItemSO so)
        {
            if (so == null || string.IsNullOrEmpty(_selectedHeroKey))
            {
                return;
            }
            InventoryManager.Instance.Equip(item, so.SlotType, _selectedHeroKey);
            RefreshStats();
            RefreshList();
        }

        private void Unequip(SlotType slot)
        {
            InventoryManager.Instance.Unequip(slot, _selectedHeroKey);
            RefreshStats();
            RefreshList();
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        private Button BuildRow(Sprite icon, string name, string meta, bool enabled, Action onClick, ItemSO so)
        {
            var row = new Button(onClick) { text = string.Empty };
            row.AddToClassList("cd-sel-row");
            row.SetEnabled(enabled);

            var iconElement = new VisualElement();
            iconElement.AddToClassList("cd-sel-row__icon");
            iconElement.pickingMode = PickingMode.Ignore;
            if (icon != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(icon);
            }
            row.Add(iconElement);

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("cd-sel-row__name");
            nameLabel.pickingMode = PickingMode.Ignore;
            if (so != null && RarityColors.TryGetValue(so.Rarity, out var color))
            {
                nameLabel.style.color = color;
            }
            row.Add(nameLabel);

            var metaLabel = new Label(meta ?? string.Empty);
            metaLabel.AddToClassList("cd-sel-row__meta");
            metaLabel.pickingMode = PickingMode.Ignore;
            row.Add(metaLabel);

            return row;
        }

        private HeroSO FindHero(string heroKey)
        {
            if (_roster == null || string.IsNullOrEmpty(heroKey))
            {
                return null;
            }
            return _roster.Heroes.Find(h => h != null && h.Label == heroKey);
        }

        private void ShowEmpty(string message)
        {
            if (_emptyLabel != null)
            {
                _emptyLabel.text = message;
            }
            SetShown(_emptyLabel, true);
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
