using System;
using System.IO;
using UnityEngine;

namespace InkFlow.Meta
{
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
        private readonly MigrationContext _migrations;

        /// <param name="migrations">Розкладка галактики й економіка для міграцій; null — дефолти (тести, майстерні).</param>
        public JsonSaveStorage(string? directory = null, string fileName = "save.json", MigrationContext? migrations = null)
        {
            var root = directory ?? Application.persistentDataPath;
            _path = Path.Combine(root, fileName);
            _tempPath = _path + ".tmp";
            _backupPath = _path + ".bak";
            _migrations = migrations ?? MigrationContext.Default;
        }

        /// <summary>Шлях за замовчуванням — його показує дев-панель.</summary>
        public static string DefaultPath =>
            Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>Повний шлях цього сховища.</summary>
        public string Path_ => _path;

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
                return SaveMigrations.Migrate(save, _migrations);
            }
            catch (Exception e)
            {
                // Нечитабельний файл НЕ стирається мовчки: перший же Persist перезаписав би save.json,
                // другий — і .bak, і колекція з нафтою зникли б назавжди. Копія лишається поруч.
                var failed = TryPreserveFailed();
                Debug.LogError($"[InkFlow] Не вдалося прочитати збереження: {e.Message}. Починаємо з чистого; " +
                               (failed is null ? "копію файлу зберегти не вдалося." : $"копія файлу — {failed}."));
                return new SaveFile();
            }
        }

        /// <summary>Копія нечитабельного файлу поруч із ним: save.json.failed-&lt;UTC&gt;; null, якщо скопіювати не вдалося.</summary>
        private string? TryPreserveFailed()
        {
            try
            {
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                var failedPath = $"{_path}.failed-{stamp}";
                File.Copy(_path, failedPath, overwrite: true);
                return failedPath;
            }
            catch (Exception copyError)
            {
                Debug.LogError($"[InkFlow] Копію зіпсованого збереження зробити не вдалося: {copyError.Message}");
                return null;
            }
        }

        /// <summary>Усі копії нечитабельних файлів поруч зі збереженням (для тестів і дев-панелі).</summary>
        public string[] FailedCopies()
        {
            var directory = Path.GetDirectoryName(_path);
            if (directory is null || directory.Length == 0 || !Directory.Exists(directory))
                return Array.Empty<string>();
            return Directory.GetFiles(directory, Path.GetFileName(_path) + ".failed-*");
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
