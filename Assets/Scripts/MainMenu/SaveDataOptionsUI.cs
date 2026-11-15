using System;
using System.IO;
using Assets.Scripts.IO;
using ImmoralityGaming.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// Options ▸ <b>Save data</b> on the title screen: export the whole save as one file, and import
    /// one back (2026-11-15, for moving a playtest save between a phone and a PC).
    ///
    /// <para>In a browser the file goes through the browser (a download, the file picker,
    /// <see cref="BrowserBridge"/>). In the editor it uses the editor's file dialogs; in a desktop
    /// build it uses a fixed file next to the save folder, which is enough for testing.</para>
    ///
    /// <para><b>An import asks first</b>, because it replaces every save file, and then restarts the
    /// game: the hub's managers and several static caches still hold the old save. In a browser that is
    /// a page reload (after the browser storage has been written); elsewhere the caches that can
    /// outlive a scene are dropped and the title scene is loaded again.</para>
    ///
    /// <para>It lives on the title screen only, which reads no save, so nothing is holding the files
    /// that are about to be replaced.</para>
    /// </summary>
    public class SaveDataOptionsUI
    {
        /// <summary>The method the browser's file picker calls back, on <see cref="_receiver"/>.</summary>
        public const string PickedCallback = "OnSaveFilePicked";

        private const string DesktopExportName = "immoral-dungeon-save.json";

        private readonly GameObject _receiver;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly Button _confirmYes;
        private readonly Button _confirmNo;
        private readonly Label _status;

        private SaveBundle _pending;

        public SaveDataOptionsUI(VisualElement root, GameObject receiver)
        {
            _receiver = receiver;
            _exportButton = root.Q<Button>("save-export");
            _importButton = root.Q<Button>("save-import");
            _confirmYes = root.Q<Button>("save-confirm-yes");
            _confirmNo = root.Q<Button>("save-confirm-no");
            _status = root.Q<Label>("save-status");

            if (_exportButton != null)
            {
                _exportButton.clicked += Export;
            }
            if (_importButton != null)
            {
                _importButton.clicked += PickImport;
                // Safari opens a file picker only from inside the tap: arm it on the press so the
                // release opens it (BrowserBridge). Trickle-down, before the button's own handler.
                _importButton.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (BrowserBridge.IsAvailable && _receiver != null)
                    {
                        CancelImport();
                        BrowserBridge.ArmFilePicker(_receiver.name, PickedCallback);
                    }
                }, TrickleDown.TrickleDown);
            }
            if (_confirmYes != null)
            {
                _confirmYes.clicked += ConfirmImport;
            }
            if (_confirmNo != null)
            {
                _confirmNo.clicked += CancelImport;
            }
        }

        /// <summary>Called whenever the Options screen opens: no half-finished import survives it.</summary>
        public void Reset()
        {
            _pending = null;
            SetConfirmShown(false);
            SetStatus(string.Empty);
        }

        // --- export -----------------------------------------------------------

        private void Export()
        {
            var storage = SaveStorage.Current;
            // The folder the game is playing: a sandbox or bot session's own, never the real save under it.
            var directory = FileHandler.CurrentDirectory;
            var bundle = SaveTransfer.Collect(storage, directory);
            if (bundle.Files.Count == 0)
            {
                SetStatus(Loc.Get("menu.options.save-nothing"));
                return;
            }
            var text = JsonUtility.ToJson(bundle, true);
            var fileName = SaveTransfer.ExportFileName(DateTime.Now);

            if (BrowserBridge.IsAvailable)
            {
                BrowserBridge.Download(fileName, text);
                SetStatus(Loc.Get("menu.options.save-exported"));
                return;
            }

#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.SaveFilePanel("Export save", "", fileName, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
#else
            var path = $"{SaveStorage.Root}/{DesktopExportName}";
#endif
            File.WriteAllText(path, text);
            SetStatus(Loc.Format("menu.options.save-exported-to", ("path", path)));
        }

        // --- import -----------------------------------------------------------

        private void PickImport()
        {
            CancelImport();

            if (BrowserBridge.IsAvailable)
            {
                // The browser answers later, through SendMessage -> MainMenuManager.OnSaveFilePicked.
                BrowserBridge.PickFile(_receiver.name, PickedCallback);
                return;
            }

#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Import save", "", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
#else
            var path = $"{SaveStorage.Root}/{DesktopExportName}";
            if (!File.Exists(path))
            {
                SetStatus(Loc.Format("menu.options.save-import-missing", ("path", path)));
                return;
            }
#endif
            OnFilePicked(File.ReadAllText(path));
        }

        /// <summary>The chosen file's text (empty if it could not be read).</summary>
        public void OnFilePicked(string text)
        {
            switch (SaveTransfer.Parse(text, out var bundle))
            {
                case SaveImportCheck.Ok:
                    _pending = bundle;
                    SetStatus(Loc.Format("menu.options.save-import-confirm", ("count", bundle.Files.Count)));
                    SetConfirmShown(true);
                    break;
                case SaveImportCheck.TooNew:
                    SetStatus(Loc.Get("menu.options.save-import-too-new"));
                    break;
                case SaveImportCheck.Empty:
                    SetStatus(Loc.Get("menu.options.save-import-empty"));
                    break;
                default:
                    SetStatus(Loc.Get("menu.options.save-import-invalid"));
                    break;
            }
        }

        private void ConfirmImport()
        {
            if (_pending == null)
            {
                return;
            }
            SaveTransfer.Import(SaveStorage.Current, FileHandler.CurrentDirectory, _pending);
            _pending = null;
            SetConfirmShown(false);
            SetStatus(Loc.Get("menu.options.save-imported"));
            Restart();
        }

        private void CancelImport()
        {
            _pending = null;
            SetConfirmShown(false);
            SetStatus(string.Empty);
        }

        /// <summary>Starts the game again around the imported files.</summary>
        private static void Restart()
        {
            if (BrowserBridge.IsAvailable)
            {
                BrowserBridge.ReloadPage();
                return;
            }

            Audio.AudioOptions.Invalidate();
            Audio.AudioOptions.Apply();
            Combat.CombatSpeed.Invalidate();
            Progression.RunHistoryRecorder.Invalidate();
            InterfaceScale.Invalidate();
            InterfaceScale.Apply();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Replace / Cancel take the place of Export / Import while an import waits.</summary>
        private void SetConfirmShown(bool shown)
        {
            SetShown(_exportButton, !shown);
            SetShown(_importButton, !shown);
            SetShown(_confirmYes, shown);
            SetShown(_confirmNo, shown);
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.EnableInClassList("cd-hidden", !shown);
            }
        }

        private void SetStatus(string text)
        {
            if (_status != null)
            {
                _status.text = text;
            }
        }
    }
}
