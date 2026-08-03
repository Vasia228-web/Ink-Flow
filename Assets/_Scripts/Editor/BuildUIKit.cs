using InkFlow.Core;
using InkFlow.Style;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace InkFlow.Editor
{
    /// <summary>
    /// Створює DesignSystem.asset, префаби UI-атомів і тестову сцену UIKit.
    /// Меню: Ink Flow → Setup → Build UI Kit.
    ///
    /// Порядок як у InkFlowBootstrap: спершу залежності (спрайти, шрифт, конфіг),
    /// потім префаби, і лише в самому кінці сцена — і всі асети перечитуються
    /// з AssetDatabase безпосередньо перед підв'язкою (див. коментар у InkFlowBootstrap).
    /// </summary>
    public static class BuildUIKit
    {
        private const string StyleFolder = "Assets/_ScriptableObjects/Style";
        private const string DesignSystemPath = StyleFolder + "/DesignSystem.asset";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string ScenePath = "Assets/Scenes/UIKit.unity";
        private const string SpriteFolder = "Assets/_Sprites/UI";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";

        [MenuItem("Ink Flow/Setup/Build UI Kit")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            GenerateUISprites.Generate();
            GenerateFontAsset.Generate();
            EnsureDesignSystem();

            InkFlowBootstrap.EnsureFolder(PrefabFolder);
            BuildGlassPanel();
            BuildNeonButton();
            BuildDropView();
            BuildCurrencyWidget();
            BuildCosmicBackground();

            BuildTestScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] UI Kit готовий: DesignSystem.asset, 5 префабів-атомів і сцена Assets/Scenes/UIKit.unity.");
        }

        // ───────────────────────── Дизайн-система ─────────────────────────

        private static void EnsureDesignSystem()
        {
            InkFlowBootstrap.EnsureFolder(StyleFolder);

            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            if (design == null)
            {
                design = ScriptableObject.CreateInstance<DesignSystem>();
                AssetDatabase.CreateAsset(design, DesignSystemPath);
            }

            // Шрифт підв'язуємо тут: значення кольорів і метрик уже стоять
            // за замовчуванням у самому класі — вони і є витягом із макета.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                var so = new SerializedObject(design);
                so.FindProperty("font").objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[InkFlow] {FontPath} не знайдено — DesignSystem лишається без шрифту.");
            }

            EditorUtility.SetDirty(design);
        }

        private static DesignSystem LoadDesign() =>
            AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);

        private static Sprite LoadSprite(string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{file}.png");

        // ───────────────────────── Атоми ─────────────────────────

        private static void BuildGlassPanel()
        {
            var design = LoadDesign();
            var rounded = LoadSprite("rounded-rect");

            var root = NewUIObject("GlassPanel", new Vector2(600f, 320f));
            try
            {
                var fill = AddImage(root, "Fill", rounded, design.GlassFill);
                var stroke = AddImage(root, "Stroke", rounded, design.GlassStroke);

                var panel = root.AddComponent<GlassPanel>();
                Wire(panel, ("design", design), ("fill", fill), ("stroke", stroke));

                SavePrefab(root, "GlassPanel");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildNeonButton()
        {
            var design = LoadDesign();
            var rounded = LoadSprite("rounded-rect");
            var glowSprite = LoadSprite("glow");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = NewUIObject("NeonButton", new Vector2(520f, 150f));
            try
            {
                // Гало виходить за межі кнопки — тому окремим об'єктом із запасом.
                var glowGo = NewChild(root, "Glow", new Vector2(520f + design.GlowButtonRadius * 2f,
                    150f + design.GlowButtonRadius * 2f));
                var glow = glowGo.AddComponent<Image>();
                glow.sprite = glowSprite;
                glow.type = Image.Type.Sliced;
                glow.raycastTarget = false;
                glow.color = DesignSystem.WithAlpha(design.AccentPrimary, design.GlowButtonAlpha);

                var bgGo = NewChild(root, "Background", Vector2.zero, stretch: true);
                var background = bgGo.AddComponent<GradientImage>();
                background.sprite = rounded;
                background.type = Image.Type.Sliced;
                background.SetGradient(design.AccentPrimary, design.AccentSecondary);

                var stroke = AddImage(root, "InnerStroke", rounded, new Color(1f, 1f, 1f, 0.5f));

                var labelGo = NewChild(root, "Label", Vector2.zero, stretch: true);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "Грати";
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = design.FontSizeBody;
                label.color = design.TextPrimary;
                label.raycastTarget = false;
                if (font != null)
                    label.font = font;

                var button = root.AddComponent<Button>();
                button.targetGraphic = background;

                var neon = root.AddComponent<NeonButton>();
                Wire(neon,
                    ("design", design), ("button", button), ("background", background),
                    ("glow", glow), ("innerStroke", stroke), ("label", label));

                SavePrefab(root, "NeonButton");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildDropView()
        {
            var design = LoadDesign();
            var circle = LoadSprite("circle-soft");
            var gloss = LoadSprite("circle-gloss");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = NewUIObject("DropView", new Vector2(160f, 160f));
            try
            {
                var glowGo = NewChild(root, "Glow", new Vector2(220f, 220f));
                var glow = glowGo.AddComponent<Image>();
                glow.sprite = circle;
                glow.raycastTarget = false;
                glow.color = design.InkGlow(InkColor.Magenta);

                var bodyGo = NewChild(root, "Body", Vector2.zero, stretch: true);
                var body = bodyGo.AddComponent<Image>();
                body.sprite = circle;
                body.color = design.Ink(InkColor.Magenta);

                var glossGo = NewChild(root, "Gloss", Vector2.zero, stretch: true);
                var glossImage = glossGo.AddComponent<Image>();
                glossImage.sprite = gloss;
                glossImage.raycastTarget = false;
                glossImage.color = new Color(1f, 1f, 1f, design.DropGlossAlpha);

                var labelGo = NewChild(root, "Density", Vector2.zero, stretch: true);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "7";
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = design.FontSizeSubtitle;
                label.color = design.TextPrimary;
                label.raycastTarget = false;
                if (font != null)
                    label.font = font;

                var drop = root.AddComponent<DropView>();
                Wire(drop,
                    ("design", design), ("body", body), ("gloss", glossImage),
                    ("glow", glow), ("densityLabel", label));

                SavePrefab(root, "DropView");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildCurrencyWidget()
        {
            var design = LoadDesign();
            var rounded = LoadSprite("rounded-rect");
            var circle = LoadSprite("circle-soft");
            var gloss = LoadSprite("circle-gloss");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = NewUIObject("CurrencyWidget", new Vector2(300f, 90f));
            try
            {
                var fill = AddImage(root, "Fill", rounded, design.GlassFill);
                var stroke = AddImage(root, "Stroke", rounded, design.GlassStroke);
                var panel = root.AddComponent<GlassPanel>();
                Wire(panel, ("design", design), ("fill", fill), ("stroke", stroke));

                var iconGo = NewChild(root, "DropIcon", new Vector2(56f, 56f));
                iconGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-95f, 0f);
                var icon = iconGo.AddComponent<Image>();
                icon.sprite = circle;
                icon.raycastTarget = false;

                var glossGo = NewChild(iconGo, "Gloss", Vector2.zero, stretch: true);
                var glossImage = glossGo.AddComponent<Image>();
                glossImage.sprite = gloss;
                glossImage.raycastTarget = false;

                var labelGo = NewChild(root, "Amount", new Vector2(180f, 60f));
                labelGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(25f, 0f);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "1 240";
                label.alignment = TextAlignmentOptions.Left;
                label.fontSize = design.FontSizeLabel;
                label.raycastTarget = false;
                if (font != null)
                    label.font = font;

                var widget = root.AddComponent<CurrencyWidget>();
                Wire(widget,
                    ("design", design), ("panel", panel), ("dropIcon", icon),
                    ("dropGloss", glossImage), ("amountLabel", label));

                SavePrefab(root, "CurrencyWidget");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildCosmicBackground()
        {
            var design = LoadDesign();

            var root = NewUIObject("CosmicBackground", Vector2.zero, stretch: true);
            try
            {
                var gradient = root.AddComponent<CosmicBackground>();
                gradient.raycastTarget = false;
                Wire(gradient, ("design", design));

                var starsGo = NewChild(root, "Stars", Vector2.zero, stretch: true);
                var stars = starsGo.AddComponent<StarField>();
                stars.raycastTarget = false;
                Wire(stars, ("design", design));

                SavePrefab(root, "CosmicBackground");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ───────────────────────── Тестова сцена ─────────────────────────

        private static void BuildTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var design = LoadDesign();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var glass = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/GlassPanel.prefab");
            var button = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/NeonButton.prefab");
            var drop = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/DropView.prefab");
            var currency = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            if (design == null || cosmic == null || glass == null || button == null || drop == null || currency == null)
            {
                Debug.LogError("[InkFlow] Не всі префаби UI Kit знайдено — сцену не збудовано.");
                return;
            }

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = design.BackgroundEdge;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("UI Root");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);

            var safeGo = NewChild(canvasGo, "SafeArea", Vector2.zero, stretch: true);
            safeGo.AddComponent<SafeAreaBinder>();
            var navigation = safeGo.AddComponent<NavigationStack>();

            var uiRoot = canvasGo.AddComponent<UIRoot>();
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler), ("navigation", navigation),
                ("safeArea", safeGo.GetComponent<SafeAreaBinder>()));

            // Заголовок
            var title = AddLabel(safeGo, "Title", "UI KIT", design, design.FontSizeTitle, font);
            Place(title, new Vector2(0f, 780f), new Vector2(900f, 90f));

            // Валюта — правий верх
            var currencyInstance = (GameObject)PrefabUtility.InstantiatePrefab(currency, safeGo.transform);
            Place(currencyInstance, new Vector2(300f, 640f), new Vector2(300f, 90f));

            // Скляна панель
            var glassInstance = (GameObject)PrefabUtility.InstantiatePrefab(glass, safeGo.transform);
            Place(glassInstance, new Vector2(0f, 330f), new Vector2(900f, 380f));
            var glassCaption = AddLabel(safeGo, "GlassCaption", "GlassPanel · radius Card 70",
                design, design.FontSizeCaption, font);
            Place(glassCaption, new Vector2(0f, 500f), new Vector2(800f, 40f));

            // Палітра крапель усередині панелі
            var palette = InkColors.All;
            for (var i = 0; i < palette.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(drop, safeGo.transform);
                instance.name = $"Drop_{palette[i]}";
                var x = -360f + i * 145f;
                Place(instance, new Vector2(x, 330f), new Vector2(160f, 160f));

                var view = instance.GetComponent<DropView>();
                view.Show(palette[i], (i + 1) * 2);
                if (i == palette.Length - 1)
                    view.SetNearMiss(true); // остання показує пульс near-miss
            }

            // Кнопки трьох тонів
            var tones = new[] { NeonButton.Tone.Primary, NeonButton.Tone.Cool, NeonButton.Tone.Warm };
            var titles = new[] { "Грати", "Нескінченний", "Магазин" };
            for (var i = 0; i < tones.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(button, safeGo.transform);
                instance.name = $"NeonButton_{tones[i]}";
                Place(instance, new Vector2(0f, -20f - i * 190f), new Vector2(520f, 150f));

                var neon = instance.GetComponent<NeonButton>();
                var so = new SerializedObject(neon);
                so.FindProperty("tone").enumValueIndex = (int)tones[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                neon.Text = titles[i];
                neon.Apply();
            }

            var hint = AddLabel(safeGo, "Hint",
                "Тапни краплю — squash & stretch. Тапни кнопку — пружне натискання.",
                design, design.FontSizeCaption, font);
            Place(hint, new Vector2(0f, -640f), new Vector2(900f, 60f));

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ───────────────────────── Утиліти ─────────────────────────

        private static GameObject NewUIObject(string name, Vector2 size, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = size;
            }

            return go;
        }

        private static GameObject NewChild(GameObject parent, string name, Vector2 size, bool stretch = false)
        {
            var go = NewUIObject(name, size, stretch);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static Image AddImage(GameObject parent, string name, Sprite sprite, Color color)
        {
            var go = NewChild(parent, name, Vector2.zero, stretch: true);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text AddLabel(GameObject parent, string name, string text,
            DesignSystem design, float size, TMP_FontAsset? font)
        {
            var go = NewChild(parent, name, new Vector2(600f, 60f));
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = design.TextMuted;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            if (font != null)
                label.font = font;
            return label;
        }

        private static void Place(Component component, Vector2 position, Vector2 size) =>
            Place(component.gameObject, position, size);

        private static void Place(GameObject go, Vector2 position, Vector2 size)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SavePrefab(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        }

        /// <summary>Проставляє приватні [SerializeField]-поля з валідацією, як у InkFlowBootstrap.</summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
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
    }
}
