using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Cards.UI;

public class CardSelectionUISetup : Editor
{
    private static readonly Color TextColor = new Color(0.18f, 0.12f, 0.06f, 1f);
    private static readonly Color LightTextColor = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color SubPanelColor = new Color(0.16f, 0.12f, 0.08f, 0.35f);

    private static Sprite _parchmentSprite;
    private static Sprite _dungeonFrameSprite;
    private static Sprite _stoneButtonSprite;
    private static Sprite _stoneButtonHoverSprite;

    [MenuItem("Tools/Cards/Setup Card Selection UI")]
    public static void Setup()
    {
        var combatCanvas = GameObject.Find("CombatCanvas");
        if (combatCanvas == null)
        {
            Debug.LogError("CombatCanvas not found in scene. Open the game scene first.");
            return;
        }

        // Delete existing if present
        var existing = combatCanvas.GetComponentInChildren<CardSelectionUI>(true);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        LoadSprites();

        // Load prefabs
        var cardEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Cards/DeckCardEntry.prefab");
        if (cardEntryPrefab == null)
        {
            Debug.LogError("DeckCardEntry.prefab not found. Run Tools > Cards > Create Deck Card Entry Prefab first.");
            return;
        }

        var targetButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Combat/TargetButton.prefab");
        if (targetButtonPrefab == null)
        {
            Debug.LogError("TargetButton.prefab not found in Assets/Prefabs/UI/Combat/");
            return;
        }

        // Root: CardSelectionUI
        var root = CreateUIObject("CardSelectionUI", combatCanvas.transform);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        var cardSelectionUI = root.AddComponent<CardSelectionUI>();

        // === CARD LIST PANEL (framed) ===
        var cardListPanel = CreateFramedPanel("CardListPanel", root.transform,
            new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.95f));

        // Get the parchment inner panel for placing children
        var cardListInner = cardListPanel.transform.Find("ParchmentBg");

        // Title
        var cardTitle = CreateLabel("CardTitle", cardListInner, "Choose a Card", 24);
        var cardTitleRT = cardTitle.GetComponent<RectTransform>();
        cardTitleRT.anchorMin = new Vector2(0, 0.9f);
        cardTitleRT.anchorMax = new Vector2(1, 1);
        cardTitleRT.offsetMin = new Vector2(10, 0);
        cardTitleRT.offsetMax = new Vector2(-10, -5);
        cardTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        // ScrollRect for card list with CardHandLayout
        var cardScrollObj = CreateScrollArea("CardScrollArea", cardListInner,
            new Vector2(0, 0.12f), new Vector2(1, 0.88f), new Vector2(10, 0), new Vector2(-10, 0));
        var cardContent = cardScrollObj.transform.Find("Content");
        var cardHandLayout = cardContent.gameObject.AddComponent<CardHandLayout>();
        var cardHandSO = new SerializedObject(cardHandLayout);
        cardHandSO.FindProperty("_cardWidth").floatValue = 180f;
        cardHandSO.FindProperty("_cardHeight").floatValue = 300f;
        cardHandSO.FindProperty("_minVisibleWidth").floatValue = 60f;
        cardHandSO.FindProperty("_maxSpacing").floatValue = 20f;
        cardHandSO.ApplyModifiedProperties();

        // Back button
        var cardBackBtn = CreateStoneButton("BackButton", cardListInner, "Back");
        var cardBackRT = cardBackBtn.GetComponent<RectTransform>();
        cardBackRT.anchorMin = new Vector2(0.3f, 0.01f);
        cardBackRT.anchorMax = new Vector2(0.7f, 0.1f);
        cardBackRT.offsetMin = Vector2.zero;
        cardBackRT.offsetMax = Vector2.zero;

        cardListPanel.SetActive(false);

        // === TARGET PANEL (framed) ===
        var targetPanel = CreateFramedPanel("TargetPanel", root.transform,
            new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f));

        var targetInner = targetPanel.transform.Find("ParchmentBg");

        // Target prompt
        var targetPrompt = CreateLabel("TargetPromptLabel", targetInner, "Select Target", 24);
        var targetPromptRT = targetPrompt.GetComponent<RectTransform>();
        targetPromptRT.anchorMin = new Vector2(0, 0.88f);
        targetPromptRT.anchorMax = new Vector2(1, 1);
        targetPromptRT.offsetMin = new Vector2(10, 0);
        targetPromptRT.offsetMax = new Vector2(-10, -5);
        targetPrompt.GetComponent<TextMeshProUGUI>().color = TextColor;

        // Scroll area for targets (vertical list with sub-panel background)
        var targetScrollBg = CreateUIObject("TargetScrollBg", targetInner);
        var targetScrollBgRT = targetScrollBg.GetComponent<RectTransform>();
        targetScrollBgRT.anchorMin = new Vector2(0.05f, 0.12f);
        targetScrollBgRT.anchorMax = new Vector2(0.95f, 0.86f);
        targetScrollBgRT.offsetMin = Vector2.zero;
        targetScrollBgRT.offsetMax = Vector2.zero;
        targetScrollBg.AddComponent<CanvasRenderer>();
        var targetBgImg = targetScrollBg.AddComponent<Image>();
        targetBgImg.color = SubPanelColor;

        var targetScrollArea = CreateUIObject("TargetScrollArea", targetScrollBg.transform);
        var targetScrollRT = targetScrollArea.GetComponent<RectTransform>();
        targetScrollRT.anchorMin = Vector2.zero;
        targetScrollRT.anchorMax = Vector2.one;
        targetScrollRT.offsetMin = new Vector2(10, 5);
        targetScrollRT.offsetMax = new Vector2(-10, -5);
        var targetVLG = targetScrollArea.AddComponent<VerticalLayoutGroup>();
        targetVLG.spacing = 6;
        targetVLG.childForceExpandWidth = true;
        targetVLG.childForceExpandHeight = false;
        targetVLG.childControlWidth = true;
        targetVLG.childControlHeight = false;
        var targetCSF = targetScrollArea.AddComponent<ContentSizeFitter>();
        targetCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Target back button
        var targetBackBtn = CreateStoneButton("TargetBackButton", targetInner, "Back");
        var targetBackRT = targetBackBtn.GetComponent<RectTransform>();
        targetBackRT.anchorMin = new Vector2(0.3f, 0.01f);
        targetBackRT.anchorMax = new Vector2(0.7f, 0.1f);
        targetBackRT.offsetMin = Vector2.zero;
        targetBackRT.offsetMax = Vector2.zero;

        targetPanel.SetActive(false);

        // === Wire up serialized fields ===
        var so = new SerializedObject(cardSelectionUI);
        so.FindProperty("_cardListPanel").objectReferenceValue = cardListPanel;
        so.FindProperty("_cardListParent").objectReferenceValue = cardContent;
        so.FindProperty("_cardButtonPrefab").objectReferenceValue = cardEntryPrefab;
        so.FindProperty("_backButton").objectReferenceValue = cardBackBtn.GetComponent<Button>();
        so.FindProperty("_targetPanel").objectReferenceValue = targetPanel;
        so.FindProperty("_targetListParent").objectReferenceValue = targetScrollArea.transform;
        so.FindProperty("_targetButtonPrefab").objectReferenceValue = targetButtonPrefab;
        so.FindProperty("_targetBackButton").objectReferenceValue = targetBackBtn.GetComponent<Button>();
        so.FindProperty("_targetPromptLabel").objectReferenceValue = targetPrompt.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(root, "Setup Card Selection UI");
        EditorUtility.SetDirty(combatCanvas);

        Debug.Log("CardSelectionUI created with fantasy theme under CombatCanvas. Save the scene to persist.");
    }

    private static void LoadSprites()
    {
        _parchmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ParchmentPanel.png");
        _dungeonFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/DungeonFrame.png");
        _stoneButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButton.png");
        _stoneButtonHoverSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/StoneButtonHover.png");
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
