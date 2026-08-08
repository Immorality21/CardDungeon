using System;
using Assets.Scripts.Combat;
using Assets.Scripts.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Cards.UI
{
    /// <summary>
    /// Hub "Forge" (UI Toolkit view-controller). A collection screen: two tabs (All Magic /
    /// Combos), a grid showing the real icon for discovered entries and a '?' for undiscovered
    /// ones. Clicking a cell inspects it (name, description, effects with their unlock levels)
    /// and lets you spend Essence to upgrade — which raises power and unlocks level-gated
    /// effects. Combos are upgradeable only once discovered (triggered in combat).
    /// Operates on a VisualElement subtree; not a MonoBehaviour.
    /// </summary>
    public class MagicForgeUI
    {
        private enum Tab { Magic, Combos }

        private readonly VisualElement _root;
        private readonly Label _essenceLabel;
        private readonly Label _emptyLabel;
        private readonly ScrollView _grid;
        private readonly VisualElement _tabsBar;
        private readonly Button _tabMagic;
        private readonly Button _tabCombos;
        private readonly Button _closeButton;

        private readonly VisualElement _inspect;
        private readonly VisualElement _inspectIcon;
        private readonly Label _inspectName;
        private readonly Label _inspectLevel;
        private readonly Label _inspectDesc;
        private readonly VisualElement _inspectEffects;
        private readonly Button _inspectBack;
        private readonly Button _inspectUpgrade;

        private Tab _tab = Tab.Magic;
        private MagicSO _inspectedMagic;
        private MagicComboSO _inspectedCombo;

        public event Action OnClosed;

        public MagicForgeUI(VisualElement root)
        {
            _root = root;
            _essenceLabel = root.Q<Label>("forge-essence");
            _emptyLabel = root.Q<Label>("forge-empty");
            _grid = root.Q<ScrollView>("forge-grid");
            _tabsBar = root.Q<VisualElement>("forge-tabs");
            _tabMagic = root.Q<Button>("tab-magic");
            _tabCombos = root.Q<Button>("tab-combos");
            _closeButton = root.Q<Button>("forge-close");

            _inspect = root.Q<VisualElement>("forge-inspect");
            _inspectIcon = root.Q<VisualElement>("inspect-icon");
            _inspectName = root.Q<Label>("inspect-name");
            _inspectLevel = root.Q<Label>("inspect-level");
            _inspectDesc = root.Q<Label>("inspect-desc");
            _inspectEffects = root.Q<VisualElement>("inspect-effects");
            _inspectBack = root.Q<Button>("inspect-back");
            _inspectUpgrade = root.Q<Button>("inspect-upgrade");

            _tabMagic.clicked += () => SetTab(Tab.Magic);
            _tabCombos.clicked += () => SetTab(Tab.Combos);
            _closeButton.clicked += Hide;
            _inspectBack.clicked += ShowGrid;
            _inspectUpgrade.clicked += OnUpgrade;

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
            SetTab(Tab.Magic);
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            _grid.Clear();
            OnClosed?.Invoke();
        }

        // ============================================================
        //  TABS + GRID
        // ============================================================

        private void SetTab(Tab tab)
        {
            _tab = tab;
            _tabMagic.EnableInClassList("cd-tab--active", tab == Tab.Magic);
            _tabCombos.EnableInClassList("cd-tab--active", tab == Tab.Combos);
            ShowGrid();
            RefreshGrid();
        }

        private void ShowGrid()
        {
            _inspectedMagic = null;
            _inspectedCombo = null;
            SetShown(_inspect, false);
            SetShown(_tabsBar, true);
            SetShown(_grid, true);
            SetShown(_closeButton, true);
        }

        private void ShowInspect()
        {
            SetShown(_tabsBar, false);
            SetShown(_grid, false);
            SetShown(_emptyLabel, false);
            SetShown(_closeButton, false);
            SetShown(_inspect, true);
        }

        private void RefreshGrid()
        {
            UpdateEssence();
            _grid.Clear();

            if (_tab == Tab.Magic)
            {
                var all = MagicCatalog.Instance.AllMagic;
                SetShown(_emptyLabel, all.Count == 0);
                foreach (var magic in all)
                {
                    if (magic == null || string.IsNullOrEmpty(magic.Key))
                    {
                        continue;
                    }
                    bool discovered = MetaProgressManager.Instance.IsMagicDiscovered(magic.Key);
                    var captured = magic;
                    _grid.Add(BuildCell(
                        discovered && magic.Icon != null ? magic.Icon : null,
                        discovered ? Initial(magic.DisplayName) : "?",
                        !discovered,
                        () => InspectMagic(captured)));
                }
            }
            else
            {
                if (!MagicComboCatalog.HasInstance)
                {
                    SetShown(_emptyLabel, true);
                    return;
                }
                var all = MagicComboCatalog.Instance.AllCombos;
                SetShown(_emptyLabel, all.Count == 0);
                foreach (var combo in all)
                {
                    if (combo == null || string.IsNullOrEmpty(combo.Key))
                    {
                        continue;
                    }
                    bool discovered = MetaProgressManager.Instance.IsComboDiscovered(combo.Key);
                    var captured = combo;
                    _grid.Add(BuildCell(
                        discovered && combo.Icon != null ? combo.Icon : null,
                        discovered ? "✦" : "?",
                        !discovered,
                        () => InspectCombo(captured)));
                }
            }
        }

        private Button BuildCell(Sprite icon, string glyph, bool locked, Action onClick)
        {
            var cell = new Button(onClick) { text = string.Empty };
            cell.AddToClassList("cd-cell");
            if (locked)
            {
                cell.AddToClassList("cd-cell--locked");
            }

            if (icon != null)
            {
                var img = new VisualElement();
                img.AddToClassList("cd-cell__icon");
                img.pickingMode = PickingMode.Ignore;
                img.style.backgroundImage = new StyleBackground(icon);
                cell.Add(img);
            }
            else
            {
                var q = new Label(glyph);
                q.AddToClassList("cd-cell__q");
                q.pickingMode = PickingMode.Ignore;
                cell.Add(q);
            }

            return cell;
        }

        // ============================================================
        //  INSPECT
        // ============================================================

        private void InspectMagic(MagicSO magic)
        {
            _inspectedMagic = magic;
            _inspectedCombo = null;
            ShowInspect();

            if (!MetaProgressManager.Instance.IsMagicDiscovered(magic.Key))
            {
                PopulateLocked("Draw this magic from an enemy to learn it.");
                return;
            }

            SetIcon(magic.Icon);
            _inspectName.text = magic.DisplayName;
            _inspectDesc.text = magic.Description;

            int level = MetaProgressManager.Instance.GetMagicUpgradeLevel(magic.Key);
            int cost = MetaProgressManager.Instance.GetMagicUpgradeCost(magic.Key);
            bool maxed = level >= MetaProgressManager.MaxMagicUpgradeLevel;
            _inspectLevel.text = LevelText(level, maxed, cost);

            BuildEffectRows(magic.Effects, level);

            SetShown(_inspectUpgrade, true);
            _inspectUpgrade.text = maxed ? "MAX" : "Upgrade";
            _inspectUpgrade.SetEnabled(!maxed && MetaProgressManager.Instance.CanUpgradeMagic(magic.Key));
        }

        private void InspectCombo(MagicComboSO combo)
        {
            _inspectedCombo = combo;
            _inspectedMagic = null;
            ShowInspect();

            if (!MetaProgressManager.Instance.IsComboDiscovered(combo.Key))
            {
                PopulateLocked("Trigger this combo in combat to learn it.");
                return;
            }

            SetIcon(combo.Icon);
            _inspectName.text = combo.ComboName;
            _inspectDesc.text = string.IsNullOrEmpty(combo.Description)
                ? $"Requires: {string.Join(" + ", combo.RequiredTags)}"
                : $"{combo.Description}\nRequires: {string.Join(" + ", combo.RequiredTags)}";

            int level = MetaProgressManager.Instance.GetComboUpgradeLevel(combo.Key);
            int cost = MetaProgressManager.Instance.GetComboUpgradeCost(combo.Key);
            bool maxed = level >= MetaProgressManager.MaxMagicUpgradeLevel;
            _inspectLevel.text = LevelText(level, maxed, cost);

            BuildEffectRows(combo.BonusEffects, level);

            SetShown(_inspectUpgrade, true);
            _inspectUpgrade.text = maxed ? "MAX" : "Upgrade";
            _inspectUpgrade.SetEnabled(!maxed && MetaProgressManager.Instance.CanUpgradeCombo(combo.Key));
        }

        private void PopulateLocked(string teaser)
        {
            SetIcon(null);
            _inspectName.text = "???";
            _inspectLevel.text = "Undiscovered";
            _inspectDesc.text = teaser;
            _inspectEffects.Clear();
            SetShown(_inspectUpgrade, false);
        }

        private void BuildEffectRows(System.Collections.Generic.List<SpellEffect> effects, int currentLevel)
        {
            _inspectEffects.Clear();
            if (effects == null)
            {
                return;
            }

            foreach (var effect in effects)
            {
                var row = new VisualElement();
                row.AddToClassList("cd-effect-row");
                bool locked = effect.UnlockLevel > currentLevel;
                if (locked)
                {
                    row.AddToClassList("cd-effect-row--locked");
                }

                var text = new Label(DescribeEffect(effect));
                text.AddToClassList("cd-effect-row__text");
                text.pickingMode = PickingMode.Ignore;
                row.Add(text);

                if (effect.UnlockLevel > 0)
                {
                    var lockLabel = new Label($"Lv {effect.UnlockLevel}");
                    lockLabel.AddToClassList("cd-effect-row__lock");
                    lockLabel.pickingMode = PickingMode.Ignore;
                    row.Add(lockLabel);
                }

                _inspectEffects.Add(row);
            }
        }

        private void OnUpgrade()
        {
            if (_inspectedMagic != null)
            {
                if (MetaProgressManager.Instance.TryUpgradeMagic(_inspectedMagic.Key))
                {
                    InspectMagic(_inspectedMagic);
                    UpdateEssence();
                }
            }
            else if (_inspectedCombo != null)
            {
                if (MetaProgressManager.Instance.TryUpgradeCombo(_inspectedCombo.Key))
                {
                    InspectCombo(_inspectedCombo);
                    UpdateEssence();
                }
            }
        }

        // ============================================================
        //  HELPERS
        // ============================================================

        private void UpdateEssence()
        {
            _essenceLabel.text = $"Essence: {MetaProgressManager.Instance.Essence}";
        }

        private void SetIcon(Sprite icon)
        {
            if (icon != null)
            {
                _inspectIcon.style.backgroundImage = new StyleBackground(icon);
            }
            else
            {
                _inspectIcon.style.backgroundImage = StyleKeyword.None;
            }
        }

        private static string LevelText(int level, bool maxed, int cost)
        {
            return maxed
                ? $"Lv {level} (MAX)"
                : $"Lv {level}/{MetaProgressManager.MaxMagicUpgradeLevel}  —  next: {cost} essence";
        }

        private static string Initial(string name)
        {
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        private static string DescribeEffect(SpellEffect e)
        {
            switch (e.EffectType)
            {
                case SpellEffectType.Damage:
                    return e.DamageType != DamageType.Normal
                        ? $"Damage {e.Power} {e.DamageType}"
                        : $"Damage {e.Power}";
                case SpellEffectType.Heal:
                    return $"Heal {e.Power}";
                case SpellEffectType.Buff:
                    return $"+{e.BuffType} {e.Power} ({e.Duration}t)";
                case SpellEffectType.Debuff:
                    return $"-{e.BuffType} {e.Power} ({e.Duration}t)";
                default:
                    return e.EffectType.ToString();
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
