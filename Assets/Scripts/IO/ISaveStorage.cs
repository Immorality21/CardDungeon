using System;

namespace Assets.Scripts.IO
{
    /// <summary>
    /// Where save files physically go. <see cref="FileHandler"/> decides <i>what</i> is saved and under
    /// which name; this decides how the bytes reach durable storage on the platform the game runs on.
    ///
    /// <para>Today: <see cref="LocalFileStorage"/> (Windows, the editor, any native build) and
    /// <see cref="WebGLSaveStorage"/> (the browser, which keeps files in IndexedDB and needs an
    /// explicit sync). A remote backend (cross-device saves) would be a third implementation; nothing
    /// above this interface would change. See the IO guide.</para>
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string path);

        /// <summary>The file's text, or null if there is no such file.</summary>
        string Read(string path);

        /// <summary>Replaces the file in one step: a crash part-way leaves the old file or the new
        /// one, never half of either.</summary>
        void Write(string path, string text);

        void Delete(string path);

        /// <summary>Full paths of the files in <paramref name="directory"/> matching
        /// <paramref name="searchPattern"/> (e.g. <c>Meta*.json</c>). Empty if the folder does not
        /// exist. Never lists the write-in-progress files the safe write leaves behind.</summary>
        string[] List(string directory, string searchPattern);

        /// <summary>Pushes everything written so far to durable storage. A no-op where a write is
        /// already durable.</summary>
        void Flush();

        /// <summary>Holds back the flush each write and delete would do until the returned handle is
        /// disposed, then flushes once. For a burst of writes that belong together (an import), so
        /// the browser storage never captures the burst half-done. Nests.</summary>
        IDisposable DeferFlush();
    }
}
