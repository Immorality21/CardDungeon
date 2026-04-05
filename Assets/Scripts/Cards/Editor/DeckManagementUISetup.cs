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
            Debug.LogWarning("DeckManagementUI already exists under MainMenuCanvas. Delete it first to re-create.");
            return;
        }

        LoadSprites();

        // Ensure card entry prefab exists
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

        // Load hero tab prefab
        var heroTabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Combat/CardButton.prefab");
        if (heroTabPrefab == null)
        {
            Debug.LogError("CardButton.prefab not found in Assets/Prefabs/UI/Combat/");
            return;
        }

        // Load hero SOs
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

        // Root panel (full screen overlay)
        var root = CreateUIObject("DeckManagementUI", mainMenuCanvas.transform);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        var deckUI = root.AddComponent<DeckManagementUI>();

        // Framed background panel (dungeon frame + parchment)
        var rootPanel = CreateFramedPanel("RootPanel", root.transform,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        // Title
        var title = CreateLabel("Title", rootPanel.transform, "Deck Management", 28);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.92f);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = new Vector2(10, 0);
        titleRT.offsetMax = new Vector2(-10, -5);
        title.GetComponent<TextMeshProUGUI>().color = TextColor;

        // === HERO TABS (top row) ===
        var heroTabArea = CreateUIObject("HeroTabArea", rootPanel.transform);
        var heroTabRT = heroTabArea.GetComponent<RectTransform>();
        heroTabRT.anchorMin = new Vector2(0.02f, 0.82f);
        heroTabRT.anchorMax = new Vector2(0.98f, 0.91f);
        heroTabRT.offsetMin = Vector2.zero;
        heroTabRT.offsetMax = Vector2.zero;
        var heroTabHLG = heroTabArea.AddComponent<HorizontalLayoutGroup>();
        heroTabHLG.spacing = 8;
        heroTabHLG.childForceExpandWidth = true;
        heroTabHLG.childForceExpandHeight = true;
        heroTabHLG.childControlWidth = true;
        heroTabHLG.childControlHeight = true;

        // === HERO NAME + DECK COUNT ===
        var heroInfoBar = CreateUIObject("HeroInfoBar", rootPanel.transform);
        var heroInfoRT = heroInfoBar.GetComponent<RectTransform>();
        heroInfoRT.anchorMin = new Vector2(0.02f, 0.74f);
        heroInfoRT.anchorMax = new Vector2(0.98f, 0.82f);
        heroInfoRT.offsetMin = Vector2.zero;
        heroInfoRT.offsetMax = Vector2.zero;
        var heroInfoHLG = heroInfoBar.AddComponent<HorizontalLayoutGroup>();
        heroInfoHLG.spacing = 10;
        heroInfoHLG.childForceExpandWidth = true;
        heroInfoHLG.childForceExpandHeight = true;
        heroInfoHLG.childControlWidth = true;
        heroInfoHLG.childControlHeight = true;

        var heroNameLabel = CreateLabel("HeroNameLabel", heroInfoBar.transform, "Hero Name", 22);
        var heroNameTMP = heroNameLabel.GetComponent<TextMeshProUGUI>();
        heroNameTMP.alignment = TextAlignmentOptions.Left;
        heroNameTMP.color = TextColor;

        var deckCountLabel = CreateLabel("DeckCountLabel", heroInfoBar.transform, "0 / 5", 20);
        var deckCountTMP = deckCountLabel.GetComponent<TextMeshProUGUI>();
        deckCountTMP.alignment = TextAlignmentOptions.Right;
        deckCountTMP.color = TextColor;

        // === DECK CARDS SECTION (left) ===
        var deckSection = CreateSubPanel("DeckSection", rootPanel.transform,
            new Vector2(0.02f, 0.12f), new Vector2(0.48f, 0.73f));

        var deckTitle = CreateLabel("DeckTitle", deckSection.transform, "Assigned Cards", 18);
        var deckTitleRT = deckTitle.GetComponent<RectTransform>();
        deckTitleRT.anchorMin = new Vector2(0, 0.9f);
        deckTitleRT.anchorMax = new Vector2(1, 1);
        deckTitleRT.offsetMin = new Vector2(5, 0);
        deckTitleRT.offsetMax = new Vector2(-5, -2);
        deckTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        // ScrollRect for deck cards
        var deckScrollObj = CreateScrollArea("DeckScroll", deckSection.transform,
            new Vector2(0, 0), new Vector2(1, 0.89f), new Vector2(5, 5), new Vector2(-5, 0));
        var deckContent = deckScrollObj.transform.Find("Content");
        deckContent.gameObject.AddComponent<CardHandLayout>();

        // === AVAILABLE CARDS SECTION (right) ===
        var availSection = CreateSubPanel("AvailableSection", rootPanel.transform,
            new Vector2(0.52f, 0.12f), new Vector2(0.98f, 0.73f));

        var availTitle = CreateLabel("AvailTitle", availSection.transform, "Available Cards", 18);
        var availTitleRT = availTitle.GetComponent<RectTransform>();
        availTitleRT.anchorMin = new Vector2(0, 0.9f);
        availTitleRT.anchorMax = new Vector2(1, 1);
        availTitleRT.offsetMin = new Vector2(5, 0);
        availTitleRT.offsetMax = new Vector2(-5, -2);
        availTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        // ScrollRect for available cards
        var availScrollObj = CreateScrollArea("AvailableScroll", availSection.transform,
            new Vector2(0, 0), new Vector2(1, 0.89f), new Vector2(5, 5), new Vector2(-5, 0));
        var availContent = availScrollObj.transform.Find("Content");
        availContent.gameObject.AddComponent<CardHandLayout>();

        // === CLOSE BUTTON ===
        var closeBtn = CreateStoneButton("CloseButton", rootPanel.transform, "Close");
        var closeBtnRT = closeBtn.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(0.35f, 0.02f);
        closeBtnRT.anchorMax = new Vector2(0.65f, 0.1f);
        closeBtnRT.offsetMin = Vector2.zero;
        closeBtnRT.offsetMax = Vector2.zero;

        rootPanel.SetActive(false);

        // === Wire up serialized fields ===
        var so = new SerializedObject(deckUI);
        so.FindProperty("_rootPanel").objectReferenceValue = rootPanel;
        so.FindProperty("_heroTabParent").objectReferenceValue = heroTabArea.transform;
        so.FindProperty("_heroTabPrefab").objectReferenceValue = heroTabPrefab;
        so.FindProperty("_heroNameLabel").objectReferenceValue = heroNameTMP;
        so.FindProperty("_deckCountLabel").objectReferenceValue = deckCountTMP;
        so.FindProperty("_deckCardParent").objectReferenceValue = deckContent;
        so.FindProperty("_deckCardPrefab").objectReferenceValue = cardEntryPrefab;
        so.FindProperty("_availableCardParent").objectReferenceValue = availContent;
        so.FindProperty("_availableCardPrefab").objectReferenceValue = cardEntryPrefab;
        so.FindProperty("_closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();

        // Wire hero SOs
        var heroesProp = so.FindProperty("_heroes");
        heroesProp.arraySize = heroSOs.Count;
        for (int i = 0; i < heroSOs.Count; i++)
        {
            heroesProp.GetArrayElementAtIndex(i).objectReferenceValue = heroSOs[i];
        }

        so.ApplyModifiedProperties();

        // Wire MainMenuManager._deckManagementUI if present
        var menuManager = mainMenuCanvas.GetComponent<MainMenuManager>();
        if (menuManager != null)
        {
            var menuSO = new SerializedObject(menuManager);
            menuSO.FindProperty("_deckManagementUI").objectReferenceValue = deckUI;
            menuSO.ApplyModifiedProperties();
        }

        Undo.RegisterCreatedObjectUndo(root, "Setup Deck Management UI");
        EditorUtility.SetDirty(mainMenuCanvas);

        Debug.Log($"DeckManagementUI created with card hand layout under MainMenuCanvas with {heroSOs.Count} heroes. Save the scene to persist.");
    }

    private static GameObject BuildDeckCardEntry()
    {
        // Create the prefab hierarchy in memory
        var root = CreateUIObject("DeckCardEntry", null);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(90, 150);

        // Background
        root.AddComponent<CanvasRenderer>();
        var bgImg = root.AddComponent<Image>();
        bgImg.sprite = _stoneButtonSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardBgColor;

        // Button for click handling
        var btn = root.AddComponent<Button>();
        var spriteState = new SpriteState();
        spriteState.highlightedSprite = _stoneButtonHoverSprite;
        spriteState.pressedSprite = _stoneButtonHoverSprite;
        spriteState.selectedSprite = _stoneButtonHoverSprite;
        btn.spriteState = spriteState;
        btn.transition = Selectable.Transition.SpriteSwap;

        // Hover effect
        root.AddComponent<CardHoverEffect>();

        // Vertical layout for contents
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 6, 4);
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        // Icon
        var iconObj = CreateUIObject("Icon", root.transform);
        iconObj.AddComponent<CanvasRenderer>();
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        var iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredHeight = 50;
        iconLE.preferredWidth = 50;

        // Name label
        var nameObj = CreateUIObject("NameLabel", root.transform);
        nameObj.AddComponent<CanvasRenderer>();
        var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Card Name";
        nameTMP.fontSize = 11;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = LightTextColor;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.enableWordWrapping = true;
        var nameLE = nameObj.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 30;

        // Description label
        var descObj = CreateUIObject("DescriptionLabel", root.transform);
        descObj.AddComponent<CanvasRenderer>();
        var descTMP = descObj.AddComponent<TextMeshProUGUI>();
        descTMP.text = "";
        descTMP.fontSize = 8;
        descTMP.fontStyle = FontStyles.Italic;
        descTMP.color = EffectsTextColor;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.enableWordWrapping = true;
        var descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = 20;

        // Effects label
        var effectsObj = CreateUIObject("EffectsLabel", root.transform);
        effectsObj.AddComponent<CanvasRenderer>();
        var effectsTMP = effectsObj.AddComponent<TextMeshProUGUI>();
        effectsTMP.text = "DMG 5";
        effectsTMP.fontSize = 9;
        effectsTMP.color = EffectsTextColor;
        effectsTMP.alignment = TextAlignmentOptions.Center;
        effectsTMP.enableWordWrapping = true;
        var effectsLE = effectsObj.AddComponent<LayoutElement>();
        effectsLE.preferredHeight = 20;

        // Save as prefab
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

        // Mask so cards clip at edges
        scrollObj.AddComponent<CanvasRenderer>();
        var maskImg = scrollObj.AddComponent<Image>();
        maskImg.color = new Color(0, 0, 0, 0.01f); // Nearly invisible, needed for Mask
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        // ScrollRect
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;

        // Content container
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
