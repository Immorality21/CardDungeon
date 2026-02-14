using ImmoralityGaming.Fundamentals;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace ImmoralityGaming.Menu
{
    public class MenuManager : SingletonBehaviour<MenuManager>
    {
        public MenuPanel ActivePanel;
        public static List<MenuPanel> AllPanels;

        public bool AutoPanelSelect = true;

        public MenuPanel HomePanel;

        public UnityEvent OnBackPressed;

        protected override void Awake()
        {
            base.Awake();

            AllPanels = GetComponentsInChildren<MenuPanel>(true).ToList();
            foreach (var panel in AllPanels)
            {
                panel.gameObject.SetActive(false);
            }
            HomePanel?.OpenPanel();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Back();
            }
        }

        public void OpenHome()
        {
            if (PopupManager.Instance.AnyPopupIsOpen)
            {
                return;
            }
            HomePanel.OpenPanel();
        }

        public MenuPanel FindPanel(Func<MenuPanel, bool> predicate)
        {
            return AllPanels.FirstOrDefault(predicate);
        }

        public MenuPanel SelectPanel(Func<MenuPanel, bool> predicate)
        {
            var panel = FindPanel(predicate);

            SelectPanel(FindPanel(predicate));

            return panel;
        }

        public void SelectPanel(MenuPanel menuPanel)
        {
            AllPanels.Find(x => x == menuPanel).OpenPanel();
        }

        public void Back()
        {
            if (PopupManager.Instance.AnyPopupIsOpen)
            {
                return;
            }

            if (ActivePanel.Parents.Count == 1)
            {
                ActivePanel.Parents.First().OpenPanel();
            }
            else if (ActivePanel.Parents.Count > 1)
            {
                if (ActivePanel.ActiveParent != null)
                {
                    ActivePanel.ActiveParent.OpenPanel();
                }
                else
                {
                    ActivePanel.Parents.First().OpenPanel();
                }
            }
            else if (ActivePanel != HomePanel && ActivePanel.PreventGoingToHome == false)
            {
                HomePanel?.OpenPanel();
            }

            OnBackPressed.Invoke();
        }
    }
}