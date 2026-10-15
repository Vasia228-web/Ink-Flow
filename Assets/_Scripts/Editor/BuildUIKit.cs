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
        private const string BlackFontPath = GenerateFontAsset.BlackPath;
        private const string BodyFontPath = GenerateFontAsset.BodyPath;

        [MenuItem("Ink Flow/Setup/Build UI Kit")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            // Поки збираємо — жодних відкладених перечитувань стилю: усі потрібні
            // Apply() викликаються нижче синхронно, до SavePrefab.
            StyleRefresh.Suspended = true;
            try { BuildAll(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildAll()
        {
            // Кожен крок в окремому try: виняток в одному не має обривати збірку мовчки —
            // саме так недоступна для читання текстура вбила генерацію шрифтів і префабів.
            Step("UI-спрайти", GenerateUISprites.Generate);
            Step("Спрайти U1Glaze", U1GlazeSprites.ImportAll);
            Step("TMP-шрифти", GenerateFontAsset.Generate);
            Step("TMP Sprite Asset", GenerateSpriteAsset.Generate);
            EnsureDesignSystem();
            // Матеріал тіні тексту читає токени DesignSystem — тому після нього, і підв'язка окремо.
            Step("Матеріал тіні тексту", GenerateFontAsset.EnsureShadowMaterial);
            WireShadowMaterial();

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

        /// <summary>Виконує крок збірки, не даючи його падінню обірвати решту.</summary>
        private static void Step(string label, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[InkFlow] Крок «{label}» впав: {e.Message}\n{e.StackTrace}");
            }
        }

        // ───────────────────────── Дизайн-система ─────────────────────────

        private static void EnsureDesignSystem()
        {
            InkFlowBootstrap.EnsureFolder(StyleFolder);

            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);

            if (design != null && design.TokenVersion < DesignSystem.CurrentTokenVersion)
            {
                // Токени виправили в коді — переносимо їх у вже створений асет.
                // Без цього гра й далі читала б старі значення, а правки жили б лише в дефолтах.
                Debug.Log($"[InkFlow] DesignSystem: токени v{design.TokenVersion} → " +
                          $"v{DesignSystem.CurrentTokenVersion}, асет перестворюю.");
                AssetDatabase.DeleteAsset(DesignSystemPath);
                design = null;
            }

            if (design == null)
            {
                design = ScriptableObject.CreateInstance<DesignSystem>();
                AssetDatabase.CreateAsset(design, DesignSystemPath);
            }

            // Шрифти підв'язуємо тут: значення кольорів і метрик уже стоять
            // за замовчуванням у самому класі — вони і є витягом із еталона.
            // U1Glaze: Black 900 (заголовки, числа, кнопки) і Bold 700 (текст); ExtraBold — старі екрани до кроку 10.
            var so = new SerializedObject(design);
            foreach (var (field, path) in new[] { ("font", FontPath), ("fontBlack", BlackFontPath), ("fontBody", BodyFontPath) })
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                    so.FindProperty(field).objectReferenceValue = font;
                else
                    Debug.LogWarning($"[InkFlow] {path} не знайдено — DesignSystem.{field} лишається порожнім (bash Tools/fetch-fonts.sh → Generate Font Assets).");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(design);
        }

        private static void WireShadowMaterial()
        {
            var design = LoadDesign();
            var material = AssetDatabase.LoadAssetAtPath<Material>(GenerateFontAsset.BlackShadowMaterialPath);
            if (design == null || material == null)
                return;
            var so = new SerializedObject(design);
            so.FindProperty("fontBlackShadow").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            var outline = LoadSprite("rounded-rect-outline");

            var root = NewUIObject("GlassPanel", new Vector2(600f, 320f));
            try
            {
                var fill = AddImage(root, "Fill", rounded, design.GlassFill);
                // Рамка — окремий спрайт-обведення: заповнений rounded-rect тут
                // лягав би суцільною плашкою поверх заливки.
                var stroke = AddImage(root, "Stroke", outline, design.GlassStroke);

                var panel = root.AddComponent<GlassPanel>();
                Wire(panel, ("design", design), ("fill", fill), ("stroke", stroke));
                // Синхронно: OnValidate тепер відкладений, а pixelsPerUnitMultiplier
                // мусить бути в префабі, інакше 9-slice радіус поїде.
                panel.Apply();

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
                glow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / design.GlowButtonRadius;

                var bgGo = NewChild(root, "Background", Vector2.zero, stretch: true);
                var background = bgGo.AddComponent<GradientImage>();
                background.sprite = rounded;
                background.type = Image.Type.Sliced;
                var tint = Color.Lerp(design.GlassFill, design.AccentPrimary, design.ButtonTintStrength);
                tint.a = design.GlassFill.a;
                background.SetGradient(tint, design.GlassFill);

                var stroke = AddImage(root, "Stroke", LoadSprite("rounded-rect-outline"),
                    DesignSystem.WithAlpha(design.AccentPrimary, design.ButtonStrokeAlpha));

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
                neon.Apply();

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
                // Гало РОЗТЯГНУТЕ по краплі, а множник сидить у localScale.
                // Доти DropView.Apply() писав йому sizeDelta — і кожне таке
                // писання посеред OnValidate давало «SendMessage cannot be called
                // during Awake…», бо змінений розмір розсилає
                // OnRectTransformDimensionsChange по дереву.
                var glowGo = NewChild(root, "Glow", Vector2.zero, stretch: true);
                glowGo.transform.localScale = Vector3.one * design.DropGlowScale;
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
                drop.Apply();

                SavePrefab(root, "DropView");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Капсула нафти U1Glaze: 9-slice capsule, крапля oil_drop 24, число Nunito Black і плитка «+»
        /// (capsule_plus + icon_plus). Ширина росте за вмістом (HorizontalLayoutGroup + fitter):
        /// «999 999 999» вміщається, не переносячись. Висота — 46 px макета.
        /// </summary>
        private static void BuildCurrencyWidget()
        {
            var design = LoadDesign();
            var height = U1.Px(46f);
            var root = NewUIObject("CurrencyWidget", new Vector2(U1.Px(118f), height));
            try
            {
                var capsule = root.AddComponent<Image>();
                capsule.sprite = U1.S("capsule");
                capsule.type = Image.Type.Sliced;
                capsule.raycastTarget = false;

                var layout = root.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset((int)U1.Px(10f), (int)U1.Px(1f), 0, 0);
                layout.spacing = U1.Px(6f);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                root.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                var element = root.AddComponent<LayoutElement>();
                element.preferredHeight = height;
                element.minHeight = height;

                var dropGo = NewChild(root, "Drop", Vector2.zero);
                var drop = dropGo.AddComponent<Image>();
                drop.sprite = U1.S("oil_drop");
                drop.raycastTarget = false;
                var dropLayout = dropGo.AddComponent<LayoutElement>();
                dropLayout.preferredWidth = dropLayout.preferredHeight = U1.Px(24f);

                var label = U1.Text(root, "Amount", "1\u00A0240", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;

                // «+»: плитка 32, але зона тапу — 44×46 (SPEC «нічого меншого за 44»): прозора підкладка ловить тап.
                var plusGo = NewChild(root, "Plus", Vector2.zero);
                var plusCatcher = plusGo.AddComponent<Image>();
                plusCatcher.color = Color.clear;
                var plusLayout = plusGo.AddComponent<LayoutElement>();
                plusLayout.preferredWidth = design.TapMin;
                plusLayout.preferredHeight = height;
                var tileGo = NewChild(plusGo, "Tile", new Vector2(U1.Px(32f), U1.Px(32f)));
                var plusTile = tileGo.AddComponent<Image>();
                plusTile.sprite = U1.S("capsule_plus");
                plusTile.raycastTarget = false;
                var plusIconGo = NewChild(tileGo, "Icon", new Vector2(U1.Px(16f), U1.Px(16f)));
                var plusIcon = plusIconGo.AddComponent<Image>();
                plusIcon.sprite = U1.S("icon_plus");
                plusIcon.raycastTarget = false;
                var plusButton = plusGo.AddComponent<Button>();
                plusButton.targetGraphic = plusTile;
                plusButton.transition = Selectable.Transition.None;
                tileGo.AddComponent<PressScale>().Bind(design);

                var widget = root.AddComponent<CurrencyWidget>();
                Wire(widget,
                    ("design", design), ("capsule", capsule), ("drop", drop),
                    ("amountLabel", label), ("plusButton", plusButton), ("plusIcon", plusIcon));
                widget.Apply();

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
            uiRoot.ApplyScaler();

            // Заголовок
            var title = AddLabel(safeGo, "Title", "UI KIT", design, design.FontSizeTitle, font);
            Place(title, new Vector2(0f, 830f), new Vector2(900f, 90f));

            // Валюта — правий верх
            var currencyInstance = (GameObject)PrefabUtility.InstantiatePrefab(currency, safeGo.transform);
            Place(currencyInstance, new Vector2(300f, 700f), new Vector2(300f, 90f));

            // Скляна панель у ролі ігрового поля — крізь неї має просвічувати фон.
            var glassInstance = (GameObject)PrefabUtility.InstantiatePrefab(glass, safeGo.transform);
            Place(glassInstance, new Vector2(0f, 150f), new Vector2(1000f, 1000f));
            var glassCaption = AddLabel(safeGo, "GlassCaption",
                "GlassPanel · темне скло, фон просвічує", design, design.FontSizeCaption, font);
            Place(glassCaption, new Vector2(0f, 590f), new Vector2(900f, 40f));

            // Сітка 6×6 крапель інтерфейсу: доказ, що гало сусідів не зливаються.
            // Крок — розмір краплі з гало плюс видимий проміжок; ігрове поле тепер
            // будується з блоків (BuildEndlessScreen), а не з крапель.
            const float dropSize = 105f;
            var pitch = dropSize * design.DropGlowScale + 24f;
            var palette = InkColors.All;
            for (var y = 0; y < 6; y++)
            {
                for (var x = 0; x < 6; x++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(drop, safeGo.transform);
                    instance.name = $"Drop_{x}_{y}";
                    Place(instance,
                        new Vector2((x - 2.5f) * pitch, 150f + (y - 2.5f) * pitch),
                        new Vector2(dropSize, dropSize));

                    var view = instance.GetComponent<DropView>();
                    view.Show(palette[(x + y * 2) % palette.Length], 1 + (x + y) % 9);
                    view.Apply();
                }
            }

            // Кнопки трьох тонів
            var tones = new[] { NeonButton.Tone.Primary, NeonButton.Tone.Cool, NeonButton.Tone.Warm };
            var titles = new[] { "Рівні", "Нескінченний", "Магазин" };
            for (var i = 0; i < tones.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(button, safeGo.transform);
                instance.name = $"NeonButton_{tones[i]}";
                Place(instance, new Vector2(0f, -450f - i * 180f), new Vector2(700f, 150f));

                var neon = instance.GetComponent<NeonButton>();
                var so = new SerializedObject(neon);
                so.FindProperty("tone").enumValueIndex = (int)tones[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                neon.Text = titles[i];
                neon.Apply();
            }

            var hint = AddLabel(safeGo, "Hint",
                $"Сітка 6×6 · крок {pitch:0} = крапля {dropSize:0} × гало {design.DropGlowScale:0.00} + проміжок 24",
                design, design.FontSizeCaption, font);
            Place(hint, new Vector2(0f, -890f), new Vector2(1000f, 60f));

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
