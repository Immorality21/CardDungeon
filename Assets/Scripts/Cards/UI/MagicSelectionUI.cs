using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// In-combat selection UI for the Draw/Magic system, built on UI Toolkit.
    /// Two compact windows: a "list" window (equipped magic slots for casting, or an
    /// enemy's draw list, or slot placement) and a "target" window (pick a combat unit).
    /// Driven entirely by CombatManager events. Rows are built as VisualElements.
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
            DrawPlacement
        }

        [SerializeField] private UIDocument _document;

        // Cached visual elements (queried from the UIDocument on first use).
        private VisualElement _listPanel;
        private VisualElement _targetPanel;
        private Label _listTitle;
        private Label _targetPrompt;
        private ScrollView _listScroll;
        private ScrollView _targetScroll;
        private Button _listBack;
        private Button _targetBack;
        private bool _refsReady;

        private ICombatUnit _currentHero;
        private SelectionMode _mode = SelectionMode.Idle;

        private int _selectedSlotIndex;
        private MagicSO _selectedMagic;
        private Enemy _drawSource;
        private MagicSO _drawMagic;
        private int _drawCharges;

        private void OnEnable()
        {
            EnsureRefs();
            CombatManager.Instance.OnMagicSlotsRequested += ShowSlotListForCast;
            CombatManager.Instance.OnAttackTargetRequested += ShowAttackTargets;
            CombatManager.Instance.OnDrawTargetRequested += ShowDrawTargets;
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

            _listPanel = root.Q<VisualElement>("magic-list-panel");
            _targetPanel = root.Q<VisualElement>("target-panel");
            _listTitle = root.Q<Label>("list-title");
            _targetPrompt = root.Q<Label>("target-prompt");
            _listScroll = root.Q<ScrollView>("list-scroll");
            _targetScroll = root.Q<ScrollView>("target-scroll");
            _listBack = root.Q<Button>("list-back");
            _targetBack = root.Q<Button>("target-back");

            if (_listBack != null)
            {
                _listBack.clicked += OnListBack;
            }
            if (_targetBack != null)
            {
                _targetBack.clicked += OnTargetBack;
            }

            HidePanel(_listPanel);
            HidePanel(_targetPanel);

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

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool selectable = _mode == SelectionMode.DrawPlacement || slot.CanCast;

                string name = slot.IsEmpty ? "(empty)" : slot.Magic.DisplayName;
                string meta = slot.IsEmpty ? "" : $"{slot.Charges}/{slot.MaxCharges}";
                Sprite icon = slot.IsEmpty ? null : slot.Magic.Icon;

                int captured = i;
                var slotRef = slot;
                _listScroll.Add(CreateRow(icon, name, meta, selectable, () => OnSlotSelected(captured, slotRef)));
            }

            ShowPanel(_listPanel);
            HidePanel(_targetPanel);
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

            PopulateTargetRows(targets, prompt);
        }

        private void SubmitCast(List<ICombatUnit> targets)
        {
            _mode = SelectionMode.Idle;
            HidePanel(_listPanel);
            HidePanel(_targetPanel);
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

            foreach (var entry in entries)
            {
                bool valid = entry != null && entry.Magic != null;
                string name = valid ? entry.Magic.DisplayName : "(none)";
                string meta = valid ? $"x{entry.Charges}" : "";
                Sprite icon = valid ? entry.Magic.Icon : null;

                var captured = entry;
                _listScroll.Add(CreateRow(icon, name, meta, valid, () => SelectDrawMagic(captured)));
            }

            ShowPanel(_listPanel);
            HidePanel(_targetPanel);
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
            CombatManager.Instance.SubmitDrawAction(_drawSource, _drawMagic, _drawCharges, slotIndex);
        }

        // ============================================================
        //  TARGET ROWS (shared)
        // ============================================================

        private void PopulateTargetRows(List<ICombatUnit> targets, string prompt)
        {
            _targetScroll.Clear();
            _targetPrompt.text = prompt;

            foreach (var target in targets)
            {
                var captured = target;
                string meta = $"HP {target.Stats.Health}";
                _targetScroll.Add(CreateRow(target.Icon, target.DisplayName, meta, true, () => OnTargetSelected(captured)));
            }

            HidePanel(_listPanel);
            ShowPanel(_targetPanel);
        }

        private void OnTargetSelected(ICombatUnit target)
        {
            switch (_mode)
            {
                case SelectionMode.AttackTarget:
                    _mode = SelectionMode.Idle;
                    HidePanel(_targetPanel);
                    CombatManager.Instance.SubmitAttackAction(target);
                    return;
                case SelectionMode.DrawTarget:
                    OnDrawSourceSelected(target);
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

            // Cast targeting steps back to the slot list; everything else returns to actions.
            if (_mode == SelectionMode.Cast && _currentHero is Hero hero &&
                DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                _listTitle.text = "Magic";
                PopulateSlotRows(DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey));
                return;
            }

            ReturnToActions();
        }

        private void ReturnToActions()
        {
            _mode = SelectionMode.Idle;
            HidePanel(_listPanel);
            HidePanel(_targetPanel);

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
            }
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        private Button CreateRow(Sprite icon, string name, string meta, bool enabled, Action onClick)
        {
            var row = new Button(onClick);
            row.text = string.Empty;
            row.AddToClassList("cd-row");
            row.SetEnabled(enabled);

            var iconElement = new VisualElement();
            iconElement.AddToClassList("cd-row__icon");
            iconElement.pickingMode = PickingMode.Ignore;
            if (icon != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(icon);
            }
            row.Add(iconElement);

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("cd-row__name");
            nameLabel.pickingMode = PickingMode.Ignore;
            row.Add(nameLabel);

            var metaLabel = new Label(meta ?? string.Empty);
            metaLabel.AddToClassList("cd-row__meta");
            metaLabel.pickingMode = PickingMode.Ignore;
            row.Add(metaLabel);

            return row;
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
