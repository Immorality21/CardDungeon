using System.IO;
using UnityEngine;

namespace Assets.Scripts.IO
{
    public sealed class FileHandler
    {
        private const string FILE_EXTENSION = ".json";

        private readonly string _directoryPath;

        /// <summary>
        /// When set, every <see cref="FileHandler"/> created afterwards reads and writes here instead
        /// of <see cref="DefaultDirectory"/>. The sandbox points it at a throwaway folder so a test
        /// session can never touch the player's save. Null means the real save folder.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        /// <summary>The player's real save folder.</summary>
        public static string DefaultDirectory => $"{Application.persistentDataPath}/savedata";

        /// <summary>The folder a handler created now would use.</summary>
        public static string CurrentDirectory =>
            string.IsNullOrEmpty(DirectoryOverride) ? DefaultDirectory : DirectoryOverride;

        public FileHandler()
        {
            _directoryPath = CurrentDirectory;
        }

        /// <summary>A handler bound to one folder, whatever the override says.</summary>
        public FileHandler(string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        public void Save(IWriteable writeable)
        {
            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            var filePath = $"{_directoryPath}/{writeable.GetFileName()}{FILE_EXTENSION}";
            var json = JsonUtility.ToJson(writeable, true);
            File.WriteAllText(filePath, json);

#if UNITY_EDITOR
            Debug.Log($"Saved {writeable.GetFileName()} to {filePath}");
#endif
        }

        public T Load<T>() where T : IWriteable, new()
        {
            var data = new T();
            var filePath = $"{_directoryPath}/{data.GetFileName()}{FILE_EXTENSION}";

            if (!File.Exists(filePath))
            {
                return data;
            }

            var json = File.ReadAllText(filePath);
            JsonUtility.FromJsonOverwrite(json, data);
            return data;
        }

        public T LoadFromFile<T>(string fileName) where T : new()
        {
            var data = new T();
            var filePath = $"{_directoryPath}/{fileName}{FILE_EXTENSION}";

            if (!File.Exists(filePath))
            {
                return data;
            }

            var json = File.ReadAllText(filePath);
            JsonUtility.FromJsonOverwrite(json, data);
            return data;
        }

        public string[] FindFiles(string prefix)
        {
            if (!Directory.Exists(_directoryPath))
            {
                return new string[0];
            }

            return Directory.GetFiles(_directoryPath, $"{prefix}*{FILE_EXTENSION}");
        }

        public void Delete(IWriteable writeable)
        {
            var filePath = $"{_directoryPath}/{writeable.GetFileName()}{FILE_EXTENSION}";

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
