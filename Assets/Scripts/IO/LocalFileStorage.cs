using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Scripts.IO
{
    /// <summary>
    /// Save files on the local file system, written safely: the new text goes to
    /// <c>&lt;file&gt;.tmp</c> first and only replaces the real file once it is complete. A crash or
    /// power cut mid-write therefore leaves the previous save intact (and Steam Cloud never syncs a
    /// half-written file).
    ///
    /// <para>Where the platform cannot replace in one step, the fallback is delete-then-move. A crash
    /// between those two leaves only the finished <c>.tmp</c>, which <see cref="Read"/> recovers.</para>
    /// </summary>
    public class LocalFileStorage : ISaveStorage
    {
        public const string TempSuffix = ".tmp";

        private int _deferDepth;
        private bool _flushPending;

        public bool Exists(string path)
        {
            return File.Exists(path) || File.Exists(path + TempSuffix);
        }

        public string Read(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            // The real file is gone but a finished temp is there: the crash came between deleting the
            // old file and moving the new one in. The temp is the newest complete save.
            var temp = path + TempSuffix;
            if (File.Exists(temp))
            {
                File.Move(temp, path);
                return File.ReadAllText(path);
            }

            return null;
        }

        public void Write(string path, string text)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = path + TempSuffix;
            File.WriteAllText(temp, text);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null);
                }
                catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException || ex is NotSupportedException)
                {
                    File.Delete(path);
                    File.Move(temp, path);
                }
            }
            else
            {
                File.Move(temp, path);
            }

            RequestFlush();
        }

        public void Delete(string path)
        {
            var existed = false;
            if (File.Exists(path))
            {
                File.Delete(path);
                existed = true;
            }
            var temp = path + TempSuffix;
            if (File.Exists(temp))
            {
                File.Delete(temp);
                existed = true;
            }
            if (existed)
            {
                RequestFlush();
            }
        }

        public string[] List(string directory, string searchPattern)
        {
            if (!Directory.Exists(directory))
            {
                return new string[0];
            }

            var found = new List<string>();
            foreach (var file in Directory.GetFiles(directory, searchPattern))
            {
                if (file.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                found.Add(file.Replace('\\', '/'));
            }
            return found.ToArray();
        }

        public virtual void Flush()
        {
        }

        public IDisposable DeferFlush()
        {
            _deferDepth++;
            return new FlushScope(this);
        }

        private void RequestFlush()
        {
            if (_deferDepth > 0)
            {
                _flushPending = true;
                return;
            }
            Flush();
        }

        private void EndDefer()
        {
            if (_deferDepth == 0)
            {
                return;
            }
            _deferDepth--;
            if (_deferDepth == 0 && _flushPending)
            {
                _flushPending = false;
                Flush();
            }
        }

        private sealed class FlushScope : IDisposable
        {
            private LocalFileStorage _owner;

            public FlushScope(LocalFileStorage owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner != null)
                {
                    _owner.EndDefer();
                    _owner = null;
                }
            }
        }
    }
}
