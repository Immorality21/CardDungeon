using Assets.Scripts.Dungeon;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUISetup : Editor
{
    private static Sprite _parchmentSprite;
    private static Sprite _stoneButtonSprite;
    private static Sprite _stoneButtonHoverSprite;
    private static Sprite _dungeonFrameSprite;
    private static Sprite _titleBannerSprite;

    private static readonly Color TextColor = new Color(0.18f, 0.12f, 0.06f, 1f);
    private static readonly Color LightTextColor = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color DarkBg = new Color(0.08f, 0.06f, 0.1f, 0.96f);

    [MenuItem("Tools/MainMenu/Setup Main Menu UI")]
    public static void Setup()
    {
        var canvas = GameObject.Find("MainMenuCanvas");
        if (canvas == null)
        {
            Debug.LogError("MainMenuCanvas not found in scene. Open the MenuScene first.");
            return;
        }

        var existing = canvas.GetComponent<MainMenuManager>();
        if (existing == null)
        {
            existing = canvas.AddComponent<MainMenuManager>();
        }

        // Load sprites
        _parchmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ParchmentPanel.png");
        _stoneButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButton.png");
        _stoneButtonHoverSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButtonHover.png");
        _dungeonFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DungeonFrame.png");
        _titleBannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/TitleBanner.png");

        // Load RunDefinition
        var runDefGuids = AssetDatabase.FindAssets("t:RunDefinitionSO");
        RunDefinitionSO runDef = null;
        if (runDefGuids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(runDefGuids[0]);
            runDef = AssetDatabase.LoadAssetAtPath<RunDefinitionSO>(path);
        }

        // Clean up old panels if they exist
        DestroyChildByName(canvas.transform, "HomePanel");
        DestroyChildByName(canvas.transform, "RunProgressPanel");
        DestroyChildByName(canvas.transform, "RunCompletePanel");
        DestroyChildByName(canvas.transform, "BackgroundOverlay");

        // === DARK BACKGROUND ===
        var bg = CreateUIObject("BackgroundOverlay", canvas.transform);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = DarkBg;

        // === HOME PANEL ===
        var homePanel = CreateFramedPanel("HomePanel", canvas.transform, new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f));

        // Title banner
        var titleArea = CreateUIObject("TitleArea", homePanel.transform);
        var titleRT = titleArea.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.72f);
        titleRT.anchorMax = new Vector2(0.95f, 0.95f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        var titleImg = titleArea.AddComponent<Image>();
        titleImg.sprite = _titleBannerSprite;
        titleImg.type = Image.Type.Sliced;
        titleImg.pixelsPerUnitMultiplier = 1f;

        var titleLabel = CreateLabel("TitleLabel", titleArea.transform, "Card Dungeon", 32);
        var titleLabelRT = titleLabel.GetComponent<RectTransform>();
        titleLabelRT.anchorMin = Vector2.zero;
        titleLabelRT.anchorMax = Vector2.one;
        titleLabelRT.offsetMin = Vector2.zero;
        titleLabelRT.offsetMax = Vector2.zero;
        var titleTMP = titleLabel.GetComponent<TextMeshProUGUI>();
        titleTMP.color = LightTextColor;

        // Button area
        var buttonArea = CreateUIObject("ButtonArea", homePanel.transform);
        var buttonAreaRT = buttonArea.GetComponent<RectTransform>();
        buttonAreaRT.anchorMin = new Vector2(0.15f, 0.1f);
        buttonAreaRT.anchorMax = new Vector2(0.85f, 0.68f);
        buttonAreaRT.offsetMin = Vector2.zero;
        buttonAreaRT.offsetMax = Vector2.zero;
        var buttonVLG = buttonArea.AddComponent<VerticalLayoutGroup>();
        buttonVLG.spacing = 16;
        buttonVLG.padding = new RectOffset(20, 20, 20, 20);
        buttonVLG.childAlignment = TextAnchor.MiddleCenter;
        buttonVLG.childForceExpandWidth = true;
        buttonVLG.childForceExpandHeight = false;
        buttonVLG.childControlWidth = true;
        buttonVLG.childControlHeight = false;

        var continueBtn = CreateStoneButton("ContinueRunButton", buttonArea.transform, "Continue Run", 60);
        var newRunBtn = CreateStoneButton("NewRunButton", buttonArea.transform, "New Run", 60);
        var manageDeckBtn = CreateStoneButton("ManageDeckButton", buttonArea.transform, "Manage Deck", 60);

        // === RUN PROGRESS PANEL ===
        var runProgressPanel = CreateFramedPanel("RunProgressPanel", canvas.transform, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f));
        runProgressPanel.SetActive(false);

        // Level indicator at top
        var levelIndicator = CreateLabel("LevelIndicatorLabel", runProgressPanel.transform, "Level 1 of 11", 20);
        var levelIndRT = levelIndicator.GetComponent<RectTransform>();
        levelIndRT.anchorMin = new Vector2(0.05f, 0.82f);
        levelIndRT.anchorMax = new Vector2(0.95f, 0.95f);
        levelIndRT.offsetMin = Vector2.zero;
        levelIndRT.offsetMax = Vector2.zero;
        var levelIndTMP = levelIndicator.GetComponent<TextMeshProUGUI>();
        levelIndTMP.color = TextColor;
        levelIndTMP.fontStyle = FontStyles.Normal;
        levelIndTMP.fontSize = 20;

        // Level name (large, centered)
        var levelName = CreateLabel("LevelNameLabel", runProgressPanel.transform, "The Tutorial", 36);
        var levelNameRT = levelName.GetComponent<RectTransform>();
        levelNameRT.anchorMin = new Vector2(0.05f, 0.5f);
        levelNameRT.anchorMax = new Vector2(0.95f, 0.78f);
        levelNameRT.offsetMin = Vector2.zero;
        levelNameRT.offsetMax = Vector2.zero;
        var levelNameTMP = levelName.GetComponent<TextMeshProUGUI>();
        levelNameTMP.color = TextColor;

        // Bottom buttons
        var progressBtnArea = CreateUIObject("ProgressButtonArea", runProgressPanel.transform);
        var progressBtnRT = progressBtnArea.GetComponent<RectTransform>();
        progressBtnRT.anchorMin = new Vector2(0.1f, 0.05f);
        progressBtnRT.anchorMax = new Vector2(0.9f, 0.25f);
        progressBtnRT.offsetMin = Vector2.zero;
        progressBtnRT.offsetMax = Vector2.zero;
        var progressHLG = progressBtnArea.AddComponent<HorizontalLayoutGroup>();
        progressHLG.spacing = 20;
        progressHLG.childAlignment = TextAnchor.MiddleCenter;
        progressHLG.childForceExpandWidth = true;
        progressHLG.childForceExpandHeight = true;
        progressHLG.childControlWidth = true;
        progressHLG.childControlHeight = true;

        var backBtn = CreateStoneButton("BackButton", progressBtnArea.transform, "Back", 50);
        var enterBtn = CreateStoneButton("EnterDungeonButton", progressBtnArea.transform, "Enter Dungeon", 50);

        // === RUN COMPLETE PANEL ===
        var runCompletePanel = CreateFramedPanel("RunCompletePanel", canvas.transform, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.85f));
        runCompletePanel.SetActive(false);

        var victoryLabel = CreateLabel("VictoryLabel", runCompletePanel.transform, "Victory!", 40);
        var victoryRT = victoryLabel.GetComponent<RectTransform>();
        victoryRT.anchorMin = new Vector2(0.05f, 0.55f);
        victoryRT.anchorMax = new Vector2(0.95f, 0.9f);
        victoryRT.offsetMin = Vector2.zero;
        victoryRT.offsetMax = Vector2.zero;
        var victoryTMP = victoryLabel.GetComponent<TextMeshProUGUI>();
        victoryTMP.color = TextColor;

        var victoryMsg = CreateLabel("VictoryMessage", runCompletePanel.transform, "You have conquered the dungeon.\nYour heroes stand victorious.", 18);
        var victoryMsgRT = victoryMsg.GetComponent<RectTransform>();
        victoryMsgRT.anchorMin = new Vector2(0.1f, 0.3f);
        victoryMsgRT.anchorMax = new Vector2(0.9f, 0.55f);
        victoryMsgRT.offsetMin = Vector2.zero;
        victoryMsgRT.offsetMax = Vector2.zero;
        var victoryMsgTMP = victoryMsg.GetComponent<TextMeshProUGUI>();
        victoryMsgTMP.color = TextColor;
        victoryMsgTMP.fontStyle = FontStyles.Normal;
        victoryMsgTMP.fontSize = 18;

        var returnBtn = CreateStoneButton("ReturnButton", runCompletePanel.transform, "Return to Menu", 50);
        var returnBtnRT = returnBtn.GetComponent<RectTransform>();
        returnBtnRT.anchorMin = new Vector2(0.25f, 0.08f);
        returnBtnRT.anchorMax = new Vector2(0.75f, 0.22f);
        returnBtnRT.offsetMin = Vector2.zero;
        returnBtnRT.offsetMax = Vector2.zero;

        // === WIRE SERIALIZED FIELDS ===
        var so = new SerializedObject(existing);

        if (runDef != null)
        {
            so.FindProperty("_runDefinition").objectReferenceValue = runDef;
        }

        so.FindProperty("_homePanel").objectReferenceValue = homePanel;
        so.FindProperty("_continueRunButton").objectReferenceValue = continueBtn.GetComponent<Button>();
        so.FindProperty("_newRunButton").objectReferenceValue = newRunBtn.GetComponent<Button>();
        so.FindProperty("_manageDeckButton").objectReferenceValue = manageDeckBtn.GetComponent<Button>();

        so.FindProperty("_runProgressPanel").objectReferenceValue = runProgressPanel;
        so.FindProperty("_levelIndicatorLabel").objectReferenceValue = levelIndTMP;
        so.FindProperty("_levelNameLabel").objectReferenceValue = levelNameTMP;
        so.FindProperty("_enterDungeonButton").objectReferenceValue = enterBtn.GetComponent<Button>();
        so.FindProperty("_backButton").objectReferenceValue = backBtn.GetComponent<Button>();

        so.FindProperty("_runCompletePanel").objectReferenceValue = runCompletePanel;
        so.FindProperty("_runCompleteReturnButton").objectReferenceValue = returnBtn.GetComponent<Button>();

        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(bg, "Setup Main Menu UI");
        EditorUtility.SetDirty(canvas);

        Debug.Log("Main Menu UI created under MainMenuCanvas. Save the scene to persist.");
    }

    private static GameObject CreateFramedPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        // Outer frame with dungeon border
        var frame = CreateUIObject(name, parent);
        var frameRT = frame.GetComponent<RectTransform>();
        frameRT.anchorMin = anchorMin;
        frameRT.anchorMax = anchorMax;
        frameRT.offsetMin = Vector2.zero;
        frameRT.offsetMax = Vector2.zero;
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = _dungeonFrameSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.pixelsPerUnitMultiplier = 1f;

        // Inner parchment background (inset from frame)
        var inner = CreateUIObject("ParchmentBg", frame.transform);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.06f, 0.06f);
        innerRT.anchorMax = new Vector2(0.94f, 0.94f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = _parchmentSprite;
        innerImg.type = Image.Type.Tiled;
        innerImg.pixelsPerUnitMultiplier = 2f;

        return frame;
    }

    private static GameObject CreateStoneButton(string name, Transform parent, string label, int height)
    {
        var obj = CreateUIObject(name, parent);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        obj.AddComponent<CanvasRenderer>();
        var img = obj.AddComponent<Image>();
        img.sprite = _stoneButtonSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;

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
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LightTextColor;
        tmp.alignment = TextAlignmentOptions.Center;

        return obj;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.layer = 5;
        obj.AddComponent<RectTransform>();
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
        tmp.color = TextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return obj;
    }

    private static void DestroyChildByName(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            DestroyImmediate(child.gameObject);
        }
    }
}
