using InkFlow.Style;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static InkFlow.Editor.UiBuilder;

namespace InkFlow.Editor
{
    /// <summary>
    /// Збирає екран планети-вітрини (майстер-док §12) за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Planet Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Прив'язки: шапка до верху SafeArea,
    /// підказка, прогрес і кнопка «Колекція» — до низу, планета центрується в тому, що лишилось.
    /// Слоти — чотири шари під диском (плями, панелі, рамки, пікселі), щоб канвас батчив кожен шар
    /// одним викликом; пул — на найбільшу планету з запасом.
    /// </summary>
    public static class BuildPlanetScreen
    {
        private const string ScenePath = "Assets/Scenes/Planet.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string PlanetShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";
        private const string ZoneShaderPath = "Assets/_Shaders/InkFlowZone.shader";

        /// <summary>Маркерів у пулі: найбільша планета дефолтної розкладки — 12, конфіг дозволяє до 24.</summary>
        public const int MaxSlots = 24;

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        [MenuItem("Ink Flow/Setup/Build Planet Screen")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            StyleRefresh.Suspended = true;
            try { BuildScene(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath) == null)
            {
                Debug.LogError($"[InkFlow] Немає {DesignSystemPath}. Спершу: Ink Flow → Setup → Build UI Kit.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Асети — строго після NewScene (див. коментар у InkFlowBootstrap).
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);

            var rounded = LoadSprite("rounded-rect");
            var outline = LoadSprite("rounded-rect-outline");
            var circle = LoadSprite("circle-soft");
            var circleOutline = LoadSprite("circle-outline");
            var quad = LoadSprite("white-quad");
            var planetShader = AssetDatabase.LoadAssetAtPath<Shader>(PlanetShaderPath);
            var zoneShader = AssetDatabase.LoadAssetAtPath<Shader>(ZoneShaderPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (planetShader == null) missing.Add(PlanetShaderPath);
            if (zoneShader == null) missing.Add(ZoneShaderPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            K1Sprites.AllPresent(missing);
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Планету НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
                return;
            }

            CreateCamera(design!);

            var canvasGo = new GameObject("UI Root");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);

            var safe = Child(canvasGo, "SafeArea");
            Stretch(safe);
            var safeBinder = safe.AddComponent<SafeAreaBinder>();
            var navigation = safe.AddComponent<NavigationStack>();

            var uiRoot = canvasGo.AddComponent<UIRoot>();
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler),
                ("navigation", navigation), ("safeArea", safeBinder));

            var screenGo = Child(safe, "PlanetScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<PlanetScreen>();

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                out var backButton, out var title, out var currency);

            var collectionButton = BuildCollectionButton(screenGo, design!, font, rounded!, out var collectionLabel, out var buttonTop);
            var progress = BuildProgress(screenGo, design!, font, buttonTop);
            var hint = BuildHint(screenGo, design!, font, buttonTop);

            var stageRoot = BuildStage(screenGo, design!, quad!, circle!, outline!, zoneShader!,
                out var stage, out var disc, out var atmosphere, out var flash, out var moon, out var confetti, out var markers);

            BuildSlotSheet(screenGo, design!, font, rounded!, outline!,
                out var sheet, out var sheetTitle, out var replaceButton, out var replaceLabel,
                out var removeButton, out var removeLabel, out var cancelButton, out var cancelLabel);

            BuildCompletionCard(screenGo, design!, font, rounded!,
                out var card, out var kicker, out var completionTitle, out var nextButton, out var nextLabel);

            Wire(screen,
                ("design", design!), ("planetShader", planetShader!),
                ("backButton", backButton), ("planetTitle", title), ("currency", currency),
                ("stageRoot", stageRoot), ("stage", stage), ("disc", disc),
                ("atmosphere", atmosphere), ("completionFlash", flash), ("moon", moon), ("confetti", confetti),
                ("progressLabel", progress), ("rotateHint", hint),
                ("collectionButton", collectionButton), ("collectionLabel", collectionLabel),
                ("slotSheet", sheet), ("sheetTitle", sheetTitle),
                ("replaceButton", replaceButton), ("replaceLabel", replaceLabel),
                ("removeButton", removeButton), ("removeLabel", removeLabel),
                ("cancelButton", cancelButton), ("cancelLabel", cancelLabel),
                ("completionCard", card), ("completionKicker", kicker),
                ("completionTitle", completionTitle), ("nextPlanetButton", nextButton),
                ("nextPlanetLabel", nextLabel));
            WireArray(stage, "markers", markers);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Планету зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: padding 0 18 4; кнопка 40; назва 15/800 ls .04em ──
        private static RectTransform BuildHeader(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite circleOutline, GameObject currencyPrefab,
            out Button backButton, out TMP_Text title, out CurrencyWidget currency)
        {
            var height = M(44f);
            var go = Child(parent, "Header");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(M(18f), -height);
            rect.offsetMax = new Vector2(-M(18f), 0f);

            var backSize = M(40f);
            var backGo = Child(go, "Back");
            var backFill = backGo.AddComponent<Image>();
            backFill.sprite = circle;
            backFill.color = design.CircleButtonFill;
            Place(backFill, Vector2.zero, new Vector2(backSize, backSize),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var ringGo = Child(backGo, "Ring");
            Stretch(ringGo);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = circleOutline;
            ring.color = design.GlassStroke;
            ring.raycastTarget = false;

            var chevron = Label(backGo, "Chevron", "‹", design, font,
                M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(chevron.gameObject);

            backButton = backGo.AddComponent<Button>();
            backButton.targetGraphic = backFill;

            // Заголовок центруємо у ВІЛЬНОМУ проміжку між кнопкою «‹» і капсулою валюти.
            title = Label(go, "PlanetTitle", "Терра Прима", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(-M(36f), 0f), new Vector2(M(182f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, go.transform);
            currency = currencyGo.GetComponent<CurrencyWidget>();
            Place(currency, Vector2.zero, new Vector2(M(112f), M(40f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            return rect;
        }

        // ── Кнопка «Колекція»: 200×50 r26 градієнт, при низу safe area ──
        private static Button BuildCollectionButton(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, out TMP_Text label, out float top)
        {
            var height = M(50f);
            var bottom = M(24f);
            var go = Child(parent, "CollectionButton");
            var fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(26f));
            fill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(fill, new Vector2(0f, bottom), new Vector2(M(200f), height),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            label = Label(go, "Label", "Колекція", design, font,
                design.FontSizePaintButton, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            top = bottom + height;
            return button;
        }

        // ── Прогрес «3 / 7 слотів»: над кнопкою ──
        private static TMP_Text BuildProgress(GameObject parent, DesignSystem design, TMP_FontAsset? font, float buttonTop)
        {
            var label = Label(parent, "Progress", "3 / 7 слотів · тапни на порожній", design, font,
                design.FontSizeGalaxyTitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(label, new Vector2(0f, buttonTop + M(12f)), new Vector2(M(340f), M(20f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            return label;
        }

        // ── Підказка: 11.5/700 ──
        private static TMP_Text BuildHint(GameObject parent, DesignSystem design, TMP_FontAsset? font, float buttonTop)
        {
            var hint = Label(parent, "RotateHint",
                "<sprite name=\"retry\"> Крути планету пальцем, щоб дістати всі слоти",
                design, font, design.FontSizeSmall, design.TextDim, TextAlignmentOptions.Center);
            Place(hint, new Vector2(0f, buttonTop + M(36f)), new Vector2(M(340f), M(16f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            return hint;
        }

        // ── Планета: S = 264 макета; слоти — чотири шари під диском ──
        private static RectTransform BuildStage(GameObject parent, DesignSystem design,
            Sprite quad, Sprite circle, Sprite outline, Shader zoneShader,
            out PlanetStage stage, out Image disc, out Image atmosphere, out Image flash,
            out RectTransform moon, out DripPool confetti, out SlotMarker[] markers)
        {
            var size = M(264f);
            var go = Child(parent, "Stage");
            var stageRect = go.GetComponent<RectTransform>();
            stageRect.anchorMin = stageRect.anchorMax = new Vector2(0.5f, 0.5f);
            stageRect.pivot = new Vector2(0.5f, 0.5f);
            // Трохи вище центру: знизу підказка, прогрес і кнопка, згори лише шапка.
            stageRect.anchoredPosition = new Vector2(0f, M(30f));
            stageRect.sizeDelta = new Vector2(size, size);

            // Матеріал серпанку створює PlanetScreen у рантаймі: матеріал, зроблений
            // тут, запікся б у файл сцени окремим об'єктом і жив своїм життям.
            atmosphere = Quad(go, "Atmosphere", quad, size * design.PlanetAtmosphereScale);

            flash = Quad(go, "Flash", circle, size * design.PlanetFlashScale);
            flash.color = Color.white;

            // Диск — і планета, і зона захоплення дотику: обертання та влучання
            // в слот рахує PlanetStage на цьому ж об'єкті.
            var discGo = Child(go, "Disc");
            disc = discGo.AddComponent<Image>();
            disc.sprite = quad;
            disc.raycastTarget = true;
            Place(disc, Vector2.zero, new Vector2(size, size),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            stage = discGo.AddComponent<PlanetStage>();

            var sockets = Layer(discGo, "Sockets");
            var plates = Layer(discGo, "Plates");
            var frames = Layer(discGo, "Frames");
            var pixels = Layer(discGo, "Pixels");
            var panel = K1Sprites.Panel;

            markers = new SlotMarker[MaxSlots];
            for (var i = 0; i < MaxSlots; i++)
            {
                // Влучання рахує PlanetStage — графіка слотів у raycast не бере участі,
                // інакше передній і задній слоти сперечались би за дотик.
                var socket = Quad(sockets, $"Socket{i}", quad, 100f);
                var plate = Quad(plates, $"Plate{i}", panel != null ? panel : quad, 100f);
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = K1Sprites.PanelMultiplier(M(10f));
                var frame = Quad(frames, $"Frame{i}", outline, 100f);
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(10f));
                var pixelGo = Child(pixels, $"Pixels{i}");
                var pixelImage = pixelGo.AddComponent<RawImage>();
                pixelImage.raycastTarget = false;
                pixelImage.color = Color.white;
                Place(pixelImage, Vector2.zero, new Vector2(100f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                var marker = socket.gameObject.AddComponent<SlotMarker>();
                Wire(marker, ("design", design),
                    ("socket", socket), ("plate", plate), ("frame", frame), ("pixels", pixelImage));
                plate.gameObject.SetActive(false);
                frame.gameObject.SetActive(false);
                pixelGo.SetActive(false);
                socket.gameObject.SetActive(false);
                markers[i] = marker;
            }

            // Матеріали порожніх плям створює й нищить стадія — їй і шейдер.
            Wire(stage, ("design", design), ("disc", disc), ("zoneShader", zoneShader));

            var moonGo = Child(go, "Moon");
            var moonImage = moonGo.AddComponent<Image>();
            moonImage.sprite = circle;
            moonImage.color = design.PlanetMoonColor;
            moonImage.raycastTarget = false;
            Place(moonImage, Vector2.zero, new Vector2(M(26f), M(26f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            moon = moonGo.GetComponent<RectTransform>();

            var confettiGo = Child(go, "Confetti");
            var confettiRect = confettiGo.GetComponent<RectTransform>();
            confettiRect.anchorMin = confettiRect.anchorMax = new Vector2(0.5f, 0.5f);
            confettiRect.sizeDelta = Vector2.zero;
            confetti = confettiGo.AddComponent<DripPool>();
            Wire(confetti, ("design", design), ("dropSprite", circle));

            return stageRect;
        }

        private static GameObject Layer(GameObject disc, string name)
        {
            var go = Child(disc, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return go;
        }

        // ── Лист зайнятого слота: «Замінити» / «Повернути в колекцію» / «Скасувати» ──
        private static void BuildSlotSheet(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline,
            out RectTransform sheet, out TMP_Text title, out Button replaceButton, out TMP_Text replaceLabel,
            out Button removeButton, out TMP_Text removeLabel, out Button cancelButton, out TMP_Text cancelLabel)
        {
            var go = Child(parent, "SlotSheet");
            Stretch(go);
            sheet = go.GetComponent<RectTransform>();

            var scrim = go.AddComponent<Image>();
            scrim.color = design.OverScrim;
            scrim.raycastTarget = true;

            var panelGo = Child(go, "Panel");
            var panel = panelGo.AddComponent<GradientImage>();
            panel.sprite = rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            panel.SetGradient(design.OverCardFrom, design.OverCardTo);
            Place(panel, Vector2.zero, new Vector2(M(300f), M(236f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            title = Label(panelGo, "Title", "Картинка", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -M(22f)), new Vector2(M(260f), M(30f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            replaceButton = SheetButton(panelGo, "Replace", design, font, rounded, null, "Замінити",
                new Vector2(0f, -M(70f)), design.AccentTeal, design.AccentBlue, design.TextPrimary, out replaceLabel);
            removeButton = SheetButton(panelGo, "Remove", design, font, rounded, outline, "Повернути в колекцію",
                new Vector2(0f, -M(124f)), design.GlassFill, design.GlassFill, design.TextPrimary, out removeLabel);

            var cancelGo = Child(panelGo, "Cancel");
            cancelLabel = Label(cancelGo, "Label", "Скасувати", design, font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
            cancelLabel.raycastTarget = true;
            Stretch(cancelLabel.gameObject);
            var cancelRect = cancelGo.GetComponent<RectTransform>();
            cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(0f, M(14f));
            cancelRect.sizeDelta = new Vector2(M(160f), M(40f));
            cancelButton = cancelGo.AddComponent<Button>();
            cancelButton.targetGraphic = cancelLabel;

            go.SetActive(false);
        }

        private static Button SheetButton(GameObject parent, string name, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite? outline, string caption, Vector2 position, Color from, Color to, Color text,
            out TMP_Text label)
        {
            var go = Child(parent, name);
            var fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            fill.SetGradient(from, to);
            Place(fill, position, new Vector2(M(252f), M(46f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            if (outline != null)
            {
                var strokeGo = Child(go, "Stroke");
                Stretch(strokeGo);
                var stroke = strokeGo.AddComponent<Image>();
                stroke.sprite = outline;
                stroke.type = Image.Type.Sliced;
                stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
                stroke.color = design.GlassStroke;
                stroke.raycastTarget = false;
            }

            label = Label(go, "Label", caption, design, font, design.FontSizeShopCard, text, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }

        // ── Картка завершення ──
        private static void BuildCompletionCard(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded,
            out RectTransform card, out TMP_Text kicker, out TMP_Text title,
            out Button nextButton, out TMP_Text nextLabel)
        {
            var go = Child(parent, "CompletionCard");
            Stretch(go);
            card = go.GetComponent<RectTransform>();

            var scrim = go.AddComponent<GradientImage>();
            scrim.SetGradient(new Color(0.031f, 0.016f, 0.071f, 0.82f),
                new Color(0.031f, 0.016f, 0.071f, 0f));
            scrim.raycastTarget = true;

            kicker = Label(go, "Kicker", "ПЛАНЕТА ОЖИЛА", design, font,
                design.FontSizeLabel, design.AccentTeal, TextAlignmentOptions.Center);
            Place(kicker, new Vector2(0f, M(150f)), new Vector2(M(320f), M(16f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            title = Label(go, "Title", "Терра Прима ожила!", design, font,
                design.FontSizeCompletion, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, M(120f)), new Vector2(M(340f), M(34f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            var buttonGo = Child(go, "NextPlanet");
            var fill = buttonGo.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            fill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(fill, new Vector2(0f, M(58f)), new Vector2(M(230f), M(52f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            nextLabel = Label(buttonGo, "Label", "Наступна планета", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(nextLabel.gameObject);

            nextButton = buttonGo.AddComponent<Button>();
            nextButton.targetGraphic = fill;
        }

        private static Image Quad(GameObject parent, string name, Sprite sprite, float side)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            Place(image, Vector2.zero, new Vector2(side, side),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            return image;
        }
    }
}
