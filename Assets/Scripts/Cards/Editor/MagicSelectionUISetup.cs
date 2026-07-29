using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Cards.UI;

public class MagicSelectionUISetup : Editor
{
    private static readonly Color TextColor = new Color(0.18f, 0.12f, 0.06f, 1f);
    private static readonly Color LightTextColor = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color SubPanelColor = new Color(0.16f, 0.12f, 0.08f, 0.35f);

    private static Sprite _parchmentSprite;
    private static Sprite _dungeonFrameSprite;
    private static Sprite _stoneButtonSprite;
    private static Sprite _stoneButtonHoverSprite;

    // Compact lower-center command window (fraction of screen). FFVIII-style: small,
    // out of the way, anchored toward the bottom rather than filling the screen.
    private static readonly Vector2 WindowAnchorMin = new Vector2(0.34f, 0.14f);
    private static readonly Vector2 WindowAnchorMax = new Vector2(0.66f, 0.56f);

    [MenuItem("Tools/Cards/Setup Magic Selection UI")]
    public static void Setup()
    {
        var combatCanvas = GameObject.Find("CombatCanvas");
        if (combatCanvas == null)
        {
            Debug.LogError("CombatCanvas not found in scene. Open the game scene first.");
            return;
        }

        // Delete existing if present
        var existing = combatCanvas.GetComponentInChildren<MagicSelectionUI>(true);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        LoadSprites();

        var targetButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Combat/TargetButton.prefab");
        if (targetButtonPrefab == null)
        {
            Debug.LogError("TargetButton.prefab not found in Assets/Prefabs/UI/Combat/");
            return;
        }

        var magicRowPrefab = BuildMagicRowPrefab();

        // Root: fills the canvas but is transparent; the windows inside are compact.
        var root = CreateUIObject("MagicSelectionUI", combatCanvas.transform);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        var magicSelectionUI = root.AddComponent<MagicSelectionUI>();

        // === MAGIC / SLOT LIST WINDOW (compact) ===
        var cardListPanel = CreateFramedPanel("MagicListPanel", root.transform, WindowAnchorMin, WindowAnchorMax);
        var cardListInner = cardListPanel.transform.Find("ParchmentBg");

        var listTitle = CreateLabel("MagicTitle", cardListInner, "Magic", 22);
        var listTitleRT = listTitle.GetComponent<RectTransform>();
        listTitleRT.anchorMin = new Vector2(0, 0.87f);
        listTitleRT.anchorMax = new Vector2(1, 1);
        listTitleRT.offsetMin = new Vector2(8, 0);
        listTitleRT.offsetMax = new Vector2(-8, -4);
        listTitle.GetComponent<TextMeshProUGUI>().color = TextColor;

        var listContent = CreateVerticalScrollList("MagicListScroll", cardListInner,
            new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.85f));

        var listBackBtn = CreateStoneButton("BackButton", cardListInner, "Back");
        var listBackRT = listBackBtn.GetComponent<RectTransform>();
        listBackRT.anchorMin = new Vector2(0.28f, 0.01f);
        listBackRT.anchorMax = new Vector2(0.72f, 0.12f);
        listBackRT.offsetMin = Vector2.zero;
        listBackRT.offsetMax = Vector2.zero;

        cardListPanel.SetActive(false);

        // === TARGET WINDOW (same compact footprint) ===
        var targetPanel = CreateFramedPanel("TargetPanel", root.transform, WindowAnchorMin, WindowAnchorMax);
        var targetInner = targetPanel.transform.Find("ParchmentBg");

        var targetPrompt = CreateLabel("TargetPromptLabel", targetInner, "Select Target", 22);
        var targetPromptRT = targetPrompt.GetComponent<RectTransform>();
        targetPromptRT.anchorMin = new Vector2(0, 0.87f);
        targetPromptRT.anchorMax = new Vector2(1, 1);
        targetPromptRT.offsetMin = new Vector2(8, 0);
        targetPromptRT.offsetMax = new Vector2(-8, -4);
        targetPrompt.GetComponent<TextMeshProUGUI>().color = TextColor;

        var targetContent = CreateVerticalScrollList("TargetListScroll", targetInner,
            new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.85f));

        var targetBackBtn = CreateStoneButton("TargetBackButton", targetInner, "Back");
        var targetBackRT = targetBackBtn.GetComponent<RectTransform>();
        targetBackRT.anchorMin = new Vector2(0.28f, 0.01f);
        targetBackRT.anchorMax = new Vector2(0.72f, 0.12f);
        targetBackRT.offsetMin = Vector2.zero;
        targetBackRT.offsetMax = Vector2.zero;

        targetPanel.SetActive(false);

        // === Wire serialized fields ===
        var so = new SerializedObject(magicSelectionUI);
        so.FindProperty("_cardListPanel").objectReferenceValue = cardListPanel;
        so.FindProperty("_cardListParent").objectReferenceValue = listContent;
        so.FindProperty("_cardButtonPrefab").objectReferenceValue = magicRowPrefab;
        so.FindProperty("_backButton").objectReferenceValue = listBackBtn.GetComponent<Button>();
        so.FindProperty("_targetPanel").objectReferenceValue = targetPanel;
        so.FindProperty("_targetListParent").objectReferenceValue = targetContent;
        so.FindProperty("_targetButtonPrefab").objectReferenceValue = targetButtonPrefab;
        so.FindProperty("_targetBackButton").objectReferenceValue = targetBackBtn.GetComponent<Button>();
        so.FindProperty("_targetPromptLabel").objectReferenceValue = targetPrompt.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(root, "Setup Magic Selection UI");
        EditorUtility.SetDirty(combatCanvas);

        Debug.Log("MagicSelectionUI created as compact lower-center windows under CombatCanvas. Save the scene to persist.");
    }

    /// <summary>
    /// Compact one-line magic row: icon (left), name (middle), charges (right).
    /// Children are named to match MagicSelectionUI's lookups (Icon / NameLabel / DescriptionLabel).
    /// </summary>
    private static GameObject BuildMagicRowPrefab()
    {
        var obj = CreateUIObject("MagicRow", null);
        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 50);

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

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
        le.minHeight = 50;

        // Icon (left)
        var icon = CreateUIObject("Icon", obj.transform);
        icon.AddComponent<CanvasRenderer>();
        var iconImg = icon.AddComponent<Image>();
        iconImg.preserveAspect = true;
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 0.5f);
        iconRT.anchorMax = new Vector2(0, 0.5f);
        iconRT.pivot = new Vector2(0, 0.5f);
        iconRT.sizeDelta = new Vector2(38, 38);
        iconRT.anchoredPosition = new Vector2(8, 0);

        // Name (middle, left-aligned)
        var name = CreateUIObject("NameLabel", obj.transform);
        name.AddComponent<CanvasRenderer>();
        var nameTMP = name.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Magic";
        nameTMP.fontSize = 18;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = LightTextColor;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        var nameRT = name.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 1);
        nameRT.offsetMin = new Vector2(54, 0);
        nameRT.offsetMax = new Vector2(-96, 0);

        // Charges / description (right-aligned)
        var desc = CreateUIObject("DescriptionLabel", obj.transform);
        desc.AddComponent<CanvasRenderer>();
        var descTMP = desc.AddComponent<TextMeshProUGUI>();
        descTMP.text = "";
        descTMP.fontSize = 15;
        descTMP.fontStyle = FontStyles.Normal;
        descTMP.color = LightTextColor;
        descTMP.alignment = TextAlignmentOptions.MidlineRight;
        var descRT = desc.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(1, 0);
        descRT.anchorMax = new Vector2(1, 1);
        descRT.pivot = new Vector2(1, 0.5f);
        descRT.sizeDelta = new Vector2(90, 0);
        descRT.anchoredPosition = new Vector2(-10, 0);

        const string folder = "Assets/Prefabs/UI/Combat";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Combat");
        }

        var prefabPath = folder + "/MagicRow.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
        DestroyImmediate(obj);

        Debug.Log("MagicRow.prefab created at " + prefabPath);
        return prefab;
    }

    /// <summary>Masked, vertically-scrolling list. Returns the Content transform to parent rows under.</summary>
    private static Transform CreateVerticalScrollList(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var viewport = CreateUIObject(name, parent);
        var viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = anchorMin;
        viewportRT.anchorMax = anchorMax;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewport.AddComponent<CanvasRenderer>();
        var bgImg = viewport.AddComponent<Image>();
        bgImg.color = SubPanelColor;
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        var scrollRect = viewport.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 20f;

        var content = CreateUIObject("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        return content.transform;
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
