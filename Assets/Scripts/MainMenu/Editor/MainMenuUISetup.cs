using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bootstraps the title screen: ensures the shared PanelSettings asset exists, then drops a
/// UIDocument into the open MenuScene wired to MainMenu.uxml and the MainMenuManager controller.
/// Replaces any prior menu instance.
///
/// <para>It wires <b>one</b> serialized ref now - the document. The run definition and the party
/// roster went to <c>HubUISetup</c> with the screens that needed them, which is the whole point of
/// the split: this scene can be shown before a save file is chosen.</para>
/// </summary>
public class MainMenuUISetup : Editor
{
    private const string UxmlPath = "Assets/UI/MainMenu/MainMenu.uxml";
    private const string PanelSettingsPath = "Assets/UI/CardDungeonPanelSettings.asset";
    private const string ThemePath = "Assets/UI/CardDungeonTheme.tss";

    [MenuItem("Tools/MainMenu/Setup Main Menu UI")]
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

        // Remove the prior menu (old uGUI canvas hosting MainMenuManager, or a previous bootstrap).
        var existing = Object.FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var go = new GameObject("MainMenuUITK");
        Undo.RegisterCreatedObjectUndo(go, "Setup Main Menu UI");

        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        doc.visualTreeAsset = uxml;

        var manager = go.AddComponent<MainMenuManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("_document").objectReferenceValue = doc;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);

        Debug.Log("Title screen (UI Toolkit) created in the open scene. Save the scene to persist.");
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
