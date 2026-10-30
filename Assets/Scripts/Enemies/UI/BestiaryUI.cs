using System;
using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.Items.UI;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Enemies.UI
{
    /// <summary>
    /// Hub "Bestiary" (UI Toolkit view-controller, not a MonoBehaviour - same shape as
    /// <c>MagicForgeUI</c> and <c>MerchantUI</c>, constructed by <c>MainMenuManager</c> from the
    /// <c>bestiary-view</c> subtree).
    ///
    /// <para>It is the permanent home of everything the in-combat Inspect page shows, plus the
    /// things only a collection screen can: how much of the roster has been met, and how many of
    /// each the party has killed. Every enemy in <see cref="EnemyCatalogSO"/> is listed even when
    /// unmet - a collection with invisible gaps is not a collection - but an unmet row shows no name
    /// and no icon.</para>
    ///
    /// <para>All wording and colour come from <see cref="BestiaryPresenter"/> and
    /// <see cref="BestiaryLineView"/>, which the combat page uses too, so the two can never
    /// disagree about what a 120% fire resistance is called.</para>
    ///
    /// <para><b>The Hall of Knowledge since 2026-10-29</b> (owner): the lot was renamed and the screen
    /// grew a <b>Recipes</b> tab (Left/Right switch tabs). Every recipe is shown in its tree under the
    /// item it is made from (<see cref="RecipeTreeOps"/>); one not yet found is a selectable "???" row
    /// in its true place - the owner's choice, so a hint can be added to that row later. The code keeps
    /// the Bestiary's names (<c>HubService.Bestiary</c>, the <c>bestiary</c> save key).</para>
    /// </summary>
    public class BestiaryUI
    {
        private static string UnknownName
        {
            get { return Loc.Get("bestiary.unknown-name"); }
        }

        private enum Tab { Monsters, Recipes }

        private readonly VisualElement _root;
        private readonly Label _progress;
        private readonly ScrollView _list;
        private readonly ScrollView _detail;
        private readonly Button _closeButton;
        private readonly VisualElement _portrait;
        private readonly Label _name;
        private readonly Label _sub;
        private readonly Label _listTitle;
        private readonly Button _tabMonsters;
        private readonly Button _tabRecipes;
        private Tab _tab = Tab.Monsters;

        // The Recipes tab's rows, parallel to _rows while that tab is up.
        private readonly List<RecipeTreeNode> _recipeRows = new List<RecipeTreeNode>();

        // Only the met enemies are rows the cursor walks; _rowEntries maps a row to its catalog index.
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<int> _rowEntries = new List<int>();
        private List<EnemySO> _catalog = new List<EnemySO>();
        private int _selected = -1;
        private bool _isShown;

        public event Action OnClosed;

        public BestiaryUI(VisualElement root)
        {
            _root = root;
            _progress = root.Q<Label>("bestiary-progress");
            _list = root.Q<ScrollView>("bestiary-list");
            _detail = root.Q<ScrollView>("bestiary-detail");
            _closeButton = root.Q<Button>("bestiary-close");
            _portrait = root.Q<VisualElement>("bestiary-portrait");
            _name = root.Q<Label>("bestiary-name");
            _sub = root.Q<Label>("bestiary-sub");
            _listTitle = root.Q<Label>("bestiary-list-title");
            _tabMonsters = root.Q<Button>("bestiary-tab-monsters");
            _tabRecipes = root.Q<Button>("bestiary-tab-recipes");
            foreach (var tab in new[] { _tabMonsters, _tabRecipes })
            {
                if (tab != null)
                {
                    tab.focusable = false;
                }
            }
            if (_tabMonsters != null)
            {
                _tabMonsters.clicked += () => SetTab(Tab.Monsters);
            }
            if (_tabRecipes != null)
            {
                _tabRecipes.clicked += () => SetTab(Tab.Recipes);
            }

            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
                _closeButton.focusable = false;
            }
            if (_list != null)
            {
                _list.focusable = false;
            }

            // Arrow keys walk the list, like every other screen. The cursor is this screen's own
            // rather than the shared KeyboardNavigator's because the rows are not Buttons and because
            // moving the cursor here has to re-render the detail column beside it, not just highlight.
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<NavigationMoveEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationSubmitEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationCancelEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _isShown = true;
            _root.style.display = DisplayStyle.Flex;
            _catalog = LoadCatalog();
            SetTab(Tab.Monsters);

            _root.focusable = true;
            if (_root.panel != null)
            {
                _root.Focus();
            }
        }

        public void Hide()
        {
            _isShown = false;
            _root.focusable = false; // stop being a focus/nav target once closed
            _root.style.display = DisplayStyle.None;
            _list?.Clear();
            _detail?.Clear();
            _rows.Clear();
            _rowEntries.Clear();
            _recipeRows.Clear();
            OnClosed?.Invoke();
        }

        /// <summary>Shows one tab and opens it on its first row.</summary>
        private void SetTab(Tab tab)
        {
            _tab = tab;
            _tabMonsters?.EnableInClassList("cd-inv-tab--active", tab == Tab.Monsters);
            _tabRecipes?.EnableInClassList("cd-inv-tab--active", tab == Tab.Recipes);
            if (_listTitle != null)
            {
                _listTitle.text = tab == Tab.Monsters ? Loc.Get("bestiary.list.enemies-met") : Loc.Get("bestiary.list.recipe-trees");
            }
            _selected = -1;
            if (tab == Tab.Monsters)
            {
                RefreshList();
            }
            else
            {
                RefreshRecipes();
            }
            // Open on the first entry rather than a "Select an enemy." page beside a live list.
            if (_rows.Count > 0)
            {
                Select(0);
                _list?.ScrollTo(_rows[0]);
            }
            else
            {
                ShowEmptyDetail();
            }
        }

        // ============================================================
        //  KEYBOARD
        // ============================================================

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_isShown)
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveSelection(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                    SetTab(_tab == Tab.Monsters ? Tab.Recipes : Tab.Monsters);
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                case KeyCode.Backspace:
                case KeyCode.X:
                    Hide();
                    evt.StopPropagation();
                    break;
            }
        }

        /// <summary>
        /// Moves the cursor and reads the entry in one step - there is nothing to confirm on this
        /// screen, so making Enter a second, separate press would only add a key that does nothing.
        /// </summary>
        private void MoveSelection(int delta)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            int index = _selected < 0
                ? (delta > 0 ? 0 : _rows.Count - 1)
                : (_selected + delta + _rows.Count) % _rows.Count;

            Select(index);
            _list?.ScrollTo(_rows[index]);
        }

        /// <summary>
        /// The catalog from Resources. Missing asset degrades to an empty screen with a line saying
        /// so, rather than a null reference on a hub screen the player can always open.
        /// </summary>
        private static List<EnemySO> LoadCatalog()
        {
            var catalog = EnemyCatalogSO.Load();
            if (catalog == null || catalog.Enemies == null)
            {
                Debug.LogWarning(
                    "EnemyCatalog not found at Resources/EnemyCatalog. The bestiary will be empty.");
                return new List<EnemySO>();
            }

            var result = new List<EnemySO>(catalog.Enemies.Count);
            foreach (var definition in catalog.Enemies)
            {
                if (definition != null)
                {
                    result.Add(definition);
                }
            }
            return result;
        }

        // ============================================================
        //  LIST
        // ============================================================

        /// <summary>
        /// Met enemies as full rows the cursor walks, then every enemy not yet met as a compact
        /// "? ? ?" row at the bottom - the collection still shows its gaps (a collection with invisible
        /// gaps is not a collection) without the unknowns being four fifths of the list.
        /// </summary>
        private void RefreshList()
        {
            _list.Clear();
            _rows.Clear();
            _rowEntries.Clear();

            var knowledge = MetaProgressManager.Instance.GetBestiary();
            int seenCount = BestiaryPresenter.SeenCount(_catalog, knowledge);
            _progress.text = _catalog.Count == 0
                ? Loc.Get("bestiary.progress.no-catalog")
                : Loc.Format("bestiary.progress.discovered", ("seen", seenCount), ("total", _catalog.Count));
            // Read: the town stops flagging the Bestiary until someone new is met.
            MetaProgressManager.Instance.MarkBestiaryViewed(seenCount);

            int unknown = 0;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var definition = _catalog[i];
                var known = BestiaryOps.Find(knowledge, definition.SaveKey);
                if (known == null)
                {
                    unknown++;
                    continue;
                }

                int rowIndex = _rows.Count;
                var row = MakeRow(definition.Sprite, definition.Label,
                    known.Kills > 0 ? Loc.Format("bestiary.row.slain", ("kills", known.Kills)) : Loc.Get("bestiary.row.seen-not-slain"));
                row.RegisterCallback<ClickEvent>(_ => Select(rowIndex));
                _list.Add(row);
                _rows.Add(row);
                _rowEntries.Add(i);
            }

            for (int i = 0; i < unknown; i++)
            {
                var row = MakeRow(null, UnknownName, null);
                row.AddToClassList("cd-inv-row--static");
                row.AddToClassList("cd-bestiary-row--unknown");
                _list.Add(row);
            }

            RenderSelection();
        }

        private static VisualElement MakeRow(Sprite sprite, string name, string caption)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-inv-row");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("cd-inv-row__icon");
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            var nameLabel = new Label(name) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("cd-inv-row__name");
            text.Add(nameLabel);
            if (!string.IsNullOrEmpty(caption))
            {
                var captionLabel = new Label(caption) { pickingMode = PickingMode.Ignore };
                captionLabel.AddToClassList("cd-inv-row__caption");
                text.Add(captionLabel);
            }
            row.Add(text);
            return row;
        }

        private void Select(int rowIndex)
        {
            _selected = rowIndex;
            RenderSelection();
            if (_tab == Tab.Recipes)
            {
                ShowRecipeDetail(_recipeRows[rowIndex]);
                return;
            }
            ShowDetail(_catalog[_rowEntries[rowIndex]]);
        }

        private void RenderSelection()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].EnableInClassList("cd-inv-row--selected", i == _selected);
            }
        }

        // ============================================================
        //  DETAIL
        // ============================================================

        private void ShowEmptyDetail()
        {
            _detail.Clear();
            if (_tab == Tab.Recipes)
            {
                SetHead(null, Loc.Get("bestiary.empty.recipes.title"), Loc.Get("bestiary.empty.recipes.sub"));
                return;
            }
            SetHead(null, Loc.Get("bestiary.empty.enemies.title"), Loc.Get("bestiary.empty.enemies.sub"));
        }

        private void SetHead(Sprite portrait, string name, string sub)
        {
            if (_portrait != null)
            {
                _portrait.style.backgroundImage = portrait != null ? new StyleBackground(portrait) : StyleKeyword.None;
            }
            if (_name != null)
            {
                _name.text = name;
            }
            if (_sub != null)
            {
                _sub.text = sub;
            }
            // A recipe's page colours the name by rarity; every other page starts plain.
            InventoryHubUI.ApplyRarity(_name, null);
        }

        /// <summary>
        /// One enemy's page: portrait, name and health on top, then two columns - what hurts it
        /// (resistances, immunities) and what it is (stats, abilities) - with kills and drops under
        /// them, so a normal entry fits without scrolling.
        /// </summary>
        private void ShowDetail(EnemySO definition)
        {
            _detail.Clear();
            _detail.scrollOffset = Vector2.zero;
            var known = MetaProgressManager.Instance.GetBestiaryEntry(definition.SaveKey);
            if (known == null)
            {
                SetHead(null, UnknownName, Loc.Get("bestiary.detail.not-encountered"));
                return;
            }

            // Health comes from the definition's base stats here, not from a live unit: the hub has
            // no fight in progress, and a level's enemy tuning scales this per floor anyway.
            SetHead(definition.Sprite, definition.Label,
                Loc.Format("bestiary.detail.health-slain", ("health", definition.BaseStats[UnitStats.StatType.MaxHealth]), ("kills", known.Kills)));

            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.AttackLine(definition, known)));

            var page = new VisualElement();
            page.AddToClassList("cd-bestiary__page");
            var left = new VisualElement();
            left.AddToClassList("cd-bestiary__page-col");
            var right = new VisualElement();
            right.AddToClassList("cd-bestiary__page-col");
            page.Add(left);
            page.Add(right);
            _detail.Add(page);

            BestiaryLineView.AddSection(left, Loc.Get("bestiary.section.resistances"), BestiaryPresenter.ResistanceLines(definition, known));
            BestiaryLineView.AddSection(left, Loc.Get("bestiary.section.immune-to"), BestiaryPresenter.ImmunityLines(definition, known));
            BestiaryLineView.AddSection(right, Loc.Get("bestiary.section.base-stats"), BestiaryPresenter.StatLines(definition, known));
            BestiaryLineView.AddSection(right, Loc.Get("bestiary.section.abilities"), BestiaryPresenter.CompactSpellLines(definition, known));

            // One row, the same as the combat Inspect page: the names seen, then how many are left.
            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.LootSummary(definition, known)));
        }
        // ============================================================
        //  RECIPES
        // ============================================================

        /// <summary>
        /// Every recipe in its tree, one row each, indented under what it is made from. Known recipes and
        /// base items show their name and icon; a recipe not yet found is "???" in its place and is
        /// still a row the cursor stops on, because that is where its hint will go.
        /// </summary>
        private void RefreshRecipes()
        {
            _list.Clear();
            _rows.Clear();
            _rowEntries.Clear();
            _recipeRows.Clear();

            var inventory = InventoryManager.Instance;
            var all = CraftingOps.Recipes(inventory.AllItems());
            int known = 0;
            foreach (var recipe in all)
            {
                if (inventory.KnowsRecipe(recipe))
                {
                    known++;
                }
            }
            _progress.text = Loc.Format("bestiary.progress.recipes-known", ("known", known), ("total", all.Count));

            foreach (var node in RecipeTreeOps.Flatten(RecipeTreeOps.Build(all, inventory.KnowsRecipe)))
            {
                int rowIndex = _rows.Count;
                var row = node.Known
                    ? MakeRow(node.Item.Icon, NameOf(node.Item), RecipeCaption(node))
                    : MakeRow(null, UnknownName, Loc.Get("bestiary.recipe.not-found"));
                if (!node.Known)
                {
                    row.AddToClassList("cd-recipe-row--unknown");
                }
                // Indent by depth so a line of descent reads down the list.
                row.style.marginLeft = node.Depth * 28;
                row.RegisterCallback<ClickEvent>(_ => Select(rowIndex));
                _list.Add(row);
                _rows.Add(row);
                _recipeRows.Add(node);
            }

            RenderSelection();
        }

        private static string RecipeCaption(RecipeTreeNode node)
        {
            if (!node.IsRecipe)
            {
                return Loc.Get("bestiary.recipe.caption.found-or-bought");
            }
            return node.Recipe.KnownWithForge ? Loc.Get("bestiary.recipe.caption.taught") : Loc.Get("bestiary.recipe.caption.found");
        }

        /// <summary>
        /// One row's page. A known recipe: what goes in and what it makes. A base item: what it is and
        /// what it can become. A recipe not yet found: only where it sits - this is the page a hint
        /// will be added to.
        /// </summary>
        private void ShowRecipeDetail(RecipeTreeNode node)
        {
            _detail.Clear();
            _detail.scrollOffset = Vector2.zero;

            if (!node.Known)
            {
                SetHead(null, UnknownName, Loc.Get("bestiary.recipe.not-found"));
                if (node.Recipe.CraftBase != null)
                {
                    _detail.Add(DetailLine(Loc.Format("bestiary.recipe.made-from", ("item", NameOf(node.Recipe.CraftBase))), "cd-inv-detail__line"));
                }
                _detail.Add(DetailLine(Loc.Get("bestiary.recipe.where-found"), "cd-inv-detail__note"));
                AddLeadsTo(node);
                return;
            }

            var item = node.Item;
            string kind = item.Category == ItemCategory.Equipment
                ? Loc.Format("bestiary.recipe.kind", ("rarity", ItemPresenter.RarityLabel(item.Rarity)), ("slot", ItemPresenter.SlotLabel(item.SlotType)))
                : ItemPresenter.RarityLabel(item.Rarity);
            SetHead(item.Icon, NameOf(item), kind);
            InventoryHubUI.ApplyRarity(_name, item);

            if (node.IsRecipe)
            {
                _detail.Add(BestiaryLineView.Section(node.Recipe.KnownWithForge ? Loc.Get("bestiary.recipe.section.taught") : Loc.Get("bestiary.recipe.section")));
                if (node.Recipe.CraftBase != null)
                {
                    _detail.Add(DetailLine(Loc.Format("bestiary.recipe.base-line", ("item", NameOf(node.Recipe.CraftBase))), "cd-inv-detail__line"));
                }
                foreach (var line in node.Recipe.CraftMaterials)
                {
                    if (line != null && line.IsValid)
                    {
                        _detail.Add(DetailLine(Loc.Format("bestiary.recipe.material-line", ("item", NameOf(line.Material)), ("amount", Mathf.Max(1, line.Amount))), "cd-inv-detail__line"));
                    }
                }
                if (node.Recipe.CraftGold > 0)
                {
                    _detail.Add(DetailLine(Loc.Format("bestiary.recipe.gold-line", ("gold", node.Recipe.CraftGold)), "cd-inv-detail__line"));
                }
                _detail.Add(DetailLine(Loc.Get("bestiary.recipe.made-at-forge"), "cd-inv-detail__note"));
            }
            else
            {
                _detail.Add(DetailLine(Loc.Get("bestiary.recipe.not-made-at-forge"), "cd-inv-detail__note"));
            }

            AddLeadsTo(node);
            InventoryHubUI.AddItemLines(_detail, item);
        }

        /// <summary>What this row's item can become, unknown recipes as "???".</summary>
        private void AddLeadsTo(RecipeTreeNode node)
        {
            if (node.Children.Count == 0)
            {
                return;
            }
            _detail.Add(BestiaryLineView.Section(Loc.Get("bestiary.recipe.section.can-become")));
            foreach (var child in node.Children)
            {
                _detail.Add(DetailLine(child.Known ? NameOf(child.Item) : UnknownName, "cd-inv-detail__line"));
            }
        }

        private static Label DetailLine(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private static string NameOf(ItemSO item)
        {
            return item == null ? string.Empty : string.IsNullOrEmpty(item.DisplayName) ? item.Key : item.DisplayName;
        }
    }
}
