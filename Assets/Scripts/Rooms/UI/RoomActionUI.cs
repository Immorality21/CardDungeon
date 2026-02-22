using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Rooms
{
    public class RoomActionUI : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasScaler _scaler;

        // Main action panel (bottom center)
        private GameObject _mainPanel;
        private Button _examineButton;
        private Button _actionButton;

        // Combat panel (bottom center, replaces main when enemy present)
        private GameObject _combatPanel;
        private Button _fightButton;
        private Button _fleeButton;

        // Sub panel for listing options (center)
        private GameObject _subPanel;
        private Transform _optionListParent;
        private Button _backButton;

        // Detail popup
        private GameObject _detailPanel;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailMessage;
        private Button _detailOkButton;

        // World-space door confirm
        private GameObject _doorConfirmRoot;

        private Room _currentRoom;
        private Door _selectedDoor;
        private Door _entryDoor;
        private List<GameObject> _spawnedOptions = new List<GameObject>();

        private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.18f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.32f, 1f);
        private static readonly Color ButtonHoverColor = new Color(0.30f, 0.30f, 0.42f, 1f);

        private void Awake()
        {
            BuildUI();
            HideAll();
        }

        public void Show(Room room, Door entryDoor = null)
        {
            UnsubscribeDoors();
            DestroyDoorConfirm();

            _currentRoom = room;
            _entryDoor = entryDoor;
            _subPanel.SetActive(false);
            _detailPanel.SetActive(false);

            bool hasEnemy = room.Enemies.Any(e => e != null && e.IsAlive);
            _combatPanel.SetActive(hasEnemy);
            _mainPanel.SetActive(!hasEnemy);

            if (hasEnemy)
            {
                SetDoorsEnabled(room, entryDoor);
                if (_entryDoor != null)
                    _entryDoor.OnDoorClicked += OnEntryDoorFlee;
            }
            else
            {
                EnableAllDoors(room);
                SubscribeDoors();
            }
        }

        public void Hide()
        {
            HideAll();
            UnsubscribeDoors();
            DestroyDoorConfirm();
        }

        private void HideAll()
        {
            _mainPanel.SetActive(false);
            _combatPanel.SetActive(false);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(false);
        }

        // ============================================================
        //  UI CONSTRUCTION
        // ============================================================

        private void BuildUI()
        {
            var canvasObj = new GameObject("RoomActionCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasObj.AddComponent<GraphicRaycaster>();
            _scaler = canvasObj.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920, 1080);

            BuildMainPanel(canvasObj.transform);
            BuildCombatPanel(canvasObj.transform);
            BuildSubPanel(canvasObj.transform);
            BuildDetailPanel(canvasObj.transform);
        }

        private void BuildMainPanel(Transform parent)
        {
            _mainPanel = CreatePanel(parent, "MainPanel", new Vector2(0, 0), new Vector2(300, 70));
            SetAnchors(_mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            _mainPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            var hlg = _mainPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            _examineButton = CreateButton(_mainPanel.transform, "Examine");
            _actionButton = CreateButton(_mainPanel.transform, "Action");

            _examineButton.onClick.AddListener(OnExamine);
            _actionButton.onClick.AddListener(OnAction);
        }

        private void BuildCombatPanel(Transform parent)
        {
            _combatPanel = CreatePanel(parent, "CombatPanel", new Vector2(0, 0), new Vector2(300, 70));
            SetAnchors(_combatPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            _combatPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            var hlg = _combatPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            _fightButton = CreateButton(_combatPanel.transform, "Fight");
            _fleeButton = CreateButton(_combatPanel.transform, "Flee");

            _fightButton.onClick.AddListener(OnFight);
            _fleeButton.onClick.AddListener(OnFlee);
        }

        private void BuildSubPanel(Transform parent)
        {
            _subPanel = CreatePanel(parent, "SubPanel", new Vector2(0, 0), new Vector2(400, 350));
            SetAnchors(_subPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var vlg = _subPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var scrollArea = new GameObject("OptionList");
            scrollArea.transform.SetParent(_subPanel.transform, false);
            var scrollRT = scrollArea.AddComponent<RectTransform>();
            scrollRT.sizeDelta = new Vector2(0, 240);
            var scrollVLG = scrollArea.AddComponent<VerticalLayoutGroup>();
            scrollVLG.spacing = 6;
            scrollVLG.childForceExpandWidth = true;
            scrollVLG.childForceExpandHeight = false;
            scrollVLG.childAlignment = TextAnchor.UpperCenter;
            var fitter = scrollArea.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layoutEl = scrollArea.AddComponent<LayoutElement>();
            layoutEl.flexibleHeight = 1;
            _optionListParent = scrollArea.transform;

            _backButton = CreateButton(_subPanel.transform, "Back");
            var backLE = _backButton.gameObject.AddComponent<LayoutElement>();
            backLE.preferredHeight = 45;
            _backButton.onClick.AddListener(OnBack);
        }

        private void BuildDetailPanel(Transform parent)
        {
            _detailPanel = CreatePanel(parent, "DetailPanel", new Vector2(0, 0), new Vector2(450, 260));
            SetAnchors(_detailPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var vlg = _detailPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _detailTitle = CreateText(_detailPanel.transform, "DetailTitle", "Title", 22, FontStyles.Bold);
            var titleLE = _detailTitle.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 30;

            _detailMessage = CreateText(_detailPanel.transform, "DetailMessage", "", 17, FontStyles.Normal);
            _detailMessage.alignment = TextAlignmentOptions.TopLeft;
            var msgLE = _detailMessage.gameObject.AddComponent<LayoutElement>();
            msgLE.flexibleHeight = 1;

            _detailOkButton = CreateButton(_detailPanel.transform, "Ok");
            var okLE = _detailOkButton.gameObject.AddComponent<LayoutElement>();
            okLE.preferredHeight = 45;
            // Default listener set per-use via ShowDetail/ShowCombatResult
        }

        // ============================================================
        //  EXAMINE / ACTION FLOWS
        // ============================================================

        private void OnExamine()
        {
            ShowOptionList(
                _currentRoom.RoomSO.ExamineOptions,
                text => ShowDetail("Examine", text));
        }

        private void OnAction()
        {
            ShowOptionList(
                _currentRoom.RoomSO.ActionOptions,
                text => ShowDetail("Action", text));
        }

        private void ShowOptionList(List<string> options, Action<string> onSelect)
        {
            _mainPanel.SetActive(false);
            _subPanel.SetActive(true);
            ClearOptions();

            if (options == null || options.Count == 0)
            {
                ShowDetail("Nothing", "There is nothing here.");
                return;
            }

            foreach (var option in options)
            {
                var btn = CreateButton(_optionListParent, option);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 42;
                var captured = option;
                btn.onClick.AddListener(() => onSelect(captured));
                _spawnedOptions.Add(btn.gameObject);
            }
        }

        private void ShowDetail(string title, string message)
        {
            _subPanel.SetActive(false);
            _detailPanel.SetActive(true);
            _detailTitle.text = title;
            _detailMessage.text = message;

            _detailOkButton.onClick.RemoveAllListeners();
            _detailOkButton.onClick.AddListener(() =>
            {
                _detailPanel.SetActive(false);
                _subPanel.SetActive(true);
            });
        }

        private void ClearOptions()
        {
            foreach (var obj in _spawnedOptions)
                Destroy(obj);
            _spawnedOptions.Clear();
        }

        private void OnBack()
        {
            _subPanel.SetActive(false);
            ClearOptions();
            _mainPanel.SetActive(true);
        }

        // ============================================================
        //  COMBAT
        // ============================================================

        private void OnFight()
        {
            var player = GameManager.Instance.Player;
            var enemy = _currentRoom.Enemies.FirstOrDefault(e => e != null && e.IsAlive);
            if (enemy == null) return;

            var log = "";

            // Player attacks enemy
            int playerDmg = Mathf.Max(1, player.Stats.Attack - enemy.Stats.Defense);
            enemy.Stats.Health -= playerDmg;
            log += $"You deal {playerDmg} damage to the enemy.\n";

            if (!enemy.IsAlive)
            {
                log += "Enemy defeated!";
                InventoryManager.Instance.TryDropItem(enemy.LootItem);
                Destroy(enemy.gameObject);
                _currentRoom.Enemies.Remove(enemy);

                // Check if more enemies remain
                bool moreEnemies = _currentRoom.Enemies.Any(e => e != null && e.IsAlive);
                if (moreEnemies)
                {
                    log += $"\n{_currentRoom.Enemies.Count(e => e != null && e.IsAlive)} enemies remaining!";
                    ShowCombatResult("Enemy Down!", log, showNormalAfter: false, returnToCombat: true);
                }
                else
                {
                    _combatPanel.SetActive(false);
                    ShowCombatResult("Victory!", log, showNormalAfter: true);
                }
                return;
            }

            // Enemy attacks player
            int enemyDmg = Mathf.Max(1, enemy.Stats.Attack - player.Stats.Defense);
            player.Stats.Health -= enemyDmg;
            log += $"Enemy deals {enemyDmg} damage to you.\n";
            log += $"\nYour HP: {player.Stats.Health}/{player.Stats.MaxHealth}";
            log += $"\nEnemy HP: {enemy.Stats.Health}/{enemy.Stats.MaxHealth}";

            if (player.Stats.Health <= 0)
            {
                _combatPanel.SetActive(false);
                ShowCombatResult("You Died!", log, showNormalAfter: false);
                return;
            }

            // Both alive — show log and return to combat panel
            ShowCombatResult("Combat", log, showNormalAfter: false, returnToCombat: true);
        }

        private void OnFlee()
        {
            var player = GameManager.Instance.Player;
            if (player.PreviousRoom == null)
            {
                _combatPanel.SetActive(false);
                ShowCombatResult("Flee", "Nowhere to flee!", showNormalAfter: false, returnToCombat: true);
                return;
            }

            _combatPanel.SetActive(false);
            UnsubscribeDoors();
            DestroyDoorConfirm();

            // The entry door is the one we came through — use it to flee back
            var fleeDoor = _entryDoor;
            EnableAllDoors(_currentRoom);

            player.PlaceInRoom(player.PreviousRoom);
            GameManager.Instance.EnterRoom(player.CurrentRoom, fleeDoor);
        }

        private void OnEntryDoorFlee(Door door)
        {
            OnFlee();
        }

        private void ShowCombatResult(string title, string message, bool showNormalAfter, bool returnToCombat = false)
        {
            _mainPanel.SetActive(false);
            _combatPanel.SetActive(false);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(true);
            _detailTitle.text = title;
            _detailMessage.text = message;

            _detailOkButton.onClick.RemoveAllListeners();
            _detailOkButton.onClick.AddListener(() =>
            {
                _detailPanel.SetActive(false);
                if (showNormalAfter)
                {
                    EnableAllDoors(_currentRoom);
                    _mainPanel.SetActive(true);
                    SubscribeDoors();
                }
                else if (returnToCombat)
                {
                    _combatPanel.SetActive(true);
                }
                // else: game over — panels stay hidden
            });
        }

        // ============================================================
        //  DOOR CLICK (always active while in a room)
        // ============================================================

        private void SetDoorsEnabled(Room room, Door excludeDoor)
        {
            if (room == null) return;
            foreach (var door in room.Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = excludeDoor == null || door == excludeDoor;
            }
        }

        private void EnableAllDoors(Room room)
        {
            if (room == null) return;
            foreach (var door in room.Doors)
            {
                var col = door.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = true;
            }
        }

        private void SubscribeDoors()
        {
            if (_currentRoom == null) return;
            foreach (var door in _currentRoom.Doors)
                door.OnDoorClicked += OnDoorSelected;
        }

        private void UnsubscribeDoors()
        {
            if (_currentRoom == null) return;
            foreach (var door in _currentRoom.Doors)
                door.OnDoorClicked -= OnDoorSelected;
            if (_entryDoor != null)
                _entryDoor.OnDoorClicked -= OnEntryDoorFlee;
        }

        private void OnDoorSelected(Door door)
        {
            _selectedDoor = door;
            ShowDoorConfirm(door);
        }

        private void ShowDoorConfirm(Door door)
        {
            DestroyDoorConfirm();

            _doorConfirmRoot = new GameObject("DoorConfirm");
            _doorConfirmRoot.transform.position = door.transform.position + Vector3.up * 1.2f;

            var canvas = _doorConfirmRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;
            _doorConfirmRoot.AddComponent<GraphicRaycaster>();

            var rt = _doorConfirmRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220, 80);
            rt.localScale = Vector3.one * 0.015f;

            var bg = _doorConfirmRoot.AddComponent<Image>();
            bg.color = PanelColor;

            var hlg = _doorConfirmRoot.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var confirmBtn = CreateButton(_doorConfirmRoot.transform, "Go");
            confirmBtn.onClick.AddListener(OnConfirmMove);

            var cancelBtn = CreateButton(_doorConfirmRoot.transform, "Cancel");
            cancelBtn.onClick.AddListener(OnCancelMove);
        }

        private void DestroyDoorConfirm()
        {
            if (_doorConfirmRoot != null)
            {
                Destroy(_doorConfirmRoot);
                _doorConfirmRoot = null;
            }
        }

        private void OnConfirmMove()
        {
            DestroyDoorConfirm();
            UnsubscribeDoors();

            var player = GameManager.Instance.Player;
            var fromRoom = _currentRoom;
            var usedDoor = _selectedDoor;
            player.PlaceAtDoor(usedDoor, fromRoom);

            EnableAllDoors(fromRoom);

            var destRoom = usedDoor.GetOtherRoom(fromRoom);
            _selectedDoor = null;

            GameManager.Instance.EnterRoom(destRoom, usedDoor);
        }

        private void OnCancelMove()
        {
            DestroyDoorConfirm();
            _selectedDoor = null;
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

        private void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
        }
    }
}
