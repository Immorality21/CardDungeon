using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ImmoralityGaming.Menu
{
	public class MenuPanel : MonoBehaviour
	{
	    public List<MenuPanel> Parents;
	    [HideInInspector]
	    public MenuPanel ActiveParent;
        public bool ShowBackButton = true;
        public bool PreventGoingToHome = false;

        public UnityEvent OnPanelOpen;
        public UnityEvent OnPanelClose;

	    public virtual void OpenPanel()
	    {
			if (Parents.Contains(MenuManager.Instance.ActivePanel))
	        {
	            ActiveParent = MenuManager.Instance.ActivePanel;
	        } 

	        gameObject.SetActive(true);
	        if (MenuManager.Instance.ActivePanel != null && MenuManager.Instance.ActivePanel != this)
	        {
				MenuManager.Instance.ActivePanel.ClosePanel();
	        }

	        MenuManager.Instance.ActivePanel = this;
            OnPanelOpen?.Invoke();
        }

	    public virtual void ClosePanel()
	    {
            OnPanelClose?.Invoke();
            gameObject.SetActive(false);
        }
	}
}