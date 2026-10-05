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
    ///
    /// ЗАПОБІЖНИК: нечитабельний файл не стирається. Його відкладають поруч (копією, а якщо файл
    /// не читається взагалі — перейменуванням, яке читати не мусить); якщо не вдалось і це,
    /// сховище більше не пише поверх <c>save.json</c> — краще втратити сесію, ніж роки прогресу.
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

        /// <summary>
        /// Читання провалилось, а відкласти файл поруч не вдалося: на диску лежить єдиний примірник
        /// прогресу, якого ми не змогли прочитати. Поки true, <see cref="Save"/> його не чіпає.
        /// </summary>
        public bool LoadFailedWithoutBackup { get; private set; }

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
                LoadFailedWithoutBackup = failed is null;
                Debug.LogError($"[InkFlow] Не вдалося прочитати збереження: {e.Message}. Починаємо з чистого; " +
                               (failed is null
                                   ? "копію файлу зберегти не вдалося — поверх нього нічого не пишемо."
                                   : $"копія файлу — {failed}."));
                return new SaveFile();
            }
        }

        /// <summary>
        /// Відкладає нечитабельний файл поруч: save.json.failed-&lt;UTC&gt;. Спершу копією (сам файл лишається
        /// на місці); якщо файл не читається навіть для копіювання — перейменуванням, якому читати
        /// не треба. null, якщо не вдалося ні те, ні те.
        /// </summary>
        private string? TryPreserveFailed()
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var failedPath = $"{_path}.failed-{stamp}";
            try
            {
                File.Copy(_path, failedPath, overwrite: true);
                return failedPath;
            }
            catch (Exception copyError)
            {
                Debug.LogWarning($"[InkFlow] Копію зіпсованого збереження зробити не вдалося ({copyError.Message}) — перейменовуємо.");
            }

            try
            {
                if (File.Exists(failedPath))
                    File.Delete(failedPath);
                File.Move(_path, failedPath);
                return failedPath;
            }
            catch (Exception moveError)
            {
                Debug.LogError($"[InkFlow] Відкласти зіпсоване збереження не вдалося: {moveError.Message}");
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

            if (LoadFailedWithoutBackup)
            {
                // Ще одна спроба відкласти оригінал: доступ до файлу міг повернутись.
                LoadFailedWithoutBackup = File.Exists(_path) && TryPreserveFailed() is null;
                if (LoadFailedWithoutBackup)
                {
                    Debug.LogError("[InkFlow] Збереження не записано: на диску лежить нечитабельний оригінал, " +
                                   "якого не вдалося відкласти, і стирати його ми не маємо права.");
                    return;
                }
            }

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
            LoadFailedWithoutBackup = false;
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            if (File.Exists(_backupPath)) File.Delete(_backupPath);
        }
    }
}
