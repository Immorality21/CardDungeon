using UnityEngine;
using UnityEditor;
using ImmoralityGaming.Menu;

[CustomEditor(typeof(MenuPanel), true)]
[CanEditMultipleObjects]
public class MenuPanelEditor : Editor
{

    private MenuPanel menuPanel;
    
    public void OnEnable()
    {
        menuPanel = (MenuPanel)target;
        EditorUtility.SetDirty(menuPanel);
        if (!Application.isPlaying)
        {
            ActivateThisPanel();
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Open panel (in playmode)"))
        {
            menuPanel.OpenPanel();
        }
    }

    private void ActivateThisPanel()
    {
        var otherPanels = FindObjectOfType<MenuManager>().GetComponentsInChildren<MenuPanel>();
        foreach (var panel in otherPanels)
        {
            if (panel == menuPanel)
            {
                continue;
            }
            panel.gameObject.SetActive(false);
        }
        if (!menuPanel.gameObject.activeSelf)
        {
            menuPanel.gameObject.SetActive(true);
        }
    }
}