using ImmoralityGaming.Fundamentals;
using UnityEngine;
using UnityEngine.UI;

namespace ImmoralityGaming.Menu
{
    public class PopupManager : SingletonBehaviour<PopupManager>
    {
        public TMPro.TextMeshProUGUI Title, Message;
        public Button Confirm, Close;
        public GameObject Panel;

        public bool AnyPopupIsOpen
        {
            get
            {
                return Panel != null && Panel.activeSelf;
            }
        }

        public enum DialogType
        {
            Normal,
            NormalWithoutAnimation,
            DoNotCloseOnOk,
            Locked,
            OkOnly
        }

        public void ClosePopup()
        {
            Panel.SetActive(false);
        }

        public void ShowPopup(PopupSettings settings)
        {
            settings.OnShowAction?.Invoke();

            Panel.SetActive(true);
            Title.text = settings.Title;
            Message.text = settings.Message;

            Confirm.onClick.RemoveAllListeners();
            Close.onClick.RemoveAllListeners();

            Confirm.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = (settings.PositiveButtonText == string.Empty) ? "Ok" : settings.PositiveButtonText;
            Close.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = (settings.NegativeButtonText == string.Empty) ? "Close" : settings.NegativeButtonText;

            Confirm.interactable = true;

            if (settings.PositiveAction != null)
            {
                Confirm.gameObject.SetActive(true);
                Confirm.onClick.AddListener(() => settings.PositiveAction());

                if (settings.NegativeAction != null)
                {
                    Close.onClick.AddListener(() => settings.NegativeAction());
                }
            }
            else
            {
                Confirm.gameObject.SetActive(false);

                if (settings.NegativeAction != null)
                {
                    Close.onClick.AddListener(() => settings.NegativeAction());
                }
            }

            if (settings.DialogType == DialogType.Locked)
            {
                Confirm.gameObject.SetActive(false);
                Close.gameObject.SetActive(false);
            }
            else if (settings.DialogType == DialogType.OkOnly)
            {
                Close.gameObject.SetActive(false);
                Confirm.gameObject.SetActive(true);
                Confirm.onClick.AddListener(() => Panel.SetActive(false));
            }
            else if (settings.DialogType == DialogType.DoNotCloseOnOk)
            {
                Close.gameObject.SetActive(true);
                Close.onClick.AddListener(() => Panel.SetActive(false));
            }
            else
            {
                Close.gameObject.SetActive(true);
                Confirm.onClick.AddListener(() => Panel.SetActive(false));
                Close.onClick.AddListener(() => Panel.SetActive(false));
            }

            if (settings.OnCloseAction != null)
            {
                Confirm.onClick.AddListener(() => settings.OnCloseAction());
                Close.onClick.AddListener(() => settings.OnCloseAction());
            }

            if (settings.HideNegativeButton)
            {
                Close.gameObject.SetActive(false);
            }
        }

        public void ShowNoInternetPopup()
        {
            ShowPopup(new PopupSettings
            {
                Title = "No internet!",
                Message = "Failed to connect to the internet. Make sure you have Wifi or Mobile Data enabled and try again.",
                DialogType = DialogType.DoNotCloseOnOk,
                PositiveButtonText = "Retry",
                //PositiveAction = () =>
                //{
                //    if (NetworkManager.InvokeNoInternetActions())
                //    {
                //        ClosePopup();
                //    }
                //}
            });

            Close.gameObject.SetActive(false);
        }

        public void SetPositiveButtonState(bool state)
        {
            Confirm.interactable = state;
        }

        public void ShowGenericError(string text = "")
        {
            var settings = new PopupSettings
            {
                Title = "Error",
                Message = text == string.Empty ? "Something went wrong." : text
            };

            ShowPopup(settings);
        }
    }
}