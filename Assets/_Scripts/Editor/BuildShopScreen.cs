using System.Collections.Generic;
using InkFlow.Meta;
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
    /// Збирає магазин за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Shop Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Картки створюються тут, один
    /// раз: під час скролу не інстанціюється нічого.
    /// </summary>
    public static class BuildShopScreen
    {
        private const string ScenePath = "Assets/Scenes/Shop.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        // ── Розміри з макета ──
        private static readonly float CardWidth = M(154f);
        private static readonly float CardHeight = M(197f);
        private static readonly float CardGap = M(12f);
        private static readonly float SideMargin = M(18f);

        [MenuItem("Ink Flow/Setup/Build Shop Screen")]
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
            var nebula = LoadSprite("nebula");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (gloss == null) missing.Add($"{SpriteFolder}/circle-gloss.png");
            if (nebula == null) missing.Add($"{SpriteFolder}/nebula.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Магазин НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "ShopScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<ShopScreen>();

            var headerHeight = M(46f);
            var tabsHeight = M(38f);

            var header = BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                out var backButton, out var title, out var currency, out var plusButton);
            BuildTabs(screenGo, design!, font, rounded!, headerHeight, tabsHeight,
                out var paintTabButton, out var oilTabButton, out var paintTabFill,
                out var oilTabFill, out var paintTabLabel, out var oilTabLabel);

            var contentTop = headerHeight + tabsHeight + M(14f);

            var paintTab = BuildPaintTab(screenGo, design!, font, rounded!, outline!, circle!, gloss!, nebula!,
                contentTop, out var weekly, out var sectionTitles, out var sectionSubtitles, out var cards);

            var oilTab = BuildOilTab(screenGo, design!, font, rounded!, outline!, circle!, gloss!,
                contentTop, out var oilPromise, out var packs, out var bundlesTitle, out var bundleCards);

            BuildQuantitySheet(screenGo, design!, font, rounded!, outline!,
                out var sheet, out var sheetTitle, out var qButtons, out var qFills,
                out var qLabels, out var qTotals, out var confirmButton, out var confirmFill,
                out var confirmLabel, out var cancelButton, out var cancelLabel);

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("title", title),
                ("currency", currency), ("plusButton", plusButton),
                ("paintTabButton", paintTabButton), ("oilTabButton", oilTabButton),
                ("paintTabFill", paintTabFill), ("oilTabFill", oilTabFill),
                ("paintTabLabel", paintTabLabel), ("oilTabLabel", oilTabLabel),
                ("paintTabRoot", paintTab), ("oilTabRoot", oilTab),
                ("weeklyBackground", weekly.Background), ("weeklyGlow", weekly.Glow),
                ("weeklyDrop", weekly.Drop), ("weeklyKicker", weekly.Kicker),
                ("weeklyName", weekly.Name), ("weeklyOldPrice", weekly.OldPrice),
                ("weeklyPrice", weekly.Price), ("weeklyTimer", weekly.Timer),
                ("weeklyBuyButton", weekly.BuyButton), ("weeklyBuyFill", weekly.BuyFill),
                ("weeklyBuyLabel", weekly.BuyLabel),
                ("oilPromise", oilPromise), ("bundlesTitle", bundlesTitle),
                ("quantitySheet", sheet), ("quantityTitle", sheetTitle),
                ("confirmButton", confirmButton), ("confirmFill", confirmFill),
                ("confirmLabel", confirmLabel), ("cancelButton", cancelButton),
                ("cancelLabel", cancelLabel));

            WireArray(screen, "sectionTitles", sectionTitles);
            WireArray(screen, "sectionSubtitles", sectionSubtitles);
            WireArray(screen, "paintCards", cards);
            WireArray(screen, "oilPacks", packs);
            WireArray(screen, "bundles", bundleCards);
            WireArray(screen, "quantityButtons", qButtons);
            WireArray(screen, "quantityFills", qFills);
            WireArray(screen, "quantityLabels", qLabels);
            WireArray(screen, "quantityTotals", qTotals);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Магазин зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: padding 0 18 12; «МАГАЗИН» 15/800 ls .18em; «+» 30 ──
        private static RectTransform BuildHeader(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite circleOutline, GameObject currencyPrefab,
            out Button backButton, out TMP_Text title, out CurrencyWidget currency, out Button plusButton)
        {
            var height = M(46f);
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

            title = Label(go, "Title", "МАГАЗИН", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(180f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // «+» — короткий шлях у «Нафту». Стоїть праворуч від капсули валюти.
            var plusSize = M(30f);
            var plusGo = Child(go, "Plus");
            var plusFill = plusGo.AddComponent<GradientImage>();
            plusFill.sprite = circle;
            plusFill.SetGradient(design.AccentGold, design.AccentPrimary);
            Place(plusFill, Vector2.zero, new Vector2(plusSize, plusSize),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var plusLabel = Label(plusGo, "Sign", "+", design, font,
                M(21f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(plusLabel.gameObject);

            plusButton = plusGo.AddComponent<Button>();
            plusButton.targetGraphic = plusFill;

            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, go.transform);
            currency = currencyGo.GetComponent<CurrencyWidget>();
            Place(currency, new Vector2(-plusSize - M(7f), 0f), new Vector2(M(112f), M(40f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            return rect;
        }

        // ── Таби: margin 0 18 14, padding 4, r22; чип r18, 15/800 ──
        private static void BuildTabs(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, float headerHeight, float height,
            out Button paintButton, out Button oilButton, out GradientImage paintFill,
            out GradientImage oilFill, out TMP_Text paintLabel, out TMP_Text oilLabel)
        {
            var go = Child(parent, "Tabs");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -headerHeight - height);
            rect.offsetMax = new Vector2(-SideMargin, -headerHeight);

            var track = go.AddComponent<Image>();
            track.sprite = rounded;
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            track.color = new Color(1f, 1f, 1f, 0.05f);
            track.raycastTarget = false;

            paintButton = BuildTabChip(go, design, font, rounded, "Фарби", 0, out paintFill, out paintLabel);
            oilButton = BuildTabChip(go, design, font, rounded, "Нафта", 1, out oilFill, out oilLabel);
        }

        private static Button BuildTabChip(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, string text, int index, out GradientImage fill, out TMP_Text label)
        {
            var go = Child(parent, $"Tab_{index}");
            var rect = go.GetComponent<RectTransform>();
            var pad = M(4f);
            rect.anchorMin = new Vector2(index * 0.5f, 0f);
            rect.anchorMax = new Vector2((index + 1) * 0.5f, 1f);
            rect.offsetMin = new Vector2(pad, pad);
            rect.offsetMax = new Vector2(-pad, -pad);

            fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
            fill.SetGradient(Color.clear, Color.clear);

            label = Label(go, "Label", text, design, font,
                design.FontSizeBody, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }

        /// <summary>Посилання на банер «Фарба тижня» — їх забагато для out-параметрів.</summary>
        private readonly struct WeeklyRefs
        {
            public WeeklyRefs(GradientImage background, Image glow, GradientImage drop, TMP_Text kicker,
                TMP_Text name, TMP_Text oldPrice, TMP_Text price, TMP_Text timer,
                Button buyButton, GradientImage buyFill, TMP_Text buyLabel)
            {
                Background = background; Glow = glow; Drop = drop; Kicker = kicker;
                Name = name; OldPrice = oldPrice; Price = price; Timer = timer;
                BuyButton = buyButton; BuyFill = buyFill; BuyLabel = buyLabel;
            }

            public GradientImage Background { get; }
            public Image Glow { get; }
            public GradientImage Drop { get; }
            public TMP_Text Kicker { get; }
            public TMP_Text Name { get; }
            public TMP_Text OldPrice { get; }
            public TMP_Text Price { get; }
            public TMP_Text Timer { get; }
            public Button BuyButton { get; }
            public GradientImage BuyFill { get; }
            public TMP_Text BuyLabel { get; }
        }

        // ── Вкладка «Фарби» ──
        private static RectTransform BuildPaintTab(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle, Sprite gloss, Sprite nebula,
            float contentTop, out WeeklyRefs weekly, out TMP_Text[] sectionTitles,
            out TMP_Text[] sectionSubtitles, out PaintCard[] cards)
        {
            var scroll = BuildVerticalScroll(parent, "PaintTab", contentTop, out var content);
            var catalog = ShopCatalog.CreateMock();

            var y = -M(6f);
            var bannerHeight = M(106f);
            weekly = BuildWeeklyBanner(content, design, font, rounded, circle, gloss, nebula,
                y, bannerHeight);
            y -= bannerHeight + M(22f);

            var titleRow = M(24f);
            var rowHeight = CardHeight + M(14f);

            sectionTitles = new TMP_Text[catalog.Sections.Count];
            sectionSubtitles = new TMP_Text[catalog.Sections.Count];
            var all = new List<PaintCard>();

            for (var s = 0; s < catalog.Sections.Count; s++)
            {
                var section = catalog.Sections[s];

                sectionTitles[s] = Label(content, $"SectionTitle{s}", section.Title, design, font,
                    design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Left);
                Place(sectionTitles[s], new Vector2(SideMargin, y), new Vector2(M(160f), titleRow),
                    new Vector2(0f, 1f), new Vector2(0f, 1f));

                sectionSubtitles[s] = Label(content, $"SectionSub{s}", section.Subtitle, design, font,
                    design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.Left);
                Place(sectionSubtitles[s], new Vector2(SideMargin + M(160f), y - M(3f)),
                    new Vector2(M(200f), titleRow), new Vector2(0f, 1f), new Vector2(0f, 1f));

                y -= titleRow + M(9f);

                var row = BuildCardRow(content, scroll, y, rowHeight);
                for (var i = 0; i < section.Items.Count; i++)
                {
                    var card = BuildPaintCard(row, design, font, rounded, outline, circle, gloss,
                        new Vector2(SideMargin + i * (CardWidth + CardGap), 0f));
                    all.Add(card);
                }

                // Ширина стрічки — щоб останню картку було видно повністю.
                var rowContent = (RectTransform)row.transform;
                rowContent.sizeDelta = new Vector2(
                    SideMargin * 2f + section.Items.Count * CardWidth + (section.Items.Count - 1) * CardGap, 0f);

                y -= rowHeight + M(18f);
            }

            cards = all.ToArray();
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y + M(26f));
            return scroll.GetComponent<RectTransform>();
        }

        private static WeeklyRefs BuildWeeklyBanner(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite circle, Sprite gloss, Sprite nebula,
            float y, float height)
        {
            var go = Child(parent, "Weekly");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, y - height);
            rect.offsetMax = new Vector2(-SideMargin, y);

            var background = go.AddComponent<GradientImage>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(24f));

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = LoadSprite("rounded-rect-outline");
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(24f));
            stroke.color = new Color(1f, 1f, 1f, 0.14f);
            stroke.raycastTarget = false;

            // М'яке кольорове світіння в тон фарби — купол, а не рант: тут воно
            // має розмивати, а не окреслювати.
            var glowGo = Child(go, "Glow");
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = nebula;
            glow.raycastTarget = false;
            Place(glow, new Vector2(M(20f), M(30f)), new Vector2(M(170f), M(170f)),
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));

            var dropSize = M(74f);
            var dropGo = Child(go, "Drop");
            var drop = dropGo.AddComponent<GradientImage>();
            drop.sprite = circle;
            drop.raycastTarget = false;
            Place(drop, new Vector2(M(16f) + dropSize * 0.5f, 0f), new Vector2(dropSize, dropSize),
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));

            var glossGo = Child(dropGo, "Gloss");
            Stretch(glossGo);
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = gloss;
            glossImage.raycastTarget = false;

            var textX = M(16f) + dropSize + M(15f);
            var kicker = Label(go, "Kicker", "ФАРБА ТИЖНЯ", design, font,
                design.FontSizeCaption, design.AccentTeal, TextAlignmentOptions.Left);
            Place(kicker, new Vector2(textX, M(30f)), new Vector2(M(180f), M(14f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var name = Label(go, "Name", "Океан", design, font,
                design.FontSizeTitle, design.TextPrimary, TextAlignmentOptions.Left);
            Place(name, new Vector2(textX, M(8f)), new Vector2(M(180f), M(24f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var oldPrice = Label(go, "OldPrice", "<s>48</s>", design, font,
                design.FontSizeLabel, design.TextFaint, TextAlignmentOptions.Left);
            Place(oldPrice, new Vector2(textX, -M(14f)), new Vector2(M(40f), M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var price = Label(go, "Price", "34", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Left);
            Place(price, new Vector2(textX + M(44f), -M(14f)), new Vector2(M(60f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var timer = Label(go, "Timer", "ще 3 дні", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Left);
            Place(timer, new Vector2(textX, -M(34f)), new Vector2(M(120f), M(14f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var buyGo = Child(go, "Buy");
            var buyFill = buyGo.AddComponent<GradientImage>();
            buyFill.sprite = rounded;
            buyFill.type = Image.Type.Sliced;
            buyFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            Place(buyFill, new Vector2(-M(16f), 0f), new Vector2(M(88f), height - M(32f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var buyLabel = Label(buyGo, "Label", "Купити", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(buyLabel.gameObject);

            var buyButton = buyGo.AddComponent<Button>();
            buyButton.targetGraphic = buyFill;

            return new WeeklyRefs(background, glow, drop, kicker, name, oldPrice, price, timer,
                buyButton, buyFill, buyLabel);
        }

        /// <summary>Горизонтальна стрічка карток із пропуском вертикальних жестів назовні.</summary>
        private static GameObject BuildCardRow(GameObject parent, ScrollRect outer, float y, float height)
        {
            var go = Child(parent, "Row");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, y - height);
            rect.offsetMax = new Vector2(0f, y);

            // Порядок компонентів важливий: NestedScrollForwarder мусить стояти
            // ПЕРЕД ScrollRect, щоб отримати жест першим і встигнути вимкнути скрол.
            var forwarder = go.AddComponent<NestedScrollForwarder>();
            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.scrollSensitivity = 0f;

            var catcher = go.AddComponent<Image>();
            catcher.color = Color.clear;

            var viewportGo = Child(go, "Viewport");
            Stretch(viewportGo);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = content;
            Wire(forwarder, ("inner", scroll), ("outer", outer));

            return contentGo;
        }

        private static PaintCard BuildPaintCard(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle, Sprite gloss,
            Vector2 position)
        {
            var go = Child(parent, "PaintCard");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            fill.color = new Color(1f, 1f, 1f, 0.05f);

            var glowGo = Child(go, "SpecialGlow");
            Stretch(glowGo, -M(6f));
            var specialGlow = glowGo.AddComponent<Image>();
            specialGlow.sprite = LoadSprite("glow");
            specialGlow.type = Image.Type.Sliced;
            specialGlow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(6f);
            specialGlow.raycastTarget = false;
            glowGo.transform.SetAsFirstSibling();

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var cardStroke = strokeGo.AddComponent<Image>();
            cardStroke.sprite = outline;
            cardStroke.type = Image.Type.Sliced;
            cardStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            cardStroke.raycastTarget = false;

            // ── Мензурка: top 15, right 11, 15×44, r 4/4/7/7 ──
            var tubeGo = Child(go, "Beaker");
            var beakerBackground = tubeGo.AddComponent<Image>();
            beakerBackground.sprite = rounded;
            beakerBackground.type = Image.Type.Sliced;
            beakerBackground.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(5f));
            beakerBackground.raycastTarget = false;
            Place(beakerBackground, new Vector2(-M(11f), -M(15f)), new Vector2(M(15f), M(44f)),
                new Vector2(1f, 1f), new Vector2(1f, 1f));

            var tubeStrokeGo = Child(tubeGo, "Stroke");
            Stretch(tubeStrokeGo);
            var beakerStroke = tubeStrokeGo.AddComponent<Image>();
            beakerStroke.sprite = outline;
            beakerStroke.type = Image.Type.Sliced;
            beakerStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(5f));
            beakerStroke.raycastTarget = false;

            var maskGo = Child(tubeGo, "Clip");
            Stretch(maskGo, design.ShopBeakerInset);
            maskGo.AddComponent<RectMask2D>();

            var fillGo = Child(maskGo, "Fill");
            var beakerFillImage = fillGo.AddComponent<Image>();
            beakerFillImage.sprite = rounded;
            beakerFillImage.type = Image.Type.Sliced;
            beakerFillImage.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(4f));
            beakerFillImage.raycastTarget = false;
            var beakerFill = fillGo.GetComponent<RectTransform>();
            // Росте знизу вгору — тому якір і півот унизу.
            beakerFill.anchorMin = new Vector2(0f, 0f);
            beakerFill.anchorMax = new Vector2(1f, 0f);
            beakerFill.pivot = new Vector2(0.5f, 0f);
            beakerFill.anchoredPosition = Vector2.zero;
            beakerFill.sizeDelta = new Vector2(0f, 0f);

            // ── Вміст ──
            var dropSize = M(62f);
            var dropGo = Child(go, "Drop");
            var drop = dropGo.AddComponent<GradientImage>();
            drop.sprite = circle;
            drop.raycastTarget = false;
            Place(drop, new Vector2(0f, -M(16f) - dropSize * 0.5f), new Vector2(dropSize, dropSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var glossGo = Child(dropGo, "Gloss");
            Stretch(glossGo);
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = gloss;
            glossImage.raycastTarget = false;

            var nameLabel = Label(go, "Name", "Малина", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Place(nameLabel, new Vector2(0f, -M(90f)), new Vector2(CardWidth, M(18f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var ownedLabel = Label(go, "Owned", "2.5 л", design, font,
                design.FontSizeSmall, design.TextMuted, TextAlignmentOptions.Center);
            Place(ownedLabel, new Vector2(0f, -M(110f)), new Vector2(CardWidth, M(14f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var priceLabel = Label(go, "Price", "12", design, font,
                design.FontSizeShopPrice, design.TextPrimary, TextAlignmentOptions.Right);
            Place(priceLabel, new Vector2(-M(6f), -M(132f)), new Vector2(M(40f), M(20f)),
                new Vector2(0.5f, 1f), new Vector2(1f, 0.5f));

            var priceUnit = Label(go, "PriceUnit", "/л", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Left);
            Place(priceUnit, new Vector2(M(2f), -M(132f)), new Vector2(M(30f), M(16f)),
                new Vector2(0.5f, 1f), new Vector2(0f, 0.5f));

            var buyGo = Child(go, "Buy");
            var buyFill = buyGo.AddComponent<GradientImage>();
            buyFill.sprite = rounded;
            buyFill.type = Image.Type.Sliced;
            buyFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(15f));
            Place(buyFill, new Vector2(0f, M(13f)), new Vector2(CardWidth - M(24f), M(34f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            var buyStrokeGo = Child(buyGo, "Stroke");
            Stretch(buyStrokeGo);
            var buyStroke = buyStrokeGo.AddComponent<Image>();
            buyStroke.sprite = outline;
            buyStroke.type = Image.Type.Sliced;
            buyStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(15f));
            buyStroke.raycastTarget = false;

            var buyLabel = Label(buyGo, "Label", "Купити", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(buyLabel.gameObject);

            var buyButton = buyGo.AddComponent<Button>();
            buyButton.targetGraphic = buyFill;

            var card = go.AddComponent<PaintCard>();
            Wire(card,
                ("design", design), ("cardStroke", cardStroke), ("specialGlow", specialGlow),
                ("beakerBackground", beakerBackground), ("beakerStroke", beakerStroke),
                ("beakerFill", beakerFill), ("beakerFillImage", beakerFillImage),
                ("drop", drop), ("nameLabel", nameLabel), ("ownedLabel", ownedLabel),
                ("priceLabel", priceLabel), ("priceUnitLabel", priceUnit),
                ("buyButton", buyButton), ("buyFill", buyFill), ("buyStroke", buyStroke),
                ("buyLabel", buyLabel));
            return card;
        }

        // ── Вкладка «Нафта» ──
        private static RectTransform BuildOilTab(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle, Sprite gloss,
            float contentTop, out TMP_Text promise, out OilPackCard[] packs,
            out TMP_Text bundlesTitle, out BundleCard[] bundles)
        {
            var scroll = BuildVerticalScroll(parent, "OilTab", contentTop, out var content);
            var catalog = ShopCatalog.CreateMock();

            var y = -M(2f);
            promise = Label(content, "Promise",
                "Нафта прискорює красу, а не силу.", design, font,
                design.FontSizeCardSubtitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(promise, new Vector2(0f, y), new Vector2(M(340f), M(44f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            y -= M(60f);

            var packWidth = (M(354f) - M(12f)) * 0.5f;
            var packHeight = M(160f);
            packs = new OilPackCard[catalog.OilPacks.Count];
            for (var i = 0; i < catalog.OilPacks.Count; i++)
            {
                var column = i % 2;
                var row = i / 2;
                var x = SideMargin + column * (packWidth + M(12f));
                packs[i] = BuildOilPack(content, design, font, rounded, outline, circle, gloss,
                    new Vector2(x, y - row * (packHeight + M(12f))), new Vector2(packWidth, packHeight));
            }

            y -= 2f * (packHeight + M(12f)) + M(14f);

            bundlesTitle = Label(content, "BundlesTitle", "Набори", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Left);
            Place(bundlesTitle, new Vector2(SideMargin, y), new Vector2(M(200f), M(24f)),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            y -= M(36f);

            var bundleHeight = M(78f);
            bundles = new BundleCard[catalog.Bundles.Count];
            for (var i = 0; i < catalog.Bundles.Count; i++)
            {
                bundles[i] = BuildBundle(content, design, font, rounded, outline, circle,
                    y - i * (bundleHeight + M(12f)), bundleHeight);
            }

            y -= catalog.Bundles.Count * (bundleHeight + M(12f));
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y + M(26f));
            return scroll.GetComponent<RectTransform>();
        }

        private static OilPackCard BuildOilPack(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle, Sprite gloss,
            Vector2 position, Vector2 size)
        {
            var go = Child(parent, "OilPack");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            fill.color = new Color(1f, 1f, 1f, 0.05f);
            fill.raycastTarget = false;

            var hotGlowGo = Child(go, "HotGlow");
            Stretch(hotGlowGo, -M(6f));
            var hotGlow = hotGlowGo.AddComponent<Image>();
            hotGlow.sprite = LoadSprite("glow");
            hotGlow.type = Image.Type.Sliced;
            hotGlow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(6f);
            hotGlow.raycastTarget = false;
            hotGlowGo.transform.SetAsFirstSibling();

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var cardStroke = strokeGo.AddComponent<Image>();
            cardStroke.sprite = outline;
            cardStroke.type = Image.Type.Sliced;
            cardStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            cardStroke.raycastTarget = false;

            var badgeGo = Child(go, "Badge");
            var badgeFill = badgeGo.AddComponent<GradientImage>();
            badgeFill.sprite = rounded;
            badgeFill.type = Image.Type.Sliced;
            badgeFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(10f));
            badgeFill.raycastTarget = false;
            Place(badgeFill, new Vector2(0f, M(9f)), new Vector2(M(96f), M(20f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var badgeLabel = Label(badgeGo, "Label", "Популярне", design, font,
                design.FontSizeCaption, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(badgeLabel.gameObject);

            var dropGo = Child(go, "Drop");
            var dropImage = dropGo.AddComponent<GradientImage>();
            dropImage.sprite = circle;
            dropImage.raycastTarget = false;
            dropImage.SetGradient(design.AccentSecondary, design.BackgroundEdge);
            Place(dropImage, new Vector2(0f, -M(19f)), new Vector2(M(58f), M(63f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));
            var drop = dropGo.GetComponent<RectTransform>();

            var glossGo = Child(dropGo, "Gloss");
            Stretch(glossGo);
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = gloss;
            glossImage.raycastTarget = false;

            var amount = Label(go, "Amount", "1 200", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(amount, new Vector2(0f, M(52f)), new Vector2(size.x, M(22f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            var buyGo = Child(go, "Buy");
            var buyFill = buyGo.AddComponent<GradientImage>();
            buyFill.sprite = rounded;
            buyFill.type = Image.Type.Sliced;
            buyFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            Place(buyFill, new Vector2(0f, M(14f)), new Vector2(size.x - M(48f), M(36f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            var priceLabel = Label(buyGo, "Price", "$3.99", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(priceLabel.gameObject);

            var buyButton = buyGo.AddComponent<Button>();
            buyButton.targetGraphic = buyFill;

            var card = go.AddComponent<OilPackCard>();
            Wire(card,
                ("design", design), ("cardStroke", cardStroke), ("hotGlow", hotGlow),
                ("drop", drop), ("badgeFill", badgeFill), ("badgeLabel", badgeLabel),
                ("amountLabel", amount), ("buyButton", buyButton), ("buyFill", buyFill),
                ("priceLabel", priceLabel));
            return card;
        }

        private static BundleCard BuildBundle(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle,
            float y, float height)
        {
            var go = Child(parent, "Bundle");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, y - height);
            rect.offsetMax = new Vector2(-SideMargin, y);

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            fill.color = new Color(1f, 1f, 1f, 0.05f);
            fill.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            stroke.color = new Color(1f, 1f, 1f, 0.1f);
            stroke.raycastTarget = false;

            // Кольорові краплі набору — накладені одна на одну, як у макеті.
            var dots = new GradientImage[6];
            for (var i = 0; i < dots.Length; i++)
            {
                var dotGo = Child(go, $"Dot{i}");
                var dot = dotGo.AddComponent<GradientImage>();
                dot.sprite = circle;
                dot.raycastTarget = false;
                Place(dot, new Vector2(M(12f) + i * M(9f), 0f), new Vector2(M(15f), M(19f)),
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                dotGo.transform.SetSiblingIndex(dots.Length - i);
                dots[i] = dot;
            }

            var textX = M(12f) + M(9f) * 6f + M(16f);
            var nameLabel = Label(go, "Name", "Стартовий набір", design, font,
                design.FontSizeShopPrice, design.TextPrimary, TextAlignmentOptions.Left);
            Place(nameLabel, new Vector2(textX, M(12f)), new Vector2(M(180f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var description = Label(go, "Description", "6 базових фарб · по 3 л", design, font,
                design.FontSizeSmall, design.TextMuted, TextAlignmentOptions.Left);
            Place(description, new Vector2(textX, -M(8f)), new Vector2(M(200f), M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var oldPrice = Label(go, "OldPrice", "<s>$6.99</s>", design, font,
                design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.Right);
            Place(oldPrice, new Vector2(-M(70f), M(16f)), new Vector2(M(60f), M(16f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var savingGo = Child(go, "Saving");
            var savingFill = savingGo.AddComponent<GradientImage>();
            savingFill.sprite = rounded;
            savingFill.type = Image.Type.Sliced;
            savingFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(9f));
            savingFill.raycastTarget = false;
            Place(savingFill, new Vector2(-M(12f), M(16f)), new Vector2(M(48f), M(18f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var savingLabel = Label(savingGo, "Label", "−45%", design, font,
                design.FontSizeCaption, design.ShopOnLightText, TextAlignmentOptions.Center);
            Stretch(savingLabel.gameObject);

            var buyGo = Child(go, "Buy");
            var buyFill = buyGo.AddComponent<GradientImage>();
            buyFill.sprite = rounded;
            buyFill.type = Image.Type.Sliced;
            buyFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            Place(buyFill, new Vector2(-M(12f), -M(14f)), new Vector2(M(76f), M(32f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var priceLabel = Label(buyGo, "Price", "$3.99", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(priceLabel.gameObject);

            var buyButton = buyGo.AddComponent<Button>();
            buyButton.targetGraphic = buyFill;

            var card = go.AddComponent<BundleCard>();
            Wire(card,
                ("design", design), ("nameLabel", nameLabel), ("descriptionLabel", description),
                ("oldPriceLabel", oldPrice), ("savingFill", savingFill), ("savingLabel", savingLabel),
                ("buyButton", buyButton), ("buyFill", buyFill), ("priceLabel", priceLabel));
            WireArray(card, "colorDots", dots);
            return card;
        }

        // ── Аркуш вибору кількості ──
        private static void BuildQuantitySheet(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline,
            out RectTransform sheet, out TMP_Text title, out Button[] buttons,
            out GradientImage[] fills, out TMP_Text[] labels, out TMP_Text[] totals,
            out Button confirmButton, out GradientImage confirmFill, out TMP_Text confirmLabel,
            out Button cancelButton, out TMP_Text cancelLabel)
        {
            var go = Child(parent, "QuantitySheet");
            Stretch(go);
            sheet = go.GetComponent<RectTransform>();

            var scrim = go.AddComponent<Image>();
            scrim.color = new Color(0.031f, 0.016f, 0.071f, 0.62f);

            var panelGo = Child(go, "Panel");
            var panel = panelGo.AddComponent<Image>();
            panel.sprite = rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            panel.color = design.GlassFillRaised;
            var panelHeight = M(210f);
            Place(panel, Vector2.zero, new Vector2(M(340f), panelHeight),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var panelStrokeGo = Child(panelGo, "Stroke");
            Stretch(panelStrokeGo);
            var panelStroke = panelStrokeGo.AddComponent<Image>();
            panelStroke.sprite = outline;
            panelStroke.type = Image.Type.Sliced;
            panelStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            panelStroke.color = design.GlassStroke;
            panelStroke.raycastTarget = false;

            title = Label(panelGo, "Title", "Малина · 12 /л", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -M(18f)), new Vector2(M(300f), M(24f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var count = ShopCatalog.Quantities.Length;
            buttons = new Button[count];
            fills = new GradientImage[count];
            labels = new TMP_Text[count];
            totals = new TMP_Text[count];

            var optionWidth = M(96f);
            var optionGap = M(10f);
            var startX = -(count - 1) * 0.5f * (optionWidth + optionGap);

            for (var i = 0; i < count; i++)
            {
                var option = ShopCatalog.Quantities[i];
                var optionGo = Child(panelGo, $"Option{i}");
                var optionFill = optionGo.AddComponent<GradientImage>();
                optionFill.sprite = rounded;
                optionFill.type = Image.Type.Sliced;
                optionFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
                Place(optionFill, new Vector2(startX + i * (optionWidth + optionGap), M(6f)),
                    new Vector2(optionWidth, M(64f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                labels[i] = Label(optionGo, "Label",
                    option.Badge == null ? $"{option.Liters} л" : $"{option.Liters} л  {option.Badge}",
                    design, font, design.FontSizeShopPrice, design.TextPrimary, TextAlignmentOptions.Center);
                Place(labels[i], new Vector2(0f, M(10f)), new Vector2(optionWidth, M(20f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                totals[i] = Label(optionGo, "Total", "12", design, font,
                    design.FontSizeSmall, design.TextMuted, TextAlignmentOptions.Center);
                Place(totals[i], new Vector2(0f, -M(12f)), new Vector2(optionWidth, M(16f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                buttons[i] = optionGo.AddComponent<Button>();
                buttons[i].targetGraphic = optionFill;
                fills[i] = optionFill;
            }

            var confirmGo = Child(panelGo, "Confirm");
            confirmFill = confirmGo.AddComponent<GradientImage>();
            confirmFill.sprite = rounded;
            confirmFill.type = Image.Type.Sliced;
            confirmFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            Place(confirmFill, new Vector2(0f, M(28f)), new Vector2(M(300f), M(48f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            confirmLabel = Label(confirmGo, "Label", "Купити · 12", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(confirmLabel.gameObject);

            confirmButton = confirmGo.AddComponent<Button>();
            confirmButton.targetGraphic = confirmFill;

            var cancelGo = Child(panelGo, "Cancel");
            var cancelHit = cancelGo.AddComponent<Image>();
            cancelHit.color = Color.clear;
            Place(cancelHit, new Vector2(0f, M(6f)), new Vector2(M(160f), M(28f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            cancelLabel = Label(cancelGo, "Label", "Скасувати", design, font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
            Stretch(cancelLabel.gameObject);

            cancelButton = cancelGo.AddComponent<Button>();
            cancelButton.targetGraphic = cancelHit;
        }

        /// <summary>Вертикальний скрол на всю площу під табами.</summary>
        private static ScrollRect BuildVerticalScroll(GameObject parent, string name,
            float top, out GameObject content)
        {
            var go = Child(parent, name);
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
    }
}
