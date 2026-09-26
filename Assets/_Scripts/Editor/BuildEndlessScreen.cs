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
    /// Збирає екран забігу: шапка, картинка W4DarkCanvas, рахунок і рекорд,
    /// поле 8×8 K1Candy (лунки, гало, блоки, блиски), лоток на три фігури, картка перед
    /// забігом і картка фіналу. Меню: Ink Flow → Setup → Build Endless Screen.
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
            var cardGlow = LoadSprite("card-glow");
            var darkCanvas = AssetDatabase.LoadAssetAtPath<Shader>(PictureViewBuilder.DarkCanvasShaderPath);

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"),
                (circle, "circle-soft"), (circleOutline, "circle-outline"),
                (nebula, "nebula"), (glow, "glow"), (retry, "icon-retry"), (cardGlow, "card-glow")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (darkCanvas == null) missing.Add(PictureViewBuilder.DarkCanvasShaderPath);
            K1Sprites.AllPresent(missing);
            A1Sprites.AllPresent(missing);
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

            // Розкладка (§11) — правило RunLayout (Core): картинка по центру, рахунок і рекорд
            // праворуч угорі без фону, лоток при низу, поле — що лишилось. Тут — початкові
            // позиції для сцени-майстерні (полотно 1080×1920 = 390×693 px макета); у грі
            // EndlessScreen.Layout() перераховує їх під фактичний екран.
            var layout = RunLayout.For(390f, 1920f / K, design!.BoardSideMargin);
            var headerHeight = M(RunLayout.HeaderHeight);
            var blockTop = M(layout.PictureTop);
            var blockHeight = M(RunLayout.PictureHeight);
            var pictureWidth = M(layout.PictureWidth);
            var statsWidth = M(layout.StatsWidth);
            var statsHeight = M(36f);
            var boardTop = M(layout.BoardTop);
            var boardSide = M(layout.BoardSide);
            var trayHeight = M(RunLayout.TrayHeight);

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, retry!,
                headerHeight, out var backButton, out var title, out var restartButton);

            var picture = BuildPicture(screenGo, design!, font, rounded!, outline!, nebula!, circle!,
                blockTop, pictureWidth, blockHeight);

            var statsRoot = BuildStats(screenGo, design!, font, blockTop + M(4f), statsWidth, statsHeight,
                out var scoreLabel, out var scoreNumber, out var recordLabel, out var recordNumber);

            var board = BuildBoard(screenGo, design!, font, rounded!, cardGlow!, boardTop, boardSide,
                out var boardPlate, out var boardPlateStroke, out var pulse);

            var tray = BuildTray(screenGo, design!, trayHeight);

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
                out var overFinishPicture, out var overFinishPictureLabel);

            var completion = BuildCompletion(screenGo, design!, font, rounded!, outline!, nebula!, circle!);

            Wire(screen,
                ("design", design!),
                ("backButton", backButton), ("title", title), ("restartButton", restartButton),
                ("scoreLabel", scoreLabel), ("scoreNumber", scoreNumber),
                ("recordLabel", recordLabel), ("recordNumber", recordNumber),
                ("pictureRoot", picture.GetComponent<RectTransform>()), ("statsRoot", statsRoot),
                ("boardRoot", board.GetComponent<RectTransform>()), ("trayRoot", tray.GetComponent<RectTransform>()),
                ("board", board), ("tray", tray), ("picture", picture),
                ("drops", drops), ("completion", completion),
                ("boardPlate", boardPlate), ("boardPlateStroke", boardPlateStroke), ("pulse", pulse),
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

        // ── Рахунок над рекордом, праворуч угорі (§11): лише підпис і число, без фону ──
        private static RectTransform BuildStats(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            float top, float width, float rowHeight,
            out TMP_Text scoreLabel, out TMP_Text scoreNumber, out TMP_Text recordLabel, out TMP_Text recordNumber)
        {
            var go = Child(parent, "Stats");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-SideMargin, -top);
            rect.sizeDelta = new Vector2(width, rowHeight * 2f + M(12f));

            Stat(go, "Score", design, font, width, rowHeight, 0f, "РАХУНОК", "0", design.TextPrimary,
                out scoreLabel, out scoreNumber);
            Stat(go, "Record", design, font, width, rowHeight, -(rowHeight + M(12f)), "РЕКОРД", "0", design.AccentGold,
                out recordLabel, out recordNumber);
            return rect;
        }

        private static void Stat(GameObject parent, string name, DesignSystem design, TMP_FontAsset? font,
            float width, float height, float y, string caption, string value, Color valueColor,
            out TMP_Text label, out TMP_Text number)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);

            label = Label(go, "Label", caption, design, font,
                design.FontSizeStatLabel, design.TextDim, TextAlignmentOptions.Right);
            Place(label, Vector2.zero, new Vector2(width, M(11f)),
                new Vector2(1f, 1f), new Vector2(1f, 1f));
            // Підпис теж не має права лізти на панель картинки: на полотні 353 колонка вужча за «РАХУНОК».
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = design.FontSizeStatLabel;
            label.fontSizeMin = design.FontSizeStatLabel * 0.5f;

            // Розмір шрифту й формат числа рахує ScoreFormat у рантаймі (§11): повний запис,
            // менший шрифт, далі компактний — цифри табличні, ширина не стрибає.
            number = Label(go, "Number", value, design, font,
                design.FontSizeScoreNumber, valueColor, TextAlignmentOptions.Right);
            Place(number, Vector2.zero, new Vector2(width, M(22f)),
                new Vector2(1f, 0f), new Vector2(1f, 0f));
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.overflowMode = TextOverflowModes.Overflow;
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

        // ── Панель картинки по центру: плитка 164 із назвою й полотном 132, під нею лічильник кроків ──
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
                width, M(164f), M(132f), withTitle: true, captionHeight: M(22f), particle: circle);
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

        // ── Поле K1Candy: панель зі спрайта + тінь, 64 лунки, на кожну клітинку гало / блок / блиск, підсвітки, привид ──
        private static BoardView BuildBoard(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite cardGlow, float top, float side,
            out Image plate, out Image plateStroke, out BoardPulse pulse)
        {
            var go = Child(parent, "Board");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(side, side);

            var radius = design.BoardPanelRadius;

            // Тінь під панеллю: чорна ~60 %, зсув униз, розмиття (K1).
            var shadowGo = Child(go, "Shadow");
            Stretch(shadowGo, -design.PanelShadowBlur);
            shadowGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -design.PanelShadowOffset);
            plateStroke = shadowGo.AddComponent<Image>();
            plateStroke.sprite = cardGlow;
            plateStroke.type = Image.Type.Sliced;
            plateStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius + design.PanelShadowBlur);
            plateStroke.color = design.PanelShadow;
            plateStroke.raycastTarget = false;

            // Тривога поля A1Breathe (§11), шар 1: зовнішнє сяйво ПІД панеллю (панель ховає його серцевину).
            var outerCalm = A1Sprites.Layer(go, "PulseOuterCalm", "glow_outer_calm");
            var outerCritical = A1Sprites.Layer(go, "PulseOuterCritical", "glow_outer_critical");

            var plateGo = Child(go, "Plate");
            Stretch(plateGo);
            plate = plateGo.AddComponent<Image>();
            plate.sprite = K1Sprites.Panel != null ? K1Sprites.Panel : rounded;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = K1Sprites.Panel != null ? K1Sprites.PanelMultiplier(radius) : GlassPanel.PixelsPerUnitFor(radius);
            plate.color = Color.white;
            plate.raycastTarget = false;


            // Вузол тряски: розтягнутий на панель, півот ТОЙ САМИЙ, що в панелі, — тож його спокій
            // (0, 0) за побудовою, і BoardView.EndShake повертає його туди, як би тряску не обірвали.
            var shakeGo = Child(go, "Shake");
            Stretch(shakeGo);
            var shakeRect = shakeGo.GetComponent<RectTransform>();
            shakeRect.pivot = rect.pivot;

            // Полотно з півотом у ЛІВОМУ ВЕРХНЬОМУ куті: BoardGeometry рахує центри клітинок звідти.
            // Його localPosition НЕ нуль (−пів панелі, 0) — тому в нього ніхто нічого не пише.
            var canvasGo = Child(shakeGo, "Canvas");
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0f, 1f);
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var geometry = design.BoardGeometryFor(8, 8);
            var count = geometry.Width * geometry.Height;
            var sockets = Layer(canvasGo, "Sockets", "Socket", count, K1Sprites.Socket, Color.white, active: true);
            var glows = Layer(canvasGo, "Glows", "Glow", count, K1Sprites.Glow, Color.white, active: false);
            var blocks = Layer(canvasGo, "Blocks", "Block", count, K1Sprites.Block, Color.white, active: false);
            var overlays = Layer(canvasGo, "Highlights", "Shine", count, K1Sprites.Highlight, Color.white, active: false);
            var highlights = Layer(canvasGo, "LineHighlights", "Line", count, rounded, Color.clear, active: false);
            foreach (var h in highlights)
            {
                h.type = Image.Type.Sliced;
                h.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(geometry.Box * 0.22f));
            }

            // Привид: до п'яти блоків із блиском під спільним CanvasGroup (альфа — валідно / ні).
            var ghostGo = Child(canvasGo, "Ghost");
            Stretch(ghostGo);
            var ghostGroup = ghostGo.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.interactable = false;
            var ghostBlocks = Layer(ghostGo, "Blocks", "Block", 5, K1Sprites.Block, Color.white, active: false);
            var ghostOverlays = Layer(ghostGo, "Shines", "Shine", 5, K1Sprites.Highlight, Color.white, active: false);
            ghostGo.SetActive(false);

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

            // Тривога поля A1Breathe, шари 3–4: внутрішнє сяйво в межах поля над лунками й блоками,
            // тонка рамка над усім. Альфою всіх п'яти керує BoardPulse.
            var innerCalm = A1Sprites.Layer(go, "PulseInnerCalm", "glow_inner_calm");
            var innerCritical = A1Sprites.Layer(go, "PulseInnerCritical", "glow_inner_critical");
            var frameLine = A1Sprites.Layer(go, "PulseFrame", "frame_line");
            pulse = go.AddComponent<BoardPulse>();
            Wire(pulse, ("design", design), ("outerCalm", outerCalm), ("outerCritical", outerCritical),
                ("innerCalm", innerCalm), ("innerCritical", innerCritical), ("frame", frameLine));
            pulse.Apply();

            var audioGo = Child(go, "Feedback");
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var feedback = audioGo.AddComponent<BoardFeedback>();
            Wire(feedback, ("source", audio));

            var board = go.AddComponent<BoardView>();
            Wire(board, ("design", design), ("canvasRect", canvasRect), ("shakeRect", shakeRect), ("feedback", feedback), ("ghostGroup", ghostGroup));
            WireArray(board, "sockets", sockets);
            WireArray(board, "glows", glows);
            WireArray(board, "blocks", blocks);
            WireArray(board, "overlays", overlays);
            WireArray(board, "highlights", highlights);
            WireArray(board, "ghostBlocks", ghostBlocks);
            WireArray(board, "ghostOverlays", ghostOverlays);
            WireArray(board, "floats", floats);
            board.ApplyGeometry();
            return board;
        }

        /// <summary>Шар однакових спрайтів: один контейнер на шар, щоб канвас батчив його одним викликом.</summary>
        private static Image[] Layer(GameObject parent, string name, string prefix, int count, Sprite? sprite, Color color, bool active)
        {
            var root = Child(parent, name);
            Stretch(root);
            var images = new Image[count];
            for (var i = 0; i < count; i++)
            {
                var cellGo = Child(root, $"{prefix}{i}");
                var image = cellGo.AddComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = color;
                image.raycastTarget = false;
                Place(image, Vector2.zero, new Vector2(M(38f), M(38f)), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
                cellGo.SetActive(active);
                images[i] = image;
            }
            return images;
        }

        // ── Лоток K1Candy: три слоти зі спрайта, у кожному до п'яти зменшених блоків ──
        private static TrayView BuildTray(GameObject parent, DesignSystem design, float height)
        {
            // Лоток притиснутий до низу safe area (§11) — на будь-якому екрані; бічне поле — як у панелі поля.
            var margin = M(design.BoardSideMargin);
            var go = Child(parent, "Tray");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(margin, 0f);
            rect.offsetMax = new Vector2(-margin, height);

            const int slotCount = 3;
            var gap = M(9f);
            var slotWidth = (M(390f) - margin * 2f - gap * (slotCount - 1)) / slotCount;
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

                // Спрайт слота — і фон, і зона жесту.
                var slotImage = slotGo.AddComponent<Image>();
                slotImage.sprite = K1Sprites.TraySlot;
                slotImage.type = Image.Type.Sliced;
                slotImage.pixelsPerUnitMultiplier = K1Sprites.SlotMultiplier(design.TraySlotRadius);
                slotImage.color = Color.white;
                slotImage.raycastTarget = true;

                var cellsGo = Child(slotGo, "Cells");
                Stretch(cellsGo);
                var cellsRoot = cellsGo.GetComponent<RectTransform>();
                var glows = Layer(cellsGo, "Glows", "Glow", 5, K1Sprites.Glow, Color.white, active: false);
                var blocks = Layer(cellsGo, "Blocks", "Block", 5, K1Sprites.Block, Color.white, active: false);
                var overlays = Layer(cellsGo, "Shines", "Shine", 5, K1Sprites.Highlight, Color.white, active: false);
                foreach (var layer in new[] { glows, blocks, overlays })
                    foreach (var image in layer)
                    {
                        var r = (RectTransform)image.transform;
                        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); // центри клітинок — від центру слота
                    }

                var piece = slotGo.AddComponent<PieceView>();
                Wire(piece, ("design", design), ("slot", slotImage), ("cellsRoot", cellsRoot), ("group", group));
                WireArray(piece, "glows", glows);
                WireArray(piece, "blocks", blocks);
                WireArray(piece, "overlays", overlays);
                var so = new SerializedObject(piece);
                so.FindProperty("cellStep").floatValue = M(design.BoardGeometryFor(8, 8).Step * design.TrayPieceScale);
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
            // Вертикальний стос із запасом між блоками; коли екран нижчий — EndlessScreen.FitOverCard
            // меншає всю панель, щоб рахунок угорі не зрізало.
            Place(panel, Vector2.zero, new Vector2(M(296f), M(528f)),
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
            Place(scoreLabel, new Vector2(0f, -M(18f)), new Vector2(M(240f), M(16f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            scoreNumber = Label(panelGo, "ScoreNumber", "0", design, font,
                design.FontSizeOverScore, design.TextPrimary, TextAlignmentOptions.Center);
            Place(scoreNumber, new Vector2(0f, -M(36f)), new Vector2(M(260f), M(62f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var chipGo = Child(panelGo, "RecordChip");
            recordChip = chipGo.AddComponent<GradientImage>();
            recordChip.sprite = rounded;
            recordChip.type = Image.Type.Sliced;
            recordChip.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            recordChip.raycastTarget = false;
            Place(recordChip, new Vector2(0f, -M(104f)), new Vector2(M(174f), M(34f)),
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
            Place(rewardFill, new Vector2(0f, -M(146f)), new Vector2(M(190f), M(42f)),
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
            Place(bestLabel, new Vector2(0f, -M(196f)), new Vector2(M(270f), M(20f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            bestLabel.enableAutoSizing = true;
            bestLabel.fontSizeMax = design.FontSizeOverBest;
            bestLabel.fontSizeMin = design.FontSizeOverBest * 0.7f;

            // Галерея партії: підпис і до трьох мініатюр зібраних картинок.
            collectedLabel = Label(panelGo, "Collected", "ЗІБРАНО · 1", design, font,
                design.FontSizeOverCollected, design.TextDim, TextAlignmentOptions.Center);
            Place(collectedLabel, new Vector2(0f, -M(224f)), new Vector2(M(260f), M(16f)),
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
                thumbRect.anchoredPosition = new Vector2(-thumbsWidth * 0.5f + thumbSide * 0.5f + i * (thumbSide + thumbGap), -M(244f));
                thumbRect.sizeDelta = new Vector2(thumbSide, thumbSide);
                thumbs[i] = PictureViewBuilder.MakePictureView(thumbGo, design, font, rounded, outline, nebula,
                    thumbSide, thumbSide, M(48f), withTitle: false, captionHeight: 0f, particle: circle);
                thumbGo.SetActive(false);
            }

            // Чипи знизу вгору: «В меню», «Ще раз», ряд роликів («Продовжити» до фіналу / «Подвоїти»
            // у фіналі), «Домалювати одразу · N нафти» (§13) — кожен у своєму ряду, текст в один рядок.
            var chipH = M(40f);
            continueButton = OverChip(panelGo, "Continue", design, font, rounded, outline, "Продовжити за ролик",
                new Vector2(0f, M(120f)), new Vector2(M(244f), chipH), out continueLabel);
            doubleButton = OverChip(panelGo, "Double", design, font, rounded, outline, "Подвоїти нафту · ролик",
                new Vector2(0f, M(120f)), new Vector2(M(244f), chipH), out doubleLabel);
            finishButton = OverChip(panelGo, "FinishPicture", design, font, rounded, outline, "Домалювати одразу · 40 нафти",
                new Vector2(0f, M(168f)), new Vector2(M(244f), chipH), out finishLabel);
            // В один рядок на будь-якому екрані: на 750×1334 картка меншає, і напис стискається шрифтом, а не крапками.
            finishLabel.textWrappingMode = TextWrappingModes.NoWrap;
            finishLabel.overflowMode = TextOverflowModes.Overflow;
            finishLabel.enableAutoSizing = true;
            finishLabel.fontSizeMax = design.FontSizeOverSecondary;
            finishLabel.fontSizeMin = design.FontSizeOverSecondary * 0.6f;

            var againGo = Child(panelGo, "Again");
            againFill = againGo.AddComponent<GradientImage>();
            againFill.sprite = rounded;
            againFill.type = Image.Type.Sliced;
            againFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(26f));
            Place(againFill, new Vector2(0f, M(62f)), new Vector2(M(244f), M(52f)),
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
            Place(menuLabel, new Vector2(0f, M(22f)), new Vector2(M(140f), M(24f)),
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
