using System.IO;
using UnityEngine;

namespace Assets.Scripts.IO
{
    public sealed class FileHandler
    {
        private const string FILE_EXTENSION = ".json";

        private readonly string _directoryPath;

        public FileHandler()
        {
            _directoryPath = $"{Application.persistentDataPath}/savedata";
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
