using UnityEngine;

namespace Assets.Scripts.IO
{
    /// <summary>
    /// Reads and writes the game's JSON save files by name. <i>Where</i> the bytes go is
    /// <see cref="ISaveStorage"/>'s job (<see cref="SaveStorage.Current"/>): safe writes on the local
    /// disk, IndexedDB in a browser. The first load or save in a folder brings it up to the current
    /// <see cref="SaveFormat"/>.
    /// </summary>
    public sealed class FileHandler
    {
        private const string FILE_EXTENSION = ".json";

        private readonly string _directoryPath;
        private readonly ISaveStorage _storage;

        /// <summary>
        /// When set, every <see cref="FileHandler"/> created afterwards reads and writes here instead
        /// of <see cref="DefaultDirectory"/>. The sandbox points it at a throwaway folder so a test
        /// session can never touch the player's save. Null means the real save folder.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        /// <summary>The player's real save folder.</summary>
        public static string DefaultDirectory => $"{SaveStorage.Root}/savedata";

        /// <summary>The folder a handler created now would use.</summary>
        public static string CurrentDirectory =>
            string.IsNullOrEmpty(DirectoryOverride) ? DefaultDirectory : DirectoryOverride;

        public FileHandler()
            : this(CurrentDirectory)
        {
        }

        /// <summary>A handler bound to one folder, whatever the override says.</summary>
        public FileHandler(string directoryPath)
            : this(directoryPath, SaveStorage.Current)
        {
        }

        /// <summary>A handler over a given storage (tests use an in-memory one).</summary>
        public FileHandler(string directoryPath, ISaveStorage storage)
        {
            _directoryPath = directoryPath;
            _storage = storage;
        }

        public string DirectoryPath => _directoryPath;

        public void Save(IWriteable writeable)
        {
            SaveFormat.Ensure(_storage, _directoryPath, writing: true);
            if (RefuseWrite(writeable.GetFileName()))
            {
                return;
            }

            var filePath = PathOf(writeable.GetFileName());
            var json = JsonUtility.ToJson(writeable, true);
            _storage.Write(filePath, json);

#if UNITY_EDITOR
            Debug.Log($"Saved {writeable.GetFileName()} to {filePath}");
#endif
        }

        public T Load<T>() where T : IWriteable, new()
        {
            var data = new T();
            return LoadInto(data, data.GetFileName());
        }

        public T LoadFromFile<T>(string fileName) where T : new()
        {
            return LoadInto(new T(), fileName);
        }

        public string[] FindFiles(string prefix)
        {
            return _storage.List(_directoryPath, $"{prefix}*{FILE_EXTENSION}");
        }

        public void Delete(IWriteable writeable)
        {
            SaveFormat.Ensure(_storage, _directoryPath, writing: false);
            if (RefuseWrite(writeable.GetFileName()))
            {
                return;
            }
            _storage.Delete(PathOf(writeable.GetFileName()));
        }

        /// <summary>A folder written by a newer build is not changed by this one (see
        /// <see cref="SaveFormat"/>).</summary>
        private bool RefuseWrite(string fileName)
        {
            if (!SaveFormat.IsReadOnly(_directoryPath))
            {
                return false;
            }
            Debug.LogError($"Not saving {fileName}: the save folder was written by a newer version of the game.");
            return true;
        }

        private T LoadInto<T>(T data, string fileName)
        {
            SaveFormat.Ensure(_storage, _directoryPath, writing: false);

            var json = _storage.Read(PathOf(fileName));
            if (json == null)
            {
                return data;
            }

            JsonUtility.FromJsonOverwrite(json, data);
            return data;
        }

        private string PathOf(string fileName)
        {
            return $"{_directoryPath}/{fileName}{FILE_EXTENSION}";
        }
    }
}
