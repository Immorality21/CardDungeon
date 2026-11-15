namespace Assets.Scripts.IO
{
    /// <summary>
    /// Save files in a browser build. Unity's WebGL file system keeps <c>/idbfs</c> in memory and
    /// copies it to the browser's IndexedDB only when asked, so every write and delete ends with a
    /// sync (<c>SaveStorage_Sync</c> in <c>Assets/Plugins/WebGL/SaveStorage.jslib</c>, which folds
    /// overlapping requests into one). Without it a save could vanish when the tab closes.
    ///
    /// <para>The folder is fixed (<see cref="SaveStorage.WebGLRoot"/>), not
    /// <c>Application.persistentDataPath</c>: that path is a hash of the page URL, and itch.io serves
    /// every upload from a new URL, so each new build used to start with an empty save.</para>
    /// </summary>
    public sealed class WebGLSaveStorage : LocalFileStorage
    {
        public override void Flush()
        {
            BrowserBridge.SyncFileSystem();
        }
    }
}
