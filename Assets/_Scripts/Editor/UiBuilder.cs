using InkFlow.Style;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Спільні помічники збирачів екранів. Винесені сюди, коли з'явився другий
    /// екран: копія цих семи методів розійшлася б із оригіналом на першій же правці,
    /// а розбіжність у Wire() коштувала б тихих null-посилань у сцені.
    ///
    /// Викликаються через <c>using static InkFlow.Editor.UiBuilder;</c>, тому
    /// в білдерах виглядають як власні методи.
    /// </summary>
    internal static class UiBuilder
    {
        internal const string SpriteFolder = "Assets/_Sprites/UI";

        /// <summary>
        /// Спрайт-асет іконок вішаємо на кожен напис явно: покладатись на TMP
        /// Settings ризиковано — одна забута галочка й ★ знову стає квадратом.
        /// </summary>
        internal static TMP_SpriteAsset? IconSprites { get; set; }

        internal static void CreateCamera(DesignSystem design)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = design.BackgroundEdge;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        internal static Sprite LoadSprite(string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{file}.png");

        internal const string ScreenPrefabFolder = "Assets/_Prefabs/Screens";

        internal static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        internal static GameObject Child(RectTransform parent, string name) => Child(parent.gameObject, name);

        internal static void Stretch(GameObject go, float inset = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        internal static TMP_Text Label(GameObject parent, string name, string text, DesignSystem design,
            TMP_FontAsset? font, float size, Color color, TextAlignmentOptions alignment)
        {
            _ = design;
            var go = Child(parent, name);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            if (font != null)
                label.font = font;
            if (IconSprites != null)
                label.spriteAsset = IconSprites;
            return label;
        }

        internal static TMP_Text Label(RectTransform parent, string name, string text, DesignSystem design,
            TMP_FontAsset? font, float size, Color color, TextAlignmentOptions alignment) =>
            Label(parent.gameObject, name, text, design, font, size, color, alignment);

        /// <summary>
        /// Зберігає корінь екрана префабом у {ScreenPrefabFolder}.
        ///
        /// Заради цього префаба все й робиться: `BuildMainScene` складає з дев'яти
        /// таких коренів один застосунок, не дублюючи жодного рядка розкладки.
        /// Окремі сцени екранів лишаються — на них зручно правити один екран,
        /// не тягаючи решту.
        /// </summary>
        internal static void SaveScreenPrefab(GameObject screenRoot)
        {
            InkFlowBootstrap.EnsureFolder(ScreenPrefabFolder);
            var path = $"{ScreenPrefabFolder}/{screenRoot.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(screenRoot, path, out var ok);
            if (!ok)
                Debug.LogError($"[InkFlow] Не вдалося зберегти префаб екрана: {path}");
        }

        internal static void Place(Component component, Vector2 position, Vector2 size,
            Vector2 anchor, Vector2 pivot)
        {
            var rect = component.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// Записує посилання в [SerializeField]-поля й ПЕРЕЧИТУЄ їх. Перевірка не
        /// зайва: закриття сцени вивантажує незакорінені асети, і посилання, взяте
        /// до NewScene, тихо стає «fake null» — у сцену лягає порожнє поле.
        /// </summary>
        internal static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                if (value == null)
                {
                    Debug.LogError($"[InkFlow] null у поле '{field}' на {target.GetType().Name}");
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
                    Debug.LogError($"[InkFlow] Поле '{field}' на {target.GetType().Name} записалось як null.");
        }

        /// <summary>Записує масив посилань у поле-масив (слоти каруселі, крапки пагінації).</summary>
        internal static void WireArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[InkFlow] Масив '{field}' не знайдено на {target.GetType().Name}");
                return;
            }

            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
