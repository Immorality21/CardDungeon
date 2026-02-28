using System;
using System.Collections.Generic;
using Assets.Scripts.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Items.UI
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class InventoryUI : MonoBehaviour
    {
        // Root container
        private GameObject _rootPanel;

        // Stats summary (top)
        private TextMeshProUGUI _statsText;

        // Equipment panel (left)
        private Transform _equipSlotsParent;
        private List<GameObject> _spawnedSlotEntries = new List<GameObject>();

        // Bag panel (right)
        private Transform _bagListParent;
        private List<GameObject> _spawnedBagEntries = new List<GameObject>();

        // Detail panel (center overlay)
        private GameObject _detailPanel;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private Button _detailActionButton;
        private TextMeshProUGUI _detailActionLabel;
        private Button _detailCloseButton;

        private ItemSaveData _selectedItem;
        private bool _selectedIsEquipped;

        private bool _isOpen;

        private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.18f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.32f, 1f);
        private static readonly Color ButtonHoverColor = new Color(0.30f, 0.30f, 0.42f, 1f);
        private static readonly Color SlotEmptyColor = new Color(0.18f, 0.18f, 0.26f, 0.8f);
        private static readonly Color HeaderColor = new Color(0.16f, 0.16f, 0.24f, 1f);

        private static readonly Dictionary<ItemRarity, Color> RarityColors = new Dictionary<ItemRarity, Color>
        {
            { ItemRarity.Common, new Color(0.78f, 0.78f, 0.78f) },
            { ItemRarity.Uncommon, new Color(0.30f, 0.85f, 0.30f) },
            { ItemRarity.Rare, new Color(0.30f, 0.50f, 1.00f) },
            { ItemRarity.Epic, new Color(0.70f, 0.30f, 0.90f) },
            { ItemRarity.Legendary, new Color(1.00f, 0.65f, 0.00f) }
        };

        private void Awake()
        {
            BuildPanels();
            _rootPanel.SetActive(false);
        }

        private void Start()
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshAll;
        }

        private void OnDestroy()
        {
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshAll;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (_isOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            _isOpen = true;
            _rootPanel.SetActive(true);
            _detailPanel.SetActive(false);
            RefreshAll();
        }

        public void Close()
        {
            _isOpen = false;
            _rootPanel.SetActive(false);
        }

        private void RefreshAll()
        {
            if (!_isOpen)
            {
                return;
            }

            RefreshStats();
            RefreshEquipmentSlots();
            RefreshBag();
        }

        // ============================================================
        //  STATS SUMMARY
        // ============================================================

        private void RefreshStats()
        {
            var player = GameManager.Instance.Player;
            if (player == null)
            {
                _statsText.text = "No player";
                return;
            }

            var raw = InventoryManager.Instance.ComputeRawBonuses();
            var pct = InventoryManager.Instance.ComputePercentageBonuses();

            int effAtk = player.GetEffectiveAttack();
            int effDef = player.GetEffectiveDefense();
            int effHp = player.GetEffectiveMaxHealth();

            string atkBonus = FormatBonus(raw[StatType.Attack], pct[StatType.Attack]);
            string defBonus = FormatBonus(raw[StatType.Defense], pct[StatType.Defense]);
            string hpBonus = FormatBonus(raw[StatType.MaxHealth], pct[StatType.MaxHealth]);

            _statsText.text =
                $"ATK: {effAtk} ({player.Stats.Attack}{atkBonus})   " +
                $"DEF: {effDef} ({player.Stats.Defense}{defBonus})   " +
                $"HP: {player.Stats.Health}/{effHp} ({player.Stats.MaxHealth}{hpBonus})";
        }

        private string FormatBonus(float rawVal, float pctVal)
        {
            var parts = "";
            if (rawVal != 0)
            {
                parts += $"+{rawVal:0}";
            }
            if (pctVal != 0)
            {
                if (parts.Length > 0)
                {
                    parts += " ";
                }
                parts += $"+{pctVal:0}%";
            }
            if (parts.Length > 0)
            {
                return " " + parts;
            }
            return "";
        }

        // ============================================================
        //  EQUIPMENT SLOTS
        // ============================================================

        private void RefreshEquipmentSlots()
        {
            foreach (var obj in _spawnedSlotEntries)
            {
                Destroy(obj);
            }
            _spawnedSlotEntries.Clear();

            foreach (SlotType slot in Enum.GetValues(typeof(SlotType)))
            {
                var equipped = InventoryManager.Instance.GetEquipped(slot);
                var entry = CreateSlotEntry(slot, equipped);
                entry.transform.SetParent(_equipSlotsParent, false);
                _spawnedSlotEntries.Add(entry);
            }
        }

        private GameObject CreateSlotEntry(SlotType slot, ItemSaveData item)
        {
            var obj = new GameObject(slot.ToString());
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 40);
            var img = obj.AddComponent<Image>();
            img.color = item != null ? ButtonColor : SlotEmptyColor;

            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ButtonHoverColor;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            string label;
            if (item != null)
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                string displayName = so != null ? so.DisplayName : item.ItemKey;
                label = $"[{slot}] {displayName}";
            }
            else
            {
                label = $"[{slot}] Empty";
            }

            var text = CreateText(obj.transform, slot + "Text", label, 15, FontStyles.Normal);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(8, 0, 8, 0);

            if (item != null)
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                if (so != null && RarityColors.TryGetValue(so.Rarity, out var rarityColor))
                {
                    text.color = rarityColor;
                }
            }

            var capturedItem = item;
            btn.onClick.AddListener(() =>
            {
                if (capturedItem != null)
                {
                    ShowDetail(capturedItem, true);
                }
            });

            return obj;
        }

        // ============================================================
        //  BAG
        // ============================================================

        private void RefreshBag()
        {
            foreach (var obj in _spawnedBagEntries)
            {
                Destroy(obj);
            }
            _spawnedBagEntries.Clear();

            var bagItems = InventoryManager.Instance.GetBagItems();
            if (bagItems.Count == 0)
            {
                var emptyLabel = new GameObject("EmptyLabel");
                var rt = emptyLabel.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0, 40);
                emptyLabel.transform.SetParent(_bagListParent, false);
                var le = emptyLabel.AddComponent<LayoutElement>();
                le.preferredHeight = 40;
                var text = CreateText(emptyLabel.transform, "EmptyText", "Bag is empty", 15, FontStyles.Italic);
                text.color = new Color(0.5f, 0.5f, 0.5f);
                _spawnedBagEntries.Add(emptyLabel);
                return;
            }

            foreach (var item in bagItems)
            {
                var entry = CreateBagEntry(item);
                entry.transform.SetParent(_bagListParent, false);
                _spawnedBagEntries.Add(entry);
            }
        }

        private GameObject CreateBagEntry(ItemSaveData item)
        {
            var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
            string displayName = so != null ? so.DisplayName : item.ItemKey;

            var obj = new GameObject(displayName);
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 36);
            var img = obj.AddComponent<Image>();
            img.color = ButtonColor;

            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 36;

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ButtonHoverColor;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            string rarityTag = so != null ? $"[{so.Rarity}] " : "";
            var text = CreateText(obj.transform, "ItemText", rarityTag + displayName, 14, FontStyles.Normal);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(8, 0, 8, 0);

            if (so != null && RarityColors.TryGetValue(so.Rarity, out var rarityColor))
            {
                text.color = rarityColor;
            }

            var capturedItem = item;
            btn.onClick.AddListener(() => ShowDetail(capturedItem, false));

            return obj;
        }

        // ============================================================
        //  DETAIL PANEL
        // ============================================================

        private void ShowDetail(ItemSaveData item, bool isEquipped)
        {
            _selectedItem = item;
            _selectedIsEquipped = isEquipped;
            _detailPanel.SetActive(true);

            var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
            if (so == null)
            {
                _detailTitle.text = item.ItemKey;
                _detailBody.text = "Unknown item";
                _detailActionButton.gameObject.SetActive(false);
                return;
            }

            Color rarityColor = RarityColors.TryGetValue(so.Rarity, out var rc) ? rc : Color.white;
            _detailTitle.text = so.DisplayName;
            _detailTitle.color = rarityColor;

            string body = $"Rarity: {so.Rarity}\n";
            body += $"Level: {so.ItemLevel}\n";
            body += $"Slot: {so.SlotType}\n\n";

            if (so.Bonuses.Count > 0)
            {
                body += "Bonuses:\n";
                foreach (var bonus in so.Bonuses)
                {
                    if (bonus.BonusType == BonusType.Raw)
                    {
                        body += $"  +{bonus.Value:0} {bonus.StatType}\n";
                    }
                    else
                    {
                        body += $"  +{bonus.Value:0}% {bonus.StatType}\n";
                    }
                }
            }
            else
            {
                body += "No bonuses";
            }

            _detailBody.text = body;

            _detailActionButton.gameObject.SetActive(true);
            _detailActionButton.onClick.RemoveAllListeners();

            if (isEquipped)
            {
                _detailActionLabel.text = "Unequip";
                _detailActionButton.onClick.AddListener(() =>
                {
                    if (Enum.TryParse<SlotType>(item.EquippedSlot, out var slot))
                    {
                        InventoryManager.Instance.Unequip(slot);
                    }
                    _detailPanel.SetActive(false);
                });
            }
            else
            {
                _detailActionLabel.text = "Equip";
                _detailActionButton.onClick.AddListener(() =>
                {
                    InventoryManager.Instance.Equip(item, so.SlotType);
                    _detailPanel.SetActive(false);
                });
            }
        }

        // ============================================================
        //  PANEL CONSTRUCTION (builds child elements under this Canvas)
        // ============================================================

        private void BuildPanels()
        {
            var canvasTransform = transform;

            // Root panel — fills most of the screen
            _rootPanel = CreatePanel(canvasTransform, "RootPanel", Vector2.zero, Vector2.zero);
            var rootRT = _rootPanel.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0.05f, 0.05f);
            rootRT.anchorMax = new Vector2(0.95f, 0.95f);
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            var rootVLG = _rootPanel.AddComponent<VerticalLayoutGroup>();
            rootVLG.spacing = 6;
            rootVLG.padding = new RectOffset(10, 10, 10, 10);
            rootVLG.childForceExpandWidth = true;
            rootVLG.childForceExpandHeight = false;

            // Title bar with close button
            BuildTitleBar(_rootPanel.transform);

            // Stats bar
            BuildStatsBar(_rootPanel.transform);

            // Main content area (equipment left, bag right)
            BuildContentArea(_rootPanel.transform);

            // Detail panel (overlay on canvas)
            BuildDetailPanel(canvasTransform);
        }

        private void BuildTitleBar(Transform parent)
        {
            var bar = new GameObject("TitleBar");
            bar.transform.SetParent(parent, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 40);
            var img = bar.AddComponent<Image>();
            img.color = HeaderColor;
            var le = bar.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(12, 12, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var titleText = CreateText(bar.transform, "TitleLabel", "Inventory", 22, FontStyles.Bold);
            var titleLE = titleText.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;

            var closeBtn = CreateButton(bar.transform, "Close [I]");
            var closeBtnLE = closeBtn.gameObject.AddComponent<LayoutElement>();
            closeBtnLE.preferredWidth = 100;
            closeBtnLE.preferredHeight = 32;
            closeBtn.onClick.AddListener(Close);
        }

        private void BuildStatsBar(Transform parent)
        {
            var bar = new GameObject("StatsBar");
            bar.transform.SetParent(parent, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30);
            var img = bar.AddComponent<Image>();
            img.color = new Color(0.14f, 0.14f, 0.20f, 0.9f);
            var le = bar.AddComponent<LayoutElement>();
            le.preferredHeight = 30;

            _statsText = CreateText(bar.transform, "StatsText", "", 15, FontStyles.Normal);
            _statsText.alignment = TextAlignmentOptions.Center;
        }

        private void BuildContentArea(Transform parent)
        {
            var content = new GameObject("ContentArea");
            content.transform.SetParent(parent, false);
            var rt = content.AddComponent<RectTransform>();
            var le = content.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;

            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Equipment panel (left, ~40%)
            BuildEquipmentPanel(content.transform);

            // Bag panel (right, ~60%)
            BuildBagPanel(content.transform);
        }

        private void BuildEquipmentPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "EquipPanel", Vector2.zero, Vector2.zero);
            var panelLE = panel.AddComponent<LayoutElement>();
            panelLE.flexibleWidth = 0.4f;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var header = CreateText(panel.transform, "EquipHeader", "Equipment", 18, FontStyles.Bold);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            // Scroll area for slots
            var scrollArea = new GameObject("EquipSlotList");
            scrollArea.transform.SetParent(panel.transform, false);
            var scrollRT = scrollArea.AddComponent<RectTransform>();
            var scrollVLG = scrollArea.AddComponent<VerticalLayoutGroup>();
            scrollVLG.spacing = 4;
            scrollVLG.childForceExpandWidth = true;
            scrollVLG.childForceExpandHeight = false;
            scrollVLG.childAlignment = TextAnchor.UpperCenter;
            var scrollLE = scrollArea.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1;

            _equipSlotsParent = scrollArea.transform;
        }

        private void BuildBagPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "BagPanel", Vector2.zero, Vector2.zero);
            var panelLE = panel.AddComponent<LayoutElement>();
            panelLE.flexibleWidth = 0.6f;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var header = CreateText(panel.transform, "BagHeader", "Bag", 18, FontStyles.Bold);
            var headerLE = header.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28;

            // Scroll area for bag items
            var scrollObj = new GameObject("BagScroll");
            scrollObj.transform.SetParent(panel.transform, false);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            var scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1;

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            var vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.white;

            // Content
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewport.transform, false);
            var contentRT = contentObj.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.offsetMin = new Vector2(0, 0);
            contentRT.offsetMax = new Vector2(0, 0);

            var contentVLG = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing = 4;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;
            scrollRect.viewport = vpRT;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            _bagListParent = contentObj.transform;
        }

        private void BuildDetailPanel(Transform parent)
        {
            _detailPanel = CreatePanel(parent, "DetailPanel", Vector2.zero, new Vector2(400, 320));
            var detailRT = _detailPanel.GetComponent<RectTransform>();
            detailRT.anchorMin = new Vector2(0.5f, 0.5f);
            detailRT.anchorMax = new Vector2(0.5f, 0.5f);

            var vlg = _detailPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _detailTitle = CreateText(_detailPanel.transform, "DetailTitle", "", 20, FontStyles.Bold);
            var titleLE = _detailTitle.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 28;

            _detailBody = CreateText(_detailPanel.transform, "DetailBody", "", 15, FontStyles.Normal);
            _detailBody.alignment = TextAlignmentOptions.TopLeft;
            var bodyLE = _detailBody.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1;

            // Button row
            var btnRow = new GameObject("ButtonRow");
            btnRow.transform.SetParent(_detailPanel.transform, false);
            var btnRowRT = btnRow.AddComponent<RectTransform>();
            var btnRowLE = btnRow.AddComponent<LayoutElement>();
            btnRowLE.preferredHeight = 42;
            var btnRowHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnRowHLG.spacing = 10;
            btnRowHLG.childAlignment = TextAnchor.MiddleCenter;
            btnRowHLG.childForceExpandWidth = true;
            btnRowHLG.childForceExpandHeight = true;

            _detailActionButton = CreateButton(btnRow.transform, "Equip");
            _detailActionLabel = _detailActionButton.GetComponentInChildren<TextMeshProUGUI>();

            _detailCloseButton = CreateButton(btnRow.transform, "Close");
            _detailCloseButton.onClick.AddListener(() => _detailPanel.SetActive(false));

            _detailPanel.SetActive(false);
        }

        // ============================================================
        //  UI HELPERS
        // ============================================================

        private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var img = panel.AddComponent<Image>();
            img.color = PanelColor;
            return panel;
        }

        private Button CreateButton(Transform parent, string label)
        {
            var btnObj = new GameObject(label + "Btn");
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 42);

            var img = btnObj.AddComponent<Image>();
            img.color = ButtonColor;

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ButtonHoverColor;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            CreateText(btnObj.transform, label + "Text", label, 18, FontStyles.Bold);
            return btn;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, FontStyles style)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            var rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
