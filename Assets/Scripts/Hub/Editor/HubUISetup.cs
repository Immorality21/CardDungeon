using System.IO;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bootstraps the hub scene: ensures the shared PanelSettings asset exists, drops a UIDocument wired
/// to Hub.uxml and the <see cref="HubManager"/> controller into the open scene, and — unlike the
/// other bootstraps — makes sure the scene-wired singletons the hub screens need are present.
///
/// <para><b>That last part is not optional.</b> <c>MagicCatalog</c> and <c>MagicComboCatalog</c> are
/// <c>SingletonBehaviour</c>s holding <c>[SerializeField]</c> lists, and <c>SingletonBehaviour</c>
/// auto-creates a bare GameObject when it finds none — so a hub scene missing them does not throw,
/// it silently produces an <i>empty</i> catalog and the Forge renders nothing. <c>MetaProgressManager</c>
/// and <c>InventoryManager</c> load from disk in Awake and would survive auto-creation, but the
/// meta-progress prefab is instantiated too so the scene says what it depends on.</para>
///
/// <para>Operates on <b>the open scene</b>, like every other bootstrap here. Run it with HubScene
/// open, then save. Menu: <b>Tools → Hub → Setup Hub UI</b>.</para>
/// </summary>
public class HubUISetup : Editor
{
    private const string UxmlPath = "Assets/UI/Hub/Hub.uxml";
    private const string PanelSettingsPath = "Assets/UI/CardDungeonPanelSettings.asset";
    private const string ThemePath = "Assets/UI/CardDungeonTheme.tss";

    private static readonly string[] RequiredPrefabs =
    {
        "Assets/Prefabs/MagicCatalog.prefab",
        "Assets/Prefabs/MagicComboCatalog.prefab",
        "Assets/Prefabs/MetaProgressManager.prefab"
    };

    [MenuItem("Tools/Hub/Setup Hub UI")]
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

        RunDefinitionSO runDef = null;
        var runDefGuids = AssetDatabase.FindAssets("t:RunDefinitionSO");
        if (runDefGuids.Length > 0)
        {
            runDef = AssetDatabase.LoadAssetAtPath<RunDefinitionSO>(AssetDatabase.GUIDToAssetPath(runDefGuids[0]));
        }

        Assets.Scripts.Heroes.PartyRosterSO partyRoster = null;
        var rosterGuids = AssetDatabase.FindAssets("t:PartyRosterSO");
        if (rosterGuids.Length > 0)
        {
            partyRoster = AssetDatabase.LoadAssetAtPath<Assets.Scripts.Heroes.PartyRosterSO>(
                AssetDatabase.GUIDToAssetPath(rosterGuids[0]));
        }

        var existing = Object.FindAnyObjectByType<HubManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var go = new GameObject("HubUITK");
        Undo.RegisterCreatedObjectUndo(go, "Setup Hub UI");

        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        doc.visualTreeAsset = uxml;

        var manager = go.AddComponent<HubManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("_document").objectReferenceValue = doc;
        if (runDef != null)
        {
            so.FindProperty("_runDefinition").objectReferenceValue = runDef;
        }
        if (partyRoster != null)
        {
            so.FindProperty("_partyRoster").objectReferenceValue = partyRoster;
        }
        so.ApplyModifiedProperties();

        EnsureEventSystem();
        EnsureCamera();
        foreach (var path in RequiredPrefabs)
        {
            EnsurePrefabInstance(path);
        }

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);

        Debug.Log("Hub (UI Toolkit) created in the open scene. Save the scene to persist.");
    }

    /// <summary>Instantiates a prefab into the open scene if nothing there already comes from it.</summary>
    private static void EnsurePrefabInstance(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab not found at {prefabPath}; the hub may be missing a manager.");
            return;
        }

        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab)
            {
                return;
            }
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Setup Hub UI");
    }

    /// <summary>Without one, UI Toolkit receives no input at all.</summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        Undo.RegisterCreatedObjectUndo(go, "Setup Hub UI");
    }

    /// <summary>A scene with no camera renders a "No cameras rendering" warning behind the panel.</summary>
    private static void EnsureCamera()
    {
        if (Camera.main != null)
        {
            return;
        }
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.02f, 0.07f, 1f);
        camera.orthographic = true;
        Undo.RegisterCreatedObjectUndo(go, "Setup Hub UI");
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
