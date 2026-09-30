using System;
using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Items.UI
{
    /// <summary>
    /// Hub inventory (UI Toolkit view-controller). Four tabs - equipment and the ability loadout per
    /// hero, the carried consumables and the hauled materials party-wide - in one fixed frame:
    /// a header band with the tabs, a hero strip, three columns (what is worn or carried, the list to
    /// choose from, and a detail pane for whatever the cursor is on) and a hint line. The frame is
    /// authored in <c>Hub.uxml</c>; this class only fills its regions with rows.
    ///
    /// <para><b>Equipment works the FF way.</b> The cursor starts on the hero's slots. Moving it
    /// lists what fits that slot beside it - the party's bag <i>and</i> what other heroes wear - and
    /// Enter moves into that list, where every item shows its effect on the hero's stats before it
    /// is put on. Enter equips and returns to the slots; Esc backs out a layer at a time.</para>
    ///
    /// <para>Stats here are the ones the hero fights with: base, plus the sphere-grid nodes they
    /// have bought, plus gear (<see cref="HeroStatCalculator"/>) - this screen used to leave the
    /// grid out and understate every hero who had spent XP.</para>
    ///
    /// Reads the roster from a <see cref="PartyRosterSO"/> because the hub has no live Party, and all
    /// item/equip state from the (scene-independent) <see cref="InventoryManager"/>.
    /// </summary>
    public class InventoryHubUI
    {
        private enum Tab { Equipment, Spells, Consumables, Materials }

        private enum Focus { Slots, List }

        /// <summary>One selectable row: what it previews in the detail pane, and what Enter does.</summary>
        private class Entry
        {
            public VisualElement Row;
            public Label Cursor;
            public Action Preview;
            public Action Confirm;

            /// <summary>The loadout this entry would leave the hero in (equipment list only).</summary>
            public List<ItemSO> Gear;
        }

        private readonly VisualElement _root;
        private readonly PartyRosterSO _roster;
        private List<HeroSO> _ownedHeroes;

        private readonly VisualElement _heroesRow;
        private readonly Button _tabEquipment;
        private readonly Button _tabSpells;
        private readonly Button _tabConsumables;
        private readonly Button _tabMaterials;
        private readonly Label _leftTitle;
        private readonly VisualElement _left;
        private readonly Label _listTitle;
        private readonly ScrollView _list;
        private readonly Label _emptyLabel;
        private readonly VisualElement _detailIcon;
        private readonly Label _detailName;
        private readonly Label _detailSub;
        private readonly ScrollView _detailBody;
        private readonly Label _hint;
        private readonly Button _closeButton;

        private Tab _tab = Tab.Equipment;
        private Focus _focus = Focus.Slots;
        private string _selectedHeroKey;
        private bool _isShown;
        private int _slotIndex;

        // The chosen spell loadouts, loaded on Show and written straight back on every change. A
        // loadout is a preference, not a gain, so it is not deferred to a level clear the way XP and
        // loot are - dying must not cost the player their equipment layout.
        private readonly FileHandler _files = new FileHandler();
        private MagicLoadoutSaveData _loadout;

        private readonly List<Entry> _slotEntries = new List<Entry>();
        private readonly List<Entry> _listEntries = new List<Entry>();
        private int _listIndex = -1;

        public event Action OnClosed;

        public InventoryHubUI(VisualElement root, PartyRosterSO roster)
        {
            _root = root;
            _roster = roster;

            _heroesRow = root.Q<VisualElement>("inventory-heroes");
            _tabEquipment = root.Q<Button>("tab-equipment");
            _tabSpells = root.Q<Button>("tab-spells");
            _tabConsumables = root.Q<Button>("tab-consumables");
            _tabMaterials = root.Q<Button>("tab-materials");
            _leftTitle = root.Q<Label>("inv-left-title");
            _left = root.Q<VisualElement>("inv-left");
            _listTitle = root.Q<Label>("inv-list-title");
            _list = root.Q<ScrollView>("inventory-scroll");
            _emptyLabel = root.Q<Label>("inventory-empty");
            _detailIcon = root.Q<VisualElement>("inv-detail-icon");
            _detailName = root.Q<Label>("inv-detail-name");
            _detailSub = root.Q<Label>("inv-detail-sub");
            _detailBody = root.Q<ScrollView>("inv-detail-body");
            _hint = root.Q<Label>("inventory-hint");
            _closeButton = root.Q<Button>("inventory-close");

            WireTab(_tabEquipment, Tab.Equipment);
            WireTab(_tabSpells, Tab.Spells);
            WireTab(_tabConsumables, Tab.Consumables);
            WireTab(_tabMaterials, Tab.Materials);
            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
                _closeButton.focusable = false;
            }

            // Keep keyboard focus on the view root, not the ScrollViews, so the cursor owns the
            // arrows (UITK's default focus navigation would otherwise steal them).
            if (_list != null)
            {
                _list.focusable = false;
            }
            if (_detailBody != null)
            {
                _detailBody.focusable = false;
            }

            // The view root owns keyboard input while open; swallow UITK's built-in navigation so
            // it can't move focus off the root after the first arrow (mirrors the battle UI).
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<NavigationMoveEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationSubmitEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationCancelEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });

            _root.style.display = DisplayStyle.None;
        }

        private void WireTab(Button tab, Tab which)
        {
            if (tab == null)
            {
                return;
            }
            tab.clicked += () => SetTab(which);
            tab.focusable = false;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            _isShown = true;
            _tab = Tab.Equipment;
            _focus = Focus.Slots;
            _slotIndex = 0;

            // The hub has no wired Managers prefab; touch Instance so the manager auto-creates and
            // loads (in Awake) before we read equip/consumable state.
            _ = InventoryManager.Instance;

            _loadout = _files.Load<MagicLoadoutSaveData>();

            // The catalog lists every hero in the game; only the ones the player has actually
            // acquired get a card here.
            _ownedHeroes = null;
            _selectedHeroKey = null;
            var owned = RosterHeroes();
            if (owned.Count > 0)
            {
                _selectedHeroKey = owned[0].SaveKey;
            }

            Refresh();
            FocusRoot();
        }

        public void Hide()
        {
            _isShown = false;
            _root.style.display = DisplayStyle.None;
            _root.focusable = false; // stop being a focus/nav target once closed
            OnClosed?.Invoke();
        }

        private void FocusRoot()
        {
            if (_root != null && _root.panel != null)
            {
                _root.focusable = true;
                _root.Focus();
            }
        }

        // ============================================================
        //  TABS AND HEROES
        // ============================================================

        private bool IsPerHeroTab => _tab == Tab.Equipment || _tab == Tab.Spells;

        /// <summary>Left/Right step through the tabs, clamped so the ends of the row feel like ends.</summary>
        private void CycleTab(int delta)
        {
            int next = Mathf.Clamp((int)_tab + delta, 0, (int)Tab.Materials);
            if (next != (int)_tab)
            {
                SetTab((Tab)next);
            }
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            _focus = tab == Tab.Equipment ? Focus.Slots : Focus.List;
            Refresh();
        }

        private void Refresh()
        {
            RefreshTabs();
            RefreshHeroes();
            RefreshBody();
        }

        private void RefreshTabs()
        {
            _tabEquipment?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Equipment);
            _tabSpells?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Spells);
            _tabConsumables?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Consumables);
            _tabMaterials?.EnableInClassList("cd-inv-tab--active", _tab == Tab.Materials);
        }

        private void RefreshHeroes()
        {
            if (_heroesRow == null)
            {
                return;
            }
            _heroesRow.Clear();

            // Muted, never hidden: the strip keeps its height on every tab so nothing below moves.
            bool perHero = IsPerHeroTab;
            _heroesRow.EnableInClassList("cd-inv-heroes--muted", !perHero);

            foreach (var hero in RosterHeroes())
            {
                if (hero == null)
                {
                    continue;
                }

                var key = hero.SaveKey;
                var card = new VisualElement();
                card.AddToClassList("cd-inv-hero");
                card.EnableInClassList("cd-inv-hero--active", perHero && key == _selectedHeroKey);
                card.pickingMode = perHero ? PickingMode.Position : PickingMode.Ignore;
                card.tooltip = hero.DisplayName;

                var sprite = new VisualElement { pickingMode = PickingMode.Ignore };
                sprite.AddToClassList("cd-inv-hero__sprite");
                if (hero.Sprite != null)
                {
                    sprite.style.backgroundImage = new StyleBackground(hero.Sprite);
                }
                card.Add(sprite);

                var name = new Label(hero.DisplayName) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("cd-inv-hero__name");
                card.Add(name);

                if (perHero)
                {
                    card.RegisterCallback<ClickEvent>(_ => SelectHero(key));
                }
                _heroesRow.Add(card);
            }

            // The strip's right end: the selected hero at a glance on the per-hero tabs, the purse on
            // the party-wide ones.
            var summary = new VisualElement { pickingMode = PickingMode.Ignore };
            summary.AddToClassList("cd-inv-heroes__summary");
            var selected = FindHero(_selectedHeroKey);
            if (perHero && selected != null)
            {
                var stats = StatsWith(CurrentGear());
                var save = HeroRoster.GetHeroSave(selected);
                int xp = save != null ? save.CurrentXp : 0;
                AddSummaryLine(summary, selected.DisplayName, "cd-inv-heroes__summary-name");
                AddSummaryLine(summary, $"HP {stats[StatType.MaxHealth]} \u00b7 {xp} XP to spend", null);
            }
            else if (MetaProgressManager.HasInstance)
            {
                AddSummaryLine(summary, $"{MetaProgressManager.Instance.Gold} gold", "cd-inv-heroes__summary-name");
            }
            _heroesRow.Add(summary);
        }

        private static void AddSummaryLine(VisualElement parent, string text, string extraClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("cd-inv-heroes__summary-line");
            if (extraClass != null)
            {
                label.AddToClassList(extraClass);
            }
            parent.Add(label);
        }

        /// <summary>
        /// The heroes this screen manages: the *owned* subset of the catalog, via
        /// <see cref="HeroRoster"/>. Cached per Show() because it reads the party save off disk.
        /// </summary>
        private List<HeroSO> RosterHeroes()
        {
            if (_ownedHeroes == null)
            {
                _ownedHeroes = _roster != null
                    ? HeroRoster.GetOwnedHeroes(_roster)
                    : new List<HeroSO>();
            }
            return _ownedHeroes;
        }

        private void SelectHero(string heroKey)
        {
            if (heroKey == _selectedHeroKey)
            {
                return;
            }
            _selectedHeroKey = heroKey;
            if (_tab == Tab.Equipment)
            {
                _focus = Focus.Slots;
            }
            RefreshHeroes();
            RefreshBody();
        }

        private void CycleHero(int delta)
        {
            var heroes = RosterHeroes();
            if (heroes.Count <= 1)
            {
                return;
            }
            int current = heroes.FindIndex(h => h != null && h.SaveKey == _selectedHeroKey);
            if (current < 0)
            {
                current = 0;
            }
            int count = heroes.Count;
            var hero = heroes[((current + delta) % count + count) % count];
            if (hero != null)
            {
                SelectHero(hero.SaveKey);
            }
        }

        // ============================================================
        //  BODY
        // ============================================================

        private void RefreshBody()
        {
            _left?.Clear();
            _list?.Clear();
            _slotEntries.Clear();
            _listEntries.Clear();
            _listIndex = -1;
            SetShown(_emptyLabel, false);

            switch (_tab)
            {
                case Tab.Equipment:
                    BuildEquipment();
                    break;
                case Tab.Spells:
                    BuildSpells();
                    break;
                case Tab.Consumables:
                    BuildConsumables();
                    break;
                default:
                    BuildMaterials();
                    break;
            }

            RenderCursor();
            RefreshHint();
        }

        private void RefreshHint()
        {
            if (_hint == null)
            {
                return;
            }

            switch (_tab)
            {
                case Tab.Equipment:
                    _hint.text = _focus == Focus.Slots
                        ? "↑↓ slot · Enter choose an item · Q/E hero · ←→ tab · Esc back"
                        : "↑↓ item · Enter equip · Esc back to the slots";
                    break;
                case Tab.Spells:
                    _hint.text = "↑↓ ability · Enter carry or put away · Q/E hero · ←→ tab · Esc back";
                    break;
                default:
                    _hint.text = "↑↓ browse · ←→ tab · Esc back";
                    break;
            }
        }

        // ------------------------------------------------------------ equipment

        private void BuildEquipment()
        {
            var hero = FindHero(_selectedHeroKey);
            SetText(_leftTitle, "Equipped");
            if (hero == null)
            {
                SetText(_listTitle, string.Empty);
                ShowEmpty("No hero to equip yet.");
                ClearDetail();
                return;
            }

            var slots = ItemPresenter.SlotOrder;
            _slotIndex = Mathf.Clamp(_slotIndex, 0, slots.Length - 1);

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var worn = WornItem(slot);
                int index = i;

                var entry = new Entry
                {
                    Preview = () => ShowSlotDetail(slot),
                    Confirm = () => EnterList(),
                };
                // An empty slot leads with the slot's name: a column of "Empty Empty Empty" says
                // nothing at a glance.
                entry.Row = BuildRow(
                    worn != null ? worn.Icon : null,
                    worn != null ? worn.DisplayName : ItemPresenter.SlotLabel(slot),
                    worn != null ? ItemPresenter.SlotLabel(slot) : "Empty",
                    string.Empty,
                    worn,
                    out entry.Cursor);
                entry.Row.EnableInClassList("cd-inv-row--empty", worn == null);
                entry.Row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    // Hovering a slot takes the cursor back from the list, so the mouse is never stuck
                    // on one side after brushing past the other.
                    if (_focus == Focus.List)
                    {
                        _focus = Focus.Slots;
                        _listIndex = -1;
                        RefreshHint();
                    }
                    else if (_slotIndex == index)
                    {
                        return;
                    }
                    MoveSlotTo(index);
                });
                entry.Row.RegisterCallback<ClickEvent>(_ =>
                {
                    _focus = Focus.Slots;
                    MoveSlotTo(index);
                    EnterList();
                });
                _slotEntries.Add(entry);
                _left?.Add(entry.Row);
            }

            BuildCandidates(slots[_slotIndex]);
        }

        /// <summary>
        /// What can go in <paramref name="slot"/>: a "take it off" row when something is worn, then
        /// every item of that slot the party owns - the bag first, then what other heroes wear.
        /// </summary>
        private void BuildCandidates(SlotType slot)
        {
            _list?.Clear();
            _listEntries.Clear();
            SetShown(_emptyLabel, false);
            SetText(_listTitle, "Fits " + ItemPresenter.SlotLabel(slot));

            var worn = WornItem(slot);
            if (worn != null)
            {
                var remove = new Entry
                {
                    Gear = ItemPresenter.SwapOut(CurrentGear(), slot),
                    Preview = () => ShowItemDetail(worn, SlotRemovalPreview(slot),
                        $"Taking it off leaves the {ItemPresenter.SlotLabel(slot).ToLowerInvariant()} slot empty.",
                        removing: true),
                    Confirm = () => Unequip(slot),
                };
                remove.Row = BuildRow(null, "Take off " + worn.DisplayName, "Leave the slot empty",
                    string.Empty, null, out remove.Cursor);
                var glyph = new Label("✕") { pickingMode = PickingMode.Ignore };
                glyph.AddToClassList("cd-inv-row__glyph");
                remove.Row.Q(className: "cd-inv-row__icon").Add(glyph);
                AddListEntry(remove);
            }

            var bag = new List<ItemSaveData>();
            var others = new List<ItemSaveData>();
            foreach (var item in InventoryManager.Instance.GetAllEquipment())
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                if (so == null || so.SlotType != slot)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(item.EquippedSlot))
                {
                    bag.Add(item);
                }
                else if (item.EquippedHeroKey != _selectedHeroKey)
                {
                    others.Add(item);
                }
            }

            foreach (var item in bag)
            {
                AddCandidate(item, null);
            }
            foreach (var item in others)
            {
                var wearer = FindHero(item.EquippedHeroKey);
                AddCandidate(item, wearer != null
                    ? wearer.DisplayName
                    : (string.IsNullOrEmpty(item.EquippedHeroKey) ? "someone" : item.EquippedHeroKey));
            }

            if (_listEntries.Count == 0)
            {
                ShowEmpty($"Nothing the party owns fits the {ItemPresenter.SlotLabel(slot).ToLowerInvariant()} slot.");
            }
        }

        private void AddCandidate(ItemSaveData item, string wornBy)
        {
            var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
            var gear = ItemPresenter.SwapIn(CurrentGear(), so);
            var preview = StatsWith(gear);
            var current = StatsWith(CurrentGear());

            var entry = new Entry
            {
                Gear = gear,
                Preview = () => ShowItemDetail(so, preview,
                    wornBy != null ? $"Worn by {wornBy} - equipping it takes it from them." : null),
                Confirm = () => Equip(item, so),
            };

            string caption = wornBy != null
                ? $"Worn by {wornBy}"
                : ItemPresenter.RarityLabel(so.Rarity);
            entry.Row = BuildRow(so.Icon, so.DisplayName, caption, string.Empty, so, out entry.Cursor);

            // The two biggest changes it makes, each in its own colour: one number alone lied - a
            // staff read "+5 INT" while costing 4 Strength.
            var meta = entry.Row.Q<Label>(className: "cd-inv-row__meta");
            var chips = new VisualElement { pickingMode = PickingMode.Ignore };
            chips.AddToClassList("cd-inv-row__chips");
            foreach (var change in BiggestChanges(current, preview, 2))
            {
                string sign = change.Amount > 0 ? "+" : "\u2212";
                var chip = new Label($"{sign}{Mathf.Abs(change.Amount)} {StatCatalog.ShortName(change.Stat)}")
                {
                    pickingMode = PickingMode.Ignore,
                };
                chip.AddToClassList("cd-inv-row__chip");
                chip.AddToClassList(change.Amount > 0 ? "cd-inv-delta--up" : "cd-inv-delta--down");
                chips.Add(chip);
            }
            meta.parent.Insert(meta.parent.IndexOf(meta), chips);
            meta.RemoveFromHierarchy();

            AddListEntry(entry);
        }

        private void MoveSlotTo(int index)
        {
            _slotIndex = Mathf.Clamp(index, 0, ItemPresenter.SlotOrder.Length - 1);
            BuildCandidates(ItemPresenter.SlotOrder[_slotIndex]);
            RenderCursor();
        }

        private void EnterList()
        {
            if (_tab != Tab.Equipment || _listEntries.Count == 0)
            {
                return;
            }
            _focus = Focus.List;
            _listIndex = 0;
            RenderCursor();
            RefreshHint();
        }

        private void LeaveList()
        {
            _focus = Focus.Slots;
            _listIndex = -1;
            RenderCursor();
            RefreshHint();
        }

        private void Equip(ItemSaveData item, ItemSO so)
        {
            if (so == null || string.IsNullOrEmpty(_selectedHeroKey))
            {
                return;
            }

            // Worn by someone else: take it off them first. Equip only clears the target hero's slot.
            // Keyed on the slot rather than the wearer, so an old save's item marked equipped with no
            // hero key is still lifted out of the cache before it moves.
            if (!string.IsNullOrEmpty(item.EquippedSlot) && item.EquippedHeroKey != _selectedHeroKey)
            {
                InventoryManager.Instance.Unequip(so.SlotType, item.EquippedHeroKey ?? string.Empty);
            }
            InventoryManager.Instance.Equip(item, so.SlotType, _selectedHeroKey);
            _focus = Focus.Slots;
            RefreshHeroes();
            RefreshBody();
        }

        private void Unequip(SlotType slot)
        {
            InventoryManager.Instance.Unequip(slot, _selectedHeroKey);
            _focus = Focus.Slots;
            RefreshHeroes();
            RefreshBody();
        }

        private ItemSO WornItem(SlotType slot)
        {
            var saved = InventoryManager.Instance.GetEquipped(slot, _selectedHeroKey);
            return saved != null ? InventoryManager.Instance.GetItemSO(saved.ItemKey) : null;
        }

        private List<ItemSO> CurrentGear()
        {
            return InventoryManager.Instance.GetEquippedItems(_selectedHeroKey);
        }

        private StatBlock SlotRemovalPreview(SlotType slot)
        {
            return StatsWith(ItemPresenter.SwapOut(CurrentGear(), slot));
        }

        // ------------------------------------------------------------ abilities

        /// <summary>
        /// The loadout: every ability the hero's sphere grid has taught them, with the ones they carry
        /// into a run marked, against a scarce number of slots. An empty choice auto-fills from what
        /// they know (<see cref="MagicLoadoutOps.Resolve"/>), and toggling writes the resolved list,
        /// so the first click also commits whatever the auto-fill had picked.
        /// </summary>
        private void BuildSpells()
        {
            var hero = FindHero(_selectedHeroKey);
            SetText(_leftTitle, "Carried");
            SetText(_listTitle, "Known abilities");
            if (hero == null)
            {
                ShowEmpty("No hero selected.");
                ClearDetail();
                return;
            }

            var nodes = ActivatedNodesOf(hero);
            var known = SphereGridOps.KnownMagicForNodes(hero.SphereGrid, nodes);
            int slots = SlotCount(hero, nodes);
            var carried = MagicLoadoutOps.Resolve(known, _loadout.ChosenFor(_selectedHeroKey), slots);

            // Left: the slots as pips, then what fills them.
            var pips = new VisualElement();
            pips.AddToClassList("cd-inv-pips");
            var pipsLabel = new Label("Slots");
            pipsLabel.AddToClassList("cd-inv-pips__label");
            pips.Add(pipsLabel);
            for (int i = 0; i < slots; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("cd-inv-pip");
                pip.EnableInClassList("cd-inv-pip--filled", i < carried.Count);
                pips.Add(pip);
            }
            _left?.Add(pips);

            foreach (var key in carried)
            {
                var magic = Magic(key);
                var row = BuildRow(magic != null ? magic.Icon : null, magic != null ? magic.DisplayName : key,
                    $"{ChargesOf(known, key)} charges a run", string.Empty, null, out _);
                row.AddToClassList("cd-inv-row--static");
                _left?.Add(row);
            }
            if (carried.Count == 0)
            {
                var none = new Label("Nothing carried.");
                none.AddToClassList("cd-inv-empty");
                _left?.Add(none);
            }

            if (known.Count == 0)
            {
                ShowEmpty($"{hero.DisplayName} knows no abilities yet. Learn one on the sphere grid.");
                ClearDetail();
                return;
            }

            var unit = SnapshotOf(hero);
            foreach (var entry in known)
            {
                var key = entry.Key;
                var magic = Magic(key);
                bool isCarried = carried.Contains(key);

                var listEntry = new Entry
                {
                    Preview = () => ShowAbilityDetail(hero, magic, key, isCarried, entry.Value, unit, known, slots),
                    Confirm = () => ToggleSpell(key),
                };
                listEntry.Row = BuildRow(
                    magic != null ? magic.Icon : null,
                    magic != null ? magic.DisplayName : key,
                    isCarried ? "Carried" : "Not carried",
                    isCarried ? "✓" : string.Empty,
                    null,
                    out listEntry.Cursor);
                if (isCarried)
                {
                    listEntry.Row.Q<Label>(className: "cd-inv-row__meta").AddToClassList("cd-inv-delta--up");
                }
                AddListEntry(listEntry);
            }
        }

        private void ToggleSpell(string magicKey)
        {
            var hero = FindHero(_selectedHeroKey);
            if (hero == null)
            {
                return;
            }

            var nodes = ActivatedNodesOf(hero);
            var known = SphereGridOps.KnownMagicForNodes(hero.SphereGrid, nodes);

            // A hero never goes in with nothing: an empty choice means "never chosen" and auto-fills
            // (MagicLoadoutOps.Resolve, by design), so putting away the last one would quietly carry
            // something else instead. Refuse it; the detail pane already says why.
            var carriedNow = MagicLoadoutOps.Resolve(known, _loadout.ChosenFor(_selectedHeroKey), SlotCount(hero, nodes));
            if (carriedNow.Count == 1 && carriedNow[0] == magicKey)
            {
                return;
            }
            var entry = _loadout.For(_selectedHeroKey);
            entry.EquippedKeys = MagicLoadoutOps.Toggle(known, entry.EquippedKeys, magicKey, SlotCount(hero, nodes));
            _files.Save(_loadout);

            int keep = _listIndex;
            RefreshBody();
            SetListIndex(keep);
        }

        private static int SlotCount(HeroSO hero, List<string> nodes)
        {
            return EquippedMagicState.DefaultSlotCount + SphereGridOps.SlotBonusForNodes(hero.SphereGrid, nodes);
        }

        private static int ChargesOf(List<KeyValuePair<string, int>> known, string key)
        {
            return known != null ? MagicLoadoutOps.ChargesFor(known, key) : 0;
        }

        private static MagicSO Magic(string key)
        {
            return MagicCatalog.HasInstance ? MagicCatalog.Instance.GetMagic(key) : null;
        }

        // ------------------------------------------------------------ consumables and materials

        private void BuildConsumables()
        {
            SetText(_leftTitle, "Party pouch");
            SetText(_listTitle, "Consumables");

            var consumables = InventoryManager.Instance.GetConsumables();
            int total = 0;
            foreach (var item in consumables)
            {
                total += Mathf.Max(0, item.Quantity);
            }
            AddSummary($"{consumables.Count} kinds · {total} in all",
                "Shared by the whole party, and used from the Item command in a fight.");

            if (consumables.Count == 0)
            {
                ShowEmpty("No consumables carried.");
                ClearDetail();
                return;
            }

            foreach (var item in consumables)
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                if (so == null)
                {
                    continue;
                }
                int quantity = item.Quantity;
                var entry = new Entry
                {
                    Preview = () => ShowStackDetail(so, quantity, ItemPresenter.ConsumableEffectLine(so)),
                };
                entry.Row = BuildRow(so.Icon, so.DisplayName, ItemPresenter.RarityLabel(so.Rarity),
                    "x" + quantity, so, out entry.Cursor);
                AddListEntry(entry);
            }
        }

        /// <summary>
        /// The raw stuff the party has hauled home, biggest pile first. Spent by sphere-grid nodes and
        /// buildings; this screen only shows the piles. Where a material comes from is deliberately
        /// not said anywhere (the owner's rule: discovery stays a discovery).
        /// </summary>
        private void BuildMaterials()
        {
            SetText(_leftTitle, "Stores");
            SetText(_listTitle, "Materials");

            var materials = InventoryManager.Instance.GetMaterials();

            // One row per material key: several stacks of the same thing read as one pile.
            var totals = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var stack in materials)
            {
                if (!totals.ContainsKey(stack.ItemKey))
                {
                    totals[stack.ItemKey] = 0;
                    order.Add(stack.ItemKey);
                }
                totals[stack.ItemKey] += Mathf.Max(0, stack.Quantity);
            }

            int all = 0;
            foreach (var entry in totals)
            {
                all += entry.Value;
            }
            AddSummary($"{order.Count} kinds · {all} in all", "Kept in the Storehouse between runs.");

            if (order.Count == 0)
            {
                ShowEmpty("No materials yet.");
                ClearDetail();
                return;
            }

            order.Sort((a, b) =>
            {
                int byCount = totals[b].CompareTo(totals[a]);
                return byCount != 0 ? byCount : string.CompareOrdinal(a, b);
            });

            foreach (var key in order)
            {
                var so = InventoryManager.Instance.GetItemSO(key);
                if (so == null)
                {
                    continue;
                }
                int count = totals[key];
                var entry = new Entry
                {
                    Preview = () => ShowStackDetail(so, count, null),
                };
                entry.Row = BuildRow(so.Icon, so.DisplayName, ItemPresenter.RarityLabel(so.Rarity),
                    "x" + count, so, out entry.Cursor);
                AddListEntry(entry);
            }
        }

        private void AddSummary(string headline, string note)
        {
            var head = new Label(headline);
            head.AddToClassList("cd-inv-summary__head");
            _left?.Add(head);

            var body = new Label(note);
            body.AddToClassList("cd-inv-summary__note");
            _left?.Add(body);
        }

        // ============================================================
        //  DETAIL PANE
        // ============================================================

        private void ClearDetail()
        {
            SetIcon(_detailIcon, null, null);
            SetText(_detailName, string.Empty);
            SetText(_detailSub, string.Empty);
            _detailBody?.Clear();
        }

        private void SetDetailHead(Sprite icon, string name, string sub, ItemSO rarityOf)
        {
            SetIcon(_detailIcon, icon, rarityOf);
            SetText(_detailName, name);
            SetText(_detailSub, sub);
            ApplyRarity(_detailName, rarityOf);
            _detailBody?.Clear();
            if (_detailBody != null)
            {
                _detailBody.scrollOffset = Vector2.zero;
            }
        }

        /// <summary>A slot on the doll: what is in it, and the hero's stats as they stand.</summary>
        private void ShowSlotDetail(SlotType slot)
        {
            var worn = WornItem(slot);
            // Counted from the list itself, so a spare copy of the worn item counts - comparing item
            // definitions used to hide it.
            int fits = CandidatesFor(slot).Count;

            if (worn != null)
            {
                SetDetailHead(worn.Icon, worn.DisplayName,
                    $"{ItemPresenter.SlotLabel(slot)} · {ItemPresenter.RarityLabel(worn.Rarity)}", worn);
                AddItemLines(worn);
            }
            else
            {
                SetDetailHead(null, "Empty", ItemPresenter.SlotLabel(slot), null);
            }

            AddNote(fits > 0
                ? $"{fits} other item{(fits == 1 ? "" : "s")} fit{(fits == 1 ? "s" : "")} here - press Enter to choose."
                : "Nothing else the party owns fits here.");
            AddStatSection(StatsWith(CurrentGear()), null);
        }

        /// <summary>
        /// An item to put on (or the slot to empty): its description and bonuses, then every stat
        /// as it is now against what it would be.
        /// </summary>
        private void ShowItemDetail(ItemSO item, StatBlock preview, string note, bool removing = false)
        {
            if (item == null)
            {
                ClearDetail();
                return;
            }

            SetDetailHead(item.Icon, item.DisplayName,
                $"{ItemPresenter.SlotLabel(item.SlotType)} · {ItemPresenter.RarityLabel(item.Rarity)}", item);
            AddItemLines(item);
            if (!string.IsNullOrEmpty(note))
            {
                AddNote(note);
            }
            AddStatSection(StatsWith(CurrentGear()), preview, removing ? "Stats without it" : "Stats if equipped");
        }

        private void ShowStackDetail(ItemSO item, int count, string effect)
        {
            SetDetailHead(item.Icon, item.DisplayName, $"{ItemPresenter.RarityLabel(item.Rarity)} · x{count}", item);
            if (!string.IsNullOrEmpty(effect))
            {
                AddLine(effect);
            }
            if (!string.IsNullOrEmpty(item.Description))
            {
                AddDescription(item.Description);
            }
        }

        private void ShowAbilityDetail(HeroSO hero, MagicSO magic, string key, bool isCarried, int charges,
            ICombatUnit unit, List<KeyValuePair<string, int>> known, int slots)
        {
            if (magic == null)
            {
                SetDetailHead(null, key, isCarried ? "Carried" : "Not carried", null);
                return;
            }

            SetDetailHead(magic.Icon, magic.DisplayName,
                $"{AbilityDescriber.TargetLabel(magic.TargetType)} · {charges} charges a run", null);

            int level = 0;
            int bonus = 0;
            if (MetaProgressManager.HasInstance)
            {
                level = MetaProgressManager.Instance.GetMagicUpgradeLevel(key);
                bonus = MetaProgressManager.Instance.GetMagicPowerBonus(key);
            }

            // Described through the hero's own stats (grid + gear), so the numbers are the ones this
            // hero will hit for - a null caster would read them as base values. Flavour in italics,
            // the mechanics upright under their own heading.
            if (!string.IsNullOrEmpty(magic.Description))
            {
                AddDescription(magic.Description.Trim());
            }
            var effects = AbilityDescriber.EffectLines(magic, unit, null, bonus, level);
            _detailBody?.Add(BestiaryLineView.Section("Effect"));
            if (effects.Count == 0)
            {
                AddLine(AbilityDescriber.TargetLabel(magic.TargetType));
            }
            foreach (var effect in effects)
            {
                AddLine(effect);
            }
            if (level > 0)
            {
                AddLine($"Forge level {level}.");
            }

            if (isCarried)
            {
                var carriedNow = MagicLoadoutOps.Resolve(known, _loadout.ChosenFor(_selectedHeroKey), slots);
                AddNote(carriedNow.Count == 1
                    ? "Carried. A hero always takes at least one ability - carry another to swap this one out."
                    : "Carried. Enter puts it away.");
                return;
            }

            // A full loadout drops its oldest pick to make room - say which before it happens.
            var chosen = MagicLoadoutOps.Resolve(known, _loadout.ChosenFor(_selectedHeroKey), slots);
            var after = MagicLoadoutOps.Toggle(known, new List<string>(chosen), key, slots);
            string displaced = null;
            foreach (var carriedKey in chosen)
            {
                if (!after.Contains(carriedKey))
                {
                    var m = Magic(carriedKey);
                    displaced = m != null ? m.DisplayName : carriedKey;
                    break;
                }
            }
            AddNote(displaced != null
                ? $"Slots are full: carrying this puts {displaced} away."
                : "Enter to carry it into the next run.");
        }

        private void AddItemLines(ItemSO item)
        {
            if (_detailBody != null)
            {
                AddItemLines(_detailBody, item);
            }
        }

        /// <summary>An item's description and what it grants, as chips. Shared with the merchant.</summary>
        internal static void AddItemLines(VisualElement body, ItemSO item)
        {
            if (!string.IsNullOrEmpty(item.Description))
            {
                var desc = new Label(item.Description);
                desc.AddToClassList("cd-inv-detail__desc");
                body.Add(desc);
            }

            // A wrapping row of short chips, not a list: four bonuses as lines pushed the stats and
            // resistances below the bottom of a pane that does not scroll.
            var chips = ItemPresenter.BonusChips(item);
            chips.AddRange(ItemPresenter.ResistanceLines(item));
            if (chips.Count == 0)
            {
                return;
            }
            body.Add(BestiaryLineView.Section("Grants"));
            var row = new VisualElement();
            row.AddToClassList("cd-inv-grants");
            foreach (var text in chips)
            {
                var chip = new Label(text);
                chip.AddToClassList("cd-inv-grant");
                row.Add(chip);
            }
            body.Add(row);
        }

        /// <summary>
        /// Every stat, now and - when an item is being considered - after, as a two-column grid of
        /// short names. A one-per-row list ran past the bottom of the pane and hid Health.
        /// </summary>
        private void AddStatSection(StatBlock current, StatBlock preview, string previewTitle = "Stats if equipped")
        {
            _detailBody?.Add(BestiaryLineView.Section(preview != null ? previewTitle : "Stats"));
            var grid = new VisualElement();
            grid.AddToClassList("cd-inv-stats");
            foreach (var stat in StatCatalog.Types)
            {
                int now = current[stat];
                bool changes = preview != null && preview[stat] != now;
                int then = changes ? preview[stat] : now;

                grid.Add(StatCell(StatCatalog.ShortName(stat),
                    changes ? $"{now} \u2192 {then}" : now.ToString(),
                    changes ? (then > now ? 1 : -1) : 0,
                    StatCatalog.DisplayName(stat)));
            }
            _detailBody?.Add(grid);

            // Resistance only when there is any to speak of - an all-zero section is noise. Gear and
            // grid nodes only: a hero's innate resistances are authored on the dungeon's Hero
            // component, which does not exist in the hub, and no hero authors any today.
            var hero = FindHero(_selectedHeroKey);
            var nodeResist = hero != null
                ? SphereGridOps.ResistancesForNodes(hero.SphereGrid, ActivatedNodesOf(hero))
                : null;
            var resistNow = ItemPresenter.SumResistances(CurrentGear(), nodeResist);
            Dictionary<DamageType, float> resistThen = null;
            if (preview != null && _listIndex >= 0 && _listIndex < _listEntries.Count)
            {
                resistThen = ItemPresenter.SumResistances(PreviewGear(), nodeResist);
            }

            var types = new List<DamageType>(resistNow.Keys);
            if (resistThen != null)
            {
                foreach (var type in resistThen.Keys)
                {
                    if (!types.Contains(type))
                    {
                        types.Add(type);
                    }
                }
            }
            if (types.Count == 0)
            {
                return;
            }

            // The same two-column grid as the stats, so the pane fits without scrolling.
            _detailBody?.Add(BestiaryLineView.Section("Resistances"));
            var resistGrid = new VisualElement();
            resistGrid.AddToClassList("cd-inv-stats");
            resistGrid.AddToClassList("cd-inv-stats--wide");
            foreach (var type in types)
            {
                resistNow.TryGetValue(type, out float now);
                float then = now;
                if (resistThen != null)
                {
                    resistThen.TryGetValue(type, out then);
                }
                bool changes = !Mathf.Approximately(now, then);
                string name = ItemPresenter.DamageTypeName(type);
                resistGrid.Add(StatCell(name,
                    changes ? $"{Percent(now)} → {Percent(then)}" : Percent(now),
                    changes ? (then > now ? 1 : -1) : 0,
                    name + " resistance"));
            }
            _detailBody?.Add(resistGrid);
        }

        /// <summary>One cell of a stat grid; <paramref name="direction"/> is +1 up, -1 down, 0 unchanged.</summary>
        internal static VisualElement StatCell(string label, string value, int direction, string tooltip)
        {
            var cell = new VisualElement();
            cell.AddToClassList("cd-inv-stat");
            cell.tooltip = tooltip;
            var labelElement = new Label(label);
            labelElement.AddToClassList("cd-inv-stat__label");
            cell.Add(labelElement);
            var valueElement = new Label(value);
            valueElement.AddToClassList("cd-inv-stat__value");
            if (direction != 0)
            {
                valueElement.AddToClassList(direction > 0 ? "cd-inv-delta--up" : "cd-inv-delta--down");
            }
            cell.Add(valueElement);
            return cell;
        }

        /// <summary>
        /// The gear the highlighted list entry would leave the hero in - carried on the entry itself,
        /// so it cannot drift from the order the list was built in.
        /// </summary>
        private List<ItemSO> PreviewGear()
        {
            if (_tab == Tab.Equipment && _listIndex >= 0 && _listIndex < _listEntries.Count
                && _listEntries[_listIndex].Gear != null)
            {
                return _listEntries[_listIndex].Gear;
            }
            return CurrentGear();
        }

        /// <summary>The candidate items for a slot, in the order the list shows them.</summary>
        private List<ItemSO> CandidatesFor(SlotType slot)
        {
            var bag = new List<ItemSO>();
            var others = new List<ItemSO>();
            foreach (var item in InventoryManager.Instance.GetAllEquipment())
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                if (so == null || so.SlotType != slot)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(item.EquippedSlot))
                {
                    bag.Add(so);
                }
                else if (item.EquippedHeroKey != _selectedHeroKey)
                {
                    others.Add(so);
                }
            }
            bag.AddRange(others);
            return bag;
        }

        private void AddDescription(string text)
        {
            var label = new Label(text);
            label.AddToClassList("cd-inv-detail__desc");
            _detailBody?.Add(label);
        }

        private void AddLine(string text)
        {
            var label = new Label(text);
            label.AddToClassList("cd-inv-detail__line");
            _detailBody?.Add(label);
        }

        private void AddNote(string text)
        {
            var label = new Label(text);
            label.AddToClassList("cd-inv-detail__note");
            _detailBody?.Add(label);
        }

        private static string Percent(float value)
        {
            return (value > 0f ? "+" : string.Empty) + Mathf.RoundToInt(value) + "%";
        }

        // ============================================================
        //  STATS
        // ============================================================

        /// <summary>
        /// The selected hero's effective stats with <paramref name="gear"/> worn: base + bought grid
        /// nodes + gear, exactly as a fight computes them.
        /// </summary>
        private StatBlock StatsWith(IEnumerable<ItemSO> gear)
        {
            var hero = FindHero(_selectedHeroKey);
            if (hero == null)
            {
                return new StatBlock();
            }
            var baseStats = HeroStatCalculator.BaseStatsForNodes(hero, ActivatedNodesOf(hero));
            return HeroStatCalculator.WithGear(baseStats, gear);
        }

        private HeroSnapshotUnit SnapshotOf(HeroSO hero)
        {
            var gear = InventoryManager.Instance.GetEquippedItems(hero.SaveKey);
            var baseStats = HeroStatCalculator.BaseStatsForNodes(hero, ActivatedNodesOf(hero));
            return new HeroSnapshotUnit(hero, HeroStatCalculator.WithGear(baseStats, gear));
        }

        private struct StatChange
        {
            public StatType Stat;
            public int Amount;
        }

        /// <summary>The <paramref name="count"/> largest stat changes, biggest first.</summary>
        private static List<StatChange> BiggestChanges(StatBlock before, StatBlock after, int count)
        {
            var changes = new List<StatChange>();
            foreach (var stat in StatCatalog.Types)
            {
                int delta = after[stat] - before[stat];
                if (delta != 0)
                {
                    changes.Add(new StatChange { Stat = stat, Amount = delta });
                }
            }
            changes.Sort((a, b) => Mathf.Abs(b.Amount).CompareTo(Mathf.Abs(a.Amount)));
            if (changes.Count > count)
            {
                changes.RemoveRange(count, changes.Count - count);
            }
            return changes;
        }

        /// <summary>The selected hero's activated grid nodes, straight off the party save.</summary>
        private static List<string> ActivatedNodesOf(HeroSO hero)
        {
            var save = HeroRoster.GetHeroSave(hero);
            return save != null && save.ActivatedNodes != null
                ? save.ActivatedNodes
                : new List<string>();
        }

        // ============================================================
        //  ROWS AND THE CURSOR
        // ============================================================

        /// <summary>
        /// A row: cursor, icon tile, a name with a caption under it, and a right-hand note. A plain
        /// VisualElement (not a Button) so it never takes focus - the cursor and the mouse drive it.
        /// </summary>
        private VisualElement BuildRow(Sprite icon, string name, string caption, string meta, ItemSO rarityOf,
            out Label cursor)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-inv-row");

            cursor = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            cursor.AddToClassList("cd-inv-row__cursor");
            row.Add(cursor);

            var tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("cd-inv-row__icon");
            SetIcon(tile, icon, rarityOf);
            row.Add(tile);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            var nameLabel = new Label(name) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("cd-inv-row__name");
            ApplyRarity(nameLabel, rarityOf);
            text.Add(nameLabel);
            if (!string.IsNullOrEmpty(caption))
            {
                var captionLabel = new Label(caption) { pickingMode = PickingMode.Ignore };
                captionLabel.AddToClassList("cd-inv-row__caption");
                text.Add(captionLabel);
            }
            row.Add(text);

            var metaLabel = new Label(meta ?? string.Empty) { pickingMode = PickingMode.Ignore };
            metaLabel.AddToClassList("cd-inv-row__meta");
            row.Add(metaLabel);

            return row;
        }

        private void AddListEntry(Entry entry)
        {
            int index = _listEntries.Count;
            _listEntries.Add(entry);
            entry.Row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_tab == Tab.Equipment)
                {
                    _focus = Focus.List;
                    RefreshHint();
                }
                SetListIndex(index);
            });
            entry.Row.RegisterCallback<ClickEvent>(_ =>
            {
                SetListIndex(index);
                entry.Confirm?.Invoke();
            });
            _list?.Add(entry.Row);
        }

        private void SetListIndex(int index)
        {
            if (_listEntries.Count == 0)
            {
                _listIndex = -1;
            }
            else
            {
                _listIndex = Mathf.Clamp(index, 0, _listEntries.Count - 1);
            }
            RenderCursor();
        }

        /// <summary>
        /// Draws the cursor and refreshes the detail pane. On Equipment the slot column holds it
        /// until Enter moves it into the list, where the chosen slot stays marked.
        /// </summary>
        private void RenderCursor()
        {
            bool listActive = _tab != Tab.Equipment || _focus == Focus.List;
            if (listActive && _listIndex < 0 && _listEntries.Count > 0 && _tab != Tab.Equipment)
            {
                _listIndex = 0;
            }

            for (int i = 0; i < _slotEntries.Count; i++)
            {
                bool here = i == _slotIndex;
                bool cursorHere = here && _focus == Focus.Slots;
                _slotEntries[i].Row.EnableInClassList("cd-inv-row--selected", cursorHere);
                _slotEntries[i].Row.EnableInClassList("cd-inv-row--held", here && _focus == Focus.List);
                SetText(_slotEntries[i].Cursor, cursorHere ? "▸" : string.Empty);
            }

            for (int i = 0; i < _listEntries.Count; i++)
            {
                bool cursorHere = listActive && i == _listIndex;
                _listEntries[i].Row.EnableInClassList("cd-inv-row--selected", cursorHere);
                SetText(_listEntries[i].Cursor, cursorHere ? "▸" : string.Empty);
            }

            if (_tab == Tab.Equipment && _focus == Focus.Slots)
            {
                if (_slotIndex >= 0 && _slotIndex < _slotEntries.Count)
                {
                    _slotEntries[_slotIndex].Preview?.Invoke();
                }
            }
            else if (_listIndex >= 0 && _listIndex < _listEntries.Count)
            {
                _listEntries[_listIndex].Preview?.Invoke();
                // A freshly rebuilt row has no layout yet, and ScrollTo on it does nothing - wait a frame.
                var row = _listEntries[_listIndex].Row;
                _list?.schedule.Execute(() =>
                {
                    if (row.panel != null)
                    {
                        _list.ScrollTo(row);
                    }
                });
            }
        }

        private void MoveCursor(int delta)
        {
            if (_tab == Tab.Equipment && _focus == Focus.Slots)
            {
                int count = ItemPresenter.SlotOrder.Length;
                MoveSlotTo(((_slotIndex + delta) % count + count) % count);
                return;
            }
            if (_listEntries.Count == 0)
            {
                return;
            }
            int n = _listEntries.Count;
            SetListIndex(((_listIndex + delta) % n + n) % n);
        }

        private void Confirm()
        {
            if (_tab == Tab.Equipment && _focus == Focus.Slots)
            {
                EnterList();
                return;
            }
            if (_listIndex >= 0 && _listIndex < _listEntries.Count)
            {
                _listEntries[_listIndex].Confirm?.Invoke();
            }
        }

        private void Back()
        {
            if (_tab == Tab.Equipment && _focus == Focus.List)
            {
                LeaveList();
                return;
            }
            Hide();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_isShown)
            {
                return;
            }
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveCursor(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveCursor(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    Confirm();
                    evt.StopPropagation();
                    break;
                case KeyCode.LeftArrow:
                    CycleTab(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.RightArrow:
                    CycleTab(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Q:
                    if (IsPerHeroTab)
                    {
                        CycleHero(-1);
                    }
                    evt.StopPropagation();
                    break;
                case KeyCode.E:
                    if (IsPerHeroTab)
                    {
                        CycleHero(1);
                    }
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                case KeyCode.Backspace:
                    Back();
                    evt.StopPropagation();
                    break;
            }
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        private HeroSO FindHero(string heroKey)
        {
            if (_roster == null || string.IsNullOrEmpty(heroKey))
            {
                return null;
            }
            return RosterHeroes().Find(h => h != null && h.SaveKey == heroKey);
        }

        private void ShowEmpty(string message)
        {
            SetText(_emptyLabel, message);
            SetShown(_emptyLabel, true);
        }

        internal static void SetIcon(VisualElement tile, Sprite icon, ItemSO rarityOf)
        {
            if (tile == null)
            {
                return;
            }
            tile.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground((Texture2D)null);
            tile.AddToClassList("cd-inv-tile");
            ApplyRarity(tile, rarityOf);
        }

        /// <summary>Rarity colour via the theme's classes, so the palette lives in one file.</summary>
        internal static void ApplyRarity(VisualElement element, ItemSO item)
        {
            if (element == null)
            {
                return;
            }
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                element.RemoveFromClassList(ItemPresenter.RarityClass(rarity));
            }
            if (item != null)
            {
                element.AddToClassList(ItemPresenter.RarityClass(item.Rarity));
            }
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
    }
}
