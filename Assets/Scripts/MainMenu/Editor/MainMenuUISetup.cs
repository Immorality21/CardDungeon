using Assets.Scripts.Cards.UI;
using Assets.Scripts.Dungeon;
using Assets.Scripts.MainMenu;
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
        DestroyChildByName(canvas.transform, "MerchantPanel");
        DestroyChildByName(canvas.transform, "ForgePanel");
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

        var continueBtn = CreateStoneButton("ContinueRunButton", buttonArea.transform, "Continue Run", 52);
        var newRunBtn = CreateStoneButton("NewRunButton", buttonArea.transform, "New Run", 52);
        var merchantBtn = CreateStoneButton("MerchantButton", buttonArea.transform, "Visit Merchant", 52);
        var forgeBtn = CreateStoneButton("ForgeButton", buttonArea.transform, "Magic Forge", 52);

        // Currency header (bottom strip of home panel)
        var currencyArea = CreateUIObject("CurrencyHeader", homePanel.transform);
        var currencyRT = currencyArea.GetComponent<RectTransform>();
        currencyRT.anchorMin = new Vector2(0.1f, 0.01f);
        currencyRT.anchorMax = new Vector2(0.9f, 0.09f);
        currencyRT.offsetMin = Vector2.zero;
        currencyRT.offsetMax = Vector2.zero;
        var currencyHLG = currencyArea.AddComponent<HorizontalLayoutGroup>();
        currencyHLG.childAlignment = TextAnchor.MiddleCenter;
        currencyHLG.childForceExpandWidth = true;
        currencyHLG.childForceExpandHeight = true;
        currencyHLG.childControlWidth = true;
        currencyHLG.childControlHeight = true;

        var homeGoldLabel = CreateLabel("GoldLabel", currencyArea.transform, "Gold: 0", 18);
        homeGoldLabel.GetComponent<TextMeshProUGUI>().color = TextColor;
        var homeEssenceLabel = CreateLabel("EssenceLabel", currencyArea.transform, "Essence: 0", 18);
        homeEssenceLabel.GetComponent<TextMeshProUGUI>().color = TextColor;

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

        // === MERCHANT PANEL ===
        var merchantPanel = CreateFramedPanel("MerchantPanel", canvas.transform, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.9f));
        merchantPanel.SetActive(false);

        var merchantTitle = CreateLabel("MerchantTitle", merchantPanel.transform, "Merchant", 34);
        AnchorStretchTop(merchantTitle, 0.82f, 0.95f);
        merchantTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        var merchantGold = CreateLabel("MerchantGoldLabel", merchantPanel.transform, "Gold: 0", 18);
        AnchorStretchTop(merchantGold, 0.74f, 0.81f);
        merchantGold.GetComponent<TextMeshProUGUI>().color = TextColor;

        var merchantEssence = CreateLabel("MerchantEssenceLabel", merchantPanel.transform, "Essence: 0", 16);
        AnchorStretchTop(merchantEssence, 0.68f, 0.74f);
        merchantEssence.GetComponent<TextMeshProUGUI>().color = TextColor;

        var potionRow = CreateOfferRow("PotionOffer", merchantPanel.transform, 0.5f, 0.66f,
            "Enlarge Potion Belt", out var potionLabel, out var potionBuyBtn);

        var merchantFeedback = CreateLabel("MerchantFeedback", merchantPanel.transform, "", 16);
        AnchorStretchTop(merchantFeedback, 0.2f, 0.3f);
        merchantFeedback.GetComponent<TextMeshProUGUI>().color = new Color(0.1f, 0.4f, 0.1f, 1f);

        var merchantClose = CreateStoneButton("MerchantCloseButton", merchantPanel.transform, "Back", 50);
        var merchantCloseRT = merchantClose.GetComponent<RectTransform>();
        merchantCloseRT.anchorMin = new Vector2(0.3f, 0.06f);
        merchantCloseRT.anchorMax = new Vector2(0.7f, 0.16f);
        merchantCloseRT.offsetMin = Vector2.zero;
        merchantCloseRT.offsetMax = Vector2.zero;

        var merchantUI = canvas.GetComponent<MerchantUI>();
        if (merchantUI == null)
        {
            merchantUI = canvas.AddComponent<MerchantUI>();
        }
        var merchantSO = new SerializedObject(merchantUI);
        merchantSO.FindProperty("_rootPanel").objectReferenceValue = merchantPanel;
        merchantSO.FindProperty("_goldLabel").objectReferenceValue = merchantGold.GetComponent<TextMeshProUGUI>();
        merchantSO.FindProperty("_essenceLabel").objectReferenceValue = merchantEssence.GetComponent<TextMeshProUGUI>();
        merchantSO.FindProperty("_potionButton").objectReferenceValue = potionBuyBtn;
        merchantSO.FindProperty("_potionLabel").objectReferenceValue = potionLabel;
        merchantSO.FindProperty("_feedbackLabel").objectReferenceValue = merchantFeedback.GetComponent<TextMeshProUGUI>();
        merchantSO.FindProperty("_closeButton").objectReferenceValue = merchantClose.GetComponent<Button>();
        merchantSO.ApplyModifiedProperties();

        // === FORGE PANEL (magic upgrades) ===
        var forgePanel = CreateFramedPanel("ForgePanel", canvas.transform, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.9f));
        forgePanel.SetActive(false);

        var forgeTitle = CreateLabel("ForgeTitle", forgePanel.transform, "Magic Forge", 34);
        AnchorStretchTop(forgeTitle, 0.85f, 0.96f);
        forgeTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        var forgeEssence = CreateLabel("ForgeEssenceLabel", forgePanel.transform, "Essence: 0", 18);
        AnchorStretchTop(forgeEssence, 0.78f, 0.85f);
        forgeEssence.GetComponent<TextMeshProUGUI>().color = TextColor;

        // Scrollable-ish list container (vertical layout) for card rows
        var listArea = CreateUIObject("CardListArea", forgePanel.transform);
        var listRT = listArea.GetComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0.08f, 0.18f);
        listRT.anchorMax = new Vector2(0.92f, 0.77f);
        listRT.offsetMin = Vector2.zero;
        listRT.offsetMax = Vector2.zero;
        var listVLG = listArea.AddComponent<VerticalLayoutGroup>();
        listVLG.spacing = 8;
        listVLG.padding = new RectOffset(8, 8, 8, 8);
        listVLG.childAlignment = TextAnchor.UpperCenter;
        listVLG.childForceExpandWidth = true;
        listVLG.childForceExpandHeight = false;
        listVLG.childControlWidth = true;
        listVLG.childControlHeight = false;

        var forgeEmpty = CreateLabel("ForgeEmptyLabel", forgePanel.transform,
            "No magic in the catalog yet.", 16);
        AnchorStretchTop(forgeEmpty, 0.45f, 0.6f);
        forgeEmpty.GetComponent<TextMeshProUGUI>().color = TextColor;

        var forgeRowTemplate = CreateForgeRowTemplate(listArea.transform);

        var forgeClose = CreateStoneButton("ForgeCloseButton", forgePanel.transform, "Back", 50);
        var forgeCloseRT = forgeClose.GetComponent<RectTransform>();
        forgeCloseRT.anchorMin = new Vector2(0.3f, 0.06f);
        forgeCloseRT.anchorMax = new Vector2(0.7f, 0.15f);
        forgeCloseRT.offsetMin = Vector2.zero;
        forgeCloseRT.offsetMax = Vector2.zero;

        var forgeUI = canvas.GetComponent<MagicForgeUI>();
        if (forgeUI == null)
        {
            forgeUI = canvas.AddComponent<MagicForgeUI>();
        }
        var forgeSO = new SerializedObject(forgeUI);
        forgeSO.FindProperty("_rootPanel").objectReferenceValue = forgePanel;
        forgeSO.FindProperty("_essenceLabel").objectReferenceValue = forgeEssence.GetComponent<TextMeshProUGUI>();
        forgeSO.FindProperty("_rowParent").objectReferenceValue = listArea.transform;
        forgeSO.FindProperty("_rowTemplate").objectReferenceValue = forgeRowTemplate;
        forgeSO.FindProperty("_emptyLabel").objectReferenceValue = forgeEmpty.GetComponent<TextMeshProUGUI>();
        forgeSO.FindProperty("_closeButton").objectReferenceValue = forgeClose.GetComponent<Button>();
        forgeSO.ApplyModifiedProperties();

        // === WIRE SERIALIZED FIELDS ===
        var so = new SerializedObject(existing);

        if (runDef != null)
        {
            so.FindProperty("_runDefinition").objectReferenceValue = runDef;
        }

        so.FindProperty("_homePanel").objectReferenceValue = homePanel;
        so.FindProperty("_continueRunButton").objectReferenceValue = continueBtn.GetComponent<Button>();
        so.FindProperty("_newRunButton").objectReferenceValue = newRunBtn.GetComponent<Button>();

        so.FindProperty("_runProgressPanel").objectReferenceValue = runProgressPanel;
        so.FindProperty("_levelIndicatorLabel").objectReferenceValue = levelIndTMP;
        so.FindProperty("_levelNameLabel").objectReferenceValue = levelNameTMP;
        so.FindProperty("_enterDungeonButton").objectReferenceValue = enterBtn.GetComponent<Button>();
        so.FindProperty("_backButton").objectReferenceValue = backBtn.GetComponent<Button>();

        so.FindProperty("_runCompletePanel").objectReferenceValue = runCompletePanel;
        so.FindProperty("_runCompleteReturnButton").objectReferenceValue = returnBtn.GetComponent<Button>();

        so.FindProperty("_merchantButton").objectReferenceValue = merchantBtn.GetComponent<Button>();
        so.FindProperty("_merchantUI").objectReferenceValue = merchantUI;
        so.FindProperty("_forgeButton").objectReferenceValue = forgeBtn.GetComponent<Button>();
        so.FindProperty("_cardUpgradeUI").objectReferenceValue = forgeUI;
        so.FindProperty("_goldLabel").objectReferenceValue = homeGoldLabel.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_essenceLabel").objectReferenceValue = homeEssenceLabel.GetComponent<TextMeshProUGUI>();

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

    private static void AnchorStretchTop(GameObject go, float minY, float maxY)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, minY);
        rt.anchorMax = new Vector2(0.95f, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateOfferRow(
        string name,
        Transform parent,
        float anchorMinY,
        float anchorMaxY,
        string defaultLabel,
        out TextMeshProUGUI infoLabel,
        out Button buyButton)
    {
        var row = CreateUIObject(name, parent);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.06f, anchorMinY);
        rowRT.anchorMax = new Vector2(0.94f, anchorMaxY);
        rowRT.offsetMin = Vector2.zero;
        rowRT.offsetMax = Vector2.zero;

        var labelObj = CreateLabel("InfoLabel", row.transform, defaultLabel, 16);
        var labelRT = labelObj.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(0.64f, 1f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelObj.GetComponent<TextMeshProUGUI>();
        labelTMP.color = TextColor;
        labelTMP.fontStyle = FontStyles.Normal;
        labelTMP.alignment = TextAlignmentOptions.Left;
        infoLabel = labelTMP;

        var buyObj = CreateStoneButton("BuyButton", row.transform, "Buy", 44);
        var buyRT = buyObj.GetComponent<RectTransform>();
        buyRT.anchorMin = new Vector2(0.67f, 0.1f);
        buyRT.anchorMax = new Vector2(1f, 0.9f);
        buyRT.offsetMin = Vector2.zero;
        buyRT.offsetMax = Vector2.zero;
        buyButton = buyObj.GetComponent<Button>();

        return row;
    }

    private static GameObject CreateForgeRowTemplate(Transform parent)
    {
        var row = CreateUIObject("CardUpgradeRow", parent);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 54;
        le.minHeight = 54;

        var bg = row.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.12f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.padding = new RectOffset(10, 10, 4, 4);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var nameObj = CreateLabel("NameLabel", row.transform, "Card", 16);
        var nameTMP = nameObj.GetComponent<TextMeshProUGUI>();
        nameTMP.color = TextColor;
        nameTMP.alignment = TextAlignmentOptions.Left;
        var nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.preferredWidth = 150;
        nameLE.minWidth = 120;

        var infoObj = CreateLabel("InfoLabel", row.transform, "Lv 0", 14);
        var infoTMP = infoObj.GetComponent<TextMeshProUGUI>();
        infoTMP.color = TextColor;
        infoTMP.fontStyle = FontStyles.Normal;
        infoTMP.alignment = TextAlignmentOptions.Left;
        var infoLE = infoObj.AddComponent<LayoutElement>();
        infoLE.flexibleWidth = 1;

        var upgradeBtn = CreateStoneButton("UpgradeButton", row.transform, "Upgrade", 40);
        var upgradeLE = upgradeBtn.GetComponent<LayoutElement>();
        upgradeLE.preferredWidth = 130;
        upgradeLE.minWidth = 110;

        row.SetActive(false);
        return row;
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
