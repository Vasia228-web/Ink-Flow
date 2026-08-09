using System.IO;
using InkFlow.App;
using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Ідемпотентний бутстрап АСЕТІВ: конфіги балансу, стартові рівні й Addressables-група.
    /// Меню: Ink Flow → Setup → Bootstrap Assets. Batch:
    ///   Unity -batchmode -quit -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.InkFlowBootstrap.BootstrapAssets
    ///
    /// Сцен більше не будує: кожен екран збирає свій Build*Screen, і поле партії —
    /// теж екран (LevelScreen). Тут лишились спільні службові речі, якими ті збирачі
    /// користуються: EnsureEditMode, EnsureFolder, Wire.
    /// </summary>
    public static class InkFlowBootstrap
    {
        private const string BalanceConfigPath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";

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
            LevelAuthoring.CreateStarterLevels();
            EnsureAddressableLevels();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] Bootstrap завершено: конфіги, рівні й Addressables на місці. " +
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
        }

        private static void EnsureAsset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        // ---------- Addressables ----------

        private static void EnsureAddressableLevels()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("Levels") ?? settings.CreateGroup(
                "Levels", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            RemoveDanglingEntries(settings, group);

            foreach (var path in LevelAuthoring.LevelAssetPaths())
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    Debug.LogError($"[InkFlow] Рівень не знайдено: {path}");
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = LevelCatalog.AddressPrefix + Path.GetFileNameWithoutExtension(path);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        }

        /// <summary>
        /// Прибирає записи на асети, яких уже немає. Addressables зберігають GUID, а не шлях,
        /// тож перейменований чи видалений рівень лишає «висячий» запис, який ламає
        /// збірку контенту — і робить це мовчки, аж до білда.
        /// </summary>
        private static void RemoveDanglingEntries(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var stale = new System.Collections.Generic.List<AddressableAssetEntry>();
            foreach (var entry in group.entries)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                    stale.Add(entry);
            }

            foreach (var entry in stale)
            {
                Debug.Log($"[InkFlow] Прибрано висячий Addressables-запис '{entry.address}' (асет видалено).");
                settings.RemoveAssetEntry(entry.guid, false);
            }
        }

        // ---------- Сцена ----------

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
