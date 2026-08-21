using System.IO;
using Assets.Scripts.Cards.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bootstraps the UI Toolkit magic/draw selection window: ensures a shared PanelSettings
/// asset exists, then drops a UIDocument into the open scene wired to MagicSelection.uxml
/// and the MagicSelectionUI controller. Re-runnable; replaces any prior instance.
/// </summary>
public class MagicSelectionUISetup : Editor
{
    private const string UxmlPath = "Assets/UI/Combat/MagicSelection.uxml";
    private const string PanelSettingsPath = "Assets/UI/CardDungeonPanelSettings.asset";
    private const string ThemePath = "Assets/UI/CardDungeonTheme.tss";

    [MenuItem("Tools/Cards/Setup Magic Selection UI")]
    public static void Setup()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (uxml == null)
        {
            Debug.LogError($"UXML not found at {UxmlPath}.");
            return;
        }

        var panelSettings = EnsurePanelSettings();
        if (panelSettings == null)
        {
            Debug.LogError("Could not create or load PanelSettings.");
            return;
        }

        // Replace any prior instance (old uGUI object or a previous UITK bootstrap).
        var existing = Object.FindAnyObjectByType<MagicSelectionUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var go = new GameObject("MagicSelectionUITK");
        Undo.RegisterCreatedObjectUndo(go, "Setup Magic Selection UI");

        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        doc.visualTreeAsset = uxml;

        var ui = go.AddComponent<MagicSelectionUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("_document").objectReferenceValue = doc;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);

        Debug.Log("MagicSelectionUI (UI Toolkit) created in the open scene. Save the scene to persist.");
    }

    private static PanelSettings EnsurePanelSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder("Assets/UI");

        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.match = 0.5f;
        // Render above the existing uGUI combat canvas (which sits at sorting order 0).
        settings.sortingOrder = 100;

        var theme = EnsureTheme();
        if (theme != null)
        {
            settings.themeStyleSheet = theme;
        }

        AssetDatabase.CreateAsset(settings, PanelSettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    /// <summary>
    /// Returns a usable theme: reuses any existing ThemeStyleSheet, otherwise writes a
    /// minimal default-runtime theme. Our own USS carries the visual look regardless, so
    /// even a bare theme renders the windows correctly.
    /// </summary>
    private static ThemeStyleSheet EnsureTheme()
    {
        var guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
        if (guids.Length > 0)
        {
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        EnsureFolder("Assets/UI");
        if (!File.Exists(ThemePath))
        {
            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath);
        }
        return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }
        var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        var leaf = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
