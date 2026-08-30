using System;
using System.Collections.Generic;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

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
    /// </summary>
    public class BestiaryUI
    {
        private const string UnknownName = "? ? ?";

        private readonly VisualElement _root;
        private readonly Label _progress;
        private readonly ScrollView _list;
        private readonly ScrollView _detail;
        private readonly Button _closeButton;

        private readonly List<VisualElement> _rows = new List<VisualElement>();
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
            _selected = -1;
            RefreshList();
            ShowEmptyDetail();

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
            OnClosed?.Invoke();
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
                case KeyCode.Escape:
                case KeyCode.Backspace:
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

        private void RefreshList()
        {
            _list.Clear();
            _rows.Clear();

            var knowledge = MetaProgressManager.Instance.GetBestiary();
            _progress.text = _catalog.Count == 0
                ? "No enemy catalog found."
                : $"{BestiaryPresenter.SeenCount(_catalog, knowledge)} of {_catalog.Count} discovered";

            for (int i = 0; i < _catalog.Count; i++)
            {
                var definition = _catalog[i];
                var known = BestiaryOps.Find(knowledge, definition.SaveKey);
                int index = i;

                var row = new VisualElement();
                row.AddToClassList("cd-bestiary-row");
                if (known == null)
                {
                    row.AddToClassList("cd-bestiary-row--locked");
                }

                var icon = new VisualElement();
                icon.AddToClassList("cd-bestiary-row__icon");
                icon.pickingMode = PickingMode.Ignore;
                if (known != null && definition.Sprite != null)
                {
                    icon.style.backgroundImage = new StyleBackground(definition.Sprite);
                }
                row.Add(icon);

                var name = new Label(known != null ? definition.Label : UnknownName);
                name.AddToClassList("cd-bestiary-row__name");
                name.pickingMode = PickingMode.Ignore;
                row.Add(name);

                var kills = new Label(known != null && known.Kills > 0 ? "x" + known.Kills : string.Empty);
                kills.AddToClassList("cd-bestiary-row__kills");
                kills.pickingMode = PickingMode.Ignore;
                row.Add(kills);

                row.RegisterCallback<ClickEvent>(_ => Select(index));

                _list.Add(row);
                _rows.Add(row);
            }

            RenderSelection();
        }

        private void Select(int index)
        {
            _selected = index;
            RenderSelection();
            ShowDetail(_catalog[index]);
        }

        private void RenderSelection()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].EnableInClassList("cd-bestiary-row--selected", i == _selected);
            }
        }

        // ============================================================
        //  DETAIL
        // ============================================================

        private void ShowEmptyDetail()
        {
            _detail.Clear();
            var hint = new Label("Select an enemy.");
            hint.AddToClassList("cd-info-label");
            _detail.Add(hint);
        }

        private void ShowDetail(EnemySO definition)
        {
            _detail.Clear();
            var known = MetaProgressManager.Instance.GetBestiaryEntry(definition.SaveKey);

            var title = new Label(known != null ? definition.Label : UnknownName);
            title.AddToClassList("cd-scan__name");
            _detail.Add(title);

            if (known == null)
            {
                var hint = new Label("Not yet encountered.");
                hint.AddToClassList("cd-info-label");
                _detail.Add(hint);
                return;
            }

            // Health comes from the definition's base stats here, not from a live unit: the hub has
            // no fight in progress, and a level's enemy tuning scales this per floor anyway.
            _detail.Add(BestiaryLineView.Row(new BestiaryLine(
                "Health",
                definition.BaseStats[UnitStats.StatType.MaxHealth].ToString(),
                BestiaryTone.Neutral)));
            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.AttackLine(definition, known)));
            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.KillsLine(known)));
            _detail.Add(BestiaryLineView.Row(BestiaryPresenter.LootLine(definition, known)));

            BestiaryLineView.AddSection(
                _detail, "Resistances", BestiaryPresenter.ResistanceLines(definition, known));
            BestiaryLineView.AddSection(
                _detail, "Base stats", BestiaryPresenter.StatLines(definition, known));
            BestiaryLineView.AddSection(
                _detail,
                "Draw",
                BestiaryPresenter.DrawLines(
                    definition, MetaProgressManager.Instance.IsMagicDiscovered));
        }
    }
}
