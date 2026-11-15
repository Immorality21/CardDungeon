using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.IO
{
    /// <summary>One save file inside an exported bundle.</summary>
    [Serializable]
    public class SaveBundleFile
    {
        public string Name;
        public string Json;
    }

    /// <summary>A whole save folder as one JSON document, for moving a save between devices.</summary>
    [Serializable]
    public class SaveBundle
    {
        public const string GameTag = "immoral-dungeon-save";

        /// <summary>Always <see cref="GameTag"/> in an export. No initializer: JsonUtility keeps a
        /// field's default when the text lacks it, which would pass any JSON off as a save.</summary>
        public string Game;
        public int FormatVersion;
        public string GameVersion;
        public string ExportedUtc;
        public List<SaveBundleFile> Files = new List<SaveBundleFile>();
    }

    public enum SaveImportCheck
    {
        Ok,
        /// <summary>Not a save bundle at all, or one of its files is not readable JSON.</summary>
        NotASave,
        /// <summary>A save bundle without any game files in it.</summary>
        Empty,
        /// <summary>Written by a newer build than this one.</summary>
        TooNew,
    }

    /// <summary>
    /// Export and import of a whole save folder (Options ▸ Save data). Pure over an
    /// <see cref="ISaveStorage"/>, so the browser, the desktop and the tests share it; moving the text
    /// in and out of the device is the caller's job (<see cref="BrowserBridge"/> on WebGL).
    ///
    /// <para><c>SaveInfo.json</c> is not a file in the bundle: its version travels as
    /// <see cref="SaveBundle.FormatVersion"/>, and an import writes it back so the next load runs the
    /// usual <see cref="SaveFormat"/> steps on an older bundle.</para>
    ///
    /// <para><b>Device settings stay on the device</b> (<see cref="DeviceFiles"/>: volume, interface
    /// size, combat speed). They are left out of an export and kept through an import, so a phone's
    /// 150% interface does not follow the save onto a monitor.</para>
    ///
    /// <para><b>An import never leaves the folder empty-handed.</b> Every file is checked as JSON
    /// before anything is touched; the current save is written to <see cref="BackupPath"/> first; the
    /// new files are written before the old ones are deleted; and the browser storage is synced once,
    /// at the end, rather than once per file.</para>
    /// </summary>
    public static class SaveTransfer
    {
        /// <summary>Preferences of this device, not progress: never exported, never replaced.</summary>
        public static readonly string[] DeviceFiles = { "Audio", "Display", "Gameplay" };

        /// <summary>The suggested file name for an export.</summary>
        public static string ExportFileName(DateTime now)
        {
            return $"immoral-dungeon-save-{now:yyyy-MM-dd-HHmm}.json";
        }

        /// <summary>Where an import keeps the save it replaced: a bundle beside the save folder,
        /// importable again by hand.</summary>
        public static string BackupPath(string directory)
        {
            return $"{directory}_backup/before-import.json";
        }

        public static SaveBundle Collect(ISaveStorage storage, string directory)
        {
            // A folder with no SaveInfo.json is version 0 (written before versions were recorded); it
            // must travel as 0 so the importing build runs every step on it.
            var info = SaveFormat.ReadInfo(storage, directory);
            var bundle = new SaveBundle
            {
                Game = SaveBundle.GameTag,
                FormatVersion = info != null ? info.FormatVersion : 0,
                GameVersion = Application.version,
                ExportedUtc = DateTime.UtcNow.ToString("o"),
            };

            var files = storage.List(directory, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == SaveFormat.InfoFileName || IsDeviceFile(name))
                {
                    continue;
                }
                var json = storage.Read(path);
                if (json != null)
                {
                    bundle.Files.Add(new SaveBundleFile { Name = name, Json = json });
                }
            }
            return bundle;
        }

        public static string Export(ISaveStorage storage, string directory)
        {
            return JsonUtility.ToJson(Collect(storage, directory), true);
        }

        /// <summary>Reads and checks an imported text. <paramref name="bundle"/> is set only when the
        /// answer is <see cref="SaveImportCheck.Ok"/>.</summary>
        public static SaveImportCheck Parse(string text, out SaveBundle bundle)
        {
            bundle = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return SaveImportCheck.NotASave;
            }

            SaveBundle parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveBundle>(text);
            }
            catch (ArgumentException)
            {
                return SaveImportCheck.NotASave;
            }

            if (parsed == null || parsed.Game != SaveBundle.GameTag)
            {
                return SaveImportCheck.NotASave;
            }
            if (parsed.FormatVersion > SaveFormat.CurrentVersion)
            {
                return SaveImportCheck.TooNew;
            }

            var kept = new List<SaveBundleFile>();
            foreach (var file in parsed.Files ?? new List<SaveBundleFile>())
            {
                if (file == null || !IsSafeName(file.Name) || file.Name == SaveFormat.InfoFileName || IsDeviceFile(file.Name))
                {
                    continue;
                }
                // A damaged file would throw on every load after the old save is gone: refuse the
                // whole bundle instead.
                if (!IsJsonObject(file.Json))
                {
                    return SaveImportCheck.NotASave;
                }
                kept.Add(file);
            }
            if (kept.Count == 0)
            {
                return SaveImportCheck.Empty;
            }

            parsed.Files = kept;
            bundle = parsed;
            return SaveImportCheck.Ok;
        }

        /// <summary>Replaces the save files in <paramref name="directory"/> with the bundle's, keeping
        /// the device files. The caller restarts the game afterwards: managers and static caches still
        /// hold the old save.</summary>
        public static void Import(ISaveStorage storage, string directory, SaveBundle bundle)
        {
            using (storage.DeferFlush())
            {
                // 1. Keep what is about to be replaced.
                var current = Collect(storage, directory);
                if (current.Files.Count > 0)
                {
                    storage.Write(BackupPath(directory), JsonUtility.ToJson(current, true));
                }

                // 2. Write the new files, then 3. delete the old ones the bundle does not have. A
                // failure part-way leaves the old save plus some new files, never an empty folder.
                var incoming = new List<string>();
                foreach (var file in bundle.Files)
                {
                    storage.Write($"{directory}/{file.Name}.json", file.Json);
                    incoming.Add(file.Name);
                }

                foreach (var path in storage.List(directory, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (!incoming.Contains(name) && !IsDeviceFile(name) && name != SaveFormat.InfoFileName)
                    {
                        storage.Delete(path);
                    }
                }

                var info = new SaveInfoData { FormatVersion = bundle.FormatVersion, GameVersion = bundle.GameVersion };
                storage.Write(SaveFormat.InfoPath(directory), JsonUtility.ToJson(info, true));
            }

            SaveFormat.Forget(directory);
        }

        public static bool IsDeviceFile(string name)
        {
            return Array.IndexOf(DeviceFiles, name) >= 0;
        }

        /// <summary>A plain ASCII file name: letters, digits, '_' and '-', and not one of the names
        /// Windows reserves. Keeps an edited bundle from writing outside the save folder.</summary>
        public static bool IsSafeName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64)
            {
                return false;
            }
            foreach (var c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                {
                    return false;
                }
            }
            var upper = name.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL"
                || ((upper.StartsWith("COM") || upper.StartsWith("LPT")) && upper.Length == 4 && char.IsDigit(upper[3])))
            {
                return false;
            }
            return true;
        }

        /// <summary>Whether <paramref name="json"/> parses as a JSON object, the shape every save file has.</summary>
        public static bool IsJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{')
            {
                return false;
            }
            try
            {
                JsonUtility.FromJsonOverwrite(json, new JsonProbe());
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class JsonProbe
        {
        }
    }
}
