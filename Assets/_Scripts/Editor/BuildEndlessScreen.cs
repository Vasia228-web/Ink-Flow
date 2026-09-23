using System.Collections.Generic;
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
using static InkFlow.Editor.UiBuilder;

namespace InkFlow.Editor
{
    /// <summary>
    /// Збирає екран «Нескінченний» нового ядра: шапка, капсули рахунку й рекорду,
    /// три баки й змішувач, поле 8×8 із блоків і привидів, лоток на три фігури, картка фіналу.
    /// Меню: Ink Flow → Setup → Build Endless Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Геометрію поля й лотка дає
    /// <see cref="BoardGeometry"/>: індекс блока = y × 8 + x, і саме так їх читає
    /// <see cref="BoardView"/>.
    /// </summary>
    public static class BuildEndlessScreen
    {
        private const string ScenePath = "Assets/Scenes/Endless.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string PictureZoneShaderPath = "Assets/_Shaders/InkFlowPictureZone.shader";

        /// <summary>Скільки зон може показати картинка: легендарна — 15+ (§6).</summary>
        private const int ZoneSlots = 20;

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(16f);

        /// <summary>Скільки крапельок летить за новий рекорд.</summary>
        private const int ConfettiPieces = 18;

        /// <summary>Скільки написів «+фарба» може летіти одночасно.</summary>
        private const int FloatLabels = 4;

        [MenuItem("Ink Flow/Setup/Build Endless Screen")]
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

            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);

            var rounded = LoadSprite("rounded-rect");
            var outline = LoadSprite("rounded-rect-outline");
            var circle = LoadSprite("circle-soft");
            var circleOutline = LoadSprite("circle-outline");
            var nebula = LoadSprite("nebula");
            var glow = LoadSprite("glow");
            var retry = LoadSprite("icon-retry");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var zoneShader = AssetDatabase.LoadAssetAtPath<Shader>(PictureZoneShaderPath);
            var pictureArt = AssetDatabase.LoadAssetAtPath<PictureArtCatalog>(GeneratePictureArt.CatalogPath);

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"),
                (circle, "circle-soft"), (circleOutline, "circle-outline"),
                (nebula, "nebula"), (glow, "glow"), (retry, "icon-retry")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (zoneShader == null) missing.Add(PictureZoneShaderPath);
            if (pictureArt == null) missing.Add($"{GeneratePictureArt.CatalogPath} (Ink Flow → Setup → Generate Picture Art)");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Нескінченний НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "EndlessScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<EndlessScreen>();

            // Верхній блок (§5: картинка над полем, видно завжди): ліворуч картинка
            // 150×156, праворуч колонка — капсули рахунку й рекорду над баками зі
            // змішувачем. Так усе вміщається над полем 358 навіть на 844 px макета.
            var headerHeight = M(40f);
            var blockTop = headerHeight + M(12f);
            var blockHeight = M(156f);
            var pictureWidth = M(150f);
            var columnLeft = pictureWidth + M(12f);
            var columnWidth = M(358f) - columnLeft;
            var statsHeight = M(44f);
            var tanksHeight = M(80f);
            var columnTop = blockTop + (blockHeight - statsHeight - M(10f) - tanksHeight) * 0.5f;
            var tanksTop = columnTop + statsHeight + M(10f);
            var boardTop = blockTop + blockHeight + M(10f);
            var boardSide = M(BoardGeometry.Canvas);
            var trayTop = boardTop + boardSide + M(12f);
            var trayHeight = M(86f);

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, retry!,
                headerHeight, out var backButton, out var title, out var restartButton);

            var picture = BuildPicture(screenGo, design!, font, rounded!, outline!, nebula!, zoneShader!, pictureArt!,
                blockTop, pictureWidth, blockHeight);

            BuildCapsules(screenGo, design!, font, rounded!, outline!, nebula!,
                columnLeft, columnTop, columnWidth, statsHeight,
                out var scoreCapsule, out var scoreStroke, out var scoreGlow,
                out var scoreLabel, out var scoreNumber,
                out var recordCapsule, out var recordStroke, out var recordGlow,
                out var recordLabel, out var recordNumber);

            var tanks = BuildTanks(screenGo, design!, font, rounded!, outline!, circle!,
                columnLeft, tanksTop, columnWidth, tanksHeight, out var mixer);

            var board = BuildBoard(screenGo, design!, font, rounded!, outline!, boardTop, boardSide,
                out var boardPlate, out var boardPlateStroke);

            var tray = BuildTray(screenGo, design!, rounded!, outline!, trayTop, trayHeight);

            BuildOverlays(screenGo, design!, font, glow!,
                out var overflowRing, out var comboPop);

            BuildIntro(screenGo, design!, font, rounded!, outline!, nebula!, zoneShader!, pictureArt!,
                out var introCard, out var introGroup, out var introScrim, out var introPanel,
                out var introPanelStroke, out var introPicture, out var introKicker, out var introName,
                out var introRarity, out var introHint, out var introButton);

            BuildOver(screenGo, design!, font, rounded!, outline!, circle!, nebula!, zoneShader!, pictureArt!,
                out var overCard, out var overScrim, out var overPanel, out var overPanelStroke,
                out var overScoreLabel, out var overScoreNumber,
                out var recordChip, out var recordChipLabel,
                out var rewardRow, out var rewardNumber, out var rewardSuffix,
                out var overBestLabel, out var overAgain, out var overAgainFill,
                out var overAgainLabel, out var overMenu, out var overMenuLabel,
                out var confettiRoot, out var confetti,
                out var overCollectedLabel, out var overThumbs);

            Wire(screen,
                ("design", design!),
                ("backButton", backButton), ("title", title), ("restartButton", restartButton),
                ("scoreCapsule", scoreCapsule), ("scoreCapsuleStroke", scoreStroke),
                ("scoreCapsuleGlow", scoreGlow), ("scoreLabel", scoreLabel),
                ("scoreNumber", scoreNumber),
                ("recordCapsule", recordCapsule), ("recordCapsuleStroke", recordStroke),
                ("recordCapsuleGlow", recordGlow), ("recordLabel", recordLabel),
                ("recordNumber", recordNumber),
                ("board", board), ("tray", tray), ("mixer", mixer), ("picture", picture),
                ("boardPlate", boardPlate), ("boardPlateStroke", boardPlateStroke),
                ("overflowRing", overflowRing), ("comboPop", comboPop),
                ("overCard", overCard), ("overScrim", overScrim), ("overPanel", overPanel),
                ("overPanelStroke", overPanelStroke),
                ("overScoreLabel", overScoreLabel), ("overScoreNumber", overScoreNumber),
                ("recordChip", recordChip), ("recordChipLabel", recordChipLabel),
                ("rewardRow", rewardRow), ("rewardNumber", rewardNumber),
                ("rewardSuffix", rewardSuffix), ("overBestLabel", overBestLabel),
                ("overAgain", overAgain), ("overAgainFill", overAgainFill),
                ("overAgainLabel", overAgainLabel),
                ("overMenu", overMenu), ("overMenuLabel", overMenuLabel),
                ("confettiRoot", confettiRoot), ("overCollectedLabel", overCollectedLabel),
                ("introCard", introCard), ("introGroup", introGroup), ("introScrim", introScrim),
                ("introPanel", introPanel), ("introPanelStroke", introPanelStroke),
                ("introPicture", introPicture), ("introKicker", introKicker), ("introName", introName),
                ("introRarity", introRarity), ("introHint", introHint), ("introButton", introButton));
            WireArray(screen, "confetti", confetti);
            WireArray(screen, "tanks", tanks);
            WireArray(screen, "overThumbs", overThumbs);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Нескінченний зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: ‹ · НЕСКІНЧЕННИЙ · ↺ ──
        private static void BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, Sprite retry, float height,
            out Button backButton, out TMP_Text title, out Button restartButton)
        {
            var go = Child(parent, "Header");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -height);
            rect.offsetMax = new Vector2(-SideMargin, 0f);

            backButton = RoundButton(go, "Back", design, circle, circleOutline,
                new Vector2(0f, 0.5f), out var backRoot);
            var chevron = Label(backRoot, "Glyph", "‹", design, font,
                M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(chevron.gameObject);

            restartButton = RoundButton(go, "Restart", design, circle, circleOutline,
                new Vector2(1f, 0.5f), out var restartRoot);
            // ↺ — спрайт, не гліф: у Nunito його немає.
            var retryGo = Child(restartRoot, "Glyph");
            var retryIcon = retryGo.AddComponent<Image>();
            retryIcon.sprite = retry;
            retryIcon.color = design.TextPrimary;
            retryIcon.raycastTarget = false;
            Place(retryIcon, Vector2.zero, new Vector2(M(20f), M(20f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            title = Label(go, "Title", "НЕСКІНЧЕННИЙ", design, font,
                design.FontSizeGameTitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(240f), M(20f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private static Button RoundButton(GameObject parent, string name, DesignSystem design,
            Sprite circle, Sprite circleOutline, Vector2 anchor, out GameObject root)
        {
            root = Child(parent, name);
            var fill = root.AddComponent<Image>();
            fill.sprite = circle;
            fill.color = design.CircleButtonFill;
            Place(fill, Vector2.zero, new Vector2(M(40f), M(40f)), anchor, anchor);

            var ringGo = Child(root, "Ring");
            Stretch(ringGo);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = circleOutline;
            ring.color = design.GlassStroke;
            ring.raycastTarget = false;

            var button = root.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }

        // ── Дві капсули: РАХУНОК і РЕКОРД ──
        private static void BuildCapsules(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, float left, float top, float width, float height,
            out Image scoreCapsule, out Image scoreStroke, out Image scoreGlow,
            out TMP_Text scoreLabel, out TMP_Text scoreNumber,
            out Image recordCapsule, out Image recordStroke, out Image recordGlow,
            out TMP_Text recordLabel, out TMP_Text recordNumber)
        {
            var go = Child(parent, "Stats");
            TopLeft(go, left, top, width, height);

            var gap = M(8f);
            var half = (width - gap) * 0.5f;

            Capsule(go, "Score", design, font, rounded, outline, nebula,
                new Vector2(0f, 0.5f), half, height, "РАХУНОК", "0", design.TextPrimary,
                out scoreCapsule, out scoreStroke, out scoreGlow, out scoreLabel, out scoreNumber);

            Capsule(go, "Record", design, font, rounded, outline, nebula,
                new Vector2(1f, 0.5f), half, height, "РЕКОРД", "0", design.AccentGold,
                out recordCapsule, out recordStroke, out recordGlow, out recordLabel, out recordNumber);
        }

        private static void Capsule(GameObject parent, string name, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite nebula,
            Vector2 anchor, float width, float height, string caption, string value, Color valueColor,
            out Image fill, out Image stroke, out Image glow, out TMP_Text label, out TMP_Text number)
        {
            // Корінь капсули має РОЗМІР капсули, а всередині все розтягується по ньому:
            // гало, поставлене тим самим якорем, що й заливка, росло б від краю в один бік.
            var go = Child(parent, name);
            var rootRect = go.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = anchor;
            rootRect.pivot = anchor;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(width, height);

            var glowGo = Child(go, "Glow");
            Stretch(glowGo, -M(14f));
            glow = glowGo.AddComponent<Image>();
            glow.sprite = nebula;
            glow.raycastTarget = false;

            var fillGo = Child(go, "Fill");
            Stretch(fillGo);
            fill = fillGo.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            fill.color = design.StatCapsuleFill;
            fill.raycastTarget = false;

            var strokeGo = Child(fillGo, "Stroke");
            Stretch(strokeGo);
            stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            stroke.color = design.StatCapsuleStroke;
            stroke.raycastTarget = false;

            label = Label(fillGo, "Label", caption, design, font,
                design.FontSizeStatLabel, design.TextDim, TextAlignmentOptions.Center);
            Place(label, new Vector2(0f, -M(5f)), new Vector2(width, M(12f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            number = Label(fillGo, "Number", value, design, font,
                design.FontSizeScoreNumber, valueColor, TextAlignmentOptions.Center);
            Place(number, new Vector2(0f, M(4f)), new Vector2(width, M(24f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        }

        /// <summary>Прямокутник від лівого верхнього кута екрана (з бічним полем) у px макета × K.</summary>
        private static RectTransform TopLeft(GameObject go, float left, float top, float width, float height)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(SideMargin + left, -top);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        // ── Картинка: плитка 150×128 із зонами стосом + назва й підпис зони ──
        private static PictureView BuildPicture(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Shader zoneShader, PictureArtCatalog art,
            float top, float width, float height)
        {
            var go = Child(parent, "Picture");
            TopLeft(go, 0f, top, width, height);
            return MakePictureView(go, design, font, rounded, outline, nebula, zoneShader, art,
                width, M(128f), M(104f), withTitle: true, captionHeight: M(24f));
        }

        /// <summary>
        /// Плитка з картинкою: скло, гало завершення, назва згори, квадрат зон знизу і,
        /// за потреби, підпис під плиткою. Одна збірка на три місця — картинка над полем,
        /// картка перед забігом, мініатюри галереї, — щоб вони не розійшлися на першій правці.
        /// </summary>
        private static PictureView MakePictureView(GameObject go, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Shader zoneShader, PictureArtCatalog art,
            float width, float plateHeight, float canvasSide, bool withTitle, float captionHeight)
        {
            var plateGo = Child(go, "Plate");
            var plate = plateGo.GetComponent<RectTransform>();
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 1f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = new Vector2(0f, -plateHeight * 0.5f);
            plate.sizeDelta = new Vector2(width, plateHeight);

            var radius = Mathf.Min(M(18f), plateHeight * 0.25f);

            var glowGo = Child(plateGo, "Glow");
            Stretch(glowGo, -M(18f));
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = nebula;
            glow.color = design.AccentGold;
            glow.raycastTarget = false;

            var fillGo = Child(plateGo, "Fill");
            Stretch(fillGo);
            var plateFill = fillGo.AddComponent<Image>();
            plateFill.sprite = rounded;
            plateFill.type = Image.Type.Sliced;
            plateFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            plateFill.color = design.PicturePlateFill;
            plateFill.raycastTarget = false;

            var strokeGo = Child(plateGo, "Stroke");
            Stretch(strokeGo);
            var plateStroke = strokeGo.AddComponent<Image>();
            plateStroke.sprite = outline;
            plateStroke.type = Image.Type.Sliced;
            plateStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            plateStroke.color = design.PicturePlateStroke;
            plateStroke.raycastTarget = false;

            TMP_Text? title = null;
            TMP_Text? attempts = null;
            if (withTitle)
            {
                title = Label(plateGo, "Title", "КИТ · 0/5", design, font,
                    design.FontSizePictureName, design.TextPrimary, TextAlignmentOptions.Center);
                Place(title, new Vector2(0f, -M(5f)), new Vector2(width, M(14f)),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

                // §7 п.5: «Спроб лишилось» видно завжди — під назвою, золотом, лише коли є що рятувати.
                attempts = Label(plateGo, "Attempts", "СПРОБ · 3", design, font,
                    design.FontSizePictureCaption, design.AccentGold, TextAlignmentOptions.Center);
                Place(attempts, new Vector2(0f, -M(19f)), new Vector2(width, M(12f)),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                attempts.gameObject.SetActive(false);
            }

            // Квадрат зон — знизу плитки; без назви — по центру.
            var canvasGo = Child(plateGo, "Canvas");
            var canvas = canvasGo.GetComponent<RectTransform>();
            if (withTitle)
            {
                canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0f);
                canvas.pivot = new Vector2(0.5f, 0f);
                canvas.anchoredPosition = new Vector2(0f, M(6f));
            }
            else
            {
                canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0.5f);
                canvas.pivot = new Vector2(0.5f, 0.5f);
                canvas.anchoredPosition = Vector2.zero;
            }
            canvas.sizeDelta = new Vector2(canvasSide, canvasSide);

            var zones = new PictureZoneView[ZoneSlots];
            for (var i = 0; i < ZoneSlots; i++)
            {
                var zoneGo = Child(canvasGo, $"Zone_{i}");
                Stretch(zoneGo);
                var image = zoneGo.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                var zone = zoneGo.AddComponent<PictureZoneView>();
                Wire(zone, ("design", design), ("zoneShader", zoneShader), ("image", image));
                zoneGo.SetActive(false);
                zones[i] = zone;
            }

            TMP_Text? caption = null;
            if (captionHeight > 0f)
            {
                caption = Label(go, "Caption", "ТІЛО · СИНІЙ", design, font,
                    design.FontSizePictureCaption, design.TextMuted, TextAlignmentOptions.Center);
                Place(caption, new Vector2(0f, 0f), new Vector2(width, captionHeight),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            }

            var view = go.AddComponent<PictureView>();
            Wire(view, ("design", design), ("art", art), ("plate", plate), ("plateFill", plateFill),
                ("plateStroke", plateStroke), ("glow", glow), ("canvas", canvas));
            if (title != null) Wire(view, ("title", title));
            if (attempts != null) Wire(view, ("attempts", attempts));
            if (caption != null) Wire(view, ("caption", caption));
            WireArray(view, "zones", zones);
            view.Apply();
            return view;
        }

        // ── Картка перед забігом: «цього забігу — така картинка» ──
        private static void BuildIntro(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Shader zoneShader, PictureArtCatalog art,
            out RectTransform introCard, out CanvasGroup group, out Image scrim, out GradientImage panel,
            out Image panelStroke, out PictureView picture, out TMP_Text kicker, out TMP_Text name,
            out TMP_Text rarity, out TMP_Text hint, out Button button)
        {
            var go = Child(parent, "RunIntro");
            Stretch(go);
            introCard = go.GetComponent<RectTransform>();
            group = go.AddComponent<CanvasGroup>();

            scrim = go.AddComponent<Image>();
            scrim.color = design.OverScrim;
            // Тап будь-де по картці — почати. Кнопка на самому скримі, без окремої графіки.
            button = go.AddComponent<Button>();
            button.targetGraphic = scrim;
            button.transition = Selectable.Transition.None;

            var panelGo = Child(go, "Panel");
            panel = panelGo.AddComponent<GradientImage>();
            panel.sprite = rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(34f));
            panel.raycastTarget = false;
            Place(panel, Vector2.zero, new Vector2(M(296f), M(392f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var strokeGo = Child(panelGo, "Stroke");
            Stretch(strokeGo);
            panelStroke = strokeGo.AddComponent<Image>();
            panelStroke.sprite = outline;
            panelStroke.type = Image.Type.Sliced;
            panelStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(34f));
            panelStroke.color = design.GlassStroke;
            panelStroke.raycastTarget = false;

            kicker = Label(panelGo, "Kicker", "ЦЬОГО ЗАБІГУ", design, font,
                design.FontSizeIntroKicker, design.TextDim, TextAlignmentOptions.Center);
            Place(kicker, new Vector2(0f, -M(22f)), new Vector2(M(260f), M(16f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var pictureGo = Child(panelGo, "Picture");
            var pictureRect = pictureGo.GetComponent<RectTransform>();
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 1f);
            pictureRect.pivot = new Vector2(0.5f, 1f);
            pictureRect.anchoredPosition = new Vector2(0f, -M(46f));
            pictureRect.sizeDelta = new Vector2(M(200f), M(200f));
            picture = MakePictureView(pictureGo, design, font, rounded, outline, nebula, zoneShader, art,
                M(200f), M(200f), M(172f), withTitle: false, captionHeight: 0f);

            name = Label(panelGo, "Name", "КИТ", design, font,
                design.FontSizeIntroName, design.TextPrimary, TextAlignmentOptions.Center);
            Place(name, new Vector2(0f, -M(256f)), new Vector2(M(270f), M(30f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            rarity = Label(panelGo, "Rarity", "ЗВИЧАЙНА · 5 ЗОН", design, font,
                design.FontSizeIntroRarity, design.TextMuted, TextAlignmentOptions.Center);
            Place(rarity, new Vector2(0f, -M(290f)), new Vector2(M(270f), M(18f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            hint = Label(panelGo, "Hint", "Тапни, щоб грати", design, font,
                design.FontSizeIntroHint, design.TextMuted, TextAlignmentOptions.Center);
            Place(hint, new Vector2(0f, M(18f)), new Vector2(M(270f), M(20f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            go.SetActive(false);
        }

        // ── Баки й змішувач: три капсули 26×80 px макета (прототип v3, beakerVMs) + посудина 44×80 ──
        private static TankView[] BuildTanks(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, float left, float top, float width, float height,
            out MixerView mixer)
        {
            var go = Child(parent, "Tanks");
            TopLeft(go, left, top, width, height);

            var tankWidth = M(26f);
            var tankHeight = M(80f);
            var gap = M(18f);
            var mixerGap = M(34f);
            var mixerWidth = M(44f);
            var pigments = Pigments.Base;
            var totalWidth = pigments.Length * tankWidth + (pigments.Length - 1) * gap + mixerGap + mixerWidth;
            var groupLeft = -totalWidth * 0.5f;
            var tanks = new TankView[pigments.Length];

            for (var i = 0; i < pigments.Length; i++)
            {
                var tankGo = Child(go, $"Tank_{pigments[i]}");
                var tankRect = tankGo.GetComponent<RectTransform>();
                tankRect.anchorMin = tankRect.anchorMax = new Vector2(0.5f, 0.5f);
                tankRect.pivot = new Vector2(0.5f, 0.5f);
                tankRect.anchoredPosition = new Vector2(groupLeft + tankWidth * 0.5f + i * (tankWidth + gap), 0f);
                tankRect.sizeDelta = new Vector2(tankWidth, tankHeight);

                var track = tankGo.AddComponent<Image>();
                track.sprite = rounded;
                track.type = Image.Type.Sliced;
                track.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(8f));
                track.color = design.TankTrackFill;
                track.raycastTarget = false;

                // Заливка на всю висоту з півотом ЗНИЗУ: рівень — це localScale.y.
                var fill = BottomFill(tankGo, rounded, design.PigmentColor(pigments[i]), out var fillRect);

                var strokeGo = Child(tankGo, "Stroke");
                Stretch(strokeGo);
                var stroke = strokeGo.AddComponent<Image>();
                stroke.sprite = outline;
                stroke.type = Image.Type.Sliced;
                stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(8f));
                stroke.color = design.TankTrackStroke;
                stroke.raycastTarget = false;

                var number = Label(tankGo, "Number", "0", design, font,
                    design.FontSizeTankNumber, design.TextPrimary, TextAlignmentOptions.Center);
                Place(number, new Vector2(0f, M(3f)), new Vector2(tankWidth, M(14f)),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

                var tank = tankGo.AddComponent<TankView>();
                Wire(tank, ("design", design), ("track", track), ("stroke", stroke),
                    ("fill", fill), ("fillRect", fillRect), ("number", number));
                var so = new SerializedObject(tank);
                so.FindProperty("pigment").intValue = (int)pigments[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                tank.Apply();
                tanks[i] = tank;
            }

            // Посудина змішувача — праворуч від баків, ширша: у ній кружляють три краплі.
            var vesselGo = Child(go, "Mixer");
            var vessel = vesselGo.GetComponent<RectTransform>();
            vessel.anchorMin = vessel.anchorMax = new Vector2(0.5f, 0.5f);
            vessel.pivot = new Vector2(0.5f, 0.5f);
            vessel.anchoredPosition = new Vector2(groupLeft + totalWidth - mixerWidth * 0.5f, 0f);
            vessel.sizeDelta = new Vector2(mixerWidth, tankHeight);

            var mixerTrack = vesselGo.AddComponent<Image>();
            mixerTrack.sprite = rounded;
            mixerTrack.type = Image.Type.Sliced;
            mixerTrack.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(12f));
            mixerTrack.color = design.MixerTrackFill;
            mixerTrack.raycastTarget = false;

            var mixerFill = BottomFill(vesselGo, rounded, design.MixerTrackFill, out var mixerFillRect);

            var orbitGo = Child(vesselGo, "Orbit");
            var orbit = orbitGo.GetComponent<RectTransform>();
            orbit.anchorMin = orbit.anchorMax = new Vector2(0.5f, 0.5f);
            orbit.pivot = new Vector2(0.5f, 0.5f);
            orbit.anchoredPosition = Vector2.zero;
            orbit.sizeDelta = Vector2.zero;
            var drops = new Image[pigments.Length];
            for (var i = 0; i < pigments.Length; i++)
                drops[i] = Dot(orbitGo, $"Drop_{pigments[i]}", circle, design.PigmentColor(pigments[i]), M(14f));

            var mixerStrokeGo = Child(vesselGo, "Stroke");
            Stretch(mixerStrokeGo);
            var mixerStroke = mixerStrokeGo.AddComponent<Image>();
            mixerStroke.sprite = outline;
            mixerStroke.type = Image.Type.Sliced;
            mixerStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(12f));
            mixerStroke.color = design.MixerTrackStroke;
            mixerStroke.raycastTarget = false;

            // Струмені, виплеск і назва відтінку — діти РЯДКА: літають між баками й посудиною.
            var streams = new Image[pigments.Length];
            for (var i = 0; i < pigments.Length; i++)
            {
                streams[i] = Dot(go, $"Stream_{pigments[i]}", circle, design.PigmentColor(pigments[i]), M(14f));
                streams[i].gameObject.SetActive(false);
            }

            var splash = Dot(go, "Splash", circle, Color.white, M(44f));
            splash.gameObject.SetActive(false);

            var hueName = Label(go, "HueName", "ЗЕЛЕНИЙ", design, font,
                design.FontSizeHueName, design.TextPrimary, TextAlignmentOptions.Center);
            Place(hueName, Vector2.zero, new Vector2(M(220f), M(22f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            hueName.gameObject.SetActive(false);

            mixer = go.AddComponent<MixerView>();
            Wire(mixer, ("design", design), ("vessel", vessel), ("track", mixerTrack),
                ("stroke", mixerStroke), ("fill", mixerFill), ("fillRect", mixerFillRect),
                ("orbit", orbit), ("splash", splash), ("hueName", hueName));
            WireArray(mixer, "tanks", tanks);
            WireArray(mixer, "drops", drops);
            WireArray(mixer, "streams", streams);
            mixer.Apply();

            return tanks;
        }

        /// <summary>Заливка на всю висоту батька з півотом знизу: рівень анімується localScale.y.</summary>
        private static Image BottomFill(GameObject parent, Sprite rounded, Color color, out RectTransform fillRect)
        {
            var fillGo = Child(parent, "Fill");
            fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.offsetMin = new Vector2(M(2f), M(2f));
            fillRect.offsetMax = new Vector2(-M(2f), -M(2f));
            fillRect.localScale = new Vector3(1f, 0f, 1f);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(6f));
            fill.color = color;
            fill.raycastTarget = false;
            return fill;
        }

        /// <summary>Кругла крапля-спрайт заданого розміру, центрована в батькові.</summary>
        private static Image Dot(GameObject parent, string name, Sprite circle, Color color, float size)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = circle;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // ── Поле: полотно 358 px макета, 64 блоки + 64 привиди ──
        private static BoardView BuildBoard(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, float top, float side,
            out Image plate, out Image plateStroke)
        {
            var go = Child(parent, "Board");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(side, side);

            var plateGo = Child(go, "Plate");
            Stretch(plateGo);
            plate = plateGo.AddComponent<Image>();
            plate.sprite = rounded;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            plate.color = design.BoardPlateFill;
            plate.raycastTarget = false;

            var plateStrokeGo = Child(plateGo, "Stroke");
            Stretch(plateStrokeGo);
            plateStroke = plateStrokeGo.AddComponent<Image>();
            plateStroke.sprite = outline;
            plateStroke.type = Image.Type.Sliced;
            plateStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            plateStroke.color = design.BoardPlateStroke;
            plateStroke.raycastTarget = false;

            // Полотно з півотом у ЛІВОМУ ВЕРХНЬОМУ куті: BoardGeometry рахує центри
            // клітинок саме звідти, і BoardView.CellToLocal теж.
            var canvasGo = Child(go, "Canvas");
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0f, 1f);
            canvasRect.pivot = new Vector2(0f, 1f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(side, side);

            var geometry = BoardGeometry.For(8, 8);
            var blockSize = M(geometry.Block);
            var blockRadius = M(geometry.Block * design.BlockRadiusFraction);
            var ghostSize = M(geometry.Box);

            // Блоки — під привидами: привид має лягати ПОВЕРХ зайнятої клітинки.
            var blocksGo = Child(canvasGo, "Blocks");
            Stretch(blocksGo);
            var blocks = new BlockView[geometry.Width * geometry.Height];
            for (var y = 0; y < geometry.Height; y++)
                for (var x = 0; x < geometry.Width; x++)
                {
                    var position = new Vector2(M(geometry.CenterX(x)), -M(geometry.CenterY(y)));
                    blocks[y * geometry.Width + x] = BuildBlock(blocksGo, $"Block_{x}_{y}", design, rounded,
                        position, blockSize, blockRadius, active: false);
                }

            var ghostsGo = Child(canvasGo, "Ghosts");
            Stretch(ghostsGo);
            var ghosts = new Image[geometry.Width * geometry.Height];
            for (var y = 0; y < geometry.Height; y++)
                for (var x = 0; x < geometry.Width; x++)
                {
                    var ghostGo = Child(ghostsGo, $"Ghost_{x}_{y}");
                    var ghost = ghostGo.AddComponent<Image>();
                    ghost.sprite = rounded;
                    ghost.type = Image.Type.Sliced;
                    ghost.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(geometry.Box * 0.3f));
                    ghost.color = Color.clear;
                    ghost.raycastTarget = false;
                    Place(ghost, new Vector2(M(geometry.CenterX(x)), -M(geometry.CenterY(y))),
                        new Vector2(ghostSize, ghostSize), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
                    ghosts[y * geometry.Width + x] = ghost;
                }

            var floatsGo = Child(canvasGo, "Floats");
            Stretch(floatsGo);
            var floats = new TMP_Text[FloatLabels];
            for (var i = 0; i < FloatLabels; i++)
            {
                var label = Label(floatsGo, $"Float{i}", "+0", design, font,
                    design.FontSizeLineFloatPure, design.TextPrimary, TextAlignmentOptions.Center);
                Place(label, Vector2.zero, new Vector2(M(140f), M(40f)),
                    new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
                label.gameObject.SetActive(false);
                floats[i] = label;
            }

            var audioGo = Child(go, "Feedback");
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var feedback = audioGo.AddComponent<BoardFeedback>();
            Wire(feedback, ("source", audio));

            var board = go.AddComponent<BoardView>();
            Wire(board, ("design", design), ("canvasRect", canvasRect), ("feedback", feedback));
            WireArray(board, "blocks", blocks);
            WireArray(board, "ghosts", ghosts);
            WireArray(board, "floats", floats);
            return board;
        }

        /// <summary>Блок: тіло + глянець зверху. Спільний і для поля, і для лотка.</summary>
        private static BlockView BuildBlock(GameObject parent, string name, DesignSystem design, Sprite rounded,
            Vector2 position, float size, float radius, bool active)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var bodyGo = Child(go, "Body");
            Stretch(bodyGo);
            var body = bodyGo.AddComponent<Image>();
            body.sprite = rounded;
            body.type = Image.Type.Sliced;
            body.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            body.raycastTarget = false;

            var glossGo = Child(bodyGo, "Gloss");
            var glossRect = glossGo.GetComponent<RectTransform>();
            glossRect.anchorMin = new Vector2(0.14f, 0.56f);
            glossRect.anchorMax = new Vector2(0.86f, 0.9f);
            glossRect.offsetMin = glossRect.offsetMax = Vector2.zero;
            var gloss = glossGo.AddComponent<Image>();
            gloss.sprite = rounded;
            gloss.type = Image.Type.Sliced;
            gloss.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius * 0.6f);
            gloss.color = new Color(1f, 1f, 1f, design.BlockGlossAlpha);
            gloss.raycastTarget = false;

            var block = go.AddComponent<BlockView>();
            Wire(block, ("design", design), ("body", body), ("gloss", gloss));
            go.SetActive(active);
            return block;
        }

        // ── Лоток: три комірки з фігурами ──
        private static TrayView BuildTray(GameObject parent, DesignSystem design, Sprite rounded, Sprite outline,
            float top, float height)
        {
            var go = Child(parent, "Tray");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -top - height);
            rect.offsetMax = new Vector2(-SideMargin, -top);

            const int slotCount = 3;
            var gap = M(8f);
            var slotWidth = (M(390f) - SideMargin * 2f - gap * (slotCount - 1)) / slotCount;
            var cellSize = M(BoardGeometry.TrayBox);
            var cellRadius = M(BoardGeometry.TrayBox * design.BlockRadiusFraction);

            var slots = new PieceView[slotCount];
            var fills = new Image[slotCount];
            var strokes = new Image[slotCount];

            for (var i = 0; i < slotCount; i++)
            {
                var slotGo = Child(go, $"Slot{i}");
                var slotRect = slotGo.GetComponent<RectTransform>();
                slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 0.5f);
                slotRect.pivot = new Vector2(0f, 0.5f);
                slotRect.anchoredPosition = new Vector2(i * (slotWidth + gap), 0f);
                slotRect.sizeDelta = new Vector2(slotWidth, height);

                // Заливка ловить вказівник — саме вона й є зоною жесту.
                var fill = slotGo.AddComponent<Image>();
                fill.sprite = rounded;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
                fill.color = design.TraySlotFill;
                fill.raycastTarget = true;
                fills[i] = fill;

                var strokeGo = Child(slotGo, "Stroke");
                Stretch(strokeGo);
                var stroke = strokeGo.AddComponent<Image>();
                stroke.sprite = outline;
                stroke.type = Image.Type.Sliced;
                stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
                stroke.color = design.TraySlotStroke;
                stroke.raycastTarget = false;
                strokes[i] = stroke;

                var pieceGo = Child(slotGo, "Piece");
                Stretch(pieceGo);
                var group = pieceGo.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;

                var cells = new BlockView[5];
                for (var c = 0; c < cells.Length; c++)
                {
                    cells[c] = BuildBlock(pieceGo, $"Cell{c}", design, rounded, Vector2.zero,
                        cellSize, cellRadius, active: false);
                    // Клітинки фігури центруються в комірці: PieceView зміщує їх від центру.
                    var cellRect = (RectTransform)cells[c].transform;
                    cellRect.anchorMin = cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                }

                var piece = slotGo.AddComponent<PieceView>();
                Wire(piece, ("design", design), ("group", group));
                WireArray(piece, "blocks", cells);
                slots[i] = piece;
            }

            var tray = go.AddComponent<TrayView>();
            Wire(tray, ("design", design));
            WireArray(tray, "slots", slots);
            WireArray(tray, "slotFills", fills);
            WireArray(tray, "slotStrokes", strokes);
            return tray;
        }

        // ── Попередження про переповнення + множник ланцюга ──
        private static void BuildOverlays(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite glow, out Image overflowRing, out TMP_Text comboPop)
        {
            // Рант по краю ЕКРАНА — тут 9-slice `glow` доречний: екран і є
            // заокруглений прямокутник, а не куля.
            var ringGo = Child(parent, "OverflowRing");
            Stretch(ringGo);
            overflowRing = ringGo.AddComponent<Image>();
            overflowRing.sprite = glow;
            overflowRing.type = Image.Type.Sliced;
            overflowRing.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(32f);
            overflowRing.raycastTarget = false;
            overflowRing.color = Color.clear;
            ringGo.SetActive(false);

            comboPop = Label(parent, "ComboPop", "×2", design, font,
                design.FontSizeComboPop, design.TextPrimary, TextAlignmentOptions.Center);
            Place(comboPop, new Vector2(0f, 0f), new Vector2(M(300f), M(90f)),
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f));
            comboPop.raycastTarget = false;
            comboPop.gameObject.SetActive(false);
        }

        // ── Картка кінця партії ──
        private static void BuildOver(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite nebula, Shader zoneShader, PictureArtCatalog art,
            out RectTransform overCard, out Image scrim, out GradientImage panel,
            out Image panelStroke, out TMP_Text scoreLabel, out TMP_Text scoreNumber,
            out GradientImage recordChip, out TMP_Text recordChipLabel,
            out RectTransform rewardRow, out TMP_Text rewardNumber, out TMP_Text rewardSuffix,
            out TMP_Text bestLabel, out Button again, out GradientImage againFill,
            out TMP_Text againLabel, out Button menu, out TMP_Text menuLabel,
            out RectTransform confettiRoot, out Image[] confetti,
            out TMP_Text collectedLabel, out PictureView[] thumbs)
        {
            var go = Child(parent, "GameOver");
            Stretch(go);
            overCard = go.GetComponent<RectTransform>();

            scrim = go.AddComponent<Image>();
            scrim.color = design.OverScrim;

            var confettiGo = Child(go, "Confetti");
            Stretch(confettiGo);
            confettiRoot = confettiGo.GetComponent<RectTransform>();

            confetti = new Image[ConfettiPieces];
            for (var i = 0; i < ConfettiPieces; i++)
            {
                var pieceGo = Child(confettiGo, $"Piece{i}");
                var piece = pieceGo.AddComponent<Image>();
                piece.sprite = circle;
                piece.raycastTarget = false;
                Place(piece, Vector2.zero,
                    new Vector2(design.PaintConfettiSize, design.PaintConfettiSize),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                pieceGo.SetActive(false);
                confetti[i] = piece;
            }

            var panelGo = Child(go, "Panel");
            panel = panelGo.AddComponent<GradientImage>();
            panel.sprite = rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(34f));
            // 330 у макеті + ряд галереї зібраного (§11 крок 5): підпис 16 і мініатюри 60.
            Place(panel, Vector2.zero, new Vector2(M(296f), M(420f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var strokeGo = Child(panelGo, "Stroke");
            Stretch(strokeGo);
            panelStroke = strokeGo.AddComponent<Image>();
            panelStroke.sprite = outline;
            panelStroke.type = Image.Type.Sliced;
            panelStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(34f));
            panelStroke.color = design.GlassStroke;
            panelStroke.raycastTarget = false;

            scoreLabel = Label(panelGo, "ScoreLabel", "РАХУНОК", design, font,
                design.FontSizeOverLabel, design.TextDim, TextAlignmentOptions.Center);
            Place(scoreLabel, new Vector2(0f, -M(28f)), new Vector2(M(240f), M(16f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            scoreNumber = Label(panelGo, "ScoreNumber", "0", design, font,
                design.FontSizeOverScore, design.TextPrimary, TextAlignmentOptions.Center);
            Place(scoreNumber, new Vector2(0f, -M(50f)), new Vector2(M(260f), M(60f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var chipGo = Child(panelGo, "RecordChip");
            recordChip = chipGo.AddComponent<GradientImage>();
            recordChip.sprite = rounded;
            recordChip.type = Image.Type.Sliced;
            recordChip.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            recordChip.raycastTarget = false;
            Place(recordChip, new Vector2(0f, -M(120f)), new Vector2(M(174f), M(34f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            recordChipLabel = Label(chipGo, "Label", "НОВИЙ РЕКОРД", design, font,
                design.FontSizeRecordChip, design.RecordChipText, TextAlignmentOptions.Center);
            Stretch(recordChipLabel.gameObject);

            var rewardGo = Child(panelGo, "Reward");
            var rewardFill = rewardGo.AddComponent<Image>();
            rewardFill.sprite = rounded;
            rewardFill.type = Image.Type.Sliced;
            rewardFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
            rewardFill.color = design.GlassFill;
            rewardFill.raycastTarget = false;
            Place(rewardFill, new Vector2(0f, -M(162f)), new Vector2(M(190f), M(42f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rewardRow = rewardGo.GetComponent<RectTransform>();

            var oilGo = Child(rewardGo, "Oil");
            var oil = oilGo.AddComponent<Image>();
            oil.sprite = circle;
            oil.color = design.AccentGold;
            oil.raycastTarget = false;
            Place(oil, new Vector2(M(12f), 0f), new Vector2(M(24f), M(26f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            rewardNumber = Label(rewardGo, "Number", "+0", design, font,
                design.FontSizeRewardNumber, design.RecordChipFrom, TextAlignmentOptions.Left);
            Place(rewardNumber, new Vector2(M(46f), 0f), new Vector2(M(70f), M(22f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            rewardSuffix = Label(rewardGo, "Suffix", "нагорода", design, font,
                design.FontSizeOverLabel, design.TextDim, TextAlignmentOptions.Right);
            Place(rewardSuffix, new Vector2(-M(14f), 0f), new Vector2(M(70f), M(18f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            bestLabel = Label(panelGo, "Best", "Рекорд · 0", design, font,
                design.FontSizeOverBest, design.TextMuted, TextAlignmentOptions.Center);
            Place(bestLabel, new Vector2(0f, -M(214f)), new Vector2(M(260f), M(20f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            // Галерея партії: підпис і до трьох мініатюр зібраних картинок.
            collectedLabel = Label(panelGo, "Collected", "ЗІБРАНО · 1", design, font,
                design.FontSizeOverCollected, design.TextDim, TextAlignmentOptions.Center);
            Place(collectedLabel, new Vector2(0f, -M(242f)), new Vector2(M(260f), M(16f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            const int thumbCount = 3;
            var thumbSide = M(60f);
            var thumbGap = M(12f);
            var thumbsWidth = thumbCount * thumbSide + (thumbCount - 1) * thumbGap;
            thumbs = new PictureView[thumbCount];
            for (var i = 0; i < thumbCount; i++)
            {
                var thumbGo = Child(panelGo, $"Thumb{i}");
                var thumbRect = thumbGo.GetComponent<RectTransform>();
                thumbRect.anchorMin = thumbRect.anchorMax = new Vector2(0.5f, 1f);
                thumbRect.pivot = new Vector2(0.5f, 1f);
                thumbRect.anchoredPosition = new Vector2(-thumbsWidth * 0.5f + thumbSide * 0.5f + i * (thumbSide + thumbGap), -M(262f));
                thumbRect.sizeDelta = new Vector2(thumbSide, thumbSide);
                thumbs[i] = MakePictureView(thumbGo, design, font, rounded, outline, nebula, zoneShader, art,
                    thumbSide, thumbSide, M(48f), withTitle: false, captionHeight: 0f);
                thumbGo.SetActive(false);
            }

            var againGo = Child(panelGo, "Again");
            againFill = againGo.AddComponent<GradientImage>();
            againFill.sprite = rounded;
            againFill.type = Image.Type.Sliced;
            againFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(26f));
            Place(againFill, new Vector2(0f, M(54f)), new Vector2(M(244f), M(52f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            againLabel = Label(againGo, "Label", "Ще раз", design, font,
                design.FontSizeOverPrimary, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(againLabel.gameObject);

            again = againGo.AddComponent<Button>();
            again.targetGraphic = againFill;

            var menuGo = Child(panelGo, "Menu");
            menuLabel = Label(menuGo, "Label", "В меню", design, font,
                design.FontSizeOverSecondary, design.TextMuted, TextAlignmentOptions.Center);
            menuLabel.raycastTarget = true;
            Place(menuLabel, new Vector2(0f, M(20f)), new Vector2(M(140f), M(24f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var menuRect = menuGo.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.5f, 0f);
            menuRect.pivot = new Vector2(0.5f, 0f);
            menuRect.anchoredPosition = Vector2.zero;
            menuRect.sizeDelta = new Vector2(M(140f), M(44f));

            menu = menuGo.AddComponent<Button>();
            menu.targetGraphic = menuLabel;

            go.SetActive(false);
        }
    }
}
