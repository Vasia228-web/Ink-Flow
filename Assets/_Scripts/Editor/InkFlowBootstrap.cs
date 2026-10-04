using System.IO;
using InkFlow.Gameplay;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Ідемпотентний бутстрап АСЕТІВ: конфіги балансу й економіки.
    /// Меню: Ink Flow → Setup → Bootstrap Assets. Batch:
    ///   Unity -batchmode -quit -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.InkFlowBootstrap.BootstrapAssets
    ///
    /// Сцен не будує: кожен екран збирає свій Build*Screen. Рівнів і Addressables-групи
    /// `Levels` більше немає — режим «Рівні» вимкнено (документ §10); стару групу в
    /// Addressables прибирає автор руками. Тут лишились спільні службові речі, якими
    /// збирачі користуються: EnsureEditMode, EnsureFolder.
    /// </summary>
    public static class InkFlowBootstrap
    {
        private const string BalanceConfigPath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";
        private const string EconomyConfigPath = "Assets/_ScriptableObjects/Balance/EconomyConfig.asset";
        internal const string GalaxyConfigPath = "Assets/_ScriptableObjects/Balance/GalaxyConfig.asset";

        [MenuItem("Ink Flow/Setup/Bootstrap Assets")]
        public static void BootstrapAssets()
        {
            if (!EnsureEditMode())
                return;

            // Як і решта бутстрапів: поки будуємо — жодних відкладених перечитувань
            // стилю. Тут це стосується SafeAreaBinder: його Apply свідомо НЕ печемо
            // у сцену, бо safe area залежить від пристрою, а не від збірки.
            StyleRefresh.Suspended = true;
            try { BuildAll(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildAll()
        {
            if (!EnsureTmpEssentials())
            {
                Debug.LogWarning("[InkFlow] TMP Essential Resources щойно імпортовано — " +
                                 "запусти Bootstrap Assets ще раз.");
                return;
            }

            EnsureConfigs();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] Bootstrap завершено: конфіги на місці. " +
                      "Екрани збираються окремо — Ink Flow → Setup → Build …");
        }

        /// <summary>
        /// У Play Mode редакторні операції ламаються посеред роботи: NewScene кидає виняток,
        /// а ParticleSystem уже грає, тож його налаштування не застосовуються — і префаб
        /// зберігається зіпсованим. Тому спершу зупиняємось.
        /// </summary>
        internal static bool EnsureEditMode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return true;

            Debug.LogError("[InkFlow] Ця операція недоступна в Play Mode — зупини гру (⏹) і запусти ще раз.");
            EditorUtility.DisplayDialog(
                "Спочатку зупини гру",
                "Бутстрап перебудовує сцену й префаби, а в Play Mode редактор цього не дозволяє.\n\n" +
                "Натисни ⏹ (Stop) і запусти команду знову.",
                "Зрозуміло");
            return false;
        }

        /// <summary>
        /// true — асет треба створювати. Якщо файл є, але компонент у ньому не читається
        /// (напр. префаб зберігся зі втраченим скриптом), видаляємо його й перестворюємо:
        /// мовчки лишити зіпсований префаб гірше, ніж перезаписати.
        /// </summary>
        private static bool NeedsCreation<T>(string path) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return false;

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                Debug.LogWarning($"[InkFlow] {path} існує, але компонент {typeof(T).Name} у ньому втрачено — перестворюю.");
                AssetDatabase.DeleteAsset(path);
            }

            return true;
        }

        // ---------- TMP ----------

        private static bool EnsureTmpEssentials()
        {
            if (TMP_Settings.instance != null)
                return true;

            var packagePaths = new[]
            {
                "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
                "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage"
            };

            foreach (var path in packagePaths)
            {
                if (!File.Exists(Path.GetFullPath(path)))
                    continue;
                AssetDatabase.ImportPackage(path, false);
                AssetDatabase.Refresh();
                return TMP_Settings.instance != null;
            }

            Debug.LogError("[InkFlow] Не знайдено TMP Essential Resources — імпортуй вручну: " +
                           "Window → TextMeshPro → Import TMP Essential Resources.");
            return false;
        }

        // ---------- Асети ----------

        private static void EnsureConfigs()
        {
            EnsureFolder("Assets/_ScriptableObjects/Balance");
            EnsureAsset<BalanceConfig>(BalanceConfigPath);
            EnsureAsset<EconomyConfig>(EconomyConfigPath);
            EnsureAsset<GalaxyConfig>(GalaxyConfigPath);
            RefreshPictureLibrary.Refresh();
        }

        /// <summary>Розкладка галактики (§12) з дефолтами — створюється, якщо її ще немає, щоб Build Main Scene не вимагав зайвого кроку.</summary>
        internal static GalaxyConfig EnsureGalaxyConfig()
        {
            EnsureFolder("Assets/_ScriptableObjects/Balance");
            EnsureAsset<GalaxyConfig>(GalaxyConfigPath);
            return AssetDatabase.LoadAssetAtPath<GalaxyConfig>(GalaxyConfigPath);
        }

        private static void EnsureAsset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        // ---------- Утиліти ----------

        /// <summary>
        /// Проставляє приватні [SerializeField]-поля й ГОЛОСНО валідує результат:
        /// null на вході або поле, що записалось порожнім, — помилка бутстрапа,
        /// а не тиха дірка в сцені, яку знайдуть уже в Play Mode.
        /// </summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                if (value == null)
                {
                    Debug.LogError($"[InkFlow] Спроба підв'язати null у поле '{field}' на {target.GetType().Name}");
                    continue;
                }

                var property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[InkFlow] Поле '{field}' не знайдено на {target.GetType().Name}");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var check = new SerializedObject(target);
            foreach (var (field, value) in fields)
                if (value != null && check.FindProperty(field)?.objectReferenceValue == null)
                    Debug.LogError($"[InkFlow] Поле '{field}' на {target.GetType().Name} записалось як null — " +
                                   "референс застарів. Запусти Bootstrap Scene ще раз.");
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            // Явна перевірка замість string.IsNullOrEmpty: у reference-збірках, якими
            // компілює Unity, вона не має [NotNullWhen(false)], тож компілятор НЕ звужує
            // тип і видає CS8604. `is null` звужує завжди.
            if (parent is null || parent.Length == 0)
                return; // дійшли до кореня Assets — створювати нічого

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
