using System;
using System.Collections.Generic;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Items;
using Assets.Scripts.Items.UI;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Hub "Forge" (UI Toolkit view-controller): <b>craft</b> items from known recipes, spend Essence to
    /// upgrade combos, and read the collection of every known ability. Three tabs (Craft / Abilities /
    /// Combos). Operates on a VisualElement subtree; not a MonoBehaviour.
    ///
    /// <para><b>Crafting since 2026-10-29</b> (<c>docs/plans/HUB.md</c> §7 phase 7, the owner's call to
    /// bring it forward): a recipe is an item (<see cref="ItemCategory.Recipe"/>), found like loot and
    /// never used up; the Forge teaches a few basic ones itself. A craft costs the recipe's materials,
    /// its gold and sometimes a base item. The rules are <see cref="CraftingOps"/>, the transaction
    /// <see cref="InventoryManager.TryCraft"/>. Combo upgrades stay here as they were - how they should
    /// work is to be rethought later (owner).</para>
    ///
    /// <para><b>Abilities are not upgraded here since 2026-10-06</b> (<c>docs/plans/HUB.md</c> §3c):
    /// Essence became the revisit currency, and an ability grows on its hero's sphere grid, through
    /// Essence-priced nodes. The Abilities tab stays as the collection - what has been learned, what
    /// each does - with no price and no Upgrade button.</para>
    ///
    /// <para><b>On the inventory frame since 2026-09-30</b> (menu review): the
    /// <i>known</i> entries as a list (icon, name, level, next price), everything undiscovered folded
    /// into one row at the end - the old grid of 31 identical "?" tiles said only "you know one thing" -
    /// and the chosen entry's detail always beside the list, with Upgrade and the reason it is dimmed.
    /// Effect lines come from <see cref="AbilityDescriber.ForgeLine"/>, so the Forge says what the
    /// Storehouse and the combat picker say ("Bleeding 1/turn, 3 turns", not "-Bleeding 1 (3t)").</para>
    /// </summary>
    public class MagicForgeUI
    {
        private enum Tab { Craft, Magic, Combos }

        /// <summary>One entry - a known recipe, a known ability (view only) or an upgradeable combo - and its row.</summary>
        private class Entry
        {
            public ItemSO Recipe;
            public MagicSO Magic;
            public MagicComboSO Combo;
            public Button Row;
        }

        private readonly VisualElement _root;
        private readonly Label _essenceLabel;
        private readonly Button _tabCraft;
        private readonly Button _tabMagic;
        private readonly Button _tabCombos;
        private readonly Button _closeButton;
        private readonly Label _listTitle;
        private readonly ScrollView _list;
        private readonly Label _emptyLabel;
        private readonly VisualElement _detailIcon;
        private readonly Label _detailName;
        private readonly Label _detailSub;
        private readonly Button _upgradeButton;
        private readonly Label _reasonLabel;
        private readonly ScrollView _detailBody;
        private readonly Label _feedbackLabel;

        private readonly List<Entry> _entries = new List<Entry>();
        private Tab _tab = Tab.Craft;
        private int _index;

        public event Action OnClosed;

        public MagicForgeUI(VisualElement root)
        {
            _root = root;
            _essenceLabel = root.Q<Label>("forge-essence");
            _tabCraft = root.Q<Button>("forge-tab-craft");
            _tabMagic = root.Q<Button>("forge-tab-magic");
            _tabCombos = root.Q<Button>("forge-tab-combos");
            _closeButton = root.Q<Button>("forge-close");
            _listTitle = root.Q<Label>("forge-list-title");
            _list = root.Q<ScrollView>("forge-list");
            _emptyLabel = root.Q<Label>("forge-empty");
            _detailIcon = root.Q<VisualElement>("forge-detail-icon");
            _detailName = root.Q<Label>("forge-detail-name");
            _detailSub = root.Q<Label>("forge-detail-sub");
            _upgradeButton = root.Q<Button>("forge-upgrade");
            _reasonLabel = root.Q<Label>("forge-reason");
            _detailBody = root.Q<ScrollView>("forge-detail-body");
            _feedbackLabel = root.Q<Label>("forge-feedback");

            _tabCraft.clicked += () => SetTab(Tab.Craft);
            _tabMagic.clicked += () => SetTab(Tab.Magic);
            _tabCombos.clicked += () => SetTab(Tab.Combos);
            _closeButton.clicked += Hide;
            _upgradeButton.clicked += OnUpgrade;

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            if (!MagicCatalog.HasInstance)
            {
                Debug.LogWarning("MagicCatalog not found. Cannot open the Forge.");
                return;
            }

            _root.style.display = DisplayStyle.Flex;
            SetText(_feedbackLabel, string.Empty);
            SetTab(Tab.Craft);
            MetaProgressManager.Instance.MarkRecipesViewed(KnownRecipeCount());
        }

        /// <summary>
        /// How many recipes are known - the Forge's own and every one in the bags. The hub's news badge
        /// compares it with the count at the last visit.
        /// </summary>
        public static int KnownRecipeCount()
        {
            if (!InventoryManager.HasInstance)
            {
                return 0;
            }
            int known = 0;
            foreach (var recipe in CraftingOps.Recipes(InventoryManager.Instance.AllItems()))
            {
                if (InventoryManager.Instance.KnowsRecipe(recipe))
                {
                    known++;
                }
            }
            return known;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _list.Clear();
            _entries.Clear();
            OnClosed?.Invoke();
        }

        // ============================================================
        //  TABS + LIST
        // ============================================================

        private void SetTab(Tab tab)
        {
            _tab = tab;
            _index = 0;
            _tabCraft.EnableInClassList("cd-inv-tab--active", tab == Tab.Craft);
            _tabMagic.EnableInClassList("cd-inv-tab--active", tab == Tab.Magic);
            _tabCombos.EnableInClassList("cd-inv-tab--active", tab == Tab.Combos);
            Refresh();
        }

        private void Refresh()
        {
            UpdateEssence();
            BuildList();
            RefreshDetail();
        }

        private void BuildList()
        {
            _list.Clear();
            _entries.Clear();
            var meta = MetaProgressManager.Instance;
            int undiscovered = 0;

            if (_tab == Tab.Craft)
            {
                SetText(_listTitle, "Known recipes");
                foreach (var recipe in CraftingOps.Recipes(InventoryManager.Instance.AllItems()))
                {
                    if (InventoryManager.Instance.KnowsRecipe(recipe))
                    {
                        AddEntry(new Entry { Recipe = recipe });
                    }
                    else
                    {
                        undiscovered++;
                    }
                }
            }
            else if (_tab == Tab.Magic)
            {
                SetText(_listTitle, "Known abilities");
                foreach (var magic in MagicCatalog.Instance.AllMagic)
                {
                    if (magic == null || string.IsNullOrEmpty(magic.Key))
                    {
                        continue;
                    }
                    if (meta.IsMagicDiscovered(magic.Key))
                    {
                        AddEntry(new Entry { Magic = magic });
                    }
                    else
                    {
                        undiscovered++;
                    }
                }
            }
            else
            {
                SetText(_listTitle, "Known combos");
                if (MagicComboCatalog.HasInstance)
                {
                    foreach (var combo in MagicComboCatalog.Instance.AllCombos)
                    {
                        if (combo == null || string.IsNullOrEmpty(combo.Key))
                        {
                            continue;
                        }
                        if (meta.IsComboDiscovered(combo.Key))
                        {
                            AddEntry(new Entry { Combo = combo });
                        }
                        else
                        {
                            undiscovered++;
                        }
                    }
                }
            }

            // Everything not yet found, as one row: a count keeps the mystery a tile of "?" had, and
            // costs one row instead of the screen.
            if (undiscovered > 0)
            {
                _list.Add(StaticRow("?", $"{undiscovered} undiscovered", UndiscoveredCaption()));
            }

            bool none = _entries.Count == 0;
            SetShown(_emptyLabel, none);
            SetText(_emptyLabel, none ? EmptyText() : string.Empty);
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
            if (entry.Recipe != null)
            {
                InventoryHubUI.SetIcon(icon, entry.Recipe.CraftResult.Icon, entry.Recipe.CraftResult);
            }
            else
            {
                var sprite = entry.Magic != null ? entry.Magic.Icon : entry.Combo.Icon;
                if (sprite != null)
                {
                    icon.style.backgroundImage = new StyleBackground(sprite);
                }
            }
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            text.Add(MakeLabel(NameOf(entry), "cd-inv-row__name"));
            text.Add(MakeLabel(CaptionOf(entry), "cd-inv-row__caption"));
            row.Add(text);

            // A recipe's gold on the right, red when anything is missing - which part, the detail says.
            if (entry.Recipe != null)
            {
                var price = MakeLabel($"{Mathf.Max(0, entry.Recipe.CraftGold)} g", "cd-inv-row__meta", "cd-shop-price");
                price.EnableInClassList("cd-shop-price--short", CraftBlockOf(entry.Recipe) != CraftBlock.None);
                row.Add(price);
            }

            // A combo's next price on the right, red when it is out of reach, "MAX" when there is
            // none. An ability has no price: it is in the list to be read, not bought.
            if (entry.Combo != null)
            {
                var meta = MakeLabel(PriceTag(entry), "cd-inv-row__meta", "cd-shop-price");
                meta.EnableInClassList("cd-shop-price--short", !CanUpgrade(entry) && CostOf(entry) > 0);
                row.Add(meta);
            }

            entry.Row = row;
            _entries.Add(entry);
            _list.Add(row);
        }

        private VisualElement StaticRow(string glyph, string name, string caption)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("cd-inv-row");
            row.AddToClassList("cd-inv-row--static");
            row.AddToClassList("cd-inv-row--empty");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("cd-inv-row__icon");
            var q = MakeLabel(glyph, "cd-inv-row__glyph");
            icon.Add(q);
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            text.Add(MakeLabel(name, "cd-inv-row__name"));
            text.Add(MakeLabel(caption, "cd-inv-row__caption"));
            row.Add(text);
            return row;
        }

        private void Select(int index)
        {
            _index = index;
            SetText(_feedbackLabel, string.Empty);
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

        // ============================================================
        //  DETAIL
        // ============================================================

        private Entry Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

        private void RefreshDetail()
        {
            _detailBody.Clear();
            var entry = Current;
            if (entry == null)
            {
                SetIcon(null);
                InventoryHubUI.ApplyRarity(_detailName, null);
                SetText(_detailName, _tab == Tab.Craft ? "No recipe chosen"
                    : _tab == Tab.Magic ? "No ability chosen" : "No combo chosen");
                SetText(_detailSub, string.Empty);
                SetText(_reasonLabel, string.Empty);
                SetShown(_upgradeButton, false);
                return;
            }

            InventoryHubUI.ApplyRarity(_detailName, null);
            if (entry.Recipe != null)
            {
                RefreshRecipeDetail(entry.Recipe);
                return;
            }
            if (entry.Magic != null)
            {
                RefreshAbilityDetail(entry.Magic);
                return;
            }

            var meta = MetaProgressManager.Instance;
            int level = LevelOf(entry);
            int ceiling = meta.ComboUpgradeCeiling;
            int cost = CostOf(entry);

            SetIcon(entry.Combo.Icon);
            SetText(_detailName, NameOf(entry));
            SetText(_detailSub, LevelCaption(level));

            SetShown(_upgradeButton, true);
            bool can = CanUpgrade(entry);
            _upgradeButton.text = level >= MetaProgressManager.MaxComboUpgradeLevel
                ? "Fully upgraded"
                : level >= ceiling
                    ? $"Held at Lv {ceiling} by the Forge"
                    : $"Upgrade to Lv {level + 1} — {cost} essence";
            _upgradeButton.SetEnabled(can);
            SetText(_reasonLabel, can ? string.Empty : WhyNot(level, ceiling, cost));

            string description = entry.Combo.Description;
            if (!string.IsNullOrEmpty(description))
            {
                _detailBody.Add(MakeLabel(description.Trim(), "cd-inv-detail__desc"));
            }
            if (entry.Combo.RequiredTags != null)
            {
                _detailBody.Add(MakeLabel($"Triggered by: {string.Join(" + ", entry.Combo.RequiredTags)}", "cd-inv-detail__line"));
            }

            // Every effect, now → after the next upgrade; the ones a higher level unlocks, dimmed with it.
            int bonus = MetaProgressManager.ComboPowerBonusForLevel(level);
            int nextBonus = level < ceiling ? MetaProgressManager.ComboPowerBonusForLevel(level + 1) : bonus;
            var effects = entry.Combo.BonusEffects;
            _detailBody.Add(BestiaryLineView.Section("Bonus effects"));
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect == null)
                    {
                        continue;
                    }
                    bool locked = effect.UnlockLevel > level;
                    string line = AbilityDescriber.ForgeLine(effect, bonus, locked ? bonus : nextBonus);
                    var label = MakeLabel(locked ? $"Lv {effect.UnlockLevel}: {line}" : line,
                        locked ? "cd-inv-detail__note" : "cd-inv-detail__line");
                    _detailBody.Add(label);
                }
            }
        }

        /// <summary>
        /// A known recipe: what it makes, every input with have / need, and Forge with the reason it is
        /// dimmed. The result is described by the Storehouse's own lines, so a crafted sword reads the
        /// same here as in the bags.
        /// </summary>
        private void RefreshRecipeDetail(ItemSO recipe)
        {
            var result = recipe.CraftResult;
            InventoryHubUI.SetIcon(_detailIcon, result.Icon, result);
            SetText(_detailName, CraftingOps.ResultName(recipe));
            InventoryHubUI.ApplyRarity(_detailName, result);
            SetText(_detailSub, result.Category == ItemCategory.Equipment
                ? $"{ItemPresenter.RarityLabel(result.Rarity)} · {ItemPresenter.SlotLabel(result.SlotType)}"
                : ItemPresenter.RarityLabel(result.Rarity));

            var block = CraftBlockOf(recipe);
            int gold = Mathf.Max(0, recipe.CraftGold);
            SetShown(_upgradeButton, true);
            _upgradeButton.text = gold > 0 ? $"Forge — {gold} gold" : "Forge";
            _upgradeButton.SetEnabled(block == CraftBlock.None);
            SetText(_reasonLabel, CraftReason(recipe, block));

            _detailBody.Add(BestiaryLineView.Section("Needs"));
            if (recipe.CraftBase != null)
            {
                _detailBody.Add(RichLine(BaseLine(recipe.CraftBase)));
            }
            var inventory = InventoryManager.Instance;
            foreach (var line in recipe.CraftMaterials)
            {
                if (line == null || !line.IsValid)
                {
                    continue;
                }
                int need = Mathf.Max(1, line.Amount);
                int have = inventory.GetMaterialQuantity(line.Material.Key);
                _detailBody.Add(RichLine(HaveNeed(line.Material.DisplayName, have, need)));
            }
            if (gold > 0)
            {
                _detailBody.Add(RichLine(HaveNeed("Gold", MetaProgressManager.Instance.Gold, gold)));
            }

            // What it makes: the Storehouse's own lines (description, grants, reactions).
            InventoryHubUI.AddItemLines(_detailBody, result);
        }

        /// <summary>The base item's line: in the bags, on a hero (who will get the result), or missing.</summary>
        private static string BaseLine(ItemSO baseItem)
        {
            string name = string.IsNullOrEmpty(baseItem.DisplayName) ? baseItem.Key : baseItem.DisplayName;
            var copy = CraftingOps.FindBase(InventoryManager.Instance.GetItems(), baseItem.Key);
            if (copy == null)
            {
                return $"<color={MaterialCostText.ShortColour}>{name} 0/1</color>";
            }
            if (!string.IsNullOrEmpty(copy.EquippedHeroKey))
            {
                return $"<color={MaterialCostText.HaveColour}>{name} 1/1</color> (worn - the new piece goes on in its place)";
            }
            return $"<color={MaterialCostText.HaveColour}>{name} 1/1</color>";
        }

        private static string HaveNeed(string name, int have, int need)
        {
            string colour = have >= need ? MaterialCostText.HaveColour : MaterialCostText.ShortColour;
            return $"<color={colour}>{name} {Mathf.Min(have, need)}/{need}</color>";
        }

        private static Label RichLine(string text)
        {
            var label = MakeLabel(text, "cd-inv-detail__line");
            label.enableRichText = true;
            return label;
        }

        private static CraftBlock CraftBlockOf(ItemSO recipe)
        {
            return InventoryManager.Instance.CheckCraft(recipe, MetaProgressManager.Instance.Gold);
        }

        /// <summary>Why Forge is dimmed, in one line; which input is short, the Needs list colours.</summary>
        private static string CraftReason(ItemSO recipe, CraftBlock block)
        {
            switch (block)
            {
                case CraftBlock.NoBase:
                    return $"Needs a {recipe.CraftBase.DisplayName} to work into it.";
                case CraftBlock.Materials:
                    return "Not enough materials.";
                case CraftBlock.Gold:
                    return $"Need {recipe.CraftGold} gold — you have {MetaProgressManager.Instance.Gold}.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// A known ability, read-only: what it does, with no price and no Upgrade button. Effects
        /// gated behind an upgrade level are left out - nothing raises an ability's level any more,
        /// so they can never fire.
        /// </summary>
        private void RefreshAbilityDetail(MagicSO magic)
        {
            SetIcon(magic.Icon);
            SetText(_detailName, magic.DisplayName);
            SetText(_detailSub, "Grows on its hero's sphere grid");
            SetShown(_upgradeButton, false);
            SetText(_reasonLabel, string.Empty);

            if (!string.IsNullOrEmpty(magic.Description))
            {
                _detailBody.Add(MakeLabel(magic.Description.Trim(), "cd-inv-detail__desc"));
            }

            _detailBody.Add(BestiaryLineView.Section(AbilityDescriber.TargetLabel(magic)));
            if (magic.Effects != null)
            {
                foreach (var effect in magic.Effects)
                {
                    if (effect == null || effect.UnlockLevel > 0)
                    {
                        continue;
                    }
                    _detailBody.Add(MakeLabel(AbilityDescriber.ForgeLine(effect, 0, 0), "cd-inv-detail__line"));
                }
            }
            _detailBody.Add(MakeLabel("Before the target's defense. A hero adds the stat after the +.",
                "cd-inv-detail__note"));
        }

        /// <summary>
        /// Why Upgrade is dimmed. The Forge-ceiling case is the one worth having: a combo stopped by
        /// the <i>Forge</i> rather than by its own ceiling has to say so, or the player reads a dead
        /// button as a bug in the combo.
        /// </summary>
        private static string WhyNot(int level, int ceiling, int cost)
        {
            if (level >= MetaProgressManager.MaxComboUpgradeLevel)
            {
                return string.Empty;
            }
            if (level >= ceiling)
            {
                return $"The Forge allows Lv {ceiling}. Raise the Forge to go further.";
            }
            return $"Need {cost} essence — you have {MetaProgressManager.Instance.Essence}.";
        }

        private void OnUpgrade()
        {
            var entry = Current;
            if (entry != null && entry.Recipe != null)
            {
                OnCraft(entry.Recipe);
                return;
            }
            if (entry == null || entry.Combo == null)
            {
                return;
            }
            if (MetaProgressManager.Instance.TryUpgradeCombo(entry.Combo.Key))
            {
                SetText(_feedbackLabel, $"{NameOf(entry)} is now Lv {LevelOf(entry)}.");
                int keep = _index;
                Refresh();
                Select(keep);
                SetText(_feedbackLabel, $"{NameOf(entry)} is now Lv {LevelOf(entry)}.");
            }
        }

        private void OnCraft(ItemSO recipe)
        {
            if (!InventoryManager.Instance.TryCraft(recipe, out var made))
            {
                return;
            }
            int keep = _index;
            Refresh();
            Select(keep);
            SetText(_feedbackLabel, $"Forged a {made.DisplayName}. It is in the Storehouse.");
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        private string UndiscoveredCaption()
        {
            switch (_tab)
            {
                case Tab.Craft:
                    return "Found on monsters and on cleared floors";
                case Tab.Magic:
                    return "Learned on a hero's sphere grid";
                default:
                    return "Found by triggering them in combat";
            }
        }

        private string EmptyText()
        {
            switch (_tab)
            {
                case Tab.Craft:
                    return "No recipes known yet. Monsters and cleared floors give them up.";
                case Tab.Magic:
                    return "No abilities learned yet. Learn one on a hero's sphere grid.";
                default:
                    return "No combos found yet. Chain the right abilities in combat.";
            }
        }

        private static string NameOf(Entry entry)
        {
            if (entry.Recipe != null)
            {
                return CraftingOps.ResultName(entry.Recipe);
            }
            return entry.Magic != null ? entry.Magic.DisplayName : entry.Combo.ComboName;
        }

        // The three below are combo-only: an ability has no level, price or upgrade.

        private static int LevelOf(Entry entry)
        {
            return entry.Combo != null ? MetaProgressManager.Instance.GetComboUpgradeLevel(entry.Combo.Key) : 0;
        }

        private static int CostOf(Entry entry)
        {
            return entry.Combo != null ? MetaProgressManager.Instance.GetComboUpgradeCost(entry.Combo.Key) : 0;
        }

        private static bool CanUpgrade(Entry entry)
        {
            return entry.Combo != null && MetaProgressManager.Instance.CanUpgradeCombo(entry.Combo.Key);
        }

        /// <summary>The row's second line: a recipe's readiness, a combo's level, an ability's targeting.</summary>
        private static string CaptionOf(Entry entry)
        {
            if (entry.Recipe != null)
            {
                switch (CraftBlockOf(entry.Recipe))
                {
                    case CraftBlock.None:
                        return "Ready to forge";
                    case CraftBlock.NoBase:
                        return $"Needs a {entry.Recipe.CraftBase.DisplayName}";
                    case CraftBlock.Gold:
                        return "Short of gold";
                    default:
                        return "Short of materials";
                }
            }
            return entry.Magic != null
                ? AbilityDescriber.TargetLabel(entry.Magic)
                : LevelCaption(LevelOf(entry));
        }

        private static string LevelCaption(int level)
        {
            int ceiling = MetaProgressManager.Instance.ComboUpgradeCeiling;
            if (level >= MetaProgressManager.MaxComboUpgradeLevel)
            {
                return $"Lv {level} · fully upgraded";
            }
            return $"Lv {level} of {ceiling}";
        }

        private static string PriceTag(Entry entry)
        {
            int level = LevelOf(entry);
            if (level >= MetaProgressManager.MaxComboUpgradeLevel)
            {
                return "MAX";
            }
            if (level >= MetaProgressManager.Instance.ComboUpgradeCeiling)
            {
                return "Forge";
            }
            return $"{CostOf(entry)} ess";
        }

        private void UpdateEssence()
        {
            if (_tab == Tab.Craft)
            {
                SetText(_essenceLabel, $"{MetaProgressManager.Instance.Gold} gold");
                return;
            }
            int ceiling = MetaProgressManager.Instance.ComboUpgradeCeiling;
            SetText(_essenceLabel, ceiling < MetaProgressManager.MaxComboUpgradeLevel
                ? $"{MetaProgressManager.Instance.Essence} essence   ·   Forge allows Lv {ceiling}"
                : $"{MetaProgressManager.Instance.Essence} essence");
        }

        private void SetIcon(Sprite icon)
        {
            _detailIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : StyleKeyword.None;
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
    }
}
