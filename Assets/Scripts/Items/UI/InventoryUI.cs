using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Items.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI _statsText;

        [Header("Equipment")]
        [SerializeField] private Transform _equipSlotsParent;
        [SerializeField] private InventoryEntryUI _slotEntryPrefab;

        [Header("Bag")]
        [SerializeField] private Transform _bagListParent;
        [SerializeField] private InventoryEntryUI _bagEntryPrefab;

        [Header("Detail Panel")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private TextMeshProUGUI _detailBody;
        [SerializeField] private Button _detailActionButton;
        [SerializeField] private TextMeshProUGUI _detailActionLabel;
        [SerializeField] private Button _detailCloseButton;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        private List<GameObject> _spawnedSlotEntries = new List<GameObject>();
        private List<GameObject> _spawnedBagEntries = new List<GameObject>();

        private ItemSaveData _selectedItem;
        private bool _selectedIsEquipped;

        private bool _isOpen;

        private static readonly Color SlotEmptyColor = new Color(0.18f, 0.18f, 0.26f, 0.8f);
        private static readonly Color SlotFilledColor = new Color(0.22f, 0.22f, 0.32f, 1f);

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
            _rootPanel.SetActive(false);
            _detailPanel.SetActive(false);

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_detailCloseButton != null)
            {
                _detailCloseButton.onClick.AddListener(() => _detailPanel.SetActive(false));
            }
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
            var party = GameManager.Instance.Party;
            if (party == null || party.Leader == null)
            {
                _statsText.text = "No party";
                return;
            }

            var leader = party.Leader;
            var raw = InventoryManager.Instance.ComputeRawBonuses(leader.HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(leader.HeroKey);

            int effAtk = leader.GetEffectiveAttack();
            int effDef = leader.GetEffectiveDefense();
            int effHp = leader.GetEffectiveMaxHealth();

            string atkBonus = FormatBonus(raw[StatType.Attack], pct[StatType.Attack]);
            string defBonus = FormatBonus(raw[StatType.Defense], pct[StatType.Defense]);
            string hpBonus = FormatBonus(raw[StatType.MaxHealth], pct[StatType.MaxHealth]);

            _statsText.text =
                $"ATK: {effAtk} ({leader.Stats.Attack}{atkBonus})   " +
                $"DEF: {effDef} ({leader.Stats.Defense}{defBonus})   " +
                $"HP: {leader.Stats.Health}/{effHp} ({leader.Stats.MaxHealth}{hpBonus})";
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
            _spawnedSlotEntries.DestroyAndClear();

            var heroKey = GetLeaderHeroKey();
            foreach (SlotType slot in Enum.GetValues(typeof(SlotType)))
            {
                var equipped = InventoryManager.Instance.GetEquipped(slot, heroKey);
                var entry = CreateSlotEntry(slot, equipped);
                _spawnedSlotEntries.Add(entry.gameObject);
            }
        }

        private string GetLeaderHeroKey()
        {
            var party = GameManager.Instance.Party;
            if (party != null && party.Leader != null)
            {
                return party.Leader.HeroKey;
            }
            return "";
        }

        private InventoryEntryUI CreateSlotEntry(SlotType slot, ItemSaveData item)
        {
            var entry = Instantiate(_slotEntryPrefab, _equipSlotsParent);

            if (item != null)
            {
                var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
                string displayName = so != null ? so.DisplayName : item.ItemKey;
                entry.SetLabel($"[{slot}] {displayName}");
                entry.SetBackgroundColor(SlotFilledColor);

                if (so != null && RarityColors.TryGetValue(so.Rarity, out var rarityColor))
                {
                    entry.SetLabelColor(rarityColor);
                }

                var capturedItem = item;
                entry.Button.onClick.AddListener(() => ShowDetail(capturedItem, true));
            }
            else
            {
                entry.SetLabel($"[{slot}] Empty");
                entry.SetBackgroundColor(SlotEmptyColor);
            }

            return entry;
        }

        // ============================================================
        //  BAG
        // ============================================================

        private void RefreshBag()
        {
            _spawnedBagEntries.DestroyAndClear();

            var bagItems = InventoryManager.Instance.GetBagItems();
            if (bagItems.Count == 0)
            {
                var entry = Instantiate(_bagEntryPrefab, _bagListParent);
                entry.SetLabel("Bag is empty");
                entry.SetLabelColor(new Color(0.5f, 0.5f, 0.5f));
                entry.Button.interactable = false;
                _spawnedBagEntries.Add(entry.gameObject);
                return;
            }

            foreach (var item in bagItems)
            {
                var entry = CreateBagEntry(item);
                _spawnedBagEntries.Add(entry.gameObject);
            }
        }

        private InventoryEntryUI CreateBagEntry(ItemSaveData item)
        {
            var so = InventoryManager.Instance.GetItemSO(item.ItemKey);
            string displayName = so != null ? so.DisplayName : item.ItemKey;
            string rarityTag = so != null ? $"[{so.Rarity}] " : "";

            var entry = Instantiate(_bagEntryPrefab, _bagListParent);
            entry.SetLabel(rarityTag + displayName);

            if (so != null && RarityColors.TryGetValue(so.Rarity, out var rarityColor))
            {
                entry.SetLabelColor(rarityColor);
            }

            var capturedItem = item;
            entry.Button.onClick.AddListener(() => ShowDetail(capturedItem, false));

            return entry;
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

            var heroKey = GetLeaderHeroKey();
            if (isEquipped)
            {
                _detailActionLabel.text = "Unequip";
                _detailActionButton.onClick.AddListener(() =>
                {
                    if (Enum.TryParse<SlotType>(item.EquippedSlot, out var slot))
                    {
                        InventoryManager.Instance.Unequip(slot, heroKey);
                    }
                    _detailPanel.SetActive(false);
                });
            }
            else
            {
                _detailActionLabel.text = "Equip";
                _detailActionButton.onClick.AddListener(() =>
                {
                    InventoryManager.Instance.Equip(item, so.SlotType, heroKey);
                    _detailPanel.SetActive(false);
                });
            }
        }
    }
}
