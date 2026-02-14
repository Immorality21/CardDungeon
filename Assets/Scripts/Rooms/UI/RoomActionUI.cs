using System;
using System.Collections.Generic;
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
        private Button _moveButton;
        private Button _actionButton;

        // Sub panel for listing options (center)
        private GameObject _subPanel;
        private Transform _optionListParent;
        private Button _backButton;

        // Detail popup (replaces PopupManager)
        private GameObject _detailPanel;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailMessage;
        private Button _detailOkButton;

        // World-space door confirm
        private GameObject _doorConfirmRoot;

        private Room _currentRoom;
        private Door _selectedDoor;
        private List<Door> _highlightedDoors = new List<Door>();
        private List<GameObject> _spawnedOptions = new List<GameObject>();

        private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.18f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.32f, 1f);
        private static readonly Color ButtonHoverColor = new Color(0.30f, 0.30f, 0.42f, 1f);
        private static readonly Color AccentColor = new Color(0.35f, 0.65f, 0.95f, 1f);

        private void Awake()
        {
            BuildUI();
            HideAll();
        }

        public void Show(Room room)
        {
            _currentRoom = room;
            _mainPanel.SetActive(true);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(false);
            DestroyDoorConfirm();
        }

        public void Hide()
        {
            HideAll();
            UnhighlightAllDoors();
            DestroyDoorConfirm();
        }

        private void HideAll()
        {
            _mainPanel.SetActive(false);
            _subPanel.SetActive(false);
            _detailPanel.SetActive(false);
        }

        // ============================================================
        //  UI CONSTRUCTION
        // ============================================================

        private void BuildUI()
        {
            // Canvas
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
            BuildSubPanel(canvasObj.transform);
            BuildDetailPanel(canvasObj.transform);
        }

        // --- Main Panel (bottom center): Examine / Move / Action ---

        private void BuildMainPanel(Transform parent)
        {
            _mainPanel = CreatePanel(parent, "MainPanel", new Vector2(0, 0), new Vector2(420, 70));
            SetAnchors(_mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            _mainPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            var hlg = _mainPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            _examineButton = CreateButton(_mainPanel.transform, "Examine");
            _moveButton = CreateButton(_mainPanel.transform, "Move");
            _actionButton = CreateButton(_mainPanel.transform, "Action");

            _examineButton.onClick.AddListener(OnExamine);
            _moveButton.onClick.AddListener(OnMove);
            _actionButton.onClick.AddListener(OnAction);
        }

        // --- Sub Panel (center): scrollable option list + Back ---

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

            // Scroll content area
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

            // Back button
            _backButton = CreateButton(_subPanel.transform, "Back");
            var backLE = _backButton.gameObject.AddComponent<LayoutElement>();
            backLE.preferredHeight = 45;
            _backButton.onClick.AddListener(OnBack);
        }

        // --- Detail Panel (center): title, message, Ok ---

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
            _detailOkButton.onClick.AddListener(() =>
            {
                _detailPanel.SetActive(false);
                _subPanel.SetActive(true);
            });
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
        //  MOVE FLOW
        // ============================================================

        private void OnMove()
        {
            _mainPanel.SetActive(false);
            HighlightDoors();
        }

        private void HighlightDoors()
        {
            UnhighlightAllDoors();

            foreach (var door in _currentRoom.Doors)
            {
                door.Highlight();
                door.OnDoorClicked += OnDoorSelected;
                _highlightedDoors.Add(door);
            }
        }

        private void UnhighlightAllDoors()
        {
            foreach (var door in _highlightedDoors)
            {
                door.Unhighlight();
                door.OnDoorClicked -= OnDoorSelected;
            }
            _highlightedDoors.Clear();
        }

        private void OnDoorSelected(Door door)
        {
            _selectedDoor = door;
            ShowDoorConfirm(door);
        }

        private void ShowDoorConfirm(Door door)
        {
            DestroyDoorConfirm();

            // World-space canvas near the door
            _doorConfirmRoot = new GameObject("DoorConfirm");
            _doorConfirmRoot.transform.position = door.transform.position + Vector3.up * 1.2f;

            var canvas = _doorConfirmRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;
            _doorConfirmRoot.AddComponent<GraphicRaycaster>();

            var rt = _doorConfirmRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220, 80);
            rt.localScale = Vector3.one * 0.015f;

            // Background
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
            UnhighlightAllDoors();

            var player = GameManager.Instance.Player;
            var fromRoom = _currentRoom;
            player.PlaceAtDoor(_selectedDoor, fromRoom);

            var destRoom = _selectedDoor.GetOtherRoom(fromRoom);
            _selectedDoor = null;

            GameManager.Instance.EnterRoom(destRoom);
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
