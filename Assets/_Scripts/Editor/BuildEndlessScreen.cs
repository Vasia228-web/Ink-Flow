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
    /// Збирає екран забігу: шапка, піксельна картинка, капсули рахунку й рекорду,
    /// поле 8×8 із блоків і привидів, лоток на три фігури, картка перед забігом і картка фіналу.
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
        private const string BlobShaderPath = "Assets/_Shaders/InkFlowInkBlob.shader";

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
            var blobShader = AssetDatabase.LoadAssetAtPath<Shader>(BlobShaderPath);
            var quad = LoadSprite("white-quad");

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
            if (blobShader == null) missing.Add(BlobShaderPath);
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
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

            // Верхній блок (§11): картинка — головний фокус, по центру, в рамці рідкості;
            // рахунок і рекорд — праворуч угорі, компактно, одна капсула над другою.
            // Місце під шестерню (§15, Фаза 5) — крайнє праворуч у шапці.
            var headerHeight = M(40f);
            var blockTop = headerHeight + M(10f);
            var blockHeight = M(174f);
            var pictureWidth = M(196f);
            var statsWidth = M(92f);
            var statsHeight = M(36f);
            var boardTop = blockTop + blockHeight + M(8f);
            var boardSide = M(BoardGeometry.Canvas);
            var trayTop = boardTop + boardSide + M(10f);
            var trayHeight = M(86f);

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, retry!,
                headerHeight, out var backButton, out var title, out var restartButton);

            var picture = BuildPicture(screenGo, design!, font, rounded!, outline!, nebula!, circle!,
                blockTop, pictureWidth, blockHeight);

            BuildCapsules(screenGo, design!, font, rounded!, outline!, nebula!,
                blockTop + M(6f), statsWidth, statsHeight,
                out var scoreCapsule, out var scoreStroke, out var scoreGlow,
                out var scoreLabel, out var scoreNumber,
                out var recordCapsule, out var recordStroke, out var recordGlow,
                out var recordLabel, out var recordNumber);

            var board = BuildBoard(screenGo, design!, font, rounded!, outline!, boardTop, boardSide,
                out var boardPlate, out var boardPlateStroke);

            var tray = BuildTray(screenGo, design!, quad!, blobShader!, trayTop, trayHeight);

            // Краплі летять поверх поля й картинки, під картками.
            var dropsGo = Child(screenGo, "Drops");
            Stretch(dropsGo);
            var drops = dropsGo.AddComponent<DropFlock>();
            Wire(drops, ("design", design!), ("dropSprite", circle!));

            BuildOverlays(screenGo, design!, font, out var comboPop);

            BuildIntro(screenGo, design!, font, rounded!, outline!, nebula!, circle!,
                out var introCard, out var introGroup, out var introScrim, out var introPanel,
                out var introPanelStroke, out var introPicture, out var introKicker, out var introName,
                out var introRarity, out var introHint, out var introButton);

            BuildOver(screenGo, design!, font, rounded!, outline!, circle!, nebula!,
                out var overCard, out var overScrim, out var overPanel, out var overPanelStroke,
                out var overScoreLabel, out var overScoreNumber,
                out var recordChip, out var recordChipLabel,
                out var rewardRow, out var rewardNumber, out var rewardSuffix,
                out var overBestLabel, out var overAgain, out var overAgainFill,
                out var overAgainLabel, out var overMenu, out var overMenuLabel,
                out var confettiRoot, out var confetti,
                out var overCollectedLabel, out var overThumbs,
                out var overContinue, out var overContinueLabel,
                out var overDouble, out var overDoubleLabel,
                out var overRescue, out var overRescueLabel,
                out var overFinishPicture, out var overFinishPictureLabel);

            var completion = BuildCompletion(screenGo, design!, font, rounded!, outline!, nebula!, circle!);

            Wire(screen,
                ("design", design!),
                ("backButton", backButton), ("title", title), ("restartButton", restartButton),
                ("scoreCapsule", scoreCapsule), ("scoreCapsuleStroke", scoreStroke),
                ("scoreCapsuleGlow", scoreGlow), ("scoreLabel", scoreLabel),
                ("scoreNumber", scoreNumber),
                ("recordCapsule", recordCapsule), ("recordCapsuleStroke", recordStroke),
                ("recordCapsuleGlow", recordGlow), ("recordLabel", recordLabel),
                ("recordNumber", recordNumber),
                ("board", board), ("tray", tray), ("picture", picture),
                ("drops", drops), ("completion", completion),
                ("boardPlate", boardPlate), ("boardPlateStroke", boardPlateStroke),
                ("comboPop", comboPop),
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
                ("overContinue", overContinue), ("overContinueLabel", overContinueLabel),
                ("overDouble", overDouble), ("overDoubleLabel", overDoubleLabel),
                ("overRescue", overRescue), ("overRescueLabel", overRescueLabel),
                ("overFinishPicture", overFinishPicture), ("overFinishPictureLabel", overFinishPictureLabel),
                ("introCard", introCard), ("introGroup", introGroup), ("introScrim", introScrim),
                ("introPanel", introPanel), ("introPanelStroke", introPanelStroke),
                ("introPicture", introPicture), ("introKicker", introKicker), ("introName", introName),
                ("introRarity", introRarity), ("introHint", introHint), ("introButton", introButton));
            WireArray(screen, "confetti", confetti);
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

        // ── Дві капсули: РАХУНОК над РЕКОРДОМ, праворуч угорі (§11) ──
        private static void BuildCapsules(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, float top, float width, float height,
            out Image scoreCapsule, out Image scoreStroke, out Image scoreGlow,
            out TMP_Text scoreLabel, out TMP_Text scoreNumber,
            out Image recordCapsule, out Image recordStroke, out Image recordGlow,
            out TMP_Text recordLabel, out TMP_Text recordNumber)
        {
            var gap = M(6f);
            var go = Child(parent, "Stats");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-SideMargin, -top);
            rect.sizeDelta = new Vector2(width, height * 2f + gap);

            Capsule(go, "Score", design, font, rounded, outline, nebula,
                new Vector2(0.5f, 1f), width, height, "РАХУНОК", "0", design.TextPrimary,
                out scoreCapsule, out scoreStroke, out scoreGlow, out scoreLabel, out scoreNumber);

            Capsule(go, "Record", design, font, rounded, outline, nebula,
                new Vector2(0.5f, 0f), width, height, "РЕКОРД", "0", design.AccentGold,
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
            Place(label, new Vector2(0f, -M(3f)), new Vector2(width, M(11f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            number = Label(fillGo, "Number", value, design, font,
                design.FontSizeScoreNumber, valueColor, TextAlignmentOptions.Center);
            Place(number, new Vector2(0f, M(2f)), new Vector2(width, M(20f)),
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

        // ── Картинка по центру: плитка 196×150 із пікселями + назва, лічильник пікселів, «СПРОБ · N» ──
        private static PictureView BuildPicture(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Sprite circle, float top, float width, float height)
        {
            var go = Child(parent, "Picture");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width, height);
            return PictureViewBuilder.MakePictureView(go, design, font, rounded, outline, nebula,
                width, M(150f), M(118f), withTitle: true, captionHeight: M(22f), particle: circle);
        }

        // ── Картка перед забігом: «цього забігу — така картинка» ──
        private static void BuildIntro(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Sprite circle,
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
            picture = PictureViewBuilder.MakePictureView(pictureGo, design, font, rounded, outline, nebula,
                M(200f), M(200f), M(172f), withTitle: false, captionHeight: 0f, particle: circle);

            name = Label(panelGo, "Name", "КИТ", design, font,
                design.FontSizeIntroName, design.TextPrimary, TextAlignmentOptions.Center);
            Place(name, new Vector2(0f, -M(256f)), new Vector2(M(270f), M(30f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            rarity = Label(panelGo, "Rarity", "ЗВИЧАЙНА · 3 КОЛЬОРИ", design, font,
                design.FontSizeIntroRarity, design.TextMuted, TextAlignmentOptions.Center);
            Place(rarity, new Vector2(0f, -M(290f)), new Vector2(M(270f), M(18f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            hint = Label(panelGo, "Hint", "Тапни, щоб грати", design, font,
                design.FontSizeIntroHint, design.TextMuted, TextAlignmentOptions.Center);
            Place(hint, new Vector2(0f, M(18f)), new Vector2(M(270f), M(20f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            go.SetActive(false);
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

        // ── Лоток: три фігури в повітрі (§11), кожна — один квад із шейдером краплі ──
        private static TrayView BuildTray(GameObject parent, DesignSystem design, Sprite quad, Shader blobShader,
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
            var slots = new PieceView[slotCount];

            for (var i = 0; i < slotCount; i++)
            {
                var slotGo = Child(go, $"Slot{i}");
                var slotRect = slotGo.GetComponent<RectTransform>();
                slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 0.5f);
                slotRect.pivot = new Vector2(0f, 0.5f);
                slotRect.anchoredPosition = new Vector2(i * (slotWidth + gap), 0f);
                slotRect.sizeDelta = new Vector2(slotWidth, height);
                var group = slotGo.AddComponent<CanvasGroup>();

                // Квад фігури — і графіка, і зона жесту: без підкладки більше нема чому ловити палець.
                var blobGo = Child(slotGo, "Blob");
                Stretch(blobGo);
                var blob = blobGo.AddComponent<Image>();
                blob.sprite = quad;
                blob.type = Image.Type.Simple;
                blob.color = Color.white;
                blob.raycastTarget = true;
                blobGo.SetActive(false); // PieceView вмикає, коли є що показати

                var piece = slotGo.AddComponent<PieceView>();
                Wire(piece, ("design", design), ("blob", blob), ("blobShader", blobShader), ("group", group));
                var so = new SerializedObject(piece);
                so.FindProperty("cellStep").floatValue = M(BoardGeometry.TrayStep);
                so.ApplyModifiedPropertiesWithoutUndo();
                slots[i] = piece;
            }

            var tray = go.AddComponent<TrayView>();
            Wire(tray, ("design", design));
            WireArray(tray, "slots", slots);
            return tray;
        }

        // ── Множник ланцюга (червоного рантa «мало місця» більше немає — §11) ──
        private static void BuildOverlays(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            out TMP_Text comboPop)
        {
            comboPop = Label(parent, "ComboPop", "×2", design, font,
                design.FontSizeComboPop, design.TextPrimary, TextAlignmentOptions.Center);
            Place(comboPop, new Vector2(0f, 0f), new Vector2(M(300f), M(90f)),
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f));
            comboPop.raycastTarget = false;
            comboPop.gameObject.SetActive(false);
        }

        // ── Картка кінця партії ──
        private static void BuildOver(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite nebula,
            out RectTransform overCard, out Image scrim, out GradientImage panel,
            out Image panelStroke, out TMP_Text scoreLabel, out TMP_Text scoreNumber,
            out GradientImage recordChip, out TMP_Text recordChipLabel,
            out RectTransform rewardRow, out TMP_Text rewardNumber, out TMP_Text rewardSuffix,
            out TMP_Text bestLabel, out Button again, out GradientImage againFill,
            out TMP_Text againLabel, out Button menu, out TMP_Text menuLabel,
            out RectTransform confettiRoot, out Image[] confetti,
            out TMP_Text collectedLabel, out PictureView[] thumbs,
            out Button continueButton, out TMP_Text continueLabel,
            out Button doubleButton, out TMP_Text doubleLabel,
            out Button rescueButton, out TMP_Text rescueLabel,
            out Button finishButton, out TMP_Text finishLabel)
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
            // 330 у макеті + ряд галереї зібраного (§11 крок 5) + два ряди чипів §9.
            Place(panel, Vector2.zero, new Vector2(M(296f), M(530f)),
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
                thumbs[i] = PictureViewBuilder.MakePictureView(thumbGo, design, font, rounded, outline, nebula,
                    thumbSide, thumbSide, M(48f), withTitle: false, captionHeight: 0f, particle: circle);
                thumbGo.SetActive(false);
            }

            // Чипи §9: ряд 1 — «Подвоїти» | «Повернути картинку» (або «Продовжити» до фіналу),
            // ряд 2 — «Домалювати одразу» (донат). Скляні, з підписом; ховаються, коли не до чого.
            var chipH = M(40f);
            var chipGap = M(8f);
            var chipW = (M(244f) - chipGap) * 0.5f;
            continueButton = OverChip(panelGo, "Continue", design, font, rounded, outline, "Продовжити за ролик",
                new Vector2(0f, M(116f)), new Vector2(M(244f), chipH), out continueLabel);
            doubleButton = OverChip(panelGo, "Double", design, font, rounded, outline, "Подвоїти нафту · ролик",
                new Vector2(-(chipW + chipGap) * 0.5f, M(116f)), new Vector2(chipW, chipH), out doubleLabel);
            rescueButton = OverChip(panelGo, "Rescue", design, font, rounded, outline, "Повернути картинку · ролик",
                new Vector2((chipW + chipGap) * 0.5f, M(116f)), new Vector2(chipW, chipH), out rescueLabel);
            finishButton = OverChip(panelGo, "FinishPicture", design, font, rounded, outline, "Домалювати одразу",
                new Vector2(0f, M(162f)), new Vector2(M(244f), chipH), out finishLabel);

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

        // ── Картка завершеної картинки (§8): знімок-фон, картинка в рамці, свайп, підтвердження ──
        private static CompletionCard BuildCompletion(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Sprite circle)
        {
            var go = Child(parent, "Completion");
            Stretch(go);

            var backdropGo = Child(go, "Backdrop");
            Stretch(backdropGo);
            var backdrop = backdropGo.AddComponent<RawImage>();
            backdrop.raycastTarget = false;
            backdrop.color = design.CompletionBackdropTint;

            var scrimGo = Child(go, "Scrim");
            Stretch(scrimGo);
            var scrim = scrimGo.AddComponent<Image>();
            scrim.color = design.CompletionScrim;
            scrim.raycastTarget = true;

            var cardGo = Child(go, "Card");
            var card = cardGo.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(0f, M(30f));
            card.sizeDelta = new Vector2(M(300f), M(400f));

            var kicker = Label(cardGo, "Kicker", "КАРТИНКУ ДОМАЛЬОВАНО", design, font,
                design.FontSizeIntroKicker, design.TextDim, TextAlignmentOptions.Center);
            Place(kicker, new Vector2(0f, 0f), new Vector2(M(300f), M(16f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var pictureGo = Child(cardGo, "Picture");
            var pictureRect = pictureGo.GetComponent<RectTransform>();
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 1f);
            pictureRect.pivot = new Vector2(0.5f, 1f);
            pictureRect.anchoredPosition = new Vector2(0f, -M(28f));
            pictureRect.sizeDelta = new Vector2(M(260f), M(260f));
            var picture = PictureViewBuilder.MakePictureView(pictureGo, design, font, rounded, outline, nebula,
                M(260f), M(260f), M(222f), withTitle: false, captionHeight: 0f, particle: circle);

            var title = Label(cardGo, "Title", "КИТ", design, font,
                design.FontSizeCompletionTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -M(300f)), new Vector2(M(300f), M(30f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var rarity = Label(cardGo, "Rarity", "ЗВИЧАЙНА · ТВАРИНИ", design, font,
                design.FontSizeCompletionRarity, design.TextMuted, TextAlignmentOptions.Center);
            Place(rarity, new Vector2(0f, -M(334f)), new Vector2(M(300f), M(18f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var hintLeft = Label(go, "HintLeft", "‹ ВИКИНУТИ", design, font,
                design.FontSizeCompletionHint, design.TextMuted, TextAlignmentOptions.Left);
            Place(hintLeft, new Vector2(M(24f), M(120f)), new Vector2(M(160f), M(20f)),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            var hintRight = Label(go, "HintRight", "У КОЛЕКЦІЮ ›", design, font,
                design.FontSizeCompletionHint, design.TextMuted, TextAlignmentOptions.Right);
            Place(hintRight, new Vector2(-M(24f), M(120f)), new Vector2(M(160f), M(20f)),
                new Vector2(1f, 0f), new Vector2(1f, 0f));

            // Підтвердження для епічної й вище (§8): маленька скляна панель із двома кнопками.
            var confirmGo = Child(go, "Confirm");
            var confirmFill = confirmGo.AddComponent<Image>();
            confirmFill.sprite = rounded;
            confirmFill.type = Image.Type.Sliced;
            confirmFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            confirmFill.color = design.OverCardFrom;
            Place(confirmFill, new Vector2(0f, -M(150f)), new Vector2(M(300f), M(128f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var confirm = confirmGo.GetComponent<RectTransform>();

            var confirmStrokeGo = Child(confirmGo, "Stroke");
            Stretch(confirmStrokeGo);
            var confirmStroke = confirmStrokeGo.AddComponent<Image>();
            confirmStroke.sprite = outline;
            confirmStroke.type = Image.Type.Sliced;
            confirmStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            confirmStroke.color = design.GlassStroke;
            confirmStroke.raycastTarget = false;

            var confirmText = Label(confirmGo, "Text", "Викинути епічну картинку?", design, font,
                design.FontSizeOverSecondary, design.TextPrimary, TextAlignmentOptions.Center);
            Place(confirmText, new Vector2(0f, -M(16f)), new Vector2(M(270f), M(44f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            confirmText.textWrappingMode = TextWrappingModes.Normal;

            var keep = OverChip(confirmGo, "Keep", design, font, rounded, outline, "Лишити",
                new Vector2(-M(66f), M(14f)), new Vector2(M(120f), M(40f)), out var keepLabel);
            keep.gameObject.SetActive(true);
            var drop = OverChip(confirmGo, "Drop", design, font, rounded, outline, "Викинути",
                new Vector2(M(66f), M(14f)), new Vector2(M(120f), M(40f)), out var dropLabel);
            drop.gameObject.SetActive(true);
            confirmGo.SetActive(false);

            var view = go.AddComponent<CompletionCard>();
            Wire(view, ("design", design), ("backdrop", backdrop), ("scrim", scrim), ("card", card),
                ("picture", picture), ("kicker", kicker), ("title", title), ("rarity", rarity),
                ("hintLeft", hintLeft), ("hintRight", hintRight), ("confirm", confirm),
                ("confirmFill", confirmFill), ("confirmStroke", confirmStroke), ("confirmText", confirmText),
                ("keepButton", keep), ("keepLabel", keepLabel), ("dropButton", drop), ("dropLabel", dropLabel));
            view.Apply();
            go.SetActive(false);
            return view;
        }

        /// <summary>Скляний чип-кнопка картки фіналу, якорем знизу панелі.</summary>
        private static Button OverChip(GameObject parent, string name, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, string caption, Vector2 position, Vector2 size, out TMP_Text label)
        {
            var go = Child(parent, name);
            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(14f));
            fill.color = design.GlassFill;
            Place(fill, position, size, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(14f));
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            label = Label(go, "Label", caption, design, font,
                design.FontSizeOverSecondary, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            go.SetActive(false);
            return button;
        }
    }
}
