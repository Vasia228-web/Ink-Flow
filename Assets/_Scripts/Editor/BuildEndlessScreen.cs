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
    /// Збирає екран забігу U1Glaze (S02Run): шапка «‹ · НЕСКІНЧЕННИЙ · шестерня» (icon_button), ліворуч картинка
    /// W4DarkCanvas з гало рідкості, праворуч панелі РАХУНОК / РЕКОРД (panel_card), поле 8×8 K1Candy (лунки, гало,
    /// блоки, блиски), лоток на три фігури, і накладки-діалоги на затемненні: картка перед забігом, картинку
    /// домальовано (+ підтвердження для Епічної+) і програш у дві фази. Меню: Ink Flow → Setup → Build Endless Screen.
    ///
    /// Шапка й накладки — фабрики <see cref="U1"/>; позиції блоків екрана ставить <see cref="EndlessScreen.Layout"/>
    /// за правилом <see cref="RunLayout"/> (Core), тут — лише початкові для сцени-майстерні. Числа — px макета × K
    /// (K = DesignSystem.MockupToReference, 1080/402 ≈ 2.687). Геометрію поля й лотка дає <see cref="BoardGeometry"/>:
    /// індекс блока = y × 8 + x, і саме так їх читає <see cref="BoardView"/>.
    /// </summary>
    public static class BuildEndlessScreen
    {
        private const string ScenePath = "Assets/Scenes/Endless.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = DesignSystem.MockupToReference;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        /// <summary>Скільки крапельок летить за новий рекорд.</summary>
        private const int ConfettiPieces = 18;

        /// <summary>Скільки написів «+фарба» може летіти одночасно.</summary>
        private const int FloatLabels = 4;

        /// <summary>Зразків кольорів на картці перед забігом: родин у картинці 4–6 (PictureLibraryTests).</summary>
        private const int SwatchCount = 6;

        /// <summary>Мініатюр зібраного у фіналі.</summary>
        private const int ThumbCount = 3;

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
            var circle = LoadSprite("circle-soft");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var cardGlow = LoadSprite("card-glow");
            var darkCanvas = AssetDatabase.LoadAssetAtPath<Shader>(PictureViewBuilder.DarkCanvasShaderPath);

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[] { (rounded, "rounded-rect"), (circle, "circle-soft"), (cardGlow, "card-glow") })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (darkCanvas == null) missing.Add(PictureViewBuilder.DarkCanvasShaderPath);
            K1Sprites.AllPresent(missing);
            if (U1GlazeSprites.Load("dialog") == null)
                missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
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

            // Розкладка (§11) — правило RunLayout (Core): шапка, картинка ліворуч, панелі рахунку праворуч, поле,
            // лоток при низу. Тут — початкові позиції для сцени-майстерні (1080×1920 = 402×715 px макета);
            // у грі EndlessScreen.Layout() перераховує їх під фактичний екран.
            var layout = RunLayout.For(DesignSystem.MockupWidth, 1920f / K, design!.BoardSideMargin,
                design!.RunCanvasMaxSide, design!.RarityHaloReserve);

            BuildHeader(screenGo, design!, out var backButton, out var title, out var settingsButton);

            var picture = BuildPicture(screenGo, design!, layout);

            var statsRoot = BuildStats(screenGo, design!, layout,
                out var scoreLabel, out var scoreNumber, out var recordLabel, out var recordNumber);

            var board = BuildBoard(screenGo, design!, font, rounded!, cardGlow!, M(layout.BoardTop), M(layout.BoardSide),
                out var boardPlate, out var boardPlateStroke, out var pulse);

            var tray = BuildTray(screenGo, design!, M(RunLayout.TrayHeight));

            // Краплі летять поверх поля й картинки, під накладками.
            var dropsGo = Child(screenGo, "Drops");
            Stretch(dropsGo);
            var drops = dropsGo.AddComponent<DropFlock>();
            Wire(drops, ("design", design!), ("dropSprite", circle!));

            var comboPop = BuildComboPop(screenGo, design!, font);

            var intro = BuildIntro(screenGo, design!);
            var over = BuildOver(screenGo, design!, circle!);
            var completion = BuildCompletion(screenGo, design!);

            Wire(screen,
                ("design", design!),
                ("backButton", backButton), ("title", title), ("settingsButton", settingsButton),
                ("scoreLabel", scoreLabel), ("scoreNumber", scoreNumber),
                ("recordLabel", recordLabel), ("recordNumber", recordNumber),
                ("pictureRoot", picture.GetComponent<RectTransform>()), ("statsRoot", statsRoot),
                ("boardRoot", board.GetComponent<RectTransform>()), ("trayRoot", tray.GetComponent<RectTransform>()),
                ("board", board), ("tray", tray), ("picture", picture),
                ("drops", drops), ("completion", completion),
                ("boardPlate", boardPlate), ("boardPlateStroke", boardPlateStroke), ("pulse", pulse),
                ("comboPop", comboPop),
                ("introDialog", intro.Dialog), ("introButton", intro.Button), ("introKicker", intro.Kicker),
                ("introFrame", intro.Frame), ("introPicture", intro.Picture), ("introName", intro.Name),
                ("introBadge", intro.Badge), ("introColors", intro.Colors), ("introPlay", intro.Play),
                ("overDialog", over.Dialog), ("overFit", over.Fit), ("overPanel", over.Panel),
                ("overKicker", over.Kicker), ("overScore", over.Score),
                ("overRecordRow", over.RecordRow), ("overRecordValue", over.RecordValue), ("overRecordBadge", over.RecordBadge),
                ("overReward", over.Reward), ("overRewardNumber", over.RewardNumber), ("overDouble", over.Double),
                ("overPicture", over.Picture), ("overPictureFrame", over.PictureFrame), ("overPictureThumb", over.PictureThumb),
                ("overPictureTitle", over.PictureTitle), ("overPictureNote", over.PictureNote),
                ("overFinish", over.Finish), ("overFinishPrice", over.FinishPrice),
                ("overContinueLabel", over.ContinueLabel), ("overContinueAd", over.ContinueAd),
                ("overContinueOil", over.ContinueOil), ("overContinuePrice", over.ContinuePrice),
                ("overCollected", over.Collected), ("overCollectedLabel", over.CollectedLabel),
                ("overAgain", over.Again), ("overEnd", over.End), ("overMenu", over.Menu),
                ("confettiRoot", over.ConfettiRoot));
            WireArray(screen, "introSwatches", intro.Swatches);
            WireArray(screen, "overThumbFrames", over.ThumbFrames);
            WireArray(screen, "overThumbs", over.Thumbs);
            WireArray(screen, "confetti", over.Confetti);

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

        // ── Шапка S02Run_01: ‹ · НЕСКІНЧЕННИЙ · шестерня (icon_button 46, заголовок ui-label) ──
        private static void BuildHeader(GameObject parent, DesignSystem design,
            out Button backButton, out TMP_Text title, out Button settingsButton)
        {
            var go = Child(parent, "Header");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(M(RunLayout.SideMargin), -M(RunLayout.HeaderHeight));
            rect.offsetMax = new Vector2(-M(RunLayout.SideMargin), 0f);

            backButton = U1.IconButton(go, "Back", design, "icon_back", new Vector2(0f, 0.5f), Vector2.zero);
            // §15: шестерня крайня праворуч; «Заново» живе в налаштуваннях, а не в шапці.
            settingsButton = U1.IconButton(go, "Settings", design, "icon_gear", new Vector2(1f, 0.5f), Vector2.zero);

            title = U1.Text(go, "Title", "НЕСКІНЧЕННИЙ", design, U1.Role.Label, design.InkMuted);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            var side = design.IconButtonSize + M(8f);
            titleRect.offsetMin = new Vector2(side, 0f);
            titleRect.offsetMax = new Vector2(-side, 0f);
        }

        // ── Картинка ліворуч: лише полотно (без рамки, відсотка й назви — рішення Сесії 5), рідкість — гало з-під нього ──
        private static PictureView BuildPicture(GameObject parent, DesignSystem design, RunLayout layout)
        {
            var go = Child(parent, "Picture");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(M(layout.PictureLeft), -M(layout.PictureTop));
            rect.sizeDelta = new Vector2(M(layout.PictureBlock), M(layout.PictureBlock));
            return PictureViewBuilder.MakeRunPicture(go, design, M(layout.CanvasSide));
        }

        // ── Рахунок і рекорд S02Run_01: дві панелі panel_card стовпчиком, підпис ui-label і число h1 ліворуч ──
        private static RectTransform BuildStats(GameObject parent, DesignSystem design, RunLayout layout,
            out TMP_Text scoreLabel, out TMP_Text scoreNumber, out TMP_Text recordLabel, out TMP_Text recordNumber)
        {
            var go = Child(parent, "Stats");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(M(layout.StatsLeft), -M(layout.StatsTop));
            rect.sizeDelta = new Vector2(M(layout.StatsWidth), M(layout.StatsHeight));
            var column = go.AddComponent<VerticalLayoutGroup>();
            column.spacing = M(RunLayout.StatsRowGap);
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = column.childForceExpandHeight = true;

            Stat(go, "Score", "РАХУНОК", design, design.InkText, out scoreLabel, out scoreNumber);
            Stat(go, "Record", "РЕКОРД", design, design.Record, out recordLabel, out recordNumber);
            return rect;
        }

        private static void Stat(GameObject parent, string name, string caption, DesignSystem design, Color valueColor,
            out TMP_Text label, out TMP_Text number)
        {
            var panel = U1.Panel(parent, name, "panel_card");
            // Висота панелі — рівна частка стовпчика, не за вмістом: інакше різні кеглі чисел робили б панелі різними.
            var share = panel.AddComponent<LayoutElement>();
            share.minHeight = share.preferredHeight = 0f;
            share.flexibleHeight = 1f;
            var col = panel.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)M(RunLayout.StatPadLeft), (int)M(RunLayout.StatPadRight), (int)M(4f), (int)M(4f));
            col.childAlignment = TextAnchor.MiddleLeft;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            label = U1.Text(panel, "Label", caption, design, U1.Role.Label, design.InkMuted, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            // Кегль і формат числа рахує ScoreFormat у рантаймі (§11): повний запис, менший шрифт, далі компактний —
            // цифри табличні, ширина не стрибає; найбільший кегль — h1, але не вищий за висоту панелі (SE).
            number = U1.Text(panel, "Number", "0", design, U1.Role.H1, valueColor, TextAlignmentOptions.Left);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.overflowMode = TextOverflowModes.Overflow;
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
            var outerCalm = A1Layers.Layer(go, "PulseOuterCalm");
            var outerCritical = A1Layers.Layer(go, "PulseOuterCritical");

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
            var innerCalm = A1Layers.Layer(go, "PulseInnerCalm");
            var innerCritical = A1Layers.Layer(go, "PulseInnerCritical");
            var frameLine = A1Layers.Layer(go, "PulseFrame");
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
            var slotWidth = (M(DesignSystem.MockupWidth) - margin * 2f - gap * (slotCount - 1)) / slotCount;
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

        // ── Множник ланцюга по центру екрана ──
        private static TMP_Text BuildComboPop(GameObject parent, DesignSystem design, TMP_FontAsset? font)
        {
            var comboPop = Label(parent, "ComboPop", "×2", design, font,
                design.FontSizeComboPop, design.InkText, TextAlignmentOptions.Center);
            comboPop.font = design.FontBlack;
            comboPop.fontStyle = FontStyles.Normal;
            Place(comboPop, Vector2.zero, new Vector2(M(300f), M(90f)),
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f));
            comboPop.raycastTarget = false;
            comboPop.gameObject.SetActive(false);
            return comboPop;
        }

        /// <summary>Рядок по центру стовпчика діалогу (бейдж, зразки): вміст шириною за собою, висота — px макета.</summary>
        private static GameObject CenterRow(GameObject parent, string name, float heightPx, float spacingPx)
        {
            var row = Child(parent, name);
            var group = row.AddComponent<HorizontalLayoutGroup>();
            group.spacing = M(spacingPx);
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            U1.Size(row, 0f, heightPx);
            return row;
        }

        /// <summary>Кнопка на всю ширину стовпчика: висота 56 (велика) або 46 (мала).</summary>
        private static CandyButton WideButton(GameObject parent, string name, string text, DesignSystem design,
            CandyButton.Tone tone, string? icon = null, bool small = false, bool iconRight = false)
        {
            var button = U1.Button(parent, name, text, design, tone, small, icon != null ? U1.S(icon) : null, iconRight);
            U1.Size(button.gameObject, 0f, small ? 46f : 56f);
            return button;
        }

        // ── Картка перед забігом (S02Run_02): «ЦЕЙ ЗАБІГ · ТЕМА», рамка рідкості, назва, бейдж, кольори, «Тапни» ──

        private sealed class IntroParts
        {
            public DialogView Dialog = null!;
            public Button Button = null!;
            public TMP_Text Kicker = null!;
            public RarityFrameView Frame = null!;
            public PictureView Picture = null!;
            public TMP_Text Name = null!;
            public RarityBadgeView Badge = null!;
            public Image[] Swatches = System.Array.Empty<Image>();
            public TMP_Text Colors = null!;
            public CandyButton Play = null!;
        }

        private static IntroParts BuildIntro(GameObject parent, DesignSystem design)
        {
            var parts = new IntroParts();
            parts.Dialog = U1.Dialog(parent, "RunIntro", design, U1.DialogWidthPx, out var panel, U1.DialogPaddingPx);
            // Тап будь-де — почати (рішення Сесії 1 лишається): кнопка на корені діалогу ловить тапи по затемненню й панелі.
            var scrim = parts.Dialog.transform.Find("Scrim").GetComponent<Image>();
            parts.Button = parts.Dialog.gameObject.AddComponent<Button>();
            parts.Button.targetGraphic = scrim;
            parts.Button.transition = Selectable.Transition.None;

            parts.Kicker = U1.Text(panel, "Kicker", "ЦЕЙ ЗАБІГ", design, U1.Role.Label, design.InkMuted);
            parts.Kicker.textWrappingMode = TextWrappingModes.NoWrap;
            parts.Kicker.overflowMode = TextOverflowModes.Ellipsis;
            parts.Frame = U1.FramedPicture(panel, "Picture", design, big: true, sidePx: 200f, pipsRoomPx: 14f, out parts.Picture, glowRoomPx: 14f);
            parts.Name = U1.Text(panel, "Name", "Трицератопсик", design, U1.Role.H1, design.InkText);
            parts.Name.textWrappingMode = TextWrappingModes.Normal;

            var badgeRow = CenterRow(panel, "BadgeRow", 26f, 0f);
            parts.Badge = U1.RarityBadge(badgeRow, "Rarity", design, Rarity.Uncommon);

            var colors = CenterRow(panel, "Colors", 22f, 6f);
            parts.Swatches = new Image[SwatchCount];
            for (var i = 0; i < SwatchCount; i++)
            {
                var swatchGo = Child(colors, $"Swatch{i}");
                var swatch = swatchGo.AddComponent<Image>();
                swatch.sprite = U1.S("pip_filled");
                swatch.raycastTarget = false;
                U1.Size(swatchGo, 16f, 16f);
                parts.Swatches[i] = swatch;
            }
            U1.Size(Child(colors, "Space"), 2f, 0f);
            parts.Colors = U1.Text(colors, "Count", "5 кольорів", design, U1.Role.Body, design.InkMuted);
            parts.Colors.textWrappingMode = TextWrappingModes.NoWrap;

            U1.Gap(panel, 4f);
            parts.Play = WideButton(panel, "Play", "Тапни, щоб грати", design, CandyButton.Tone.Teal, "icon_play");

            parts.Dialog.gameObject.SetActive(false);
            return parts;
        }

        // ── Програш (S02Run_05): один діалог на дві фази ──

        private sealed class OverParts
        {
            public DialogView Dialog = null!;
            public RectTransform Fit = null!;
            public RectTransform Panel = null!;
            public RectTransform ConfettiRoot = null!;
            public Image[] Confetti = System.Array.Empty<Image>();
            public TMP_Text Kicker = null!;
            public TMP_Text Score = null!;
            public RectTransform RecordRow = null!;
            public TMP_Text RecordValue = null!;
            public RectTransform RecordBadge = null!;
            public RectTransform Reward = null!;
            public TMP_Text RewardNumber = null!;
            public CandyButton Double = null!;
            public RectTransform Picture = null!;
            public RarityFrameView PictureFrame = null!;
            public PictureView PictureThumb = null!;
            public TMP_Text PictureTitle = null!;
            public TMP_Text PictureNote = null!;
            public CandyButton Finish = null!;
            public TMP_Text FinishPrice = null!;
            public TMP_Text ContinueLabel = null!;
            public CandyButton ContinueAd = null!;
            public CandyButton ContinueOil = null!;
            public TMP_Text ContinuePrice = null!;
            public RectTransform Collected = null!;
            public TMP_Text CollectedLabel = null!;
            public RarityFrameView[] ThumbFrames = System.Array.Empty<RarityFrameView>();
            public PictureView[] Thumbs = System.Array.Empty<PictureView>();
            public CandyButton Again = null!;
            public Button End = null!;
            public Button Menu = null!;
        }

        private static OverParts BuildOver(GameObject parent, DesignSystem design, Sprite circle)
        {
            var parts = new OverParts();
            parts.Dialog = U1.Dialog(parent, "GameOver", design, U1.DialogWidthPx, out var panel, U1.DialogPaddingPx);
            var root = parts.Dialog.gameObject;
            parts.Panel = panel.GetComponent<RectTransform>();

            // Конфеті нового рекорду — над затемненням, під панеллю.
            var confettiGo = Child(root, "Confetti");
            Stretch(confettiGo);
            confettiGo.transform.SetSiblingIndex(1);
            parts.ConfettiRoot = confettiGo.GetComponent<RectTransform>();
            parts.Confetti = new Image[ConfettiPieces];
            for (var i = 0; i < ConfettiPieces; i++)
            {
                var pieceGo = Child(confettiGo, $"Piece{i}");
                var piece = pieceGo.AddComponent<Image>();
                piece.sprite = circle;
                piece.raycastTarget = false;
                Place(piece, Vector2.zero, new Vector2(design.PaintConfettiSize, design.PaintConfettiSize),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                pieceGo.SetActive(false);
                parts.Confetti[i] = piece;
            }

            // Обгортка панелі: її масштабує FitOverCard на низьких екранах, саму панель — поява діалогу.
            var fitGo = Child(root, "Fit");
            Stretch(fitGo);
            parts.Fit = fitGo.GetComponent<RectTransform>();
            panel.transform.SetParent(fitGo.transform, false);

            parts.Kicker = U1.Text(panel, "Kicker", "МІСЦЯ БІЛЬШЕ НЕМАЄ", design, U1.Role.Label, design.InkMuted);
            parts.Score = U1.Text(panel, "Score", "720", design, U1.Role.Display, design.InkText);
            parts.Score.textWrappingMode = TextWrappingModes.NoWrap;

            var recordRow = CenterRow(panel, "Record", 28f, 8f);
            parts.RecordRow = recordRow.GetComponent<RectTransform>();
            U1.Text(recordRow, "Caption", "Рекорд", design, U1.Role.Body, design.InkMuted);
            parts.RecordValue = U1.Text(recordRow, "Value", "7 980", design, U1.Role.H3, design.Record);
            parts.RecordValue.textWrappingMode = TextWrappingModes.NoWrap;

            var badgeRow = CenterRow(panel, "NewRecord", 26f, 0f);
            parts.RecordBadge = badgeRow.GetComponent<RectTransform>();
            U1.Badge(badgeRow, "Badge", "Новий рекорд", design, "badge_amber", design.AmberInk);

            // Нагорода фіналу: крапля + «+N», праворуч «▶ Подвоїти» за ролик.
            var reward = U1.Panel(panel, "Reward", "panel_low", shadow: false);
            parts.Reward = reward.GetComponent<RectTransform>();
            var rewardRow = reward.AddComponent<HorizontalLayoutGroup>();
            rewardRow.padding = new RectOffset((int)M(14f), (int)M(6f), (int)M(6f), (int)M(6f));
            rewardRow.spacing = M(8f);
            rewardRow.childAlignment = TextAnchor.MiddleLeft;
            rewardRow.childControlWidth = rewardRow.childControlHeight = true;
            rewardRow.childForceExpandWidth = rewardRow.childForceExpandHeight = false;
            var dropGo = Child(reward, "Drop");
            var drop = dropGo.AddComponent<Image>();
            drop.sprite = U1.S("oil_drop");
            drop.raycastTarget = false;
            U1.Size(dropGo, 24f, 24f);
            parts.RewardNumber = U1.Text(reward, "Amount", "+0", design, U1.Role.H3, design.Record, TextAlignmentOptions.Left);
            parts.RewardNumber.textWrappingMode = TextWrappingModes.NoWrap;
            U1.Size(parts.RewardNumber.gameObject, 0f, 0f).flexibleWidth = 1f;
            parts.Double = U1.Button(reward, "Double", "Подвоїти", design, CandyButton.Tone.Glass, small: true, icon: U1.S("icon_play"));
            U1.Size(parts.Double.gameObject, 140f, 46f);

            // Картинка забігу: мініатюра в малій рамці, «Назва · 55%», «Не домалюєш — картинка згорить».
            var picture = U1.Panel(panel, "PictureRow", "panel_low", shadow: false);
            parts.Picture = picture.GetComponent<RectTransform>();
            var pictureRow = picture.AddComponent<HorizontalLayoutGroup>();
            var pad = (int)M(10f);
            pictureRow.padding = new RectOffset(pad, (int)M(12f), pad, pad);
            pictureRow.spacing = M(12f);
            pictureRow.childAlignment = TextAnchor.MiddleLeft;
            pictureRow.childControlWidth = pictureRow.childControlHeight = true;
            pictureRow.childForceExpandWidth = pictureRow.childForceExpandHeight = false;
            parts.PictureFrame = U1.FramedPicture(picture, "Thumb", design, big: false, sidePx: 56f, pipsRoomPx: 0f, out parts.PictureThumb);
            var texts = Child(picture, "Texts");
            var textCol = texts.AddComponent<VerticalLayoutGroup>();
            textCol.spacing = M(2f);
            textCol.childAlignment = TextAnchor.MiddleLeft;
            textCol.childControlWidth = textCol.childControlHeight = true;
            textCol.childForceExpandWidth = true;
            textCol.childForceExpandHeight = false;
            var textSize = texts.AddComponent<LayoutElement>();
            textSize.flexibleWidth = 1f;
            textSize.minWidth = 0f;
            parts.PictureTitle = U1.Text(texts, "Title", "Ракета · 55%", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
            parts.PictureTitle.textWrappingMode = TextWrappingModes.NoWrap;
            parts.PictureTitle.overflowMode = TextOverflowModes.Ellipsis;
            parts.PictureNote = U1.Text(texts, "Note", "Не домалюєш — картинка згорить", design, U1.Role.Caption, design.Danger, TextAlignmentOptions.Left);
            parts.PictureNote.textWrappingMode = TextWrappingModes.Normal;

            parts.Finish = WideButton(panel, "Finish", "Домалювати", design, CandyButton.Tone.Teal);
            parts.FinishPrice = U1.PriceChip(parts.Finish, 500, design);

            parts.ContinueLabel = U1.Text(panel, "ContinueLabel", "ПРОДОВЖИТИ ЗАБІГ", design, U1.Role.Label, design.InkMuted);
            // Один чип продовження (рішення автора): ролик, а коли ролика вже не можна — нафта.
            parts.ContinueAd = WideButton(panel, "ContinueAd", "Ролик", design, CandyButton.Tone.Teal, "icon_play");
            parts.ContinueOil = WideButton(panel, "ContinueOil", "Продовжити", design, CandyButton.Tone.Teal);
            parts.ContinuePrice = U1.PriceChip(parts.ContinueOil, 300, design);

            // Зібране за забіг: підпис і до трьох мініатюр у малих рамках.
            var collected = CenterRow(panel, "Collected", 44f, 8f);
            parts.Collected = collected.GetComponent<RectTransform>();
            parts.CollectedLabel = U1.Text(collected, "Label", "ЗІБРАНО · 3", design, U1.Role.Label, design.InkMuted);
            parts.CollectedLabel.textWrappingMode = TextWrappingModes.NoWrap;
            parts.ThumbFrames = new RarityFrameView[ThumbCount];
            parts.Thumbs = new PictureView[ThumbCount];
            for (var i = 0; i < ThumbCount; i++)
                parts.ThumbFrames[i] = U1.FramedPicture(collected, $"Thumb{i}", design, big: false, sidePx: 44f, pipsRoomPx: 0f, out parts.Thumbs[i]);

            // Єдина коралова на діалозі — «Ще раз» фіналу.
            parts.Again = WideButton(panel, "Again", "Ще раз", design, CandyButton.Tone.Coral);
            parts.End = U1.TextButton(panel, "End", "Завершити", design, design.InkMuted);
            U1.Size(parts.End.gameObject, 0f, 44f);
            parts.Menu = U1.TextButton(panel, "Menu", "В меню", design, design.InkMuted);
            U1.Size(parts.Menu.gameObject, 0f, 44f);

            root.SetActive(false);
            return parts;
        }

        // ── Картинку домальовано (S02Run_03) + підтвердження для Епічної+ (S02Run_04) ──
        private static CompletionCard BuildCompletion(GameObject parent, DesignSystem design)
        {
            var go = Child(parent, "Completion");
            Stretch(go);
            U1.Scrim(go, design);

            // Панель діалогу — і є картка, яку рухає свайп (центр екрана — її спокій).
            var card = U1.DialogPanel(go, "Card", U1.DialogWidthPx, U1.DialogPaddingPx);
            var kicker = U1.Text(card, "Kicker", "КАРТИНКУ ДОМАЛЬОВАНО!", design, U1.Role.Label, design.Amber);
            var title = U1.Text(card, "Title", "НЛО", design, U1.Role.H1, design.InkText);
            title.textWrappingMode = TextWrappingModes.Normal;
            var badgeRow = CenterRow(card, "BadgeRow", 26f, 0f);
            var badge = U1.RarityBadge(badgeRow, "Rarity", design, Rarity.Epic);
            var frame = U1.FramedPicture(card, "Picture", design, big: true, sidePx: 220f, pipsRoomPx: 14f, out var picture, glowRoomPx: 14f);

            var buttons = Child(card, "Buttons");
            var row = buttons.AddComponent<HorizontalLayoutGroup>();
            row.spacing = M(12f);
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            U1.Size(buttons, 0f, 46f);
            var drop = U1.Button(buttons, "Drop", "Викинути", design, CandyButton.Tone.Glass, small: true, icon: U1.S("icon_back"));
            drop.Destructive = true;
            U1.Size(drop.gameObject, 0f, 46f).flexibleWidth = 1f;
            var keep = U1.Button(buttons, "Keep", "У колекцію", design, CandyButton.Tone.Teal, small: true, icon: U1.S("icon_chev"), iconRight: true);
            U1.Size(keep.gameObject, 0f, 46f).flexibleWidth = 1f;

            var hint = U1.Text(card, "Hint", "Свайпни картинку вліво або вправо", design, U1.Role.Caption, design.InkMuted);
            hint.textWrappingMode = TextWrappingModes.Normal;

            // Підтвердження — окремий діалог; на час нього картка ховається, а затемнення лишається те саме, картчине
            // (своє в підтвердження прибране: друге з появою з нуля давало б провал яскравості й зайвий виклик).
            var confirm = U1.Dialog(go, "Confirm", design, U1.DialogWidthPx, out var confirmPanel, U1.DialogPaddingPx);
            Object.DestroyImmediate(confirm.transform.Find("Scrim").gameObject);
            confirmPanel.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            var top = Child(confirmPanel, "Top");
            var topRow = top.AddComponent<HorizontalLayoutGroup>();
            topRow.spacing = M(16f);
            topRow.childAlignment = TextAnchor.MiddleLeft;
            topRow.childControlWidth = topRow.childControlHeight = true;
            topRow.childForceExpandWidth = topRow.childForceExpandHeight = false;
            var confirmFrame = U1.FramedPicture(top, "Thumb", design, big: false, sidePx: 64f, pipsRoomPx: 0f, out var confirmPicture);
            var confirmTitle = U1.Text(top, "Title", "Викинути епічну картинку?", design, U1.Role.H2, design.InkText, TextAlignmentOptions.Left);
            confirmTitle.textWrappingMode = TextWrappingModes.Normal;
            var titleSize = confirmTitle.gameObject.AddComponent<LayoutElement>();
            titleSize.flexibleWidth = 1f;
            titleSize.minWidth = 0f;
            var confirmText = U1.Text(confirmPanel, "Text", "«НЛО» не потрапить у колекцію. Цю дію не скасувати.", design,
                U1.Role.Body, design.InkMuted, TextAlignmentOptions.Left);
            confirmText.textWrappingMode = TextWrappingModes.Normal;
            U1.Gap(confirmPanel, 4f);
            var confirmKeep = WideButton(confirmPanel, "Keep", "Залишити", design, CandyButton.Tone.Teal);
            var confirmDrop = WideButton(confirmPanel, "Drop", "Викинути", design, CandyButton.Tone.Glass);
            confirmDrop.Destructive = true;
            confirm.gameObject.SetActive(false);

            var view = go.AddComponent<CompletionCard>();
            Wire(view, ("design", design), ("card", card.GetComponent<RectTransform>()), ("kicker", kicker), ("title", title),
                ("badge", badge), ("frame", frame), ("picture", picture), ("dropButton", drop), ("keepButton", keep), ("hint", hint),
                ("confirm", confirm), ("confirmFrame", confirmFrame), ("confirmPicture", confirmPicture),
                ("confirmTitle", confirmTitle), ("confirmText", confirmText),
                ("confirmKeep", confirmKeep), ("confirmDrop", confirmDrop));
            view.Apply();
            go.SetActive(false);
            return view;
        }
    }
}
