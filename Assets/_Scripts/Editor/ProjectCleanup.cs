using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InkFlow.Editor
{
    /// <summary>
    /// Валідація і чистка проєкту.
    ///
    /// Навіщо: після рефакторингу, що видаляє скрипти, сцени й префаби лишаються
    /// з мертвими GUID і сиплють «The referenced script is missing». Редактор при цьому
    /// ЗБЕРІГАЄ відкриту сцену назад на диск, тож видалити файл ззовні недостатньо —
    /// сцену треба перебудувати зсередини Unity (Ink Flow → Setup → Bootstrap Scene).
    /// </summary>
    public static class ProjectCleanup
    {
        /// <summary>Демо-контент і шаблонні залишки, яких у грі не буде.</summary>
        private static readonly string[] UnusedPaths =
        {
            "Assets/TextMesh Pro/Examples & Extras", // приклади TMP: сотні файлів і мертві посилання
            "Assets/Scenes/SampleScene.unity"        // шаблонна сцена URP; наші сцени — Boot/Meta/Game
        };

        [MenuItem("Ink Flow/Setup/Validate Project")]
        public static void Validate()
        {
            var report = new StringBuilder();
            var problems = 0;

            problems += ReportMissingScripts(report);
            problems += ReportUnusedAssets(report);

            if (problems == 0)
                Debug.Log("[InkFlow] Валідація: проблем не знайдено.");
            else
                Debug.LogWarning($"[InkFlow] Валідація знайшла {problems} проблем:\n{report}");
        }

        /// <summary>
        /// Шукає компоненти зі втраченим скриптом у відкритих сценах. Саме вони дають
        /// «The referenced script (Unknown) on this Behaviour is missing!» під час Play.
        /// </summary>
        private static int ReportMissingScripts(StringBuilder report)
        {
            var total = 0;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var affected = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                    CountMissing(root, root.name, affected);

                if (affected.Count == 0)
                    continue;

                total += affected.Count;
                report.AppendLine($"• Сцена '{scene.name}': {affected.Count} компонентів зі втраченим скриптом:");
                foreach (var path in affected)
                    report.AppendLine($"    {path}");
                report.AppendLine("  → Ink Flow → Setup → Bootstrap Scene перебудує сцену з нуля.");
            }

            return total;
        }

        private static void CountMissing(GameObject go, string path, List<string> affected)
        {
            var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            for (var i = 0; i < missing; i++)
                affected.Add(path);

            foreach (Transform child in go.transform)
                CountMissing(child.gameObject, $"{path}/{child.name}", affected);
        }

        private static int ReportUnusedAssets(StringBuilder report)
        {
            var found = 0;
            foreach (var path in UnusedPaths)
            {
                if (!AssetExists(path))
                    continue;
                found++;
                report.AppendLine($"• Зайвий асет: {path}");
            }

            if (found > 0)
                report.AppendLine("  → Ink Flow → Setup → Clean Unused Assets.");
            return found;
        }

        [MenuItem("Ink Flow/Setup/Clean Unused Assets")]
        public static void CleanUnusedAssets()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            var targets = new List<string>();
            foreach (var path in UnusedPaths)
                if (AssetExists(path))
                    targets.Add(path);

            if (targets.Count == 0)
            {
                Debug.Log("[InkFlow] Зайвих асетів не знайдено.");
                return;
            }

            // Видалення незворотне — питаємо, перш ніж чіпати проєкт.
            var list = string.Join("\n", targets);
            if (!EditorUtility.DisplayDialog(
                    "Видалити зайві асети?",
                    $"Буде видалено:\n\n{list}\n\nЦе демо-контент і шаблонні залишки, гра ними не користується.",
                    "Видалити", "Скасувати"))
                return;

            var failed = new List<string>();
            AssetDatabase.DeleteAssets(targets.ToArray(), failed);
            AssetDatabase.Refresh();
            ClearBrokenTmpSpriteAsset();

            if (failed.Count > 0)
                Debug.LogError($"[InkFlow] Не вдалося видалити:\n{string.Join("\n", failed)}");
            else
                Debug.Log($"[InkFlow] Видалено зайвих асетів: {targets.Count}.");
        }

        /// <summary>
        /// TMP Settings за замовчуванням посилається на Default Sprite Asset, який лежить
        /// УСЕРЕДИНІ «Examples &amp; Extras». Після видалення прикладів посилання стає мертвим,
        /// тож обнуляємо його: спрайт-асет потрібен лише для тегів &lt;sprite&gt;, яких у грі немає.
        /// </summary>
        private static void ClearBrokenTmpSpriteAsset()
        {
            const string path = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            var settings = AssetDatabase.LoadMainAssetAtPath(path);
            if (settings == null)
                return;

            var so = new SerializedObject(settings);
            var property = so.FindProperty("m_defaultSpriteAsset");

            // Видалений асет читається як null, але GUID у файлі лишається. Перезаписуємо
            // поле й дивимось, чи щось справді змінилось — так не чіпаємо асет даремно.
            if (property == null || property.objectReferenceValue != null)
                return;

            property.objectReferenceValue = null;
            if (!so.ApplyModifiedPropertiesWithoutUndo())
                return;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("[InkFlow] TMP Settings: прибрано мертве посилання на Default Sprite Asset.");
        }

        private static bool AssetExists(string path) =>
            AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;

        /// <summary>
        /// Прибирає компоненти зі втраченими скриптами з відкритої сцени — на випадок,
        /// коли сцену треба зберегти, а не перебудовувати (напр. хтось уже наповнив її вручну).
        /// </summary>
        [MenuItem("Ink Flow/Setup/Strip Missing Scripts From Open Scene")]
        public static void StripMissingScripts()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            var removed = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                    removed += StripRecursive(root);

                if (removed > 0)
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log(removed > 0
                ? $"[InkFlow] Прибрано компонентів зі втраченим скриптом: {removed}. Не забудь зберегти сцену."
                : "[InkFlow] Втрачених скриптів у відкритих сценах немає.");
        }

        private static int StripRecursive(GameObject go)
        {
            var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform)
                removed += StripRecursive(child.gameObject);
            return removed;
        }
    }
}
