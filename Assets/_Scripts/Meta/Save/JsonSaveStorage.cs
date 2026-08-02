using System;
using System.IO;
using UnityEngine;

namespace InkFlow.Meta
{
    public interface ISaveStorage
    {
        bool Exists { get; }
        SaveFile Load();
        void Save(SaveFile save);
        void Delete();
    }

    /// <summary>
    /// JSON-збереження в Application.persistentDataPath — працює однаково на iOS та Android (§10).
    ///
    /// АТОМАРНИЙ ЗАПИС: пишемо в save.tmp і лише потім підміняємо save.json. Без цього
    /// вбитий під час запису застосунок = втрачений прогрес гравця.
    /// BinaryFormatter не використовуємо — він заборонений і небезпечний.
    /// </summary>
    public sealed class JsonSaveStorage : ISaveStorage
    {
        private readonly string _path;
        private readonly string _tempPath;
        private readonly string _backupPath;

        public JsonSaveStorage(string? directory = null, string fileName = "save.json")
        {
            var root = directory ?? Application.persistentDataPath;
            _path = Path.Combine(root, fileName);
            _tempPath = _path + ".tmp";
            _backupPath = _path + ".bak";
        }

        public bool Exists => File.Exists(_path);

        public SaveFile Load()
        {
            if (!File.Exists(_path))
            {
                // Якщо основний файл зник, але лишився бекап від попереднього запису — беремо його.
                if (File.Exists(_backupPath))
                    File.Copy(_backupPath, _path);
                else
                    return new SaveFile();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var save = JsonUtility.FromJson<SaveFile>(json) ?? new SaveFile();
                return SaveMigrations.Migrate(save);
            }
            catch (Exception e)
            {
                Debug.LogError($"[InkFlow] Не вдалося прочитати збереження: {e.Message}. Починаємо з чистого.");
                return new SaveFile();
            }
        }

        public void Save(SaveFile save)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));

            var json = JsonUtility.ToJson(save, prettyPrint: false);
            var directory = Path.GetDirectoryName(_path);
            if (directory is not null && directory.Length > 0)
                Directory.CreateDirectory(directory);

            File.WriteAllText(_tempPath, json);

            if (File.Exists(_path))
                File.Replace(_tempPath, _path, _backupPath);
            else
                File.Move(_tempPath, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            if (File.Exists(_backupPath)) File.Delete(_backupPath);
        }
    }
}
