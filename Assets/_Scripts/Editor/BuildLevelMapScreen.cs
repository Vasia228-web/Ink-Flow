using System.Collections.Generic;
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
    /// Збирає карту рівнів за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Level Map Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Шапка й рядок ліміту
    /// зафіксовані; карта скролиться під ними. Вузли й відрізки сліду —
    /// з пулів, під час скролу не інстанціюється нічого.
    /// </summary>
    public static class BuildLevelMapScreen
    {
        private const string ScenePath = "Assets/Scenes/LevelMap.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(18f);

        /// <summary>
        /// Вузлів у пулі. У в'юпорт (~1500 одиниць) уміщується п'ять кроків по 310;
        /// беремо дванадцять — із запасом на бонусні гілки збоку й на швидкий скрол.
        /// </summary>
        private const int NodeSlots = 12;

        /// <summary>Відрізків трохи більше: між вузлами плюс бонусні відгалуження.</summary>
        private const int SegmentSlots = 14;

        [MenuItem("Ink Flow/Setup/Build Level Map Screen")]
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
            var gloss = LoadSprite("circle-gloss");
            // Гало вузла — радіальний "nebula", а НЕ 9-slice "glow": у того
            // радіус кута завжди дорівнює загасанню, тож навколо круглого вузла
            // він малює прямокутник (та сама пастка, що дала рожеву рамку в фарбуванні).
            var glow = LoadSprite("nebula");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"), (circle, "circle-soft"),
                (circleOutline, "circle-outline"), (gloss, "circle-gloss"), (glow, "nebula")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Карту рівнів НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "LevelMapScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<LevelMapScreen>();

            var headerHeight = M(46f);
            var dailyHeight = M(40f);
            var contentTop = headerHeight + dailyHeight + M(10f);

            // Карта йде ПЕРШОЮ в ієрархії — шапка й рядок ліміту мусять лежати
            // над нею, бо вони зафіксовані, а карта проїжджає під ними.
            var scroll = BuildMap(screenGo, design!, font, circle!, circleOutline!, gloss!, glow!,
                rounded!, contentTop, out var mapContent, out var nodes,
                out var segments, out var segmentImages);

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                headerHeight, out var backButton, out var title, out var currency);

            BuildDailyRow(screenGo, design!, font, rounded!, outline!, headerHeight, dailyHeight,
                out var dailyLabel, out var dailyFill, out var dailyGradient);

            BuildSheet(screenGo, design!, font, rounded!, outline!, circle!,
                out var sheet, out var sheetBackground, out var sheetTitle, out var sheetDetails,
                out var sheetStars, out var sheetPips, out var playButton, out var playFill,
                out var playLabel, out var closeArea);

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("title", title),
                ("currency", currency),
                ("dailyLabel", dailyLabel), ("dailyBarFill", dailyFill),
                ("dailyBarGradient", dailyGradient),
                ("scroll", scroll), ("mapContent", mapContent),
                ("sheet", sheet), ("sheetBackground", sheetBackground),
                ("sheetTitle", sheetTitle), ("sheetDetails", sheetDetails),
                ("sheetStars", sheetStars), ("playButton", playButton),
                ("playFill", playFill), ("playLabel", playLabel),
                ("sheetCloseArea", closeArea));

            WireArray(screen, "nodes", nodes);
            WireArray(screen, "segments", segments);
            WireArray(screen, "segmentImages", segmentImages);
            WireArray(screen, "sheetStarPips", sheetPips);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Карту рівнів зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: padding 0 18 12; «РІВНІ» 15/800 ls .18em ──
        private static void BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, GameObject currencyPrefab, float height,
            out Button backButton, out TMP_Text title, out CurrencyWidget currency)
        {
            var go = Child(parent, "Header");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -height);
            rect.offsetMax = new Vector2(-SideMargin, 0f);

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

            var chevron = Label(backGo, "Glyph", "‹", design, font,
                M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(chevron.gameObject);

            backButton = backGo.AddComponent<Button>();
            backButton.targetGraphic = backFill;

            // Центр вільного проміжку, а не центр шапки: праворуч капсула валюти.
            title = Label(go, "Title", "РІВНІ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(-M(36f), 0f), new Vector2(M(182f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, go.transform);
            currency = currencyGo.GetComponent<CurrencyWidget>();
            Place(currency, Vector2.zero, new Vector2(M(112f), M(40f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        }

        // ── Рядок денного ліміту: скляна капсула з тонкою шкалою ──
        private static void BuildDailyRow(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, float top, float height,
            out TMP_Text label, out RectTransform fill, out GradientImage gradient)
        {
            var go = Child(parent, "DailyLimit");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -top - height);
            rect.offsetMax = new Vector2(-SideMargin, -top);

            var background = go.AddComponent<Image>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(14f));
            background.color = design.GlassFill;
            background.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(14f));
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            label = Label(go, "Label", "Повна нагорода: 7 / 10 сьогодні", design, font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Left);
            Place(label, new Vector2(M(14f), 0f), new Vector2(M(220f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var trackGo = Child(go, "Track");
            var track = trackGo.AddComponent<Image>();
            track.sprite = rounded;
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(3f));
            track.color = new Color(1f, 1f, 1f, 0.1f);
            track.raycastTarget = false;
            Place(track, new Vector2(-M(14f), 0f), new Vector2(M(110f), M(6f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var fillGo = Child(trackGo, "Fill");
            gradient = fillGo.AddComponent<GradientImage>();
            gradient.sprite = rounded;
            gradient.type = Image.Type.Sliced;
            gradient.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(3f));
            gradient.raycastTarget = false;
            fill = fillGo.GetComponent<RectTransform>();
            // Росте зліва направо — звідси якір і півот ліворуч.
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);
        }

        // ── Карта: скрол на всю решту екрана ──
        private static ScrollRect BuildMap(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, Sprite gloss, Sprite glow, Sprite rounded,
            float top, out RectTransform content, out LevelNodeView[] nodes,
            out RectTransform[] segments, out Image[] segmentImages)
        {
            var go = Child(parent, "Map");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -top);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 0f;

            var catcher = go.AddComponent<Image>();
            catcher.color = Color.clear;

            var viewportGo = Child(go, "Viewport");
            Stretch(viewportGo);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = content;

            // Відрізки сліду — під вузлами, тому створюються першими.
            segments = new RectTransform[SegmentSlots];
            segmentImages = new Image[SegmentSlots];
            for (var i = 0; i < SegmentSlots; i++)
            {
                var segGo = Child(contentGo, $"Segment{i}");
                var image = segGo.AddComponent<Image>();
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(9f));
                image.raycastTarget = false;
                var segRect = segGo.GetComponent<RectTransform>();
                // Півот ліворуч: відрізок росте від вузла й повертається навколо нього.
                segRect.anchorMin = segRect.anchorMax = new Vector2(0.5f, 1f);
                segRect.pivot = new Vector2(0f, 0.5f);
                segments[i] = segRect;
                segmentImages[i] = image;
            }

            nodes = new LevelNodeView[NodeSlots];
            for (var i = 0; i < NodeSlots; i++)
                nodes[i] = BuildNode(contentGo, design, font, circle, circleOutline, gloss, glow,
                    rounded, i);

            return scroll;
        }

        private static LevelNodeView BuildNode(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite circleOutline, Sprite gloss, Sprite glowSprite,
            Sprite rounded, int index)
        {
            var go = Child(parent, $"Node{index}");
            var body = go.GetComponent<RectTransform>();
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 1f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(M(52f), M(52f));

            var glowGo = Child(go, "Glow");
            // Розтягується разом із вузлом: розмір тіла міняється в рантаймі
            // (52 / 66 / 84), і гало має йти за ним без переліку в коді.
            Stretch(glowGo, -M(26f));
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = glowSprite;
            glow.raycastTarget = false;

            var dropGo = Child(go, "Drop");
            Stretch(dropGo);
            var drop = dropGo.AddComponent<GradientImage>();
            drop.sprite = circle;

            var glossGo = Child(dropGo, "Gloss");
            Stretch(glossGo);
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = gloss;
            glossImage.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = circleOutline;
            stroke.raycastTarget = false;

            var number = Label(go, "Number", "1", design, font,
                design.FontSizeLevelNode, design.ShopOnLightText, TextAlignmentOptions.Center);
            Stretch(number.gameObject);

            // Замок і очі боса — фігурами, без символів: гліфів на них у шрифті немає.
            var lockGo = Child(go, "Lock");
            var lockRect = lockGo.GetComponent<RectTransform>();
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(M(18f), M(18f));

            var lockBody = Child(lockGo, "Body").AddComponent<Image>();
            lockBody.sprite = rounded;
            lockBody.type = Image.Type.Sliced;
            lockBody.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(3f));
            lockBody.color = new Color(1f, 1f, 1f, 0.5f);
            lockBody.raycastTarget = false;
            Place(lockBody, new Vector2(0f, -M(2f)), new Vector2(M(12f), M(8f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var shackle = Child(lockGo, "Shackle").AddComponent<Image>();
            shackle.sprite = LoadSprite("rounded-rect-outline");
            shackle.type = Image.Type.Sliced;
            shackle.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(4f));
            shackle.color = new Color(1f, 1f, 1f, 0.5f);
            shackle.raycastTarget = false;
            Place(shackle, new Vector2(0f, M(4f)), new Vector2(M(8f), M(9f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var eyesGo = Child(go, "Eyes");
            var eyes = eyesGo.GetComponent<RectTransform>();
            Stretch(eyesGo);
            // Позиції з макета: ліве 26%/38%, праве 56%/34% — навмисно несиметрично.
            EyeAt(eyesGo, circle, new Vector2(0.26f, 0.62f));
            EyeAt(eyesGo, circle, new Vector2(0.56f, 0.66f));

            var caption = Label(go, "Caption", "КЛЯКС", design, font,
                design.FontSizeLabel, design.BossCaption, TextAlignmentOptions.Center);
            Place(caption, new Vector2(0f, -M(36f)), new Vector2(M(120f), M(16f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var starsGo = Child(go, "Stars");
            var starsRow = starsGo.GetComponent<RectTransform>();
            starsRow.anchorMin = starsRow.anchorMax = new Vector2(0.5f, 0f);
            starsRow.pivot = new Vector2(0.5f, 1f);
            starsRow.anchoredPosition = new Vector2(0f, -M(4f));
            starsRow.sizeDelta = new Vector2(M(34f), M(10f));

            var pips = new Image[3];
            for (var p = 0; p < 3; p++)
            {
                var pipGo = Child(starsGo, $"Pip{p}");
                var pip = pipGo.AddComponent<Image>();
                pip.sprite = circle;
                pip.raycastTarget = false;
                Place(pip, new Vector2((p - 1) * M(11f), 0f), new Vector2(M(8f), M(10f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                pips[p] = pip;
            }

            var chipGo = Child(go, "Requirement");
            var chip = chipGo.AddComponent<Image>();
            chip.sprite = rounded;
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(12f));
            chip.color = new Color(1f, 1f, 1f, 0.06f);
            chip.raycastTarget = false;
            var chipRect = chipGo.GetComponent<RectTransform>();
            chipRect.anchorMin = chipRect.anchorMax = new Vector2(0.5f, 0.5f);
            chipRect.pivot = new Vector2(0.5f, 0.5f);
            chipRect.sizeDelta = new Vector2(M(62f), M(20f));

            var requirement = Label(chipGo, "Label", "<sprite name=\"star\"> 25", design, font,
                design.FontSizeCaption, design.AccentGold, TextAlignmentOptions.Center);
            Stretch(requirement.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = drop;

            var view = go.AddComponent<LevelNodeView>();
            Wire(view,
                ("design", design), ("body", body), ("button", button), ("drop", drop),
                ("gloss", glossImage), ("stroke", stroke), ("glow", glow),
                ("numberLabel", number), ("lockIcon", lockRect), ("bossEyes", eyes),
                ("captionLabel", caption), ("starsRow", starsRow),
                ("requirementChip", chipRect), ("requirementLabel", requirement));
            WireArray(view, "starPips", pips);
            return view;
        }

        private static void EyeAt(GameObject parent, Sprite circle, Vector2 anchor)
        {
            var go = Child(parent, "Eye");
            var eye = go.AddComponent<Image>();
            eye.sprite = circle;
            eye.color = Color.white;
            eye.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(M(13f), M(16f));
        }

        // ── Картка деталей рівня ──
        private static void BuildSheet(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle,
            out RectTransform sheet, out GradientImage background, out TMP_Text title,
            out TMP_Text details, out RectTransform stars, out Image[] pips,
            out Button playButton, out GradientImage playFill, out TMP_Text playLabel,
            out Button closeArea)
        {
            var go = Child(parent, "LevelSheet");
            Stretch(go);
            sheet = go.GetComponent<RectTransform>();

            // Затемнення на весь екран — і воно ж закриває картку по тапу повз неї.
            var scrim = go.AddComponent<Image>();
            scrim.color = new Color(0.031f, 0.016f, 0.071f, 0.55f);
            closeArea = go.AddComponent<Button>();
            closeArea.targetGraphic = scrim;

            var panelGo = Child(go, "Panel");
            background = panelGo.AddComponent<GradientImage>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            var panelHeight = M(210f);
            Place(background, new Vector2(0f, M(20f)), new Vector2(M(354f), panelHeight),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            var strokeGo = Child(panelGo, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            title = Label(panelGo, "Title", "Рівень 12", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -M(20f)), new Vector2(M(300f), M(26f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            details = Label(panelGo, "Details", "", design, font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
            Place(details, new Vector2(0f, -M(62f)), new Vector2(M(310f), M(48f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var starsGo = Child(panelGo, "Stars");
            stars = starsGo.GetComponent<RectTransform>();
            stars.anchorMin = stars.anchorMax = new Vector2(0.5f, 1f);
            stars.pivot = new Vector2(0.5f, 0.5f);
            stars.anchoredPosition = new Vector2(0f, -M(104f));
            stars.sizeDelta = new Vector2(M(70f), M(18f));

            pips = new Image[3];
            for (var p = 0; p < 3; p++)
            {
                var pipGo = Child(starsGo, $"Pip{p}");
                var pip = pipGo.AddComponent<Image>();
                pip.sprite = circle;
                pip.raycastTarget = false;
                Place(pip, new Vector2((p - 1) * M(20f), 0f), new Vector2(M(14f), M(17f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                pips[p] = pip;
            }

            var playGo = Child(panelGo, "Play");
            playFill = playGo.AddComponent<GradientImage>();
            playFill.sprite = rounded;
            playFill.type = Image.Type.Sliced;
            playFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            Place(playFill, new Vector2(0f, M(18f)), new Vector2(M(300f), M(50f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            playLabel = Label(playGo, "Label", "Грати", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(playLabel.gameObject);

            playButton = playGo.AddComponent<Button>();
            playButton.targetGraphic = playFill;
        }
    }
}
