using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Rooms;

public class RoomActionUISetup : Editor
{
    private static readonly Color TextColor = new Color(0.18f, 0.12f, 0.06f, 1f);
    private static readonly Color LightTextColor = new Color(0.95f, 0.88f, 0.72f, 1f);

    private static Sprite _parchmentSprite;
    private static Sprite _dungeonFrameSprite;
    private static Sprite _stoneButtonSprite;
    private static Sprite _stoneButtonHoverSprite;

    [MenuItem("Tools/Rooms/Setup Room Action UI")]
    public static void Setup()
    {
        var combatCanvas = GameObject.Find("CombatCanvas");
        if (combatCanvas == null)
        {
            Debug.LogError("CombatCanvas not found in scene. Open the game scene first.");
            return;
        }

        // Find or create RoomActionUI component
        var roomActionUI = combatCanvas.GetComponentInChildren<RoomActionUI>(true);
        if (roomActionUI != null)
        {
            Undo.DestroyObjectImmediate(roomActionUI.gameObject);
        }

        // Ensure CombatCanvas uses ScaleWithScreenSize
        var scaler = combatCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        LoadSprites();

        // Build option button prefab
        var optionBtnPrefab = BuildOptionButtonPrefab();

        // Root object under CombatCanvas
        var canvasObj = CreateUIObject("RoomActionUI", combatCanvas.transform);
        var rootRT = canvasObj.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        roomActionUI = canvasObj.AddComponent<RoomActionUI>();

        // === MAIN PANEL (bottom center bar) ===
        var mainPanel = CreateActionBar("MainPanel", canvasObj.transform, new Vector2(300, 70));
        var mainHLG = mainPanel.AddComponent<HorizontalLayoutGroup>();
        mainHLG.spacing = 10;
        mainHLG.padding = new RectOffset(10, 10, 10, 10);
        mainHLG.childAlignment = TextAnchor.MiddleCenter;
        mainHLG.childForceExpandWidth = true;
        mainHLG.childForceExpandHeight = true;

        var examineBtn = CreateStoneButton("ExamineBtn", mainPanel.transform, "Examine");
        var actionBtn = CreateStoneButton("ActionBtn", mainPanel.transform, "Action");
        mainPanel.SetActive(false);

        // === COMBAT PANEL (bottom center bar) ===
        var combatPanel = CreateActionBar("CombatPanel", canvasObj.transform, new Vector2(300, 70));
        var combatHLG = combatPanel.AddComponent<HorizontalLayoutGroup>();
        combatHLG.spacing = 10;
        combatHLG.padding = new RectOffset(10, 10, 10, 10);
        combatHLG.childAlignment = TextAnchor.MiddleCenter;
        combatHLG.childForceExpandWidth = true;
        combatHLG.childForceExpandHeight = true;

        var fightBtn = CreateStoneButton("FightBtn", combatPanel.transform, "Fight");
        var fleeBtn = CreateStoneButton("FleeBtn", combatPanel.transform, "Flee");
        combatPanel.SetActive(false);

        // === HERO ACTION PANEL (bottom center bar, wider) ===
        var heroPanel = CreateActionBar("HeroActionPanel", canvasObj.transform, new Vector2(460, 100));
        var heroVLG = heroPanel.AddComponent<VerticalLayoutGroup>();
        heroVLG.spacing = 6;
        heroVLG.padding = new RectOffset(10, 10, 6, 6);
        heroVLG.childAlignment = TextAnchor.MiddleCenter;
        heroVLG.childForceExpandWidth = true;
        heroVLG.childForceExpandHeight = false;

        var heroLabel = CreateLabel("HeroActionLabel", heroPanel.transform, "", 16);
        heroLabel.GetComponent<TextMeshProUGUI>().color = LightTextColor;
        var heroLabelLE = heroLabel.AddComponent<LayoutElement>();
        heroLabelLE.preferredHeight = 24;

        var buttonRow = CreateUIObject("ButtonRow", heroPanel.transform);
        var rowHLG = buttonRow.AddComponent<HorizontalLayoutGroup>();
        rowHLG.spacing = 10;
        rowHLG.childAlignment = TextAnchor.MiddleCenter;
        rowHLG.childForceExpandWidth = true;
        rowHLG.childForceExpandHeight = true;
        var rowLE = buttonRow.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 42;

        var attackBtn = CreateStoneButton("AttackBtn", buttonRow.transform, "Attack");
        var cardsBtn = CreateStoneButton("CardsBtn", buttonRow.transform, "Cards");
        var skipBtn = CreateStoneButton("SkipBtn", buttonRow.transform, "Skip");
        heroPanel.SetActive(false);

        // === SUB PANEL (center, framed) ===
        var subPanel = CreateFramedPanel("SubPanel", canvasObj.transform, new Vector2(400, 350));
        var subInner = subPanel.transform.Find("ParchmentBg");
        var subVLG = subInner.gameObject.AddComponent<VerticalLayoutGroup>();
        subVLG.spacing = 6;
        subVLG.padding = new RectOffset(12, 12, 12, 12);
        subVLG.childAlignment = TextAnchor.UpperCenter;
        subVLG.childForceExpandWidth = true;
        subVLG.childForceExpandHeight = false;

        var optionList = CreateUIObject("OptionList", subInner);
        var optionListRT = optionList.GetComponent<RectTransform>();
        optionListRT.sizeDelta = new Vector2(0, 240);
        var optListVLG = optionList.AddComponent<VerticalLayoutGroup>();
        optListVLG.spacing = 6;
        optListVLG.childForceExpandWidth = true;
        optListVLG.childForceExpandHeight = false;
        optListVLG.childAlignment = TextAnchor.UpperCenter;
        var optListCSF = optionList.AddComponent<ContentSizeFitter>();
        optListCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var optListLE = optionList.AddComponent<LayoutElement>();
        optListLE.flexibleHeight = 1;

        var subBackBtn = CreateStoneButton("BackBtn", subInner, "Back");
        var subBackLE = subBackBtn.AddComponent<LayoutElement>();
        subBackLE.preferredHeight = 45;
        subPanel.SetActive(false);

        // === DETAIL PANEL (center, framed) ===
        var detailPanel = CreateFramedPanel("DetailPanel", canvasObj.transform, new Vector2(450, 260));
        var detailInner = detailPanel.transform.Find("ParchmentBg");
        var detailVLG = detailInner.gameObject.AddComponent<VerticalLayoutGroup>();
        detailVLG.spacing = 10;
        detailVLG.padding = new RectOffset(16, 16, 16, 16);
        detailVLG.childAlignment = TextAnchor.UpperCenter;
        detailVLG.childForceExpandWidth = true;
        detailVLG.childForceExpandHeight = false;

        var detailTitle = CreateLabel("DetailTitle", detailInner, "Title", 22);
        detailTitle.GetComponent<TextMeshProUGUI>().color = TextColor;
        var titleLE = detailTitle.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 30;

        var detailMessage = CreateLabel("DetailMessage", detailInner, "", 17);
        var msgTMP = detailMessage.GetComponent<TextMeshProUGUI>();
        msgTMP.color = TextColor;
        msgTMP.fontStyle = FontStyles.Normal;
        msgTMP.alignment = TextAlignmentOptions.TopLeft;
        var msgLE = detailMessage.AddComponent<LayoutElement>();
        msgLE.flexibleHeight = 1;

        var detailOkBtn = CreateStoneButton("OkBtn", detailInner, "Ok");
        var okLE = detailOkBtn.AddComponent<LayoutElement>();
        okLE.preferredHeight = 45;
        detailPanel.SetActive(false);

        // === Wire serialized fields ===
        var so = new SerializedObject(roomActionUI);
        so.FindProperty("_mainPanel").objectReferenceValue = mainPanel;
        so.FindProperty("_examineButton").objectReferenceValue = examineBtn.GetComponent<Button>();
        so.FindProperty("_actionButton").objectReferenceValue = actionBtn.GetComponent<Button>();
        so.FindProperty("_combatPanel").objectReferenceValue = combatPanel;
        so.FindProperty("_fightButton").objectReferenceValue = fightBtn.GetComponent<Button>();
        so.FindProperty("_fleeButton").objectReferenceValue = fleeBtn.GetComponent<Button>();
        so.FindProperty("_heroActionPanel").objectReferenceValue = heroPanel;
        so.FindProperty("_heroActionLabel").objectReferenceValue = heroLabel.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_attackButton").objectReferenceValue = attackBtn.GetComponent<Button>();
        so.FindProperty("_cardsButton").objectReferenceValue = cardsBtn.GetComponent<Button>();
        so.FindProperty("_skipButton").objectReferenceValue = skipBtn.GetComponent<Button>();
        so.FindProperty("_subPanel").objectReferenceValue = subPanel;
        so.FindProperty("_optionListParent").objectReferenceValue = optionList.transform;
        so.FindProperty("_backButton").objectReferenceValue = subBackBtn.GetComponent<Button>();
        so.FindProperty("_optionButtonPrefab").objectReferenceValue = optionBtnPrefab;
        so.FindProperty("_detailPanel").objectReferenceValue = detailPanel;
        so.FindProperty("_detailTitle").objectReferenceValue = detailTitle.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_detailMessage").objectReferenceValue = msgTMP;
        so.FindProperty("_detailOkButton").objectReferenceValue = detailOkBtn.GetComponent<Button>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(canvasObj, "Setup Room Action UI");
        EditorUtility.SetDirty(combatCanvas);

        Debug.Log("RoomActionUI set up with fantasy theme under CombatCanvas. Save the scene to persist.");
    }

    private static GameObject BuildOptionButtonPrefab()
    {
        var obj = CreateUIObject("OptionButton", null);
        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 42);

        obj.AddComponent<CanvasRenderer>();
        var img = obj.AddComponent<Image>();
        img.sprite = _stoneButtonSprite;
        img.type = Image.Type.Sliced;

        var btn = obj.AddComponent<Button>();
        var spriteState = new SpriteState();
        spriteState.highlightedSprite = _stoneButtonHoverSprite;
        spriteState.pressedSprite = _stoneButtonHoverSprite;
        spriteState.selectedSprite = _stoneButtonHoverSprite;
        btn.spriteState = spriteState;
        btn.transition = Selectable.Transition.SpriteSwap;

        var textObj = CreateUIObject("Text", obj.transform);
        textObj.AddComponent<CanvasRenderer>();
        var textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8, 2);
        textRT.offsetMax = new Vector2(-8, -2);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Option";
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LightTextColor;
        tmp.alignment = TextAlignmentOptions.Center;

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 42;

        // Save as prefab
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI/Rooms"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Rooms");
        }

        var prefabPath = "Assets/Prefabs/UI/Rooms/OptionButton.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
        DestroyImmediate(obj);

        Debug.Log("OptionButton.prefab created at " + prefabPath);
        return prefab;
    }

    // ============================================================
    //  UI Construction Helpers
    // ============================================================

    private static GameObject CreateActionBar(string name, Transform parent, Vector2 size)
    {
        var panel = CreateUIObject(name, parent);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 60);
        rt.sizeDelta = size;

        panel.AddComponent<CanvasRenderer>();
        var img = panel.AddComponent<Image>();
        img.sprite = _stoneButtonSprite;
        img.type = Image.Type.Sliced;

        return panel;
    }

    private static GameObject CreateFramedPanel(string name, Transform parent, Vector2 size)
    {
        var frame = CreateUIObject(name, parent);
        var frameRT = frame.GetComponent<RectTransform>();
        frameRT.anchorMin = new Vector2(0.5f, 0.5f);
        frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = Vector2.zero;
        frameRT.sizeDelta = size;

        frame.AddComponent<CanvasRenderer>();
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = _dungeonFrameSprite;
        frameImg.type = Image.Type.Sliced;

        var inner = CreateUIObject("ParchmentBg", frame.transform);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.04f, 0.04f);
        innerRT.anchorMax = new Vector2(0.96f, 0.96f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        inner.AddComponent<CanvasRenderer>();
        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = _parchmentSprite;
        innerImg.type = Image.Type.Tiled;
        innerImg.pixelsPerUnitMultiplier = 2f;

        return frame;
    }

    private static GameObject CreateStoneButton(string name, Transform parent, string label)
    {
        var obj = CreateUIObject(name, parent);
        obj.AddComponent<CanvasRenderer>();
        var img = obj.AddComponent<Image>();
        img.sprite = _stoneButtonSprite;
        img.type = Image.Type.Sliced;

        var btn = obj.AddComponent<Button>();
        var spriteState = new SpriteState();
        spriteState.highlightedSprite = _stoneButtonHoverSprite;
        spriteState.pressedSprite = _stoneButtonHoverSprite;
        spriteState.selectedSprite = _stoneButtonHoverSprite;
        btn.spriteState = spriteState;
        btn.transition = Selectable.Transition.SpriteSwap;

        var textObj = CreateUIObject("Text", obj.transform);
        textObj.AddComponent<CanvasRenderer>();
        var textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8, 2);
        textRT.offsetMax = new Vector2(-8, -2);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LightTextColor;
        tmp.alignment = TextAlignmentOptions.Center;

        return obj;
    }

    private static GameObject CreateLabel(string name, Transform parent, string text, int fontSize)
    {
        var obj = CreateUIObject(name, parent);
        obj.AddComponent<CanvasRenderer>();
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LightTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    private static void LoadSprites()
    {
        _parchmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ParchmentPanel.png");
        _dungeonFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DungeonFrame.png");
        _stoneButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButton.png");
        _stoneButtonHoverSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButtonHover.png");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var obj = new GameObject(name);
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }
        obj.layer = 5;
        obj.AddComponent<RectTransform>();
        return obj;
    }
}
