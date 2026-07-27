using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// In-combat selection UI for the Draw/Magic system. Reuses two panels: the "slot list"
    /// panel (equipped magic slots, for casting or picking a slot to overwrite when drawing)
    /// and the "target" panel (pick a combat unit — cast target, attack target, or draw source).
    /// Driven entirely by CombatManager events.
    /// </summary>
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

        [Header("Magic Slot Panel")]
        [SerializeField] private GameObject _cardListPanel;
        [SerializeField] private Transform _cardListParent;
        [SerializeField] private GameObject _cardButtonPrefab;
        [SerializeField] private Button _backButton;

        [Header("Target Selection Panel")]
        [SerializeField] private GameObject _targetPanel;
        [SerializeField] private Transform _targetListParent;
        [SerializeField] private GameObject _targetButtonPrefab;
        [SerializeField] private Button _targetBackButton;
        [SerializeField] private TextMeshProUGUI _targetPromptLabel;

        private ICombatUnit _currentHero;
        private SelectionMode _mode = SelectionMode.Idle;

        private int _selectedSlotIndex;
        private MagicSO _selectedMagic;
        private Enemy _drawSource;
        private MagicSO _drawMagic;
        private int _drawCharges;

        private List<GameObject> _spawnedSlotButtons = new List<GameObject>();
        private List<GameObject> _spawnedTargetButtons = new List<GameObject>();

        private void OnEnable()
        {
            CombatManager.Instance.OnMagicSlotsRequested += ShowSlotListForCast;
            CombatManager.Instance.OnAttackTargetRequested += ShowAttackTargets;
            CombatManager.Instance.OnDrawTargetRequested += ShowDrawTargets;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }

        private void OnDisable()
        {
            if (CombatManager.HasInstance)
            {
                CombatManager.Instance.OnMagicSlotsRequested -= ShowSlotListForCast;
                CombatManager.Instance.OnAttackTargetRequested -= ShowAttackTargets;
                CombatManager.Instance.OnDrawTargetRequested -= ShowDrawTargets;
                CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            }
        }

        private void Start()
        {
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);

            _backButton.onClick.AddListener(OnBackToActions);
            _targetBackButton.onClick.AddListener(OnTargetBack);
        }

        // ============================================================
        //  CAST FLOW
        // ============================================================

        private void ShowSlotListForCast(ICombatUnit hero, List<MagicSlot> slots)
        {
            _currentHero = hero;
            _mode = SelectionMode.Cast;
            _selectedMagic = null;
            PopulateSlotButtons(slots);
        }

        private void PopulateSlotButtons(List<MagicSlot> slots)
        {
            _spawnedSlotButtons.DestroyAndClear();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var btnObj = Instantiate(_cardButtonPrefab, _cardListParent);
                btnObj.SetActive(true);

                SetChild(btnObj, "Icon", slot.IsEmpty ? null : slot.Magic.Icon);
                SetLabel(btnObj, "NameLabel", slot.IsEmpty ? "(empty)" : slot.Magic.DisplayName);
                SetLabel(btnObj, "DescriptionLabel", slot.IsEmpty ? "" : $"Charges: {slot.Charges}/{slot.MaxCharges}");
                SetLabel(btnObj, "EffectsLabel", slot.IsEmpty ? "" : slot.Magic.GetEffectsSummary());

                bool selectable = _mode == SelectionMode.DrawPlacement || slot.CanCast;

                var captured = i;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = selectable;
                    if (selectable)
                    {
                        btn.onClick.AddListener(() => OnSlotSelected(captured, slot));
                    }
                }

                _spawnedSlotButtons.Add(btnObj);
            }

            _cardListPanel.SetActive(true);
            _targetPanel.SetActive(false);
        }

        private void OnSlotSelected(int slotIndex, MagicSlot slot)
        {
            if (_mode == SelectionMode.DrawPlacement)
            {
                SubmitDraw(slotIndex);
                return;
            }

            // Cast mode
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

            PopulateTargetButtons(targets, prompt);
        }

        private void SubmitCast(List<ICombatUnit> targets)
        {
            _mode = SelectionMode.Idle;
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);
            CombatManager.Instance.SubmitCastAction(_selectedMagic, _selectedSlotIndex, _currentHero, targets);
        }

        // ============================================================
        //  ATTACK TARGETING
        // ============================================================

        private void ShowAttackTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            _currentHero = hero;
            _mode = SelectionMode.AttackTarget;
            PopulateTargetButtons(enemies, "Select Attack Target");
        }

        // ============================================================
        //  DRAW FLOW
        // ============================================================

        private void ShowDrawTargets(ICombatUnit hero, List<ICombatUnit> enemies)
        {
            _currentHero = hero;
            _mode = SelectionMode.DrawTarget;
            PopulateTargetButtons(enemies, "Draw Magic From");
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
            _targetPanel.SetActive(false);
            _spawnedTargetButtons.DestroyAndClear();
            PopulateDrawChoiceButtons(_drawSource.DrawableMagics);
        }

        private void PopulateDrawChoiceButtons(List<DrawableMagicEntry> entries)
        {
            _spawnedSlotButtons.DestroyAndClear();

            foreach (var entry in entries)
            {
                var btnObj = Instantiate(_cardButtonPrefab, _cardListParent);
                btnObj.SetActive(true);

                bool valid = entry != null && entry.Magic != null;
                SetChild(btnObj, "Icon", valid ? entry.Magic.Icon : null);
                SetLabel(btnObj, "NameLabel", valid ? entry.Magic.DisplayName : "(none)");
                SetLabel(btnObj, "DescriptionLabel", valid ? $"Charges: {entry.Charges}" : "");
                SetLabel(btnObj, "EffectsLabel", valid ? entry.Magic.GetEffectsSummary() : "");

                var captured = entry;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = valid;
                    if (valid)
                    {
                        btn.onClick.AddListener(() => SelectDrawMagic(captured));
                    }
                }

                _spawnedSlotButtons.Add(btnObj);
            }

            _cardListPanel.SetActive(true);
            _targetPanel.SetActive(false);
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
            PopulateSlotButtons(DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey));
        }

        private void SubmitDraw(int slotIndex)
        {
            _mode = SelectionMode.Idle;
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);
            CombatManager.Instance.SubmitDrawAction(_drawSource, _drawMagic, _drawCharges, slotIndex);
        }

        // ============================================================
        //  TARGET BUTTONS (shared)
        // ============================================================

        private void PopulateTargetButtons(List<ICombatUnit> targets, string prompt)
        {
            _spawnedTargetButtons.DestroyAndClear();
            _targetPromptLabel.text = prompt;

            foreach (var target in targets)
            {
                var btnObj = Instantiate(_targetButtonPrefab, _targetListParent);
                btnObj.SetActive(true);

                var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"{target.DisplayName} (HP: {target.Stats.Health})";
                }

                SetChild(btnObj, "Icon", target.Icon);

                var captured = target;
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnTargetSelected(captured));
                }

                _spawnedTargetButtons.Add(btnObj);
            }

            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(true);
        }

        private void OnTargetSelected(ICombatUnit target)
        {
            switch (_mode)
            {
                case SelectionMode.AttackTarget:
                    _mode = SelectionMode.Idle;
                    _targetPanel.SetActive(false);
                    _spawnedTargetButtons.DestroyAndClear();
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

        private void OnBackToActions()
        {
            ReturnToActions();
        }

        private void OnTargetBack()
        {
            _targetPanel.SetActive(false);
            _spawnedTargetButtons.DestroyAndClear();

            // For cast targeting, step back to the slot list; everything else returns to actions.
            if (_mode == SelectionMode.Cast && _currentHero is Hero hero &&
                DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                PopulateSlotButtons(DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey));
                return;
            }

            ReturnToActions();
        }

        private void ReturnToActions()
        {
            _mode = SelectionMode.Idle;
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);

            var roomActionUI = FindObjectOfType<RoomActionUI>();
            if (roomActionUI != null)
            {
                roomActionUI.ReturnToHeroActions();
            }
        }

        private void OnCombatEnded(CombatResult result)
        {
            _mode = SelectionMode.Idle;
            _cardListPanel.SetActive(false);
            _targetPanel.SetActive(false);
            _spawnedSlotButtons.DestroyAndClear();
            _spawnedTargetButtons.DestroyAndClear();
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        private static void SetLabel(GameObject root, string childName, string text)
        {
            var child = root.transform.Find(childName);
            if (child != null)
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = text;
                    return;
                }
            }

            if (childName == "NameLabel")
            {
                var fallback = root.GetComponentInChildren<TextMeshProUGUI>();
                if (fallback != null)
                {
                    fallback.text = text;
                }
            }
        }

        private static void SetChild(GameObject root, string childName, Sprite sprite)
        {
            var child = root.transform.Find(childName);
            if (child == null || sprite == null)
            {
                return;
            }
            var img = child.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
            }
        }
    }
}
