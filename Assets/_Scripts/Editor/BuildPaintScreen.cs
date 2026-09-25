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
    /// Збирає екран фарбування планети за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Paint Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Прив'язки: шапка й підпис зони
    /// до верху SafeArea, підказка/кнопка/палітра — до низу, планета центрується
    /// в тому, що лишилось.
    /// </summary>
    public static class BuildPaintScreen
    {
        private const string ScenePath = "Assets/Scenes/Paint.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string PlanetShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";
        private const string ZoneShaderPath = "Assets/_Shaders/InkFlowZone.shader";

        /// <summary>Накладок картинок на планеті (§10) і мініатюр у шухляді.</summary>
        private const int PlacementSlots = 8;
        private const int DrawerSlots = 12;

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        /// <summary>Маркерів у пулі. Майстер-док обіцяє 8–15 зон на планету.</summary>
        private const int ZoneSlots = 16;

        [MenuItem("Ink Flow/Setup/Build Paint Screen")]
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
            var gloss = LoadSprite("circle-gloss");
            var quad = LoadSprite("white-quad");
            var planetShader = AssetDatabase.LoadAssetAtPath<Shader>(PlanetShaderPath);
            var zoneShader = AssetDatabase.LoadAssetAtPath<Shader>(ZoneShaderPath);
            var nebula = LoadSprite("nebula");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (gloss == null) missing.Add($"{SpriteFolder}/circle-gloss.png");
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (planetShader == null) missing.Add(PlanetShaderPath);
            if (zoneShader == null) missing.Add(ZoneShaderPath);
            if (nebula == null) missing.Add($"{SpriteFolder}/nebula.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Фарбування НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "PaintScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<PaintScreen>();

            var header = BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                out var backButton, out var title, out var currency);
            BuildZoneLabel(screenGo, design!, font, rounded!, header,
                out var chip, out var zoneLabel);

            var palette = BuildPalette(screenGo, design!, font, rounded!, circle!, gloss!, outline!,
                out var swatches, out var shopButton, out var collectionButton);
            var drawer = BuildCollectionDrawer(screenGo, design!, font, rounded!, outline!, nebula!,
                palette.rect.height,
                out var collectionBack, out var collectionBackLabel, out var collectionEmpty,
                out var collectionThumbs, out var collectionThumbButtons,
                out var removeLastButton, out var removeLastLabel);
            var actionBlock = BuildFillButton(screenGo, design!, font, rounded!, outline!, palette,
                out var fillButton, out var fillFill, out var fillStroke, out var fillLabel);
            var hint = BuildHint(screenGo, design!, font, actionBlock);
            var placeHint = BuildPlaceHint(screenGo, design!, font, actionBlock);

            var stageRoot = BuildStage(screenGo, design!, quad!, circle!, zoneShader!,
                out var stage, out var disc, out var atmosphere,
                out var flash, out var moon, out var confetti, out var markers);
            var placements = BuildPlacements(stage.gameObject, design!, font, rounded!, outline!, nebula!);

            BuildCompletionCard(screenGo, design!, font, rounded!,
                out var card, out var kicker, out var completionTitle,
                out var nextButton, out var nextLabel);

            Wire(screen,
                ("design", design!), ("planetShader", planetShader!),
                ("backButton", backButton), ("planetTitle", title), ("currency", currency),
                ("stageRoot", stageRoot), ("stage", stage), ("disc", disc),
                ("atmosphere", atmosphere), ("completionFlash", flash), ("moon", moon),
                ("confetti", confetti),
                ("zoneLabelChip", chip), ("zoneLabel", zoneLabel), ("rotateHint", hint),
                ("fillButton", fillButton), ("fillButtonFill", fillFill),
                ("fillButtonStroke", fillStroke), ("fillButtonLabel", fillLabel),
                ("shopButton", shopButton),
                ("collectionButton", collectionButton), ("paletteRoot", palette),
                ("collectionDrawer", drawer), ("collectionBack", collectionBack),
                ("collectionBackLabel", collectionBackLabel), ("collectionEmpty", collectionEmpty),
                ("placeHint", placeHint), ("removeLastButton", removeLastButton),
                ("removeLastLabel", removeLastLabel),
                ("completionCard", card), ("completionKicker", kicker),
                ("completionTitle", completionTitle), ("nextPlanetButton", nextButton),
                ("nextPlanetLabel", nextLabel));
            WireArray(screen, "swatches", swatches);
            WireArray(screen, "collectionThumbs", collectionThumbs);
            WireArray(screen, "collectionThumbButtons", collectionThumbButtons);
            WireArray(stage, "markers", markers);
            WireArray(stage, "placements", placements);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Фарбування зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
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

            // Заголовок центруємо у ВІЛЬНОМУ проміжку між кнопкою «‹» і правим
            // блоком, а не в усій шапці: інакше він з'їжджає праворуч і лізе під
            // капсулу валюти. Числа — з розрахунку ширини цих блоків.
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

        // ── Підпис зони: смуга 34, чип padding 6/16 r16, 13/800 ──
        private static void BuildZoneLabel(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, RectTransform header,
            out RectTransform chip, out TMP_Text label)
        {
            var rowHeight = M(34f);
            var go = Child(parent, "ZoneLabel");
            chip = go.GetComponent<RectTransform>();
            chip.anchorMin = new Vector2(0.5f, 1f);
            chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -header.rect.height - M(3f));
            chip.sizeDelta = new Vector2(M(190f), rowHeight - M(6f));

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            fill.color = new Color(1f, 1f, 1f, 0.08f);
            fill.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = LoadSprite("rounded-rect-outline");
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            stroke.color = new Color(1f, 1f, 1f, 0.14f);
            stroke.raycastTarget = false;

            label = Label(go, "Text", "ОКЕАН · 3 л", design, font,
                design.FontSizeGalaxyTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);
        }

        // ── Планета: S = 264 макета ──
        private static RectTransform BuildStage(GameObject parent, DesignSystem design,
            Sprite quad, Sprite circle, Shader zoneShader,
            out PlanetStage stage, out Image disc, out Image atmosphere, out Image flash,
            out RectTransform moon, out DripPool confetti, out ZoneMarker[] markers)
        {
            var size = M(264f);
            var go = Child(parent, "Stage");
            var stageRect = go.GetComponent<RectTransform>();
            stageRect.anchorMin = stageRect.anchorMax = new Vector2(0.5f, 0.5f);
            stageRect.pivot = new Vector2(0.5f, 0.5f);
            // Трохи вище центру: знизу три блоки, згори лише шапка.
            stageRect.anchoredPosition = new Vector2(0f, M(22f));
            stageRect.sizeDelta = new Vector2(size, size);

            // Матеріал серпанку створює PaintScreen у рантаймі: матеріал, зроблений
            // тут, запікся б у файл сцени окремим об'єктом і жив своїм життям.
            atmosphere = Quad(go, "Atmosphere", quad, size * design.PlanetAtmosphereScale);

            flash = Quad(go, "Flash", circle, size * 1.4f);
            flash.color = Color.white;

            // Диск — і планета, і зона захоплення дотику: обертання та влучання
            // в зону рахує PlanetStage на цьому ж об'єкті.
            var discGo = Child(go, "Disc");
            disc = discGo.AddComponent<Image>();
            disc.sprite = quad;
            disc.raycastTarget = true;
            Place(disc, Vector2.zero, new Vector2(size, size),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            stage = discGo.AddComponent<PlanetStage>();

            var zoneRootGo = Child(discGo, "Zones");
            var zoneRoot = zoneRootGo.GetComponent<RectTransform>();
            zoneRoot.anchorMin = zoneRoot.anchorMax = new Vector2(0.5f, 0.5f);
            zoneRoot.pivot = new Vector2(0.5f, 0.5f);
            zoneRoot.sizeDelta = Vector2.zero;

            markers = new ZoneMarker[ZoneSlots];
            for (var i = 0; i < ZoneSlots; i++)
            {
                var markerGo = Child(zoneRootGo, $"Zone{i}");
                var image = markerGo.AddComponent<Image>();
                image.sprite = quad;
                // Влучання рахує PlanetStage — маркери в raycast не беруть участі,
                // інакше передня й задня зони сперечались би за дотик.
                image.raycastTarget = false;
                Place(image, Vector2.zero, new Vector2(100f, 100f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                var marker = markerGo.AddComponent<ZoneMarker>();
                Wire(marker, ("design", design), ("zoneShader", zoneShader), ("image", image));
                markers[i] = marker;
            }

            Wire(stage, ("design", design), ("disc", disc), ("zoneRoot", zoneRoot));

            var moonGo = Child(go, "Moon");
            var moonImage = moonGo.AddComponent<Image>();
            moonImage.sprite = circle;
            moonImage.color = new Color(0.94f, 0.95f, 1f, 1f);
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

        // ── Підказка: 11.5/700, padding-bottom 8 ──
        private static TMP_Text BuildHint(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, RectTransform actionBlock)
        {
            var hint = Label(parent, "RotateHint",
                "<sprite name=\"retry\"> Крути планету пальцем, щоб дістати всі зони",
                design, font, design.FontSizeSmall, design.TextDim, TextAlignmentOptions.Center);
            Place(hint, new Vector2(0f, actionBlock.offsetMax.y + M(10f)),
                new Vector2(M(320f), M(16f)), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            return hint;
        }

        // ── Кнопка дії: padding 15/0, r20, повна ширина мінус 18 ──
        private static RectTransform BuildFillButton(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, RectTransform palette,
            out Button button, out GradientImage fill, out Image stroke, out TMP_Text label)
        {
            var height = M(51f);
            var go = Child(parent, "FillButton");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(M(18f), palette.rect.height + M(2f));
            rect.offsetMax = new Vector2(-M(18f), palette.rect.height + M(2f) + height);

            fill = go.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            fill.SetGradient(design.ButtonDisabledFill, design.ButtonDisabledFill);

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(20f));
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            label = Label(go, "Label", "Оберіть зону", design, font,
                design.FontSizeSubtitle, design.TextDim, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return rect;
        }

        // ── Палітра: скрол, gap 10, padding 10/18/18; зразок 58×104; магазин 64×96 ──
        private static RectTransform BuildPalette(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite circle, Sprite gloss, Sprite outline,
            out PaintSwatch[] swatches, out Button shopButton, out Button collectionButton)
        {
            var rowHeight = M(132f);
            var swatchW = M(58f);
            var swatchH = M(104f);
            var gap = M(10f);

            var go = Child(parent, "Palette");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, rowHeight);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 0f;

            var viewportGo = Child(go, "Viewport");
            Stretch(viewportGo);
            var viewport = viewportGo.GetComponent<RectTransform>();
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;

            var count = PaintKinds.Count;
            swatches = new PaintSwatch[count];
            var x = M(18f);
            for (var i = 0; i < count; i++)
            {
                swatches[i] = BuildSwatch(contentGo, design, font, rounded, circle, gloss,
                    (PaintKind)i, new Vector2(x + swatchW * 0.5f, 0f), new Vector2(swatchW, swatchH));
                x += swatchW + gap;
            }

            // Кнопка «+ Магазин» у кінці списку — короткий шлях, не заміна вкладки.
            var shopW = M(64f);
            var shopH = M(96f);
            var shopGo = Child(contentGo, "ShopButton");
            var shopFill = shopGo.AddComponent<Image>();
            shopFill.sprite = rounded;
            shopFill.type = Image.Type.Sliced;
            shopFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            shopFill.color = new Color(1f, 1f, 1f, 0.05f);
            Place(shopFill, new Vector2(x + shopW * 0.5f, 0f), new Vector2(shopW, shopH),
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));

            var shopStrokeGo = Child(shopGo, "Stroke");
            Stretch(shopStrokeGo);
            var shopStroke = shopStrokeGo.AddComponent<Image>();
            shopStroke.sprite = outline;
            shopStroke.type = Image.Type.Sliced;
            shopStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            shopStroke.color = new Color(1f, 1f, 1f, 0.12f);
            shopStroke.raycastTarget = false;

            var plusGo = Child(shopGo, "Plus");
            var plus = plusGo.AddComponent<GradientImage>();
            plus.sprite = circle;
            plus.SetGradient(design.AccentGold, design.AccentPrimary);
            plus.raycastTarget = false;
            Place(plus, new Vector2(0f, M(12f)), new Vector2(M(34f), M(34f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var plusLabel = Label(plusGo, "Sign", "+", design, font,
                M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(plusLabel.gameObject);

            var shopLabel = Label(shopGo, "Label", "Магазин", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(shopLabel, new Vector2(0f, -M(22f)), new Vector2(shopW, M(14f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            shopButton = shopGo.AddComponent<Button>();
            shopButton.targetGraphic = shopFill;
            x += shopW + gap;

            // «Колекція» (§10) — той самий чип, що й магазин: шухляда з картинками замість палітри.
            collectionButton = Chip(contentGo, "CollectionButton", design, font, rounded, outline,
                "Колекція", new Vector2(x + shopW * 0.5f, 0f), new Vector2(shopW, shopH), out _);

            content.sizeDelta = new Vector2(x + shopW + M(18f), 0f);
            return rect;
        }

        /// <summary>Скляний чип-кнопка з підписом — для «Колекція», «‹ Фарби», «Зняти останню».</summary>
        private static Button Chip(GameObject parent, string name, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, string caption, Vector2 position, Vector2 size, out TMP_Text label)
        {
            var go = Child(parent, name);
            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            fill.color = new Color(1f, 1f, 1f, 0.05f);
            Place(fill, position, size, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            stroke.color = new Color(1f, 1f, 1f, 0.12f);
            stroke.raycastTarget = false;

            label = Label(go, "Label", caption, design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Stretch(label.gameObject);

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }

        // ── Шухляда «Колекція» (§10): місце палітри, мініатюри зібраних картинок ──
        private static RectTransform BuildCollectionDrawer(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite nebula, float rowHeight,
            out Button back, out TMP_Text backLabel, out TMP_Text empty,
            out PictureView[] thumbs, out Button[] thumbButtons,
            out Button removeLast, out TMP_Text removeLastLabel)
        {
            var go = Child(parent, "CollectionDrawer");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, rowHeight);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 0f;

            var viewportGo = Child(go, "Viewport");
            Stretch(viewportGo);
            var viewport = viewportGo.GetComponent<RectTransform>();
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;

            var chipW = M(64f);
            var chipH = M(96f);
            var gap = M(10f);
            var x = M(18f);

            back = Chip(contentGo, "Back", design, font, rounded, outline, "‹ Фарби",
                new Vector2(x + chipW * 0.5f, 0f), new Vector2(chipW, chipH), out backLabel);
            x += chipW + gap;

            var thumbW = M(84f);
            var thumbH = M(104f);
            thumbs = new PictureView[DrawerSlots];
            thumbButtons = new Button[DrawerSlots];
            for (var i = 0; i < DrawerSlots; i++)
            {
                var thumbGo = Child(contentGo, $"Thumb{i}");
                // Невидима площина під тап: мініатюра — і картинка, і кнопка.
                var hit = thumbGo.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                Place(hit, new Vector2(x + thumbW * 0.5f, 0f), new Vector2(thumbW, thumbH),
                    new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
                thumbs[i] = PictureViewBuilder.MakePictureView(thumbGo, design, font, rounded, outline, nebula, thumbW, M(84f), M(66f), withTitle: false, captionHeight: M(16f));
                var button = thumbGo.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                thumbButtons[i] = button;
                thumbGo.SetActive(false);
                x += thumbW + gap;
            }

            removeLast = Chip(contentGo, "RemoveLast", design, font, rounded, outline, "Зняти останню",
                new Vector2(x + chipW * 0.5f, 0f), new Vector2(chipW, chipH), out removeLastLabel);
            removeLast.gameObject.SetActive(false);
            x += chipW;

            content.sizeDelta = new Vector2(x + M(18f), 0f);

            empty = Label(go, "Empty", "Домалюй картинку в Нескінченному — і вона з'явиться тут", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(empty, new Vector2(M(40f), 0f), new Vector2(M(240f), M(40f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            empty.textWrappingMode = TextWrappingModes.Normal;

            go.SetActive(false);
            return rect;
        }

        /// <summary>Підказка розміщення — над кнопкою дії, лише поки обрано картинку.</summary>
        private static TMP_Text BuildPlaceHint(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, RectTransform actionBlock)
        {
            var label = Label(parent, "PlaceHint", "Тапни на планету, куди поставити «КИТ»", design, font,
                design.FontSizeCaption, design.TextPrimary, TextAlignmentOptions.Center);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(M(18f), actionBlock.offsetMax.y + M(28f));
            rect.offsetMax = new Vector2(-M(18f), actionBlock.offsetMax.y + M(28f) + M(20f));
            label.gameObject.SetActive(false);
            return label;
        }

        // ── Накладки картинок на планеті (§10): проєктуються стадією, як зони ──
        private static PlacementMarker[] BuildPlacements(GameObject disc, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula)
        {
            var rootGo = Child(disc, "Placements");
            var root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;

            var side = M(64f);
            var markers = new PlacementMarker[PlacementSlots];
            for (var i = 0; i < PlacementSlots; i++)
            {
                var go = Child(rootGo, $"Placement{i}");
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(side, side);
                var group = go.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                var picture = PictureViewBuilder.MakePictureView(go, design, font, rounded, outline, nebula,
                    side, side, M(56f), withTitle: false, captionHeight: 0f, bare: true);
                var marker = go.AddComponent<PlacementMarker>();
                Wire(marker, ("group", group), ("picture", picture));
                go.SetActive(false);
                markers[i] = marker;
            }

            return markers;
        }

        private static PaintSwatch BuildSwatch(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite circle, Sprite glossSprite,
            PaintKind kind, Vector2 position, Vector2 size)
        {
            var go = Child(parent, $"Swatch_{kind}");
            var root = go.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;
            root.sizeDelta = size;

            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            // Підйом обраного зразка робить окремий вузол: якщо рухати корінь,
            // область натискання поїде разом із ним.
            var liftGo = Child(go, "Lift");
            var lift = liftGo.GetComponent<RectTransform>();
            lift.anchorMin = Vector2.zero;
            lift.anchorMax = Vector2.one;
            lift.offsetMin = Vector2.zero;
            lift.offsetMax = Vector2.zero;

            var panelGo = Child(liftGo, "Selection");
            Stretch(panelGo);
            var panel = panelGo.AddComponent<Image>();
            panel.sprite = rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            panel.color = design.GlassFillRaised;
            panel.raycastTarget = false;

            var dropGo = Child(liftGo, "Drop");
            var drop = dropGo.AddComponent<GradientImage>();
            drop.sprite = circle;
            drop.raycastTarget = false;
            Place(drop, new Vector2(0f, M(22f)), new Vector2(M(42f), M(42f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var ringGo = Child(dropGo, "Ring");
            Stretch(ringGo);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = LoadSprite("circle-outline");
            ring.color = design.TextPrimary;
            ring.raycastTarget = false;

            var glossGo = Child(dropGo, "Gloss");
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = glossSprite;
            glossImage.raycastTarget = false;
            Stretch(glossGo);

            var nameLabel = Label(liftGo, "Name", design.PaintName(kind), design, font,
                design.FontSizeLabel, design.TextDim, TextAlignmentOptions.Center);
            Place(nameLabel, new Vector2(0f, -M(6f)), new Vector2(size.x, M(14f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var beakerGo = Child(liftGo, "Beaker");
            var beaker = beakerGo.AddComponent<Image>();
            beaker.sprite = rounded;
            beaker.type = Image.Type.Sliced;
            beaker.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(3f));
            beaker.color = new Color(1f, 1f, 1f, 0.09f);
            beaker.raycastTarget = false;
            Place(beaker, new Vector2(0f, -M(20f)), new Vector2(M(26f), M(9f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var fillGo = Child(beakerGo, "Fill");
            var beakerFill = fillGo.AddComponent<Image>();
            beakerFill.sprite = rounded;
            beakerFill.type = Image.Type.Sliced;
            beakerFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(3f));
            beakerFill.color = design.Paint(kind);
            beakerFill.raycastTarget = false;
            Place(beakerFill, Vector2.zero, new Vector2(M(26f), M(9f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var liters = Label(liftGo, "Liters", "0 л", design, font,
                design.FontSizeCaption, design.TextDim, TextAlignmentOptions.Center);
            Place(liters, new Vector2(0f, -M(33f)), new Vector2(size.x, M(12f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;

            var swatch = go.AddComponent<PaintSwatch>();
            Wire(swatch,
                ("design", design), ("button", button), ("lift", lift),
                ("selectionPanel", panel), ("drop", drop), ("gloss", glossImage),
                ("dropRing", ring), ("nameLabel", nameLabel),
                ("beakerFill", fillGo.GetComponent<RectTransform>()), ("litersLabel", liters));

            var so = new SerializedObject(swatch);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            return swatch;
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

            title = Label(go, "Title", "Терра Прима завершена!", design, font,
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
