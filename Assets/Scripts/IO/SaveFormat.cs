using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.IO
{
    /// <summary>
    /// <c>SaveInfo.json</c>: which format the files in a save folder are written in. Kept per folder,
    /// not per file, because the files change together (a format step can move a field from one file
    /// to another).
    /// </summary>
    [Serializable]
    public class SaveInfoData : IWriteable
    {
        public int FormatVersion;

        /// <summary><c>Application.version</c> of the build that last wrote the folder. For reading
        /// a bug report, not for logic.</summary>
        public string GameVersion;

        public string GetFileName()
        {
            return SaveFormat.InfoFileName;
        }
    }

    /// <summary>
    /// The save format's version and the steps that bring an older folder up to date.
    ///
    /// <para>A folder without <c>SaveInfo.json</c> is version 0: every save written before
    /// 2026-11-15. <see cref="Ensure"/> runs once per folder per session, the first time a
    /// <see cref="FileHandler"/> loads or saves there, applies each step from the folder's version to
    /// <see cref="CurrentVersion"/>, and records the new version. A folder written by a <i>newer</i>
    /// build is loaded as it is but becomes <b>read-only</b> for this session (<see cref="IsReadOnly"/>,
    /// checked by <see cref="FileHandler"/>): writing it in the old format would drop the fields this
    /// build does not know while SaveInfo still claimed the newer version.</para>
    ///
    /// <para>A damaged <c>SaveInfo.json</c> counts as missing (version 0, with a warning) and is
    /// rewritten, rather than making every load throw.</para>
    ///
    /// <para><b>To change the format:</b> bump <see cref="CurrentVersion"/> and add a case to
    /// <see cref="Step"/> that turns version N-1 into N, reading and writing through the
    /// <see cref="ISaveStorage"/> it is given. Older imported files go through the same steps.</para>
    /// </summary>
    public static class SaveFormat
    {
        public const int CurrentVersion = 1;
        public const string InfoFileName = "SaveInfo";

        private static readonly List<string> Checked = new List<string>();
        private static readonly List<string> ReadOnly = new List<string>();

        /// <summary>Whether <paramref name="directory"/> was written by a newer build and must not be
        /// written by this one.</summary>
        public static bool IsReadOnly(string directory)
        {
            return ReadOnly.Contains(directory);
        }

        /// <summary>Brings <paramref name="directory"/> up to the current format, once per session.
        /// <paramref name="writing"/> says a save is about to be written: an empty folder only gets
        /// its <c>SaveInfo.json</c> then, so merely looking for a save creates nothing.</summary>
        public static void Ensure(ISaveStorage storage, string directory, bool writing)
        {
            if (Checked.Contains(directory))
            {
                return;
            }

            var infoPath = InfoPath(directory);
            var info = ReadInfo(storage, directory);
            var version = info != null ? info.FormatVersion : 0;

            if (version > CurrentVersion)
            {
                Debug.LogWarning($"Save folder {directory} was written by a newer build (format {version}, this build knows {CurrentVersion}). Loading it as it is, and not saving to it.");
                Checked.Add(directory);
                if (!ReadOnly.Contains(directory))
                {
                    ReadOnly.Add(directory);
                }
                return;
            }

            if (info == null && !writing && storage.List(directory, "*.json").Length == 0)
            {
                // Nothing here yet; check again when something is written.
                return;
            }

            for (var from = version; from < CurrentVersion; from++)
            {
                Step(storage, directory, from);
            }

            if (info == null || version != CurrentVersion || info.GameVersion != Application.version)
            {
                var updated = new SaveInfoData { FormatVersion = CurrentVersion, GameVersion = Application.version };
                storage.Write(infoPath, JsonUtility.ToJson(updated, true));
            }

            Checked.Add(directory);
        }

        /// <summary>The folder's recorded format, or null when it has no <c>SaveInfo.json</c>.</summary>
        public static SaveInfoData ReadInfo(ISaveStorage storage, string directory)
        {
            var json = storage.Read(InfoPath(directory));
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            var info = new SaveInfoData();
            try
            {
                JsonUtility.FromJsonOverwrite(json, info);
            }
            catch (System.ArgumentException)
            {
                Debug.LogWarning($"Save folder {directory} has an unreadable {InfoFileName}.json; treating it as format 0.");
                return null;
            }
            return info;
        }

        /// <summary>Forgets that <paramref name="directory"/> was checked, so the next access checks
        /// it again (after an import replaced its files). Null forgets every folder.</summary>
        public static void Forget(string directory = null)
        {
            if (directory == null)
            {
                Checked.Clear();
                ReadOnly.Clear();
            }
            else
            {
                Checked.Remove(directory);
                ReadOnly.Remove(directory);
            }
        }

        public static string InfoPath(string directory)
        {
            return $"{directory}/{InfoFileName}.json";
        }

        /// <summary>Turns a folder in format <paramref name="from"/> into format <c>from + 1</c>.</summary>
        private static void Step(ISaveStorage storage, string directory, int from)
        {
            switch (from)
            {
                case 0:
                    // 0 -> 1: the files are unchanged; version 1 only starts recording the version.
                    break;
                default:
                    Debug.LogWarning($"No save format step from version {from}.");
                    break;
            }
        }
    }
}
