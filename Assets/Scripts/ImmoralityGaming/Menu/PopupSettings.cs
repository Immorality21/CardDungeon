using System;
using static ImmoralityGaming.Menu.PopupManager;

namespace ImmoralityGaming.Menu
{
    public class PopupSettings
    {
        public string Title = string.Empty;
        public string Message = string.Empty;

        public string PositiveButtonText = string.Empty;
        public string NegativeButtonText = string.Empty;
        public string ThirdButtonText = string.Empty;

        public Action OnShowAction = null;
        public Action OnCloseAction = null;

        public Action PositiveAction = null;
        public Action NegativeAction = null;
        public Action ThirdAction = null;

        public bool HideNegativeButton;

        public DialogType DialogType = DialogType.Normal;
    }
}