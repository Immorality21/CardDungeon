using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.IO.Editor
{
    /// <summary>
    /// <c>Tools ▸ Open Save Folder</c> and <c>Tools ▸ Delete Save…</c>: the real save folder
    /// (<see cref="FileHandler.DefaultDirectory"/>), never the sandbox's. Deleting is refused in play
    /// mode, where the running game would write the files straight back.
    /// </summary>
    public static class SaveFolderMenu
    {
        [MenuItem("Tools/Open Save Folder", false, 40)]
        public static void Open()
        {
            string folder = FileHandler.DefaultDirectory;
            // Before the first save the folder does not exist yet; open its parent rather than nothing.
            EditorUtility.RevealInFinder(Directory.Exists(folder) ? folder + "/" : Application.persistentDataPath + "/");
        }

        [MenuItem("Tools/Delete Save…", false, 41)]
        public static void Delete()
        {
            string folder = FileHandler.DefaultDirectory;
            if (!Directory.Exists(folder))
            {
                EditorUtility.DisplayDialog("Delete Save", "There is no save to delete.\n\n" + folder, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete Save",
                    "Delete the whole save folder? The next New Game starts from scratch, tutorial included.\n\n"
                    + folder + "\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
            {
                return;
            }
            Directory.Delete(folder, true);
            Debug.Log("Deleted the save folder: " + folder);
        }

        [MenuItem("Tools/Delete Save…", true)]
        private static bool CanDelete()
        {
            return !EditorApplication.isPlaying;
        }
    }
}
