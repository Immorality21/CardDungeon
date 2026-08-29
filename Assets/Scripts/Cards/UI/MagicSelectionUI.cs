using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// In-combat selection UI for the Draw/Magic system, built on UI Toolkit.
    /// Three compact windows: a "list" window (equipped magic slots for casting, or an
    /// enemy's draw list, or slot placement), a "target" window (pick a combat unit), and the
    /// "inspect" page - everything the party has learned about one enemy.
    /// Driven entirely by CombatManager events. Rows are built as VisualElements.
    ///
    /// Inspect is the odd one out: every other flow here ends in a Submit that spends the hero's
    /// turn, while Inspect submits nothing and hands the turn straight back.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MagicSelectionUI : MonoBehaviour
    {
        private enum SelectionMode
        {
            Idle,
            Cast,
            AttackTarget,
            DrawTarget,
            DrawChoice,
            DrawPlacement,
            ItemChoice,
            ItemTarget,
            InspectTarget,
            InspectDetail
        }

        [SerializeField] private UIDocument _document;

        // Cached visual elements (queried from the UIDocument on first use).
        private VisualElement _root;
        private VisualElement _listPanel;
        private VisualElement _targetPanel;
        private Label _listTitle;
        private Label _targetPrompt;
        private ScrollView _listScroll;
        private ScrollView _targetScroll;
        private Button _listBack;
        private Button _targetBack;
        private VisualElement _inspectPanel;
        private VisualElement _inspectPortrait;
        private Label _inspectName;
        private Label _inspectHealth;
        private ScrollView _inspectBody;
        private Button _inspectClose;
        private bool _refsReady;

        private ICombatUnit _currentHero;
        private SelectionMode _mode = SelectionMode.Idle;

        private int _selectedSlotIndex;
        private MagicSO _selectedMagic;
        private Enemy _drawSource;
        private MagicSO _drawMagic;
        private int _drawCharges;
        private ItemSO _selectedItem;

        // Enemies offered to the last Inspect, so closing a page can step back to the picker rather
        // than all the way out - comparing two enemies is the main reason to open it twice.
        private List<ICombatUnit> _inspectTargets;

        // Cursor-driven keyboard navigation over the currently shown selectable rows.
        private readonly List<Button> _navRows = new List<Button>();
        private readonly List<Label> _navCursors = new List<Label>();
        private readonly List<Action> _navActions = new List<Action>();
        private int _navSelected = -1;

        private void OnEnable()
        {
            EnsureRefs();
            CombatManager.Instance.OnMagicSlotsRequested += ShowSlotListForCast;
            CombatManager.Instance.OnAttackTargetRequested += ShowAttackTargets;
            CombatManager.Instance.OnDrawTargetRequested += ShowDrawTargets;
            CombatManager.Instance.OnItemListRequested += ShowItemList;
            CombatManager.Instance.OnInspectTargetRequested += ShowInspectTargets;
            CombatManager.Instance.OnHeroTurnStarted += OnHeroTurnStarted;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }

        private void OnDisable()
        {
            if (CombatManager.HasInstance)
            {
                CombatManager.Instance.OnMagicSlotsRequested -= ShowSlotListForCast;
                CombatManager.Instance.OnAttackTargetRequested -= ShowAttackTargets;
                CombatManager.Instance.OnDrawTargetRequested -= ShowDrawTargets;
                CombatManager.Instance.OnItemListRequested -= ShowItemList;
                CombatManager.Instance.OnInspectTargetRequested -= ShowInspectTargets;
                CombatManager.Instance.OnHeroTurnStarted -= OnHeroTurnStarted;
                CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            }
        }

        /// <summary>
        /// Resolves and caches visual elements. UIDocument builds its tree in its own
        /// OnEnable, whose order relative to this component isn't guaranteed, so we query
        /// lazily and tolerate a not-yet-ready document.
        /// </summary>
        private bool EnsureRefs()
        {
            if (_refsReady)
            {
                return true;
            }

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                return false;
            }
            _root = root;
            // Only focusable while a picker is open (set in BeginNavigation) — otherwise this
            // idle panel root would steal keyboard focus from the command menu's arrow nav.
            _root.focusable = false;
            // Own the arrow/submit/cancel input so keyboard focus stays on the root instead of
            // UI Toolkit's default focus navigation stealing it (which killed arrows after one press).
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<NavigationMoveEvent>(OnNavMove);
            _root.RegisterCallback<NavigationSubmitEvent>(OnNavSubmit);
            _root.RegisterCallback<NavigationCancelEvent>(OnNavCancel);

            _listPanel = root.Q<VisualElement>("magic-list-panel");
            _targetPanel = root.Q<VisualElement>("target-panel");
            _listTitle = root.Q<Label>("list-title");
            _targetPrompt = root.Q<Label>("target-prompt");
            _listScroll = root.Q<ScrollView>("list-scroll");
            _targetScroll = root.Q<ScrollView>("target-scroll");
            _listBack = root.Q<Button>("list-back");
            _targetBack = root.Q<Button>("target-back");

            _inspectPanel = root.Q<VisualElement>("inspect-panel");
            _inspectPortrait = root.Q<VisualElement>("inspect-portrait");
            _inspectName = root.Q<Label>("inspect-name");
            _inspectHealth = root.Q<Label>("inspect-health");
            _inspectBody = root.Q<ScrollView>("inspect-body");
            _inspectClose = root.Q<Button>("inspect-close");
            if (_inspectBody != null)
            {
                _inspectBody.focusable = false;
            }
            if (_inspectClose != null)
            {
                _inspectClose.focusable = false;
                _inspectClose.clicked += OnInspectBack;
            }

            // ScrollViews are focusable by default and would grab focus on the first arrow key.
            if (_listScroll != null)
            {
                _listScroll.focusable = false;
            }
            if (_targetScroll != null)
            {
                _targetScroll.focusable = false;
            }

            // Keep keyboard focus on the panel root (not individual buttons) so our cursor nav
            // owns the arrow keys instead of UI Toolkit's default focus navigation.
            if (_listBack != null)
            {
                _listBack.focusable = false;
                _listBack.clicked += OnListBack;
            }
            if (_targetBack != null)
            {
                _targetBack.focusable = false;
                _targetBack.clicked += OnTargetBack;
            }

            HidePanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);

            _refsReady = _listPanel != null && _targetPanel != null;
            return _refsReady;
        }

        // ============================================================
        //  CAST FLOW
        // ============================================================

        private void ShowSlotListForCast(ICombatUnit hero, List<MagicSlot> slots)
        {
            if (!EnsureRefs())
            {
                return;
            }

            _currentHero = hero;
            _mode = SelectionMode.Cast;
            _selectedMagic = null;
            _listTitle.text = "Magic";
            PopulateSlotRows(slots);
        }

        private void PopulateSlotRows(List<MagicSlot> slots)
        {
            _listScroll.Clear();
            ClearNav();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool selectable = _mode == SelectionMode.DrawPlacement || slot.CanCast;

                string name = slot.IsEmpty ? "(empty)" : slot.Magic.DisplayName;
                string meta = slot.IsEmpty ? "" : $"{slot.Charges}/{slot.MaxCharges}";
                Sprite icon = slot.IsEmpty ? null : slot.Magic.Icon;

                // A spell with a health cost shows its price and is refused when the caster cannot
                // pay it and stay standing — the same treatment a slot with no charges gets. The cost
                // is deterministic, so the number shown here is the number that will be charged.
                if (_mode == SelectionMode.Cast && !slot.IsEmpty)
                {
                    int upgradeLevel = MagicUpgradeLevelOf(slot.Magic);
                    int healthCost = SpellPower.TotalHealthCost(slot.Magic, _currentHero, upgradeLevel);
                    if (healthCost > 0)
                    {
                        meta = $"{meta}  {healthCost} HP";
                        if (!SpellPower.CanAfford(slot.Magic, _currentHero, upgradeLevel))
                        {
                            selectable = false;
                        }
                    }
                }

                int captured = i;
                var slotRef = slot;
                _listScroll.Add(CreateRow(icon, name, meta, selectable, () => OnSlotSelected(captured, slotRef)));
            }

            ShowPanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            BeginNavigation();
        }

        private void OnSlotSelected(int slotIndex, MagicSlot slot)
        {
            if (_mode == SelectionMode.DrawPlacement)
            {
                SubmitDraw(slotIndex);
                return;
            }

            _selectedSlotIndex = slotIndex;
            _selectedMagic = slot.Magic;

            switch (slot.Magic.TargetType)
            {
                case MagicTargetType.Self:
                    SubmitCast(new List<ICombatUnit> { _currentHero });
                    return;
                case MagicTargetType.AllEnemies:
                    SubmitCast(CombatManager.Instance.GetAliveEnemies());
                    return;
                case MagicTargetType.AllAllies:
                    SubmitCast(CombatManager.Instance.GetAliveHeroes(GameManager.Instance.Party));
                    return;
                default:
                    ShowCastTargetSelection(slot.Magic.TargetType);
                    return;
            }
        }

        private void ShowCastTargetSelection(MagicTargetType targetType)
        {
            List<ICombatUnit> targets;
            string prompt;
            if (targetType == MagicTargetType.SingleEnemy)
            {
                targets = CombatManager.Instance.GetAliveEnemies();
                prompt = "Select Enemy Target";
            }
            else
            {
                targets = CombatManager.Instance.GetAliveHeroes(GameManager.Instance.Party);
                prompt = "Select Ally Target";
            }

            // Only one valid target — cast straight at it, no picker.
            if (targets.Count == 1)
            {
                SubmitCast(new List<ICombatUnit> { targets[0] });
                return;
            }
            PopulateTargetRows(targets, prompt);
        }

        private void SubmitCast(List<ICombatUnit> targets)
        {
            _mode = SelectionMode.Idle;
            HidePanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            ReleaseFocus();
            CombatManager.Instance.SubmitCastAction(_selectedMagic, _selectedSlotIndex, _currentHero, targets);
        }

        // ============================================================
        //  ATTACK TARGETING
        // ============================================================

        private void ShowAttackTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            if (!EnsureRefs())
            {
                return;
            }

            _currentHero = hero;
            _mode = SelectionMode.AttackTarget;
            PopulateTargetRows(enemies, "Select Attack Target");
        }

        // ============================================================
        //  DRAW FLOW
        // ============================================================

        private void ShowDrawTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            if (!EnsureRefs())
            {
                return;
            }

            _currentHero = hero;
            _mode = SelectionMode.DrawTarget;

            // Only one enemy to draw from — skip the target picker.
            if (enemies.Count == 1)
            {
                OnDrawSourceSelected(enemies[0]);
                return;
            }
            PopulateTargetRows(enemies, "Draw Magic From");
        }

        private void OnDrawSourceSelected(ICombatUnit source)
        {
            _drawSource = source as Enemy;

            var hero = _currentHero as Hero;
            if (_drawSource == null || hero == null || _drawSource.DrawableMagics == null ||
                _drawSource.DrawableMagics.Count == 0 || !DungeonManager.HasInstance || DungeonManager.Instance.MagicState == null)
            {
                ReturnToActions();
                return;
            }

            // A single-magic enemy skips the choice step.
            if (_drawSource.DrawableMagics.Count == 1)
            {
                SelectDrawMagic(_drawSource.DrawableMagics[0]);
                return;
            }

            _mode = SelectionMode.DrawChoice;
            _listTitle.text = $"Draw from {_drawSource.DisplayName}";
            PopulateDrawChoiceRows(_drawSource.DrawableMagics);
        }

        private void PopulateDrawChoiceRows(List<DrawableMagicEntry> entries)
        {
            _listScroll.Clear();
            ClearNav();

            foreach (var entry in entries)
            {
                bool valid = entry != null && entry.Magic != null;

                // Magic the player has never drawn anywhere is offered unnamed and unillustrated:
                // drawing IS the discovery, so the first pull off a new enemy is a small blind
                // gamble on a turn. The charge count still shows - what the draw is worth is the
                // decision being made, and hiding that would make the row unreadable rather than
                // mysterious. The knowledge pages mask exactly the same entries; if this picker
                // named them, that gate would be walked around by opening Draw and backing out.
                bool known = valid && BestiaryPresenter.IsDrawKnown(
                    entry.Magic.Key, MetaProgressManager.Instance.IsMagicDiscovered);

                string name = !valid ? "(none)"
                    : known ? entry.Magic.DisplayName : BestiaryPresenter.Unknown;
                string meta = valid ? $"x{entry.Charges}" : "";
                Sprite icon = valid && known ? entry.Magic.Icon : null;

                var captured = entry;
                _listScroll.Add(CreateRow(icon, name, meta, valid, () => SelectDrawMagic(captured)));
            }

            ShowPanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            BeginNavigation();
        }

        private void SelectDrawMagic(DrawableMagicEntry entry)
        {
            _drawMagic = entry.Magic;
            _drawCharges = entry.Charges;

            var hero = _currentHero as Hero;
            if (hero == null || !DungeonManager.HasInstance || DungeonManager.Instance.MagicState == null)
            {
                ReturnToActions();
                return;
            }

            // Fill the first empty slot automatically; if the kit is full, let the player
            // pick which slot to overwrite.
            int emptySlot = DungeonManager.Instance.MagicState.FirstEmptySlot(hero.HeroKey);
            if (emptySlot >= 0)
            {
                SubmitDraw(emptySlot);
                return;
            }

            _mode = SelectionMode.DrawPlacement;
            _listTitle.text = "Replace which slot?";
            PopulateSlotRows(DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey));
        }

        private void SubmitDraw(int slotIndex)
        {
            _mode = SelectionMode.Idle;
            HidePanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            ReleaseFocus();
            CombatManager.Instance.SubmitDrawAction(_drawSource, _drawMagic, _drawCharges, slotIndex);
        }

        // ============================================================
        //  ITEM (CONSUMABLE) FLOW
        // ============================================================

        private void ShowItemList(ICombatUnit hero, List<ItemSaveData> consumables)
        {
            if (!EnsureRefs())
            {
                return;
            }

            _currentHero = hero;
            _selectedItem = null;
            _mode = SelectionMode.ItemChoice;
            _listTitle.text = "Item";
            PopulateItemRows(consumables);
        }

        private void PopulateItemRows(List<ItemSaveData> consumables)
        {
            _listScroll.Clear();
            ClearNav();

            foreach (var stack in consumables)
            {
                var so = InventoryManager.Instance.GetItemSO(stack.ItemKey);
                bool valid = so != null && stack.Quantity > 0;
                string name = valid ? so.DisplayName : "(none)";
                string meta = valid ? $"x{stack.Quantity}" : string.Empty;
                Sprite icon = valid ? so.Icon : null;

                var captured = so;
                _listScroll.Add(CreateRow(icon, name, meta, valid, () => OnItemSelected(captured)));
            }

            ShowPanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            BeginNavigation();
        }

        private void OnItemSelected(ItemSO item)
        {
            if (item == null)
            {
                ReturnToActions();
                return;
            }

            _selectedItem = item;

            // Consumables currently restore/aid allies — target a hero. Single ally auto-targets.
            var allies = CombatManager.Instance.GetAliveHeroes(GameManager.Instance.Party);
            if (allies.Count <= 1)
            {
                SubmitUseItem(allies.Count == 1 ? allies[0] : _currentHero);
                return;
            }

            _mode = SelectionMode.ItemTarget;
            PopulateTargetRows(allies, $"Use {item.DisplayName} on");
        }

        private void SubmitUseItem(ICombatUnit target)
        {
            _mode = SelectionMode.Idle;
            HidePanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            ReleaseFocus();
            CombatManager.Instance.SubmitUseItemAction(_selectedItem, target);
        }

        // ============================================================
        //  INSPECT ("Scan")
        // ============================================================

        /// <summary>
        /// Opens the Inspect target picker. Unlike every other command here this one submits
        /// nothing — the hero's turn is still theirs when the page closes, so it can be used to
        /// decide the turn rather than instead of it.
        /// </summary>
        private void ShowInspectTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            if (!EnsureRefs())
            {
                return;
            }

            _currentHero = hero;
            _mode = SelectionMode.InspectTarget;
            _inspectTargets = enemies;

            // Nothing to choose between — go straight to the page.
            if (enemies != null && enemies.Count == 1)
            {
                ShowInspectDetail(enemies[0]);
                return;
            }
            PopulateTargetRows(enemies, "Inspect Which?");
        }

        /// <summary>
        /// The knowledge page for one enemy. Live numbers (health, buffs, the telegraph) come from
        /// the unit standing there; everything the player had to *learn* — resistances, the element
        /// it attacks with, its loot — comes from the permanent bestiary record and reads "???"
        /// until it has been observed in the field.
        /// </summary>
        private void ShowInspectDetail(ICombatUnit target)
        {
            var enemy = target as Enemy;
            if (enemy == null || enemy.Definition == null)
            {
                ReturnToActions();
                return;
            }

            _mode = SelectionMode.InspectDetail;
            var definition = enemy.Definition;
            var known = MetaProgressManager.Instance.GetBestiaryEntry(definition.SaveKey);

            if (_inspectPortrait != null)
            {
                var icon = enemy.Icon;
                _inspectPortrait.style.backgroundImage =
                    icon != null ? new StyleBackground(icon) : new StyleBackground();
            }
            _inspectName.text = enemy.DisplayName;
            _inspectHealth.text =
                $"HP {enemy.Stats.Health} / {enemy.GetEffectiveStat(StatType.MaxHealth)}";

            _inspectBody.Clear();
            ClearNav();

            _inspectBody.Add(BestiaryLineView.Row(BestiaryPresenter.AttackLine(definition, known)));
            _inspectBody.Add(BestiaryLineView.Row(BestiaryPresenter.KillsLine(known)));
            _inspectBody.Add(BestiaryLineView.Row(BestiaryPresenter.LootLine(definition, known)));

            BestiaryLineView.AddSection(
                _inspectBody, "Resistances", BestiaryPresenter.ResistanceLines(definition, known));
            BestiaryLineView.AddSection(_inspectBody, "Stats", LiveStatLines(enemy));
            BestiaryLineView.AddSection(_inspectBody, "Condition", ConditionLines(enemy));
            BestiaryLineView.AddSection(
                _inspectBody,
                "Draw",
                BestiaryPresenter.DrawLines(
                    definition, MetaProgressManager.Instance.IsMagicDiscovered));

            HidePanel(_listPanel);
            HidePanel(_targetPanel);
            ShowPanel(_inspectPanel);

            // No rows to cursor through, but the panel still has to own the keyboard so Esc/Enter
            // dismiss the page instead of leaking to the command menu behind it.
            if (_root != null && _root.panel != null)
            {
                _root.focusable = true;
                _root.Focus();
            }
        }

        /// <summary>
        /// The enemy's stats as they stand in this fight — level tuning and buffs folded in — rather
        /// than the definition's base line the hub bestiary shows. The player is looking straight at
        /// the thing, so these are never gated.
        /// </summary>
        private List<BestiaryLine> LiveStatLines(Enemy enemy)
        {
            var lines = new List<BestiaryLine>();
            foreach (var stat in StatCatalog.Types)
            {
                if (stat == StatType.MaxHealth)
                {
                    continue;
                }

                int value = enemy.GetEffectiveStat(stat);
                if (!BestiaryPresenter.IsWorthShowing(stat, value))
                {
                    continue;
                }

                int buff = CombatManager.Instance.BuffTracker != null
                    ? CombatManager.Instance.BuffTracker.GetBuffAmount(enemy, stat)
                    : 0;
                string text = buff == 0 ? value.ToString() : $"{value} ({buff:+#;-#;0})";
                var tone = buff > 0 ? BestiaryTone.Bad : buff < 0 ? BestiaryTone.Good : BestiaryTone.Neutral;
                lines.Add(new BestiaryLine(StatCatalog.ShortName(stat), text, tone));
            }
            return lines;
        }

        /// <summary>Status effects riding on the enemy, and whether it is winding up a telegraphed hit.</summary>
        private List<BestiaryLine> ConditionLines(Enemy enemy)
        {
            var lines = new List<BestiaryLine>();

            if (enemy.IsCharging)
            {
                lines.Add(new BestiaryLine("Charging", "Heavy attack incoming", BestiaryTone.Bad));
            }

            var tracker = CombatManager.Instance.BuffTracker;
            if (tracker != null)
            {
                foreach (var statusEffect in tracker.GetActiveStatusEffects(enemy))
                {
                    // A status effect on an *enemy* is something the party put there, so it reads as
                    // in the player's favour.
                    lines.Add(new BestiaryLine(statusEffect.ToString(), "Active", BestiaryTone.Good));
                }
            }

            return lines;
        }

        /// <summary>
        /// Closing a page steps back to the picker when there was a choice to make (comparing two
        /// enemies is the point of opening it twice) and out to the command menu otherwise.
        /// </summary>
        private void OnInspectBack()
        {
            HidePanel(_inspectPanel);

            if (_inspectTargets != null && _inspectTargets.Count > 1)
            {
                _mode = SelectionMode.InspectTarget;
                PopulateTargetRows(_inspectTargets, "Inspect Which?");
                return;
            }

            ReturnToActions();
        }

        // ============================================================
        //  TARGET ROWS (shared)
        // ============================================================

        private void PopulateTargetRows(List<ICombatUnit> targets, string prompt)
        {
            _targetScroll.Clear();
            ClearNav();
            _targetPrompt.text = prompt;

            foreach (var target in targets)
            {
                var captured = target;
                string meta = $"HP {target.Stats.Health}";
                _targetScroll.Add(CreateRow(target.Icon, target.DisplayName, meta, true, () => OnTargetSelected(captured)));
            }

            HidePanel(_listPanel);
            HidePanel(_inspectPanel);
            ShowPanel(_targetPanel);
            BeginNavigation();
        }

        private void OnTargetSelected(ICombatUnit target)
        {
            switch (_mode)
            {
                case SelectionMode.AttackTarget:
                    _mode = SelectionMode.Idle;
                    HidePanel(_targetPanel);
                    ReleaseFocus();
                    CombatManager.Instance.SubmitAttackAction(target);
                    return;
                case SelectionMode.InspectTarget:
                    ShowInspectDetail(target);
                    return;
                case SelectionMode.DrawTarget:
                    OnDrawSourceSelected(target);
                    return;
                case SelectionMode.ItemTarget:
                    SubmitUseItem(target);
                    return;
                default:
                    SubmitCast(new List<ICombatUnit> { target });
                    return;
            }
        }

        // ============================================================
        //  BACK / RESET
        // ============================================================

        private void OnListBack()
        {
            ReturnToActions();
        }

        private void OnTargetBack()
        {
            HidePanel(_targetPanel);

            // Cast targeting steps back to the slot list; item targeting steps back to the item
            // list; everything else returns to actions.
            if (_mode == SelectionMode.Cast && _currentHero is Hero hero &&
                DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                _listTitle.text = "Magic";
                PopulateSlotRows(DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey));
                return;
            }

            if (_mode == SelectionMode.ItemTarget)
            {
                ShowItemList(_currentHero, InventoryManager.Instance.GetConsumables());
                return;
            }

            ReturnToActions();
        }

        private void ReturnToActions()
        {
            _mode = SelectionMode.Idle;
            HidePanel(_listPanel);
            HidePanel(_targetPanel);
            HidePanel(_inspectPanel);
            ReleaseFocus();

            var roomActionUI = FindAnyObjectByType<RoomActionUI>();
            if (roomActionUI != null)
            {
                roomActionUI.ReturnToHeroActions();
            }
        }

        /// <summary>
        /// Safeguard: a new hero turn must never inherit a selection window left open by the
        /// previous turn (e.g. an abandoned Draw/Cast pick). Force everything back to Idle so
        /// the RoomActionUI command bar is the only thing showing when a turn begins.
        /// </summary>
        private void OnHeroTurnStarted(ICombatUnit hero)
        {
            CloseAllPanels();
        }

        private void OnCombatEnded(CombatResult result)
        {
            CloseAllPanels();
        }

        private void CloseAllPanels()
        {
            _mode = SelectionMode.Idle;
            if (_refsReady)
            {
                HidePanel(_listPanel);
                HidePanel(_targetPanel);
                HidePanel(_inspectPanel);
            }
            ReleaseFocus();
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        /// <summary>
        /// A magic's upgrade level, which gates which of its effects are live and therefore what it
        /// costs. Uses <c>Instance</c> rather than <c>HasInstance</c> for the same reason
        /// <c>CombatManager.ExecuteCastAction</c> does: the manager may not exist yet mid-combat and
        /// the quoted price still has to match the one the resolver will apply.
        /// </summary>
        private static int MagicUpgradeLevelOf(MagicSO magic)
        {
            if (magic == null)
            {
                return 0;
            }
            return Progression.MetaProgressManager.Instance.GetMagicUpgradeLevel(magic.Key);
        }

        // Rows mirror the command menu: a ▸ cursor on the selected row, icon, dark name, meta.
        private Button CreateRow(Sprite icon, string name, string meta, bool enabled, Action onClick)
        {
            var row = new Button(onClick);
            row.text = string.Empty;
            row.focusable = false; // focus stays on the panel root; our cursor nav drives selection
            row.AddToClassList("cd-sel-row");
            row.SetEnabled(enabled);

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

            // Register selectable rows for cursor navigation; hovering moves the cursor too.
            if (enabled)
            {
                int idx = _navRows.Count;
                _navRows.Add(row);
                _navCursors.Add(cursor);
                _navActions.Add(onClick);
                row.RegisterCallback<MouseEnterEvent>(_ => SetNavSelected(idx));
            }

            return row;
        }

        // ============================================================
        //  CURSOR NAVIGATION (keyboard / controller)
        // ============================================================

        /// <summary>Reset the nav row set — call before (re)populating a list.</summary>
        private void ClearNav()
        {
            _navRows.Clear();
            _navCursors.Clear();
            _navActions.Clear();
            _navSelected = -1;
        }

        /// <summary>Select the first row, draw the cursor, and focus the panel for key input.</summary>
        private void BeginNavigation()
        {
            _navSelected = _navRows.Count > 0 ? 0 : -1;
            RenderNavCursor();
            if (_root != null && _root.panel != null)
            {
                _root.focusable = true;
                _root.Focus();
            }
        }

        /// <summary>Drop focusability when no picker is open so it stops being a nav target.</summary>
        private void ReleaseFocus()
        {
            if (_root != null)
            {
                _root.focusable = false;
            }
        }

        private void RenderNavCursor()
        {
            for (int i = 0; i < _navRows.Count; i++)
            {
                bool selected = i == _navSelected;
                _navCursors[i].text = selected ? "▸" : string.Empty;
                _navRows[i].EnableInClassList("cd-sel-row--selected", selected);
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

        private void BackNav()
        {
            if (IsShown(_inspectPanel))
            {
                OnInspectBack();
            }
            else if (IsShown(_targetPanel))
            {
                OnTargetBack();
            }
            else if (IsShown(_listPanel))
            {
                OnListBack();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_mode == SelectionMode.Idle)
            {
                return;
            }
            // The inspect page has nothing to cursor through - it is read and dismissed, so every
            // confirm/cancel key closes it.
            if (_mode == SelectionMode.InspectDetail)
            {
                switch (evt.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Space:
                    case KeyCode.Escape:
                    case KeyCode.Backspace:
                        OnInspectBack();
                        evt.StopPropagation();
                        break;
                }
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
                case KeyCode.Escape:
                case KeyCode.Backspace:
                    BackNav();
                    evt.StopPropagation();
                    break;
            }
        }

        // While a picker is open, swallow UI Toolkit's built-in navigation so it can't move
        // keyboard focus off the root (our OnKeyDown drives selection instead).
        private void OnNavMove(NavigationMoveEvent evt)
        {
            if (_mode != SelectionMode.Idle)
            {
                evt.StopPropagation();
            }
        }

        private void OnNavSubmit(NavigationSubmitEvent evt)
        {
            if (_mode != SelectionMode.Idle)
            {
                evt.StopPropagation();
            }
        }

        private void OnNavCancel(NavigationCancelEvent evt)
        {
            if (_mode != SelectionMode.Idle)
            {
                evt.StopPropagation();
            }
        }

        private static bool IsShown(VisualElement element)
        {
            return element != null && element.style.display.value == DisplayStyle.Flex;
        }

        private static void ShowPanel(VisualElement panel)
        {
            if (panel != null)
            {
                panel.style.display = DisplayStyle.Flex;
            }
        }

        private static void HidePanel(VisualElement panel)
        {
            if (panel != null)
            {
                panel.style.display = DisplayStyle.None;
            }
        }
    }
}
