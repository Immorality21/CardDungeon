using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Cards.UI;
using Assets.Scripts.Heroes;

public class DeckManagementUISetup : Editor
{
    private static readonly Color TextColor = new Color(0.18f, 0.12f, 0.06f, 1f);
    private static readonly Color LightTextColor = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color SubPanelColor = new Color(0.16f, 0.12f, 0.08f, 0.35f);
    private static readonly Color CardBgColor = new Color(0.25f, 0.18f, 0.10f, 1f);
    private static readonly Color EffectsTextColor = new Color(0.75f, 0.65f, 0.45f, 1f);

    private static Sprite _parchmentSprite;
    private static Sprite _dungeonFrameSprite;
    private static Sprite _stoneButtonSprite;
    private static Sprite _stoneButtonHoverSprite;

    [MenuItem("Tools/Cards/Create Deck Card Entry Prefab")]
    public static void CreateDeckCardEntryPrefab()
    {
        LoadSprites();
        var prefab = BuildDeckCardEntry();
        if (prefab != null)
        {
            Debug.Log("DeckCardEntry.prefab created at Assets/Prefabs/UI/Cards/DeckCardEntry.prefab");
        }
    }

    [MenuItem("Tools/Cards/Setup Deck Management UI")]
    public static void Setup()
    {
        var mainMenuCanvas = GameObject.Find("MainMenuCanvas");
        if (mainMenuCanvas == null)
        {
            Debug.LogError("MainMenuCanvas not found in scene. Open the menu scene first.");
            return;
        }

        var existing = mainMenuCanvas.GetComponentInChildren<DeckManagementUI>(true);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        LoadSprites();

        var cardEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Cards/DeckCardEntry.prefab");
        if (cardEntryPrefab == null)
        {
            cardEntryPrefab = BuildDeckCardEntry();
        }

        if (cardEntryPrefab == null)
        {
            Debug.LogError("Failed to create DeckCardEntry prefab.");
            return;
        }

        var heroGuids = AssetDatabase.FindAssets("t:HeroSO");
        var heroSOs = new List<HeroSO>();
        foreach (var guid in heroGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var heroSO = AssetDatabase.LoadAssetAtPath<HeroSO>(path);
            if (heroSO != null)
            {
                heroSOs.Add(heroSO);
            }
        }

        var root = CreateUIObject("DeckManagementUI", mainMenuCanvas.transform);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        var deckUI = root.AddComponent<DeckManagementUI>();

        var rootPanel = CreateFramedPanel("RootPanel", root.transform,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        var title = CreateLabel("Title", rootPanel.transform, "Deck Management", 28);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.92f);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = new Vector2(10, 0);
        titleRT.offsetMax = new Vector2(-10, -5);
        title.GetComponent<TextMeshProUGUI>().color = TextColor;

        var closeBtn = CreateStoneButton("CloseButton", rootPanel.transform, "Back to Menu");
        var closeBtnRT = closeBtn.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(0.35f, 0.02f);
        closeBtnRT.anchorMax = new Vector2(0.65f, 0.1f);
        closeBtnRT.offsetMin = Vector2.zero;
        closeBtnRT.offsetMax = Vector2.zero;

        // === HERO SELECT PANEL ===
        var heroSelectPanel = CreateUIObject("HeroSelectPanel", rootPanel.transform);
        var heroSelectRT = heroSelectPanel.GetComponent<RectTransform>();
        heroSelectRT.anchorMin = new Vector2(0.02f, 0.12f);
        heroSelectRT.anchorMax = new Vector2(0.98f, 0.91f);
        heroSelectRT.offsetMin = Vector2.zero;
        heroSelectRT.offsetMax = Vector2.zero;

        var heroSelectSubtitle = CreateLabel("Subtitle", heroSelectPanel.transform, "Select a Hero", 22);
        var heroSubtitleRT = heroSelectSubtitle.GetComponent<RectTransform>();
        heroSubtitleRT.anchorMin = new Vector2(0, 0.88f);
        heroSubtitleRT.anchorMax = new Vector2(1, 1);
        heroSubtitleRT.offsetMin = new Vector2(10, 0);
        heroSubtitleRT.offsetMax = new Vector2(-10, 0);
        heroSelectSubtitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        var heroGridScrollObj = CreateScrollArea("HeroGridScroll", heroSelectPanel.transform,
            new Vector2(0, 0), new Vector2(1, 0.87f), new Vector2(10, 10), new Vector2(-10, 0));
        var heroGridContent = heroGridScrollObj.transform.Find("Content");
        var heroHandLayout = heroGridContent.gameObject.AddComponent<CardHandLayout>();
        var heroHandSO = new SerializedObject(heroHandLayout);
        heroHandSO.FindProperty("_cardWidth").floatValue = 220f;
        heroHandSO.FindProperty("_cardHeight").floatValue = 320f;
        heroHandSO.FindProperty("_minVisibleWidth").floatValue = 120f;
        heroHandSO.FindProperty("_maxSpacing").floatValue = 40f;
        heroHandSO.ApplyModifiedProperties();

        // === DECK EDIT PANEL ===
        var deckEditPanel = CreateUIObject("DeckEditPanel", rootPanel.transform);
        var deckEditRT = deckEditPanel.GetComponent<RectTransform>();
        deckEditRT.anchorMin = new Vector2(0.02f, 0.12f);
        deckEditRT.anchorMax = new Vector2(0.98f, 0.91f);
        deckEditRT.offsetMin = Vector2.zero;
        deckEditRT.offsetMax = Vector2.zero;

        var deckBackBtn = CreateStoneButton("DeckBackButton", deckEditPanel.transform, "< Heroes");
        var deckBackRT = deckBackBtn.GetComponent<RectTransform>();
        deckBackRT.anchorMin = new Vector2(0.0f, 0.92f);
        deckBackRT.anchorMax = new Vector2(0.18f, 1.0f);
        deckBackRT.offsetMin = Vector2.zero;
        deckBackRT.offsetMax = Vector2.zero;

        var heroInfoBar = CreateUIObject("HeroInfoBar", deckEditPanel.transform);
        var heroInfoRT = heroInfoBar.GetComponent<RectTransform>();
        heroInfoRT.anchorMin = new Vector2(0.2f, 0.88f);
        heroInfoRT.anchorMax = new Vector2(1.0f, 1.0f);
        heroInfoRT.offsetMin = Vector2.zero;
        heroInfoRT.offsetMax = Vector2.zero;
        var heroInfoHLG = heroInfoBar.AddComponent<HorizontalLayoutGroup>();
        heroInfoHLG.spacing = 10;
        heroInfoHLG.childForceExpandWidth = true;
        heroInfoHLG.childForceExpandHeight = true;
        heroInfoHLG.childControlWidth = true;
        heroInfoHLG.childControlHeight = true;

        var heroNameLabel = CreateLabel("HeroNameLabel", heroInfoBar.transform, "Hero Name", 24);
        var heroNameTMP = heroNameLabel.GetComponent<TextMeshProUGUI>();
        heroNameTMP.alignment = TextAlignmentOptions.Left;
        heroNameTMP.color = TextColor;

        var deckCountLabel = CreateLabel("DeckCountLabel", heroInfoBar.transform, "0 / 5", 22);
        var deckCountTMP = deckCountLabel.GetComponent<TextMeshProUGUI>();
        deckCountTMP.alignment = TextAlignmentOptions.Right;
        deckCountTMP.color = TextColor;

        var deckSection = CreateSubPanel("DeckSection", deckEditPanel.transform,
            new Vector2(0.0f, 0.0f), new Vector2(0.49f, 0.86f));

        var deckTitle = CreateLabel("DeckTitle", deckSection.transform, "Assigned Cards", 18);
        var deckTitleRT = deckTitle.GetComponent<RectTransform>();
        deckTitleRT.anchorMin = new Vector2(0, 0.9f);
        deckTitleRT.anchorMax = new Vector2(1, 1);
        deckTitleRT.offsetMin = new Vector2(5, 0);
        deckTitleRT.offsetMax = new Vector2(-5, -2);
        deckTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        var deckScrollObj = CreateScrollArea("DeckScroll", deckSection.transform,
            new Vector2(0, 0), new Vector2(1, 0.89f), new Vector2(5, 5), new Vector2(-5, 0));
        var deckContent = deckScrollObj.transform.Find("Content");
        var deckHandLayout = deckContent.gameObject.AddComponent<CardHandLayout>();
        var deckHandSO = new SerializedObject(deckHandLayout);
        deckHandSO.FindProperty("_cardWidth").floatValue = 180f;
        deckHandSO.FindProperty("_cardHeight").floatValue = 260f;
        deckHandSO.FindProperty("_minVisibleWidth").floatValue = 80f;
        deckHandSO.FindProperty("_maxSpacing").floatValue = 20f;
        deckHandSO.ApplyModifiedProperties();

        var availSection = CreateSubPanel("AvailableSection", deckEditPanel.transform,
            new Vector2(0.51f, 0.0f), new Vector2(1.0f, 0.86f));

        var availTitle = CreateLabel("AvailTitle", availSection.transform, "Available Cards", 18);
        var availTitleRT = availTitle.GetComponent<RectTransform>();
        availTitleRT.anchorMin = new Vector2(0, 0.9f);
        availTitleRT.anchorMax = new Vector2(1, 1);
        availTitleRT.offsetMin = new Vector2(5, 0);
        availTitleRT.offsetMax = new Vector2(-5, -2);
        availTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        var availScrollObj = CreateScrollArea("AvailableScroll", availSection.transform,
            new Vector2(0, 0), new Vector2(1, 0.89f), new Vector2(5, 5), new Vector2(-5, 0));
        var availContent = availScrollObj.transform.Find("Content");
        var availHandLayout = availContent.gameObject.AddComponent<CardHandLayout>();
        var availHandSO = new SerializedObject(availHandLayout);
        availHandSO.FindProperty("_cardWidth").floatValue = 180f;
        availHandSO.FindProperty("_cardHeight").floatValue = 260f;
        availHandSO.FindProperty("_minVisibleWidth").floatValue = 80f;
        availHandSO.FindProperty("_maxSpacing").floatValue = 20f;
        availHandSO.ApplyModifiedProperties();

        rootPanel.SetActive(false);
        heroSelectPanel.SetActive(true);
        deckEditPanel.SetActive(false);

        var so = new SerializedObject(deckUI);
        so.FindProperty("_rootPanel").objectReferenceValue = rootPanel;
        so.FindProperty("_heroSelectPanel").objectReferenceValue = heroSelectPanel;
        so.FindProperty("_heroSelectParent").objectReferenceValue = heroGridContent;
        so.FindProperty("_heroSelectPrefab").objectReferenceValue = cardEntryPrefab;
        so.FindProperty("_deckEditPanel").objectReferenceValue = deckEditPanel;
        so.FindProperty("_deckBackButton").objectReferenceValue = deckBackBtn.GetComponent<Button>();
        so.FindProperty("_heroNameLabel").objectReferenceValue = heroNameTMP;
        so.FindProperty("_deckCountLabel").objectReferenceValue = deckCountTMP;
        so.FindProperty("_deckCardParent").objectReferenceValue = deckContent;
        so.FindProperty("_deckCardPrefab").objectReferenceValue = cardEntryPrefab;
        so.FindProperty("_availableCardParent").objectReferenceValue = availContent;
        so.FindProperty("_availableCardPrefab").objectReferenceValue = cardEntryPrefab;
        so.FindProperty("_closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();

        var heroesProp = so.FindProperty("_heroes");
        heroesProp.arraySize = heroSOs.Count;
        for (int i = 0; i < heroSOs.Count; i++)
        {
            heroesProp.GetArrayElementAtIndex(i).objectReferenceValue = heroSOs[i];
        }

        so.ApplyModifiedProperties();

        var menuManager = mainMenuCanvas.GetComponent<MainMenuManager>();
        if (menuManager != null)
        {
            var menuSO = new SerializedObject(menuManager);
            menuSO.FindProperty("_deckManagementUI").objectReferenceValue = deckUI;
            menuSO.ApplyModifiedProperties();
        }

        Undo.RegisterCreatedObjectUndo(root, "Setup Deck Management UI");
        EditorUtility.SetDirty(mainMenuCanvas);

        Debug.Log($"DeckManagementUI created with hero-select flow under MainMenuCanvas with {heroSOs.Count} heroes. Save the scene to persist.");
    }

    private static GameObject BuildDeckCardEntry()
    {
        var root = CreateUIObject("DeckCardEntry", null);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(180, 260);

        root.AddComponent<CanvasRenderer>();
        var bgImg = root.AddComponent<Image>();
        bgImg.sprite = _stoneButtonSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardBgColor;

        var btn = root.AddComponent<Button>();
        var spriteState = new SpriteState();
        spriteState.highlightedSprite = _stoneButtonHoverSprite;
        spriteState.pressedSprite = _stoneButtonHoverSprite;
        spriteState.selectedSprite = _stoneButtonHoverSprite;
        btn.spriteState = spriteState;
        btn.transition = Selectable.Transition.SpriteSwap;

        root.AddComponent<CardHoverEffect>();

        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 8, 6);
        vlg.spacing = 4;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        var iconObj = CreateUIObject("Icon", root.transform);
        iconObj.AddComponent<CanvasRenderer>();
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        var iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredHeight = 80;
        iconLE.preferredWidth = 80;

        var nameObj = CreateUIObject("NameLabel", root.transform);
        nameObj.AddComponent<CanvasRenderer>();
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Card Name";
        nameTMP.fontSize = 22;
        nameTMP.fontSizeMin = 10;
        nameTMP.fontSizeMax = 24;
        nameTMP.enableAutoSizing = true;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = LightTextColor;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.enableWordWrapping = true;
        var nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 48;

        var descObj = CreateUIObject("DescriptionLabel", root.transform);
        descObj.AddComponent<CanvasRenderer>();
        var descTMP = descObj.AddComponent<TextMeshProUGUI>();
        descTMP.text = "";
        descTMP.fontSize = 16;
        descTMP.fontSizeMin = 8;
        descTMP.fontSizeMax = 18;
        descTMP.enableAutoSizing = true;
        descTMP.fontStyle = FontStyles.Italic;
        descTMP.color = EffectsTextColor;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.enableWordWrapping = true;
        var descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = 36;

        var effectsObj = CreateUIObject("EffectsLabel", root.transform);
        effectsObj.AddComponent<CanvasRenderer>();
        var effectsTMP = effectsObj.AddComponent<TextMeshProUGUI>();
        effectsTMP.text = "DMG 5";
        effectsTMP.fontSize = 18;
        effectsTMP.fontSizeMin = 10;
        effectsTMP.fontSizeMax = 20;
        effectsTMP.enableAutoSizing = true;
        effectsTMP.color = EffectsTextColor;
        effectsTMP.alignment = TextAlignmentOptions.Center;
        effectsTMP.enableWordWrapping = true;
        var effectsLE = effectsObj.AddComponent<LayoutElement>();
        effectsLE.preferredHeight = 28;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI/Cards"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Cards");
        }

        var prefabPath = "Assets/Prefabs/UI/Cards/DeckCardEntry.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        return prefab;
    }

    private static GameObject CreateScrollArea(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var scrollObj = CreateUIObject(name, parent);
        var scrollRT = scrollObj.GetComponent<RectTransform>();
        scrollRT.anchorMin = anchorMin;
        scrollRT.anchorMax = anchorMax;
        scrollRT.offsetMin = offsetMin;
        scrollRT.offsetMax = offsetMax;

        scrollObj.AddComponent<CanvasRenderer>();
        var maskImg = scrollObj.AddComponent<Image>();
        maskImg.color = new Color(0, 0, 0, 0.01f);
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;

        var content = CreateUIObject("Content", scrollObj.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0);
        contentRT.anchorMax = new Vector2(0, 1);
        contentRT.pivot = new Vector2(0, 0.5f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = contentRT;

        return scrollObj;
    }

    private static void LoadSprites()
    {
        _parchmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ParchmentPanel.png");
        _dungeonFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DungeonFrame.png");
        _stoneButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButton.png");
        _stoneButtonHoverSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButtonHover.png");
    }

    private static GameObject CreateFramedPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var frame = CreateUIObject(name, parent);
        var frameRT = frame.GetComponent<RectTransform>();
        frameRT.anchorMin = anchorMin;
        frameRT.anchorMax = anchorMax;
        frameRT.offsetMin = Vector2.zero;
        frameRT.offsetMax = Vector2.zero;
        frame.AddComponent<CanvasRenderer>();
        var frameImg = frame.AddComponent<Image>();
        frameImg.sprite = _dungeonFrameSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.pixelsPerUnitMultiplier = 1f;

        var inner = CreateUIObject("ParchmentBg", frame.transform);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.06f, 0.06f);
        innerRT.anchorMax = new Vector2(0.94f, 0.94f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        inner.AddComponent<CanvasRenderer>();
        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = _parchmentSprite;
        innerImg.type = Image.Type.Tiled;
        innerImg.pixelsPerUnitMultiplier = 2f;

        return frame;
    }

    private static GameObject CreateSubPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var obj = CreateUIObject(name, parent);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        obj.AddComponent<CanvasRenderer>();
        var img = obj.AddComponent<Image>();
        img.color = SubPanelColor;

        return obj;
    }

    private static GameObject CreateStoneButton(string name, Transform parent, string label)
    {
        var obj = CreateUIObject(name, parent);
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
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LightTextColor;
        tmp.alignment = TextAlignmentOptions.Center;

        return obj;
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
}
