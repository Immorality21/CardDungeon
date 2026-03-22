using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Cards.UI;

public class CardSelectionUISetup : Editor
{
    private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.18f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.32f, 1f);

    [MenuItem("Tools/Cards/Setup Card Selection UI")]
    public static void Setup()
    {
        var combatCanvas = GameObject.Find("CombatCanvas");
        if (combatCanvas == null)
        {
            Debug.LogError("CombatCanvas not found in scene. Open the game scene first.");
            return;
        }

        // Check if already set up
        var existing = combatCanvas.GetComponentInChildren<CardSelectionUI>(true);
        if (existing != null)
        {
            Debug.LogWarning("CardSelectionUI already exists under CombatCanvas. Delete it first to re-create.");
            return;
        }

        // Load prefabs
        var cardButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Combat/CardButton.prefab");
        var targetButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Combat/TargetButton.prefab");

        if (cardButtonPrefab == null || targetButtonPrefab == null)
        {
            Debug.LogError("CardButton.prefab or TargetButton.prefab not found in Assets/Prefabs/UI/Combat/");
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

        // === CARD LIST PANEL ===
        var cardListPanel = CreatePanel("CardListPanel", root.transform, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f));

        // Title
        var cardTitle = CreateLabel("CardTitle", cardListPanel.transform, "Choose a Card", 24);
        var cardTitleRT = cardTitle.GetComponent<RectTransform>();
        cardTitleRT.anchorMin = new Vector2(0, 0.88f);
        cardTitleRT.anchorMax = new Vector2(1, 1);
        cardTitleRT.offsetMin = new Vector2(10, 0);
        cardTitleRT.offsetMax = new Vector2(-10, -5);

        // Scroll area for card list
        var cardScrollArea = CreateUIObject("CardScrollArea", cardListPanel.transform);
        var cardScrollRT = cardScrollArea.GetComponent<RectTransform>();
        cardScrollRT.anchorMin = new Vector2(0, 0.12f);
        cardScrollRT.anchorMax = new Vector2(1, 0.87f);
        cardScrollRT.offsetMin = new Vector2(10, 0);
        cardScrollRT.offsetMax = new Vector2(-10, 0);
        var cardVLG = cardScrollArea.AddComponent<VerticalLayoutGroup>();
        cardVLG.spacing = 6;
        cardVLG.childForceExpandWidth = true;
        cardVLG.childForceExpandHeight = false;
        cardVLG.childControlWidth = true;
        cardVLG.childControlHeight = false;
        var cardCSF = cardScrollArea.AddComponent<ContentSizeFitter>();
        cardCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Back button
        var cardBackBtn = CreateButton("BackButton", cardListPanel.transform, "Back");
        var cardBackRT = cardBackBtn.GetComponent<RectTransform>();
        cardBackRT.anchorMin = new Vector2(0.3f, 0.01f);
        cardBackRT.anchorMax = new Vector2(0.7f, 0.1f);
        cardBackRT.offsetMin = Vector2.zero;
        cardBackRT.offsetMax = Vector2.zero;

        cardListPanel.SetActive(false);

        // === TARGET PANEL ===
        var targetPanel = CreatePanel("TargetPanel", root.transform, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.9f));

        // Target prompt
        var targetPrompt = CreateLabel("TargetPromptLabel", targetPanel.transform, "Select Target", 24);
        var targetPromptRT = targetPrompt.GetComponent<RectTransform>();
        targetPromptRT.anchorMin = new Vector2(0, 0.88f);
        targetPromptRT.anchorMax = new Vector2(1, 1);
        targetPromptRT.offsetMin = new Vector2(10, 0);
        targetPromptRT.offsetMax = new Vector2(-10, -5);

        // Scroll area for targets
        var targetScrollArea = CreateUIObject("TargetScrollArea", targetPanel.transform);
        var targetScrollRT = targetScrollArea.GetComponent<RectTransform>();
        targetScrollRT.anchorMin = new Vector2(0, 0.12f);
        targetScrollRT.anchorMax = new Vector2(1, 0.87f);
        targetScrollRT.offsetMin = new Vector2(10, 0);
        targetScrollRT.offsetMax = new Vector2(-10, 0);
        var targetVLG = targetScrollArea.AddComponent<VerticalLayoutGroup>();
        targetVLG.spacing = 6;
        targetVLG.childForceExpandWidth = true;
        targetVLG.childForceExpandHeight = false;
        targetVLG.childControlWidth = true;
        targetVLG.childControlHeight = false;
        var targetCSF = targetScrollArea.AddComponent<ContentSizeFitter>();
        targetCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Target back button
        var targetBackBtn = CreateButton("TargetBackButton", targetPanel.transform, "Back");
        var targetBackRT = targetBackBtn.GetComponent<RectTransform>();
        targetBackRT.anchorMin = new Vector2(0.3f, 0.01f);
        targetBackRT.anchorMax = new Vector2(0.7f, 0.1f);
        targetBackRT.offsetMin = Vector2.zero;
        targetBackRT.offsetMax = Vector2.zero;

        targetPanel.SetActive(false);

        // === Wire up serialized fields ===
        var so = new SerializedObject(cardSelectionUI);
        so.FindProperty("_cardListPanel").objectReferenceValue = cardListPanel;
        so.FindProperty("_cardListParent").objectReferenceValue = cardScrollArea.transform;
        so.FindProperty("_cardButtonPrefab").objectReferenceValue = cardButtonPrefab;
        so.FindProperty("_backButton").objectReferenceValue = cardBackBtn.GetComponent<Button>();
        so.FindProperty("_targetPanel").objectReferenceValue = targetPanel;
        so.FindProperty("_targetListParent").objectReferenceValue = targetScrollArea.transform;
        so.FindProperty("_targetButtonPrefab").objectReferenceValue = targetButtonPrefab;
        so.FindProperty("_targetBackButton").objectReferenceValue = targetBackBtn.GetComponent<Button>();
        so.FindProperty("_targetPromptLabel").objectReferenceValue = targetPrompt.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(root, "Setup Card Selection UI");
        EditorUtility.SetDirty(combatCanvas);

        Debug.Log("CardSelectionUI created under CombatCanvas. Save the scene to persist.");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.layer = 5; // UI layer
        obj.AddComponent<RectTransform>();
        return obj;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var obj = CreateUIObject(name, parent);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        obj.AddComponent<CanvasRenderer>();
        var img = obj.AddComponent<Image>();
        img.color = PanelColor;

        return obj;
    }

    private static GameObject CreateButton(string name, Transform parent, string label)
    {
        var obj = CreateUIObject(name, parent);
        obj.AddComponent<CanvasRenderer>();
        var img = obj.AddComponent<Image>();
        img.color = ButtonColor;

        var btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.42f, 1f);
        btn.colors = colors;

        var textObj = CreateUIObject("Text", obj.transform);
        textObj.AddComponent<CanvasRenderer>();
        var textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
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
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }
}
