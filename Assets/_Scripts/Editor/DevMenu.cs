#if UNITY_EDITOR || DEVELOPMENT_BUILD
using InkFlow.App;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Пункт меню для дев-панелі — альтернатива довгому тапу в кут, коли грати
    /// мишею в редакторі зручніше. Працює лише в Play Mode: до нього немає
    /// ані стану гравця, ані самої панелі.
    /// </summary>
    public static class DevMenu
    {
        [MenuItem("Ink Flow/Debug/Toggle Dev Panel %#d")]
        public static void ToggleDevPanel()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[InkFlow] Дев-панель доступна лише в Play Mode.");
                return;
            }

            DevPanel.ToggleFromMenu();
        }

        [MenuItem("Ink Flow/Debug/Open Save Folder")]
        public static void OpenSaveFolder() =>
            EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
#endif
