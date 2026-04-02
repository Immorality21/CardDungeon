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

    private static Sprite _parchmentSprite;
    private static Sprite _dungeonFrameSprite;
    private static Sprite _stoneButtonSprite;
    private static Sprite _stoneButtonHoverSprite;

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

        // Load sprites
        _parchmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ParchmentPanel.png");
        _dungeonFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DungeonFrame.png");
        _stoneButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButton.png");
        _stoneButtonHoverSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButtonHover.png");

        // Load prefabs
        var cardButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Combat/CardButton.prefab");
        if (cardButtonPrefab == null)
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

        var deckScrollArea = CreateUIObject("DeckCardParent", deckSection.transform);
        var deckScrollRT = deckScrollArea.GetComponent<RectTransform>();
        deckScrollRT.anchorMin = new Vector2(0, 0);
        deckScrollRT.anchorMax = new Vector2(1, 0.89f);
        deckScrollRT.offsetMin = new Vector2(5, 5);
        deckScrollRT.offsetMax = new Vector2(-5, 0);
        var deckVLG = deckScrollArea.AddComponent<VerticalLayoutGroup>();
        deckVLG.spacing = 4;
        deckVLG.childForceExpandWidth = true;
        deckVLG.childForceExpandHeight = false;
        deckVLG.childControlWidth = true;
        deckVLG.childControlHeight = false;
        var deckCSF = deckScrollArea.AddComponent<ContentSizeFitter>();
        deckCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

        var availScrollArea = CreateUIObject("AvailableCardParent", availSection.transform);
        var availScrollRT = availScrollArea.GetComponent<RectTransform>();
        availScrollRT.anchorMin = new Vector2(0, 0);
        availScrollRT.anchorMax = new Vector2(1, 0.89f);
        availScrollRT.offsetMin = new Vector2(5, 5);
        availScrollRT.offsetMax = new Vector2(-5, 0);
        var availVLG = availScrollArea.AddComponent<VerticalLayoutGroup>();
        availVLG.spacing = 4;
        availVLG.childForceExpandWidth = true;
        availVLG.childForceExpandHeight = false;
        availVLG.childControlWidth = true;
        availVLG.childControlHeight = false;
        var availCSF = availScrollArea.AddComponent<ContentSizeFitter>();
        availCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
        so.FindProperty("_heroTabPrefab").objectReferenceValue = cardButtonPrefab;
        so.FindProperty("_heroNameLabel").objectReferenceValue = heroNameTMP;
        so.FindProperty("_deckCountLabel").objectReferenceValue = deckCountTMP;
        so.FindProperty("_deckCardParent").objectReferenceValue = deckScrollArea.transform;
        so.FindProperty("_deckCardPrefab").objectReferenceValue = cardButtonPrefab;
        so.FindProperty("_availableCardParent").objectReferenceValue = availScrollArea.transform;
        so.FindProperty("_availableCardPrefab").objectReferenceValue = cardButtonPrefab;
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

        Debug.Log($"DeckManagementUI created with fantasy theme under MainMenuCanvas with {heroSOs.Count} heroes. Save the scene to persist.");
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
        tmp.color = LightTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }
}
