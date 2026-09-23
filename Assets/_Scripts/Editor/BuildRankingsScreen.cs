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
    /// Збирає рейтинги за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Rankings Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Рядки списку створюються тут
    /// один раз і далі рециклюються — під час скролу не інстанціюється нічого.
    /// </summary>
    public static class BuildRankingsScreen
    {
        private const string ScenePath = "Assets/Scenes/Rankings.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string PlanetShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(18f);

        /// <summary>
        /// Рядків у пулі. У в'юпорт (~1350 одиниць) уміщується вісім рядків по 177;
        /// беремо десять, щоб два запасні перекривали межу під час швидкого скролу.
        /// </summary>
        private const int RowSlots = 10;

        [MenuItem("Ink Flow/Setup/Build Rankings Screen")]
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
            var quad = LoadSprite("white-quad");
            var nebula = LoadSprite("nebula");
            var glow = LoadSprite("glow");
            var planetShader = AssetDatabase.LoadAssetAtPath<Shader>(PlanetShaderPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (gloss == null) missing.Add($"{SpriteFolder}/circle-gloss.png");
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (nebula == null) missing.Add($"{SpriteFolder}/nebula.png");
            if (glow == null) missing.Add($"{SpriteFolder}/glow.png");
            if (planetShader == null) missing.Add(PlanetShaderPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Рейтинги НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "RankingsScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<RankingsScreen>();

            var headerHeight = M(46f);
            var scopeHeight = M(38f);
            var metricHeight = M(32f);
            var periodHeight = M(24f);

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                headerHeight, out var backButton, out var title, out var currency);

            var scopeTop = headerHeight;
            BuildScopeTabs(screenGo, design!, font, rounded!, scopeTop, scopeHeight,
                out var friendsButton, out var worldButton, out var friendsFill,
                out var worldFill, out var friendsLabel, out var worldLabel);

            var metricTop = scopeTop + scopeHeight + M(10f);
            BuildMetricSegments(screenGo, design!, font, rounded!, outline!, metricTop, metricHeight,
                out var metricButtons, out var metricFills, out var metricStrokes, out var metricLabels);

            var periodTop = metricTop + metricHeight + M(9f);
            BuildPeriodTabs(screenGo, design!, font, rounded!, periodTop, periodHeight,
                out var weekButton, out var allButton, out var weekLabel, out var allLabel,
                out var weekUnderline, out var allUnderline);

            var contentTop = periodTop + periodHeight + M(8f);

            var scroll = BuildScroll(screenGo, contentTop, out var content);
            var podium = BuildPodium(content, design!, font, quad!, nebula!, circle!, planetShader!,
                out var slots);
            var rowsHost = BuildRowsHost(content, design!, podium.Height);
            var rows = new RankingRow[RowSlots];
            for (var i = 0; i < RowSlots; i++)
                rows[i] = BuildRow(rowsHost, design!, font, rounded!, outline!, circle!, gloss!,
                    quad!, planetShader!, i);

            BuildEmptyState(content, design!, font, rounded!, circle!, podium.Height,
                out var emptyState, out var emptyTitle, out var emptyHint,
                out var myCodeLabel, out var enterCodeLabel);

            BuildYouCard(screenGo, design!, font, rounded!, outline!, circle!, glow!,
                out var youBackground, out var youStroke, out var youGlow, out var youPosition,
                out var youAvatar, out var youNick, out var youGap, out var youValue, out var youUnit);

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("title", title),
                ("currency", currency),
                ("friendsTabButton", friendsButton), ("worldTabButton", worldButton),
                ("friendsTabFill", friendsFill), ("worldTabFill", worldFill),
                ("friendsTabLabel", friendsLabel), ("worldTabLabel", worldLabel),
                ("weekButton", weekButton), ("allTimeButton", allButton),
                ("weekLabel", weekLabel), ("allTimeLabel", allLabel),
                ("weekUnderline", weekUnderline), ("allTimeUnderline", allUnderline),
                ("podium", podium.Rect), ("scroll", scroll),
                ("scrollContent", content.GetComponent<RectTransform>()),
                ("listContent", rowsHost.GetComponent<RectTransform>()),
                ("emptyState", emptyState), ("emptyTitle", emptyTitle), ("emptyHint", emptyHint),
                ("myCodeLabel", myCodeLabel), ("enterCodeLabel", enterCodeLabel),
                ("youBackground", youBackground), ("youStroke", youStroke), ("youGlow", youGlow),
                ("youPosition", youPosition), ("youAvatar", youAvatar), ("youNick", youNick),
                ("youGap", youGap), ("youValue", youValue), ("youUnit", youUnit));

            WireArray(screen, "metricButtons", metricButtons);
            WireArray(screen, "metricFills", metricFills);
            WireArray(screen, "metricStrokes", metricStrokes);
            WireArray(screen, "metricLabels", metricLabels);
            WireArray(screen, "podiumSlots", slots);
            WireArray(screen, "rows", rows);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Рейтинги зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: padding 0 18 12; «РЕЙТИНГИ» 15/800 ls .18em ──
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

            var chevron = Label(backGo, "Chevron", "‹", design, font,
                M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(chevron.gameObject);

            backButton = backGo.AddComponent<Button>();
            backButton.targetGraphic = backFill;

            // Центр вільного проміжку між «‹» і капсулою валюти, а не центр шапки.
            title = Label(go, "Title", "РЕЙТИНГИ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(-M(36f), 0f), new Vector2(M(182f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, go.transform);
            currency = currencyGo.GetComponent<CurrencyWidget>();
            Place(currency, Vector2.zero, new Vector2(M(112f), M(40f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        }

        // ── «Друзі» / «Світ»: padding 4, r22; чип r18 ──
        private static void BuildScopeTabs(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, float top, float height,
            out Button friendsButton, out Button worldButton, out GradientImage friendsFill,
            out GradientImage worldFill, out TMP_Text friendsLabel, out TMP_Text worldLabel)
        {
            var go = Track(parent, "ScopeTabs", rounded, top, height, M(22f), 0.05f, 0.1f);
            friendsButton = Chip(go, design, font, rounded, "Друзі", 0, 2, M(4f), M(18f),
                design.FontSizeBody, out friendsFill, out friendsLabel);
            worldButton = Chip(go, design, font, rounded, "Світ", 1, 2, M(4f), M(18f),
                design.FontSizeBody, out worldFill, out worldLabel);
        }

        // ── Метрика: padding 3, r15, три сегменти ──
        private static void BuildMetricSegments(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, float top, float height,
            out Button[] buttons, out Image[] fills, out Image[] strokes, out TMP_Text[] labels)
        {
            var go = Track(parent, "MetricSegments", rounded, top, height, M(15f), 0.04f, 0.08f);
            var names = new[] { "Планети", "Галактики", "Колекція" };

            buttons = new Button[names.Length];
            fills = new Image[names.Length];
            strokes = new Image[names.Length];
            labels = new TMP_Text[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                var segGo = Child(go, $"Segment{i}");
                var rect = segGo.GetComponent<RectTransform>();
                var pad = M(3f);
                rect.anchorMin = new Vector2(i / (float)names.Length, 0f);
                rect.anchorMax = new Vector2((i + 1) / (float)names.Length, 1f);
                rect.offsetMin = new Vector2(pad, pad);
                rect.offsetMax = new Vector2(-pad, -pad);

                var fill = segGo.AddComponent<Image>();
                fill.sprite = rounded;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(12f));
                fill.color = Color.clear;

                var strokeGo = Child(segGo, "Stroke");
                Stretch(strokeGo);
                var stroke = strokeGo.AddComponent<Image>();
                stroke.sprite = outline;
                stroke.type = Image.Type.Sliced;
                stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(12f));
                stroke.color = design.GlassStroke;
                stroke.raycastTarget = false;

                var label = Label(segGo, "Label", names[i], design, font,
                    design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
                Stretch(label.gameObject);

                var button = segGo.AddComponent<Button>();
                button.targetGraphic = fill;

                buttons[i] = button;
                fills[i] = fill;
                strokes[i] = stroke;
                labels[i] = label;
            }
        }

        // ── Період: текстові таби з підкресленням, без капсули ──
        private static void BuildPeriodTabs(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, float top, float height,
            out Button weekButton, out Button allButton, out TMP_Text weekLabel,
            out TMP_Text allLabel, out Image weekUnderline, out Image allUnderline)
        {
            var go = Child(parent, "PeriodTabs");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -top - height);
            rect.offsetMax = new Vector2(-SideMargin, -top);

            var gap = M(16f);
            var width = M(110f);

            weekButton = PeriodTab(go, design, font, rounded, "Цей тиждень",
                new Vector2(-width * 0.5f - gap * 0.5f, 0f), width, out weekLabel, out weekUnderline);
            allButton = PeriodTab(go, design, font, rounded, "За весь час",
                new Vector2(width * 0.5f + gap * 0.5f, 0f), width, out allLabel, out allUnderline);

            // Крапка-роздільник між табами — 3 px макета.
            var dotGo = Child(go, "Dot");
            var dot = dotGo.AddComponent<Image>();
            dot.sprite = LoadSprite("circle-soft");
            dot.color = new Color(1f, 1f, 1f, 0.25f);
            dot.raycastTarget = false;
            Place(dot, Vector2.zero, new Vector2(M(3f), M(3f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private static Button PeriodTab(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, string text, Vector2 position, float width,
            out TMP_Text label, out Image underline)
        {
            var go = Child(parent, $"Period_{text}");
            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            Place(hit, position, new Vector2(width, M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            label = Label(go, "Label", text, design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Place(label, new Vector2(0f, M(3f)), new Vector2(width, M(18f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var lineGo = Child(go, "Underline");
            underline = lineGo.AddComponent<Image>();
            underline.sprite = rounded;
            underline.type = Image.Type.Sliced;
            underline.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(2f));
            underline.raycastTarget = false;
            Place(underline, new Vector2(0f, -M(9f)), new Vector2(width * 0.62f, M(3f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            return button;
        }

        // ── Скрол на решту екрана ──
        private static ScrollRect BuildScroll(GameObject parent, float top, out GameObject content)
        {
            var go = Child(parent, "List");
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
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = contentRect;

            content = contentGo;
            return scroll;
        }

        /// <summary>Подіум і його висота — вона потрібна, щоб покласти список під ним.</summary>
        private readonly struct PodiumRefs
        {
            public PodiumRefs(RectTransform rect, float height)
            {
                Rect = rect;
                Height = height;
            }

            public RectTransform Rect { get; }
            public float Height { get; }
        }

        // ── Подіум: padding 16 18 20; колонки 110/90; планети 96/70; бейдж 24 ──
        private static PodiumRefs BuildPodium(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite quad, Sprite nebula, Sprite circle, Shader planetShader,
            out PodiumSlot[] slots)
        {
            var height = M(222f);
            var go = Child(parent, "Podium");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -height);
            rect.offsetMax = new Vector2(-SideMargin, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

            // Порядок у макеті: 2, 1, 3 — перше місце по центру.
            var places = new[] { 2, 1, 3 };
            var widths = new[] { M(90f), M(110f), M(90f) };
            var gap = M(8f);
            var totalWidth = widths[0] + widths[1] + widths[2] + gap * 2f;

            slots = new PodiumSlot[places.Length];
            var x = -totalWidth * 0.5f;
            for (var i = 0; i < places.Length; i++)
            {
                slots[i] = BuildPodiumSlot(go, design, font, quad, nebula, circle, planetShader,
                    places[i], new Vector2(x + widths[i] * 0.5f, 0f), widths[i], height);
                x += widths[i] + gap;
            }

            return new PodiumRefs(rect, height);
        }

        private static PodiumSlot BuildPodiumSlot(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite quad, Sprite nebula, Sprite circle, Shader planetShader,
            int place, Vector2 position, float width, float columnHeight)
        {
            var first = place == 1;
            var planetSize = first ? M(96f) : M(70f);

            var go = Child(parent, $"Podium{place}");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Перше місце підняте на 6 px макета — так подіум читається як подіум.
            rect.anchoredPosition = position + new Vector2(0f, first ? M(6f) : 0f);
            rect.sizeDelta = new Vector2(width, columnHeight);

            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;

            // Гало кольору медалі — під планетою, більше за неї в 1.7 раза.
            var glowGo = Child(go, "Glow");
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = nebula;
            glow.raycastTarget = false;
            var glowSize = planetSize * 1.7f;
            Place(glow, new Vector2(0f, -M(46f)), new Vector2(glowSize, glowSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var planetGo = Child(go, "Planet");
            var planet = planetGo.AddComponent<Image>();
            planet.sprite = quad;
            planet.raycastTarget = false;
            Place(planet, new Vector2(0f, -M(46f)), new Vector2(planetSize, planetSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            // Бейдж місця — над планетою, наполовину на неї.
            var crownSize = M(24f);
            var crownGo = Child(go, "Crown");
            var crown = crownGo.AddComponent<GradientImage>();
            crown.sprite = circle;
            crown.raycastTarget = false;
            Place(crown, new Vector2(0f, -M(2f)), new Vector2(crownSize, crownSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var crownLabel = Label(crownGo, "Label", place.ToString(), design, font,
                design.FontSizeGalaxyTitle, design.MedalText, TextAlignmentOptions.Center);
            Stretch(crownLabel.gameObject);

            var nick = Label(go, "Nick", "Гравець", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Place(nick, new Vector2(0f, -M(46f) - planetSize * 0.5f - M(14f)),
                new Vector2(width, M(18f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var value = Label(go, "Value", "0", design, font,
                first ? design.FontSizePodiumFirst : design.FontSizePodiumOther,
                design.Medal(place), TextAlignmentOptions.Right);
            Place(value, new Vector2(-M(2f), -M(46f) - planetSize * 0.5f - M(38f)),
                new Vector2(M(46f), M(28f)), new Vector2(0.5f, 1f), new Vector2(1f, 0.5f));

            var unit = Label(go, "Unit", "планет", design, font,
                design.FontSizeCaption, design.TextFaint, TextAlignmentOptions.Left);
            Place(unit, new Vector2(M(3f), -M(46f) - planetSize * 0.5f - M(41f)),
                new Vector2(M(46f), M(14f)), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f));

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;

            var slot = go.AddComponent<PodiumSlot>();
            Wire(slot,
                ("design", design), ("planetShader", planetShader), ("button", button),
                ("crown", crown), ("crownLabel", crownLabel), ("glow", glow), ("planet", planet),
                ("nickLabel", nick), ("valueLabel", value), ("unitLabel", unit));

            var so = new SerializedObject(slot);
            so.FindProperty("place").intValue = place;
            so.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        /// <summary>Вузол під подіумом, у якому живуть рядки списку.</summary>
        private static GameObject BuildRowsHost(GameObject parent, DesignSystem design, float podiumHeight)
        {
            var go = Child(parent, "Rows");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -podiumHeight);
            rect.sizeDelta = new Vector2(0f, 0f);
            _ = design;
            return go;
        }

        // ── Рядок: 56 висота; номер 24; аватар 34×37; планета 34 ──
        private static RankingRow BuildRow(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite gloss, Sprite quad,
            Shader planetShader, int index)
        {
            var height = design.RankRowHeight;
            var go = Child(parent, $"Row{index}");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -height);
            rect.offsetMax = new Vector2(-SideMargin, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

            var background = go.AddComponent<Image>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
            stroke.raycastTarget = false;

            var position = Label(go, "Position", "4", design, font,
                design.FontSizeRankRow, design.TextFaint, TextAlignmentOptions.Center);
            Place(position, new Vector2(M(14f), 0f), new Vector2(M(24f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var avatarGo = Child(go, "Avatar");
            var avatar = avatarGo.AddComponent<GradientImage>();
            avatar.sprite = circle;
            avatar.raycastTarget = false;
            Place(avatar, new Vector2(M(46f), 0f), new Vector2(M(34f), M(37f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var avatarGlossGo = Child(avatarGo, "Gloss");
            Stretch(avatarGlossGo);
            var avatarGloss = avatarGlossGo.AddComponent<Image>();
            avatarGloss.sprite = gloss;
            avatarGloss.raycastTarget = false;

            var nick = Label(go, "Nick", "Гравець", design, font,
                design.FontSizeRankRow, design.TextPrimary, TextAlignmentOptions.Left);
            Place(nick, new Vector2(M(90f), 0f), new Vector2(M(150f), M(22f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var value = Label(go, "Value", "0", design, font,
                design.FontSizeRankValue, design.TextPrimary, TextAlignmentOptions.Right);
            Place(value, new Vector2(-M(52f), M(5f)), new Vector2(M(70f), M(22f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var unit = Label(go, "Unit", "планет", design, font,
                design.FontSizeCaption, design.TextFaint, TextAlignmentOptions.Right);
            Place(unit, new Vector2(-M(52f), -M(10f)), new Vector2(M(70f), M(14f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var planetGo = Child(go, "Planet");
            var planet = planetGo.AddComponent<Image>();
            planet.sprite = quad;
            planet.raycastTarget = false;
            Place(planet, new Vector2(-M(12f), 0f), new Vector2(M(34f), M(34f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var lockGo = Child(go, "PlanetLock");
            var planetLock = lockGo.AddComponent<Image>();
            planetLock.sprite = circle;
            planetLock.raycastTarget = false;
            Place(planetLock, new Vector2(-M(12f), 0f), new Vector2(M(34f), M(34f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;

            var row = go.AddComponent<RankingRow>();
            Wire(row,
                ("design", design), ("planetShader", planetShader), ("background", background),
                ("stroke", stroke), ("button", button), ("positionLabel", position),
                ("avatar", avatar), ("avatarGloss", avatarGloss), ("nickLabel", nick),
                ("valueLabel", value), ("unitLabel", unit), ("planet", planet),
                ("planetLock", planetLock));
            return row;
        }

        // ── Порожній стан друзів ──
        private static void BuildEmptyState(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite circle, float podiumHeight,
            out RectTransform root, out TMP_Text title, out TMP_Text hint,
            out TMP_Text myCodeLabel, out TMP_Text enterCodeLabel)
        {
            var go = Child(parent, "EmptyState");
            root = go.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -podiumHeight);
            root.sizeDelta = new Vector2(0f, M(260f));

            // Велика напівпрозора крапля — той самий силует, що в аватарах.
            var dropGo = Child(go, "Drop");
            var drop = dropGo.AddComponent<Image>();
            drop.sprite = circle;
            drop.color = new Color(1f, 1f, 1f, 0.1f);
            drop.raycastTarget = false;
            Place(drop, new Vector2(0f, -M(38f)), new Vector2(M(96f), M(112f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));

            title = Label(go, "Title", "Тут поки порожньо", design, font,
                design.FontSizeSubtitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -M(168f)), new Vector2(M(300f), M(24f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            hint = Label(go, "Hint", "Додай друзів, щоб змагатися у своєму затишному колі",
                design, font, design.FontSizeCardSubtitle, design.TextFaint, TextAlignmentOptions.Center);
            Place(hint, new Vector2(0f, -M(196f)), new Vector2(M(230f), M(36f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            myCodeLabel = CodeButton(go, design, font, rounded, "Мій код",
                new Vector2(-M(60f), -M(238f)), true);
            enterCodeLabel = CodeButton(go, design, font, rounded, "Ввести код",
                new Vector2(M(60f), -M(238f)), false);
        }

        private static TMP_Text CodeButton(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, string text, Vector2 position, bool primary)
        {
            var go = Child(parent, $"Code_{text}");
            var fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            fill.SetGradient(
                primary ? design.AccentPrimary : design.GlassFill,
                primary ? design.AccentGold : design.GlassFill);
            Place(fill, position, new Vector2(M(110f), M(44f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var label = Label(go, "Label", text, design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return label;
        }

        // ── Закріплена картка «Ти»: left/right 14, bottom 16, r22 ──
        private static void BuildYouCard(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite glowSprite,
            out GradientImage background, out Image stroke, out Image glow, out TMP_Text position,
            out GradientImage avatar, out TMP_Text nick, out TMP_Text gap,
            out TMP_Text value, out TMP_Text unit)
        {
            var height = M(64f);
            var go = Child(parent, "YouCard");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(M(14f), M(16f));
            rect.offsetMax = new Vector2(-M(14f), M(16f) + height);

            // Картка стоїть НАД скролом і не входить у нього — тому лишається
            // видимою на будь-якій позиції списку.
            go.transform.SetAsLastSibling();

            var glowGo = Child(go, "Glow");
            Stretch(glowGo, -M(8f));
            glow = glowGo.AddComponent<Image>();
            glow.sprite = glowSprite;
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(8f);
            glow.raycastTarget = false;

            background = go.AddComponent<GradientImage>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            stroke.raycastTarget = false;

            position = Label(go, "Position", "#214", design, font,
                design.FontSizeRankRow, design.YouCardText, TextAlignmentOptions.Left);
            Place(position, new Vector2(M(15f), 0f), new Vector2(M(46f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var avatarGo = Child(go, "Avatar");
            avatar = avatarGo.AddComponent<GradientImage>();
            avatar.sprite = circle;
            avatar.raycastTarget = false;
            Place(avatar, new Vector2(M(66f), 0f), new Vector2(M(34f), M(37f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            nick = Label(go, "Nick", "Ти · Нова", design, font,
                design.FontSizeRankRow, design.TextPrimary, TextAlignmentOptions.Left);
            Place(nick, new Vector2(M(110f), M(7f)), new Vector2(M(160f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            gap = Label(go, "Gap", "ще +2 до №213", design, font,
                design.FontSizeSmall, design.AccentTeal, TextAlignmentOptions.Left);
            Place(gap, new Vector2(M(110f), -M(11f)), new Vector2(M(160f), M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            value = Label(go, "Value", "12", design, font,
                design.FontSizeRankValue, design.YouCardText, TextAlignmentOptions.Right);
            Place(value, new Vector2(-M(15f), M(7f)), new Vector2(M(70f), M(22f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            unit = Label(go, "Unit", "планет", design, font,
                design.FontSizeCaption, design.TextFaint, TextAlignmentOptions.Right);
            Place(unit, new Vector2(-M(15f), -M(11f)), new Vector2(M(70f), M(14f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        }

        // ── Спільні дрібниці ──

        /// <summary>Смуга-підкладка під капсулу перемикача.</summary>
        private static GameObject Track(GameObject parent, string name, Sprite rounded,
            float top, float height, float radius, float fillAlpha, float strokeAlpha)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -top - height);
            rect.offsetMax = new Vector2(-SideMargin, -top);

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            fill.color = new Color(1f, 1f, 1f, fillAlpha);
            fill.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = LoadSprite("rounded-rect-outline");
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            stroke.color = new Color(1f, 1f, 1f, strokeAlpha);
            stroke.raycastTarget = false;

            return go;
        }

        private static Button Chip(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, string text, int index, int count, float padding, float radius,
            float fontSize, out GradientImage fill, out TMP_Text label)
        {
            var go = Child(parent, $"Chip{index}");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / (float)count, 0f);
            rect.anchorMax = new Vector2((index + 1) / (float)count, 1f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);

            fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            fill.SetGradient(Color.clear, Color.clear);

            label = Label(go, "Label", text, design, font,
                fontSize, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }
    }
}
