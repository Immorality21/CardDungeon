using System.Runtime.InteropServices;

namespace Assets.Scripts.IO
{
    /// <summary>
    /// The few browser calls the save system needs, implemented in
    /// <c>Assets/Plugins/WebGL/SaveStorage.jslib</c>. Outside a WebGL player every call is a no-op
    /// and <see cref="IsAvailable"/> is false, so callers can use them without their own #if.
    ///
    /// <para><b>The file picker needs two calls on a touch screen:</b> <see cref="ArmFilePicker"/> on
    /// the button's press, so the release's own browser event can open it (Safari refuses a picker
    /// opened outside the tap, and Unity sees the tap a frame late), then <see cref="PickFile"/> on the
    /// click, which opens it only if the armed one did not.</para>
    /// </summary>
    public static class BrowserBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void SaveStorage_Sync();
        [DllImport("__Internal")] private static extern void SaveStorage_Download(string fileName, string text);
        [DllImport("__Internal")] private static extern void SaveStorage_PickFile(string gameObjectName, string methodName);
        [DllImport("__Internal")] private static extern void SaveStorage_ArmPicker(string gameObjectName, string methodName);
        [DllImport("__Internal")] private static extern void SaveStorage_Reload();

        public static bool IsAvailable => true;
        public static void SyncFileSystem() { SaveStorage_Sync(); }
        public static void Download(string fileName, string text) { SaveStorage_Download(fileName, text); }
        public static void PickFile(string gameObjectName, string methodName) { SaveStorage_PickFile(gameObjectName, methodName); }
        public static void ArmFilePicker(string gameObjectName, string methodName) { SaveStorage_ArmPicker(gameObjectName, methodName); }
        public static void ReloadPage() { SaveStorage_Reload(); }
#else
        public static bool IsAvailable => false;
        public static void SyncFileSystem() { }
        public static void Download(string fileName, string text) { }
        public static void PickFile(string gameObjectName, string methodName) { }
        public static void ArmFilePicker(string gameObjectName, string methodName) { }
        public static void ReloadPage() { }
#endif
    }
}
