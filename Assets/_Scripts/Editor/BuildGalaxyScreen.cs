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
    /// Збирає екран огляду галактики за розміткою макета «Ink Flow v2» (полотно 390×844).
    /// Меню: Ink Flow → Setup → Build Galaxy Screen.
    ///
    /// Кожне число — px макета × K, K = 1080/390 ≈ 2.769. Прив'язки: шапка до верху
    /// SafeArea, низ (назва, кнопка, пагінація) до низу SafeArea, карусель розтягується
    /// між ними — тому на 4:3 і 20:9 «дихає» простір навколо планети, а не верстка.
    /// </summary>
    public static class BuildGalaxyScreen
    {
        private const string ScenePath = "Assets/Scenes/Galaxy.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string ShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        /// <summary>Слотів у пулі каруселі: фокус ±2. Далі планета вже поза кадром.</summary>
        private const int SlotCount = 5;

        /// <summary>Крапок пагінації робимо з запасом — галактики бувають різні.</summary>
        private const int DotCount = 12;

        [MenuItem("Ink Flow/Setup/Build Galaxy Screen")]
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

            // Асети — строго після NewScene: закриття сцени вивантажує незакорінені
            // асети, і посилання, взяте раніше, тихо стає «fake null».
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);

            var rounded = LoadSprite("rounded-rect");
            var outline = LoadSprite("rounded-rect-outline");
            var circle = LoadSprite("circle-soft");
            var circleOutline = LoadSprite("circle-outline");
            var quad = LoadSprite("white-quad");
            var check = LoadSprite("icon-check");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (check == null) missing.Add($"{SpriteFolder}/icon-check.png");
            if (shader == null) missing.Add(ShaderPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Галактику НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "GalaxyScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<GalaxyScreen>();

            var header = BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                out var backButton, out var title, out var progress, out var currency);
            var bottom = BuildBottom(screenGo, design!, font, rounded!, circle!,
                out var doneBadge, out var planetName, out var planetZones,
                out var paintButton, out var paintFill, out var paintLabel,
                out var pagination, out var dots);

            var carousel = BuildCarousel(screenGo, design!, font, quad!, circle!, rounded!, outline!,
                shader!, header, bottom, out var slots, out var nextName, out var nextHint);

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("galaxyName", title),
                ("galaxyProgress", progress), ("currency", currency), ("carousel", carousel),
                ("nextGalaxyName", nextName), ("nextGalaxyHint", nextHint),
                ("doneBadge", doneBadge), ("planetName", planetName), ("planetZones", planetZones),
                ("paintButton", paintButton), ("paintButtonFill", paintFill),
                ("paintButtonLabel", paintLabel), ("pagination", pagination));
            WireArray(screen, "dots", dots);
            WireArray(carousel, "slots", slots);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Синхронно й перед збереженням: OnValidate відкладений, і дефолти
            // потрапили б у файл сцени замість справжніх значень.
            uiRoot.ApplyScaler();
            screen.Apply();

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Галактику зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ───────────────────────── Шапка ─────────────────────────
        // Макет: padding 0 18px 6px; кнопка 40 кругла; заголовок 13/800 ls .1em;
        // підпис 11/700 α.45; капсула валюти — той самий CurrencyWidget.

        private static RectTransform BuildHeader(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite circleOutline, GameObject currencyPrefab,
            out Button backButton, out TMP_Text title, out TMP_Text progress, out CurrencyWidget currency)
        {
            var headerHeight = M(46f);
            var go = Child(parent, "Header");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(M(18f), -headerHeight);
            rect.offsetMax = new Vector2(-M(18f), 0f);

            // Кругла скляна кнопка «назад».
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

            // Заголовок центруємо у ВІЛЬНОМУ проміжку між кнопкою «‹» і правим
            // блоком, а не в усій шапці: інакше він з'їжджає праворуч і лізе під
            // капсулу валюти. Числа — з розрахунку ширини цих блоків.
            title = Label(go, "GalaxyName", "ГАЛАКТИКА I · ПЕРВІСНА", design, font,
                design.FontSizeGalaxyTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(-M(36f), M(7f)), new Vector2(M(182f), M(18f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            progress = Label(go, "GalaxyProgress", "3 / 9 планет", design, font,
                design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.Center);
            Place(progress, new Vector2(-M(36f), -M(9f)), new Vector2(M(182f), M(15f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, go.transform);
            currency = currencyGo.GetComponent<CurrencyWidget>();
            Place(currency, Vector2.zero, new Vector2(M(112f), M(40f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            return rect;
        }

        // ───────────────────────── Низ ─────────────────────────
        // Макет: блок padding 2px 24px 4px, gap 7; галочка 22; назва 25/800;
        // підпис 13/700 α.55; кнопка padding 14/46 r26 градієнт #ff2d8a→#ffb300;
        // крапки 7 (активна 18×7) gap 6, ряд padding 12px 0 16px.

        private static RectTransform BuildBottom(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite circle,
            out RectTransform doneBadge, out TMP_Text planetName, out TMP_Text planetZones,
            out Button paintButton, out GradientImage paintFill, out TMP_Text paintLabel,
            out RectTransform pagination, out Image[] dots)
        {
            var dotsRowHeight = M(35f);   // 12 + 7 + 16
            var blockHeight = M(116f);    // 2 + 30 + 7 + 16 + 7 + 50 + 4

            // ── Пагінація ──
            var dotsGo = Child(parent, "Pagination");
            pagination = dotsGo.GetComponent<RectTransform>();
            pagination.anchorMin = new Vector2(0f, 0f);
            pagination.anchorMax = new Vector2(1f, 0f);
            pagination.pivot = new Vector2(0.5f, 0f);
            pagination.offsetMin = Vector2.zero;
            pagination.offsetMax = new Vector2(0f, dotsRowHeight);

            var dotSize = design.PaginationDotSize;
            var dotGap = M(6f);
            dots = new Image[DotCount];
            for (var i = 0; i < DotCount; i++)
            {
                var dotGo = Child(dotsGo, $"Dot{i}");
                var dot = dotGo.AddComponent<Image>();
                dot.sprite = rounded;
                dot.type = Image.Type.Sliced;
                dot.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(dotSize * 0.5f);
                dot.color = design.PaginationDotInactive;
                dot.raycastTarget = false;
                // Розкладку рядка рахуємо самі: LayoutGroup щокадру перебудовував би
                // розкладку, а ширина активної крапки змінюється на кожному свайпі.
                var x = (i - (DotCount - 1) * 0.5f) * (dotSize + dotGap);
                Place(dot, new Vector2(x, M(16f) + dotSize * 0.5f), new Vector2(dotSize, dotSize),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
                dots[i] = dot;
            }

            // ── Назва, підпис, кнопка ──
            var blockGo = Child(parent, "FocusBlock");
            var block = blockGo.GetComponent<RectTransform>();
            block.anchorMin = new Vector2(0f, 0f);
            block.anchorMax = new Vector2(1f, 0f);
            block.pivot = new Vector2(0.5f, 0f);
            block.offsetMin = new Vector2(M(24f), dotsRowHeight);
            block.offsetMax = new Vector2(-M(24f), dotsRowHeight + blockHeight);

            var buttonHeight = M(50f);
            var buttonY = M(4f) + buttonHeight * 0.5f;

            var buttonGo = Child(blockGo, "PaintButton");
            paintFill = buttonGo.AddComponent<GradientImage>();
            paintFill.sprite = rounded;
            paintFill.type = Image.Type.Sliced;
            paintFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(26f));
            paintFill.SetGradient(design.AccentPrimary, design.AccentGold);
            Place(paintFill, new Vector2(0f, buttonY), new Vector2(M(200f), buttonHeight),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            // Гало під кнопкою навмисно НЕМАЄ. У макеті це м'яка тінь
            // (box-shadow 0 10px 26px), а наш glow.png — рант із радіусом кута 56 px
            // спрайта; на пігулці з радіусом 72 він обводив кнопку прямокутником.
            // М'яку тінь дасть купол nebula.png, якщо колись знадобиться.

            paintLabel = Label(buttonGo, "Label", "Фарбувати", design, font,
                design.FontSizePaintButton, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(paintLabel.gameObject);

            paintButton = buttonGo.AddComponent<Button>();
            paintButton.targetGraphic = paintFill;

            planetZones = Label(blockGo, "PlanetZones", "5 / 8 зон", design, font,
                design.FontSizeGalaxyTitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(planetZones, new Vector2(0f, M(4f + 50f + 7f) + M(8f)), new Vector2(M(300f), M(18f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            planetName = Label(blockGo, "PlanetName", "Терра Прима", design, font,
                design.FontSizePlanetName, design.TextPrimary, TextAlignmentOptions.Center);
            Place(planetName, new Vector2(0f, M(4f + 50f + 7f + 16f + 7f) + M(15f)),
                new Vector2(M(300f), M(32f)), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            // Галочка завершеної планети — ліворуч від назви.
            var badgeSize = M(22f);
            var badgeGo = Child(blockGo, "DoneBadge");
            var badge = badgeGo.AddComponent<GradientImage>();
            badge.sprite = circle;
            badge.SetGradient(design.AccentLime, design.AccentTeal);
            doneBadge = badgeGo.GetComponent<RectTransform>();
            Place(badge, new Vector2(-M(90f), M(84f) + badgeSize * 0.5f), new Vector2(badgeSize, badgeSize),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            // Галочка — спрайт, не символ: ✓ (U+2713) у Nunito немає, і TMP щоразу
            // писав би «not found in font asset», підставляючи квадрат.
            var checkGo = Child(badgeGo, "Check");
            var check = checkGo.AddComponent<Image>();
            check.sprite = LoadSprite("icon-check");
            check.color = new Color(0.05f, 0.17f, 0.16f, 1f);
            check.raycastTarget = false;
            Place(check, Vector2.zero, new Vector2(M(12f), M(12f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            return block;
        }

        // ───────────────────────── Карусель ─────────────────────────
        // Макет: слот у left:50% top:45%, крок 176, масштаб max(.34, 1-|d|·.4),
        // прозорість max(.14, 1-|d|·.5); планета 186 (фінальна 208); прев'ю 150.

        private static PlanetCarousel BuildCarousel(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite quad, Sprite circle, Sprite rounded, Sprite outline,
            Shader shader, RectTransform header, RectTransform bottom,
            out PlanetView[] slots, out TMP_Text nextName, out TMP_Text nextHint)
        {
            var go = Child(parent, "Carousel");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, bottom.rect.height + M(35f));
            rect.offsetMax = new Vector2(0f, -header.rect.height);

            // Прозора площина для перетягування: без графіки з raycastTarget
            // EventSystem не доставить сюди жодного дотику.
            var catcher = go.AddComponent<Image>();
            catcher.color = Color.clear;
            catcher.raycastTarget = true;

            var carousel = go.AddComponent<PlanetCarousel>();

            // Центр слотів — на 45% висоти каруселі згори, як у макеті.
            var track = Child(go, "Track");
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.55f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = Vector2.zero;

            slots = new PlanetView[SlotCount];
            for (var i = 0; i < SlotCount; i++)
                slots[i] = BuildSlot(track, design, quad, circle, rounded, shader, i);

            var preview = BuildPreview(track, design, font, quad, out nextName, out nextHint);

            Wire(carousel, ("design", design), ("viewport", rect), ("nextGalaxyPreview", preview));
            return carousel;
        }

        private static PlanetView BuildSlot(GameObject parent, DesignSystem design,
            Sprite quad, Sprite circle, Sprite rounded, Shader shader, int index)
        {
            var size = design.PlanetSize;
            var go = Child(parent, $"Slot{index}");
            var body = go.GetComponent<RectTransform>();
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(size, size);

            // Порядок шарів: заднє кільце → серпанок → диск → переднє кільце →
            // дуга прогресу → місяці → замок.
            var ringBack = Quad(go, "RingBack", quad,
                size * design.PlanetRingWidthScale, size * design.PlanetRingHeightScale);
            var atmosphere = Quad(go, "Atmosphere", quad,
                size * design.PlanetAtmosphereScale, size * design.PlanetAtmosphereScale);
            var disc = Quad(go, "Disc", quad, size, size);
            var ringFront = Quad(go, "RingFront", quad,
                size * design.PlanetRingWidthScale, size * design.PlanetRingHeightScale);
            var progressRing = Quad(go, "ProgressRing", quad,
                size * design.PlanetProgressRingScale, size * design.PlanetProgressRingScale);

            var moons = new RectTransform[2];
            for (var m = 0; m < moons.Length; m++)
            {
                var moonSize = m == 0 ? M(16f) : M(11f);
                var moonGo = Child(go, $"Moon{m}");
                var moon = moonGo.AddComponent<Image>();
                moon.sprite = circle;
                moon.color = new Color(0.92f, 0.94f, 1f, 1f);
                moon.raycastTarget = false;
                Place(moon, Vector2.zero, new Vector2(moonSize, moonSize),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                moons[m] = moonGo.GetComponent<RectTransform>();
            }

            var lockGo = BuildLock(go, design, rounded);

            var view = go.AddComponent<PlanetView>();
            Wire(view,
                ("design", design), ("planetShader", shader), ("body", body),
                ("disc", disc), ("atmosphere", atmosphere), ("progressRing", progressRing),
                ("ringBack", ringBack), ("ringFront", ringFront), ("lockIcon", lockGo));
            WireArray(view, "moons", moons);
            return view;
        }

        /// <summary>Замок макета: тіло 22×13 r4 і дужка 12×11 з обведенням 2.5.</summary>
        private static RectTransform BuildLock(GameObject parent, DesignSystem design, Sprite rounded)
        {
            var go = Child(parent, "Lock");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(M(22f), M(20f));

            var white = new Color(1f, 1f, 1f, 0.85f);

            var bodyGo = Child(go, "Body");
            var bodyImage = bodyGo.AddComponent<Image>();
            bodyImage.sprite = rounded;
            bodyImage.type = Image.Type.Sliced;
            bodyImage.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(4f));
            bodyImage.color = white;
            bodyImage.raycastTarget = false;
            Place(bodyImage, new Vector2(0f, -M(3.5f)), new Vector2(M(22f), M(13f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var shackleGo = Child(go, "Shackle");
            var shackle = shackleGo.AddComponent<Image>();
            shackle.sprite = LoadSprite("rounded-rect-outline");
            shackle.type = Image.Type.Sliced;
            shackle.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(7f));
            shackle.color = white;
            shackle.raycastTarget = false;
            Place(shackle, new Vector2(0f, M(6f)), new Vector2(M(12f), M(14f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            _ = design;
            return rect;
        }

        /// <summary>Прев'ю наступної галактики: тьмяна куля 150 і два написи.</summary>
        private static RectTransform BuildPreview(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite quad, out TMP_Text nextName, out TMP_Text nextHint)
        {
            var go = Child(parent, "NextGalaxyPreview");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(design.PlanetSizeFinale, design.PlanetSizeFinale);

            var ballSize = M(150f);
            var ballGo = Child(go, "Ball");
            var ball = ballGo.AddComponent<Image>();
            ball.sprite = LoadSprite("nebula");
            ball.color = DesignSystem.WithAlpha(design.AccentSecondary, 0.28f);
            ball.raycastTarget = false;
            Place(ball, new Vector2(0f, M(30f)), new Vector2(ballSize, ballSize),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            nextName = Label(go, "NextName", "ГАЛАКТИКА II · СЯЙВО", design, font,
                design.FontSizeNextGalaxy, design.TextDim, TextAlignmentOptions.Center);
            Place(nextName, new Vector2(0f, -M(60f)), new Vector2(M(300f), M(22f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            nextHint = Label(go, "NextHint", "Заверши цю галактику,\nщоб відкрити наступну",
                design, font, design.FontSizeLabel, design.TextFaint, TextAlignmentOptions.Center);
            Place(nextHint, new Vector2(0f, -M(88f)), new Vector2(M(300f), M(36f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            _ = quad;
            return rect;
        }

        /// <summary>Квад під шейдер. Спрайт потрібен саме білий: без нього Image
        /// віддає UV = 0, і шейдер отримав би нуль замість координат.</summary>
        private static Image Quad(GameObject parent, string name, Sprite quad, float width, float height)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = quad;
            image.raycastTarget = false;
            Place(image, Vector2.zero, new Vector2(width, height),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            return image;
        }
    }
}
