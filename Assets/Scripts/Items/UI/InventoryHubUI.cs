using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Items.UI
{
    /// <summary>
    /// Hub inventory (UI Toolkit view-controller, mirrors <c>MagicForgeUI</c>). Three tabs:
    /// equipment per hero, the spell loadout per hero, and the carried consumables. It is the only
    /// place gear and kit are managed, since both are between-run decisions. Operates on a VisualElement subtree owned by the menu's UIDocument (not a MonoBehaviour).
    /// Reads the roster from a <see cref="PartyRosterSO"/> because the hub has no live Party, and all
    /// item/equip state from the (scene-independent) <see cref="InventoryManager"/>.
    ///
    /// Interaction mirrors the battle scene's cursor selection list (see MagicSelectionUI): a ▸
    /// cursor over the rows, driven by keyboard (Up/Down/Enter, Left/Right tabs, Q/E hero, Esc back)
    /// while staying fully mouse-usable.
    /// </summary>
    public class InventoryHubUI
    {
        private enum Tab { Equipment, Spells, Consumables }

        private readonly VisualElement _root;
        private readonly PartyRosterSO _roster;
        private List<HeroSO> _ownedHeroes;

        private readonly VisualElement _heroesRow;
        private readonly Label _statsLabel;
        private readonly VisualElement _tabsBar;
        private readonly Button _tabEquipment;
        private readonly Button _tabSpells;
        private readonly Button _tabConsumables;
        private readonly ScrollView _scroll;
        private readonly Label _emptyLabel;
        private readonly Button _closeButton;

        private Tab _tab = Tab.Equipment;
        private string _selectedHeroKey;
        private bool _isShown;

        // The chosen spell loadouts, loaded on Show and written straight back on every change. A
        // loadout is a preference, not a gain, so it is not deferred to a level clear the way XP and
        // loot are - dying must not cost the player their equipment layout.
        private readonly FileHandler _files = new FileHandler();
        private MagicLoadoutSaveData _loadout;

        // Cursor-driven keyboard navigation over the currently shown selectable rows.
        private readonly List<VisualElement> _navRows = new List<VisualElement>();
        private readonly List<Label> _navCursors = new List<Label>();
        private readonly List<Action> _navActions = new List<Action>();
        private int _navSelected = -1;

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
            _tabSpells = root.Q<Button>("tab-spells");
            _tabConsumables = root.Q<Button>("tab-consumables");
            _scroll = root.Q<ScrollView>("inventory-scroll");
            _emptyLabel = root.Q<Label>("inventory-empty");
            _closeButton = root.Q<Button>("inventory-close");

            if (_tabEquipment != null)
            {
                _tabEquipment.clicked += () => SetTab(Tab.Equipment);
                _tabEquipment.focusable = false;
            }
            if (_tabSpells != null)
            {
                _tabSpells.clicked += () => SetTab(Tab.Spells);
                _tabSpells.focusable = false;
            }
            if (_tabConsumables != null)
            {
                _tabConsumables.clicked += () => SetTab(Tab.Consumables);
                _tabConsumables.focusable = false;
            }
            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
                _closeButton.focusable = false;
            }
            if (_scroll != null)
            {
                // Keep keyboard focus on the view root, not the ScrollView, so our cursor nav owns
                // the arrows (UITK's default focus navigation would otherwise steal them).
                _scroll.focusable = false;
            }

            // The view root owns keyboard input while open; swallow UITK's built-in navigation so
            // it can't move focus off the root after the first arrow (mirrors the battle UI).
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<NavigationMoveEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationSubmitEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationCancelEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            _isShown = true;
            _tab = Tab.Equipment;

            // The hub MenuScene has no wired Managers prefab; touch Instance so the manager
            // auto-creates and loads (in Awake) before we read equip/consumable state.
            _ = InventoryManager.Instance;

            _loadout = _files.Load<MagicLoadoutSaveData>();

            // Default to the first owned hero. The catalog lists every hero in the game; only the
            // ones the player has actually acquired get gear slots here.
            _ownedHeroes = null;
            _selectedHeroKey = null;
            var owned = RosterHeroes();
            if (owned.Count > 0)
            {
                _selectedHeroKey = owned[0].SaveKey;
            }

            RefreshTabs();
            RefreshHeroes();
            RefreshStats();
            RefreshList();

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

        /// <summary>Left/Right step through the tabs. Clamped rather than wrapping, so the ends
        /// of the row feel like ends - the same behaviour two tabs had when they were a toggle.</summary>
        private void CycleTab(int delta)
        {
            int next = Mathf.Clamp((int)_tab + delta, 0, (int)Tab.Consumables);
            if (next != (int)_tab)
            {
                SetTab((Tab)next);
            }
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
            _tabSpells?.EnableInClassList("cd-tab--active", _tab == Tab.Spells);
            _tabConsumables?.EnableInClassList("cd-tab--active", _tab == Tab.Consumables);
            // Stats are an equipment readout; the hero selector serves both per-hero tabs.
            SetShown(_statsLabel, _tab == Tab.Equipment);
            SetShown(_heroesRow, _tab != Tab.Consumables);
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

            foreach (var hero in RosterHeroes())
            {
                if (hero == null)
                {
                    continue;
                }
                var key = hero.SaveKey;
                var btn = new Button(() => SelectHero(key)) { text = hero.DisplayName };
                btn.AddToClassList("cd-tab");
                btn.EnableInClassList("cd-tab--active", key == _selectedHeroKey);
                btn.focusable = false;
                _heroesRow.Add(btn);
            }
        }

        /// <summary>
        /// The heroes this screen manages: the *owned* subset of the catalog, via
        /// <see cref="HeroRoster"/>. Cached per Show() because it reads the party save off disk and
        /// the list is queried on every refresh.
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
            _selectedHeroKey = heroKey;
            RefreshHeroes();
            RefreshStats();
            RefreshList();
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
            int next = ((current + delta) % count + count) % count;
            var hero = heroes[next];
            if (hero != null)
            {
                SelectHero(hero.SaveKey);
            }
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
            if (hero == null)
            {
                _statsLabel.text = string.Empty;
                return;
            }

            var raw = InventoryManager.Instance.ComputeRawBonuses(_selectedHeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(_selectedHeroKey);

            // Every stat, generated: the four hand-written ones used to hide Intelligence, Spirit
            // and Luck entirely, which are exactly the stats that distinguish a caster.
            var parts = new List<string>();
            foreach (var stat in StatCatalog.Types)
            {
                parts.Add(StatCatalog.ShortName(stat) + " "
                    + Effective(hero.BaseStats[stat], raw[stat], pct[stat]));
            }
            _statsLabel.text = string.Join("   ", parts);
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
            ClearNav();

            if (_tab == Tab.Equipment)
            {
                RefreshEquipment();
            }
            else if (_tab == Tab.Spells)
            {
                RefreshSpells();
            }
            else
            {
                RefreshConsumables();
            }

            BeginNavigation();
        }

        private void RefreshEquipment()
        {
            SetShown(_emptyLabel, false);

            // Equipped slots (click / Enter a filled slot to unequip).
            foreach (SlotType slot in Enum.GetValues(typeof(SlotType)))
            {
                var equipped = InventoryManager.Instance.GetEquipped(slot, _selectedHeroKey);
                if (equipped != null)
                {
                    var so = InventoryManager.Instance.GetItemSO(equipped.ItemKey);
                    string name = so != null ? so.DisplayName : equipped.ItemKey;
                    var capturedSlot = slot;
                    _scroll.Add(BuildRow(so != null ? so.Icon : null, $"[{slot}] {name}", "Unequip", true,
                        () => Unequip(capturedSlot), so));
                }
                else
                {
                    _scroll.Add(BuildRow(null, $"[{slot}] Empty", string.Empty, false, null, null));
                }
            }

            // Un-equipped equipment in the bag (click / Enter to equip on the selected hero).
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

        /// <summary>
        /// The spell loadout: every magic the selected hero's sphere grid has taught them, with the
        /// ones they are carrying marked, and a header saying how many slots they have.
        ///
        /// <para>This screen exists because knowing and carrying stopped being the same thing.
        /// Under Draw a hero's slots were filled in the field and a MagicKnown node brought its own
        /// slot with it; now the grid only ever adds to what a hero <i>knows</i>, and slots stay
        /// scarce (<c>EquippedMagicState.DefaultSlotCount</c> plus <c>MagicSlot</c> nodes), so which
        /// of them to bring is a decision that has to be made somewhere. Here, between runs, beside
        /// the other one.</para>
        ///
        /// <para>A hero who never opens this still walks in armed: an empty choice auto-fills from
        /// what they know, in grid order (<see cref="MagicLoadoutOps.Resolve"/>). Toggling writes the
        /// resolved list, so the first click also commits whatever the auto-fill had picked - which
        /// is what makes "unequip one spell" behave the way it reads.</para>
        /// </summary>
        private void RefreshSpells()
        {
            var hero = FindHero(_selectedHeroKey);
            if (hero == null)
            {
                ShowEmpty("No hero selected.");
                return;
            }

            var known = SphereGridOps.KnownMagicForNodes(hero.SphereGrid, ActivatedNodesOf(hero));
            if (known.Count == 0)
            {
                ShowEmpty($"{hero.DisplayName} knows no magic yet. Learn a spell on the sphere grid.");
                return;
            }

            SetShown(_emptyLabel, false);

            int slots = EquippedMagicState.DefaultSlotCount
                + SphereGridOps.SlotBonusForNodes(hero.SphereGrid, ActivatedNodesOf(hero));
            var equipped = MagicLoadoutOps.Resolve(known, _loadout.ChosenFor(_selectedHeroKey), slots);

            var header = new Label($"Carrying {equipped.Count} of {slots} slots — {known.Count} known.");
            header.AddToClassList("cd-info-label");
            _scroll.Add(header);

            foreach (var entry in known)
            {
                var magic = MagicCatalog.HasInstance ? MagicCatalog.Instance.GetMagic(entry.Key) : null;
                string name = magic != null ? magic.DisplayName : entry.Key;
                bool carried = equipped.Contains(entry.Key);
                string meta = carried ? $"carried · {entry.Value} charges" : "known";

                var capturedKey = entry.Key;
                _scroll.Add(BuildRow(
                    magic != null ? magic.Icon : null,
                    (carried ? "* " : "  ") + name,
                    meta,
                    true,
                    () => ToggleSpell(capturedKey),
                    null));
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
            int slots = EquippedMagicState.DefaultSlotCount
                + SphereGridOps.SlotBonusForNodes(hero.SphereGrid, nodes);

            var entry = _loadout.For(_selectedHeroKey);
            entry.EquippedKeys = MagicLoadoutOps.Toggle(known, entry.EquippedKeys, magicKey, slots);
            _files.Save(_loadout);

            RefreshList();
        }

        /// <summary>The selected hero's activated grid nodes, straight off the party save.</summary>
        private static List<string> ActivatedNodesOf(HeroSO hero)
        {
            var save = HeroRoster.GetHeroSave(hero);
            return save != null && save.ActivatedNodes != null
                ? save.ActivatedNodes
                : new List<string>();
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
                // Display-only (used in combat, not the hub) → not selectable.
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
        //  ROW BUILDER (battle cd-sel-row style + rarity edge accent)
        // ============================================================

        private VisualElement BuildRow(Sprite icon, string name, string meta, bool enabled, Action onClick, ItemSO so)
        {
            // Plain VisualElement (not a Button) so it never grabs focus; the cursor nav + mouse
            // clicks drive it, exactly like the battle selection rows.
            var row = new VisualElement();
            row.AddToClassList("cd-sel-row");
            row.SetEnabled(enabled);

            // Rarity as a left-edge accent — keeps the info without tinting (and washing out) text.
            if (so != null && RarityColors.TryGetValue(so.Rarity, out var rarityColor))
            {
                row.style.borderLeftColor = rarityColor;
                row.style.borderLeftWidth = 4;
            }

            var cursor = new Label(string.Empty);
            cursor.AddToClassList("cd-sel-row__cursor");
            cursor.pickingMode = PickingMode.Ignore;
            row.Add(cursor);

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
            row.Add(nameLabel);

            var metaLabel = new Label(meta ?? string.Empty);
            metaLabel.AddToClassList("cd-sel-row__meta");
            metaLabel.pickingMode = PickingMode.Ignore;
            row.Add(metaLabel);

            if (enabled && onClick != null)
            {
                int idx = _navRows.Count;
                _navRows.Add(row);
                _navCursors.Add(cursor);
                _navActions.Add(onClick);
                row.RegisterCallback<ClickEvent>(_ => { SetNavSelected(idx); onClick(); });
                row.RegisterCallback<MouseEnterEvent>(_ => SetNavSelected(idx));
            }

            return row;
        }

        // ============================================================
        //  CURSOR NAVIGATION (keyboard / mouse), mirrors MagicSelectionUI
        // ============================================================

        private void ClearNav()
        {
            _navRows.Clear();
            _navCursors.Clear();
            _navActions.Clear();
            _navSelected = -1;
        }

        private void BeginNavigation()
        {
            _navSelected = _navRows.Count > 0 ? 0 : -1;
            RenderNavCursor();
        }

        private void RenderNavCursor()
        {
            for (int i = 0; i < _navRows.Count; i++)
            {
                bool selected = i == _navSelected;
                _navCursors[i].text = selected ? "▸" : string.Empty;
                _navRows[i].EnableInClassList("cd-sel-row--selected", selected);
            }
            if (_navSelected >= 0 && _navSelected < _navRows.Count && _scroll != null)
            {
                _scroll.ScrollTo(_navRows[_navSelected]);
            }
        }

        private void SetNavSelected(int index)
        {
            if (index < 0 || index >= _navRows.Count)
            {
                return;
            }
            _navSelected = index;
            RenderNavCursor();
        }

        private void MoveNav(int delta)
        {
            if (_navRows.Count == 0)
            {
                return;
            }
            _navSelected = (_navSelected + delta + _navRows.Count) % _navRows.Count;
            RenderNavCursor();
        }

        private void ConfirmNav()
        {
            if (_navSelected >= 0 && _navSelected < _navActions.Count)
            {
                _navActions[_navSelected]?.Invoke();
            }
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
                    MoveNav(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveNav(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ConfirmNav();
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
                    if (_tab != Tab.Consumables)
                    {
                        CycleHero(-1);
                    }
                    evt.StopPropagation();
                    break;
                case KeyCode.E:
                    if (_tab != Tab.Consumables)
                    {
                        CycleHero(1);
                    }
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                case KeyCode.Backspace:
                    Hide();
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
