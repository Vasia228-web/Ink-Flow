using InkFlow.Core;
using InkFlow.Gameplay;
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
    /// Збирає екран колекції (майстер-док §12): шапка, два ряди фільтрів (теми й рідкості),
    /// віртуалізована сітка карток. Меню: Ink Flow → Setup → Build Collection Screen.
    ///
    /// Карток у пулі — на в'юпорт із запасом у два ряди; ширина й висота картки, колонки й
    /// проміжок — токени DesignSystem. Пікселі всіх карток — із одного атласу (SlotAtlas),
    /// тож сітка малюється кількома викликами незалежно від розміру колекції.
    /// </summary>
    public static class BuildCollectionScreen
    {
        private const string ScenePath = "Assets/Scenes/Collection.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        /// <summary>Чипів рідкості («Усі» + шість). Чипи тем — за бібліотекою, див. <see cref="ThemeChipCount"/>.</summary>
        private const int RarityChips = 7;

        /// <summary>Запас чипів тем понад бібліотеку: нова тема без перезбирання екрана.</summary>
        private const int ThemeChipHeadroom = 4;

        /// <summary>«Усі теми» + стільки тем, скільки в бібліотеці, плюс запас; без бібліотеки — лише запас.</summary>
        private static int ThemeChipCount()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(MetaScreenRig<CollectionScreen>.LibraryPath);
            var themes = asset != null ? asset.ToLibrary().Themes.Count : 0;
            return 1 + themes + ThemeChipHeadroom;
        }

        /// <summary>Рядків карток у пулі: в'юпорт iPhone SE вміщає ~4, запас — два.</summary>
        private const int PoolRows = 6;

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        [MenuItem("Ink Flow/Setup/Build Collection Screen")]
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
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            K1Sprites.AllPresent(missing);
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Колекцію НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "CollectionScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<CollectionScreen>();

            var headerBottom = BuildHeader(screenGo, design!, font, circle!, circleOutline!, out var backButton, out var title, out var subtitle, out var settingsButton);
            var filtersBottom = BuildFilters(screenGo, design!, font, rounded!, circle!, circleOutline!, headerBottom,
                out var themesContent, out var themeButtons, out var themeFills, out var themeLabels,
                out var rarityButtons, out var rarityDots, out var rarityRings, out var rarityAllLabel);
            BuildGrid(screenGo, design!, font, rounded!, outline!, filtersBottom,
                out var scroll, out var content, out var cards, out var emptyLabel, out var hintLabel);

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("title", title), ("subtitle", subtitle), ("settingsButton", settingsButton),
                ("themesContent", themesContent), ("rarityAllLabel", rarityAllLabel),
                ("scroll", scroll), ("content", content), ("emptyLabel", emptyLabel), ("hintLabel", hintLabel));
            WireArray(screen, "themeButtons", themeButtons);
            WireArray(screen, "themeFills", themeFills);
            WireArray(screen, "themeLabels", themeLabels);
            WireArray(screen, "rarityButtons", rarityButtons);
            WireArray(screen, "rarityDots", rarityDots);
            WireArray(screen, "rarityRings", rarityRings);
            WireArray(screen, "cards", cards);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Колекцію зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: кнопка 40, «КОЛЕКЦІЯ» 15/800, підзаголовок 11/700 під нею ──
        private static float BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, out Button backButton, out TMP_Text title, out TMP_Text subtitle, out Button settingsButton)
        {
            var height = M(46f);
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
            settingsButton = GearButton(go, design, circle, circleOutline, LoadSprite("icon-gear"));

            title = Label(go, "Title", "КОЛЕКЦІЯ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, M(7f)), new Vector2(M(240f), M(22f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            subtitle = Label(go, "Subtitle", "Зібрано 0 різних · 0 усього", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(subtitle, new Vector2(0f, -M(10f)), new Vector2(M(280f), M(16f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            return height;
        }

        // ── Фільтри: ряд чипів тем і ряд крапок рідкості ──
        private static float BuildFilters(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite circle, Sprite circleOutline, float headerBottom,
            out RectTransform themesContent,
            out Button[] themeButtons, out GradientImage[] themeFills, out TMP_Text[] themeLabels,
            out Button[] rarityButtons, out Image[] rarityDots, out Image[] rarityRings, out TMP_Text rarityAllLabel)
        {
            var rowHeight = M(34f);
            var go = Child(parent, "Filters");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(M(18f), -(headerBottom + rowHeight * 2f + M(14f)));
            rect.offsetMax = new Vector2(-M(18f), -(headerBottom + M(4f)));

            // Теми — горизонтальний скрол: тем стане більше, ніж влізе в ряд.
            var themesGo = Child(go, "Themes");
            var themesRect = themesGo.GetComponent<RectTransform>();
            themesRect.anchorMin = new Vector2(0f, 1f);
            themesRect.anchorMax = new Vector2(1f, 1f);
            themesRect.pivot = new Vector2(0.5f, 1f);
            themesRect.offsetMin = new Vector2(0f, -rowHeight);
            themesRect.offsetMax = Vector2.zero;
            var scroll = themesGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0f;
            var viewportGo = Child(themesGo, "Viewport");
            Stretch(viewportGo);
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportGo.AddComponent<RectMask2D>();
            var contentGo = Child(viewportGo, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = content;
            themesContent = content;

            var chipW = M(92f);
            var gap = M(8f);
            var themeChips = ThemeChipCount();
            themeButtons = new Button[themeChips];
            themeFills = new GradientImage[themeChips];
            themeLabels = new TMP_Text[themeChips];
            var x = 0f;
            for (var i = 0; i < themeChips; i++)
            {
                var chipGo = Child(contentGo, $"Theme{i}");
                var fill = chipGo.AddComponent<GradientImage>();
                fill.sprite = rounded;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(17f));
                fill.SetGradient(design.GlassFill, design.GlassFill);
                Place(fill, new Vector2(x + chipW * 0.5f, 0f), new Vector2(chipW, rowHeight),
                    new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
                var label = Label(chipGo, "Label", i == 0 ? "Усі теми" : "Тема", design, font,
                    design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
                Stretch(label.gameObject);
                var button = chipGo.AddComponent<Button>();
                button.targetGraphic = fill;
                themeButtons[i] = button;
                themeFills[i] = fill;
                themeLabels[i] = label;
                x += chipW + gap;
            }
            content.sizeDelta = new Vector2(x, 0f);

            // Рідкості — «Усі» та шість крапок у колір рідкості; обрана — з кільцем.
            var raritiesGo = Child(go, "Rarities");
            var raritiesRect = raritiesGo.GetComponent<RectTransform>();
            raritiesRect.anchorMin = new Vector2(0f, 0f);
            raritiesRect.anchorMax = new Vector2(1f, 0f);
            raritiesRect.pivot = new Vector2(0.5f, 0f);
            raritiesRect.offsetMin = Vector2.zero;
            raritiesRect.offsetMax = new Vector2(0f, rowHeight);

            rarityButtons = new Button[RarityChips];
            rarityDots = new Image[RarityChips];
            rarityRings = new Image[RarityChips];
            var dot = M(26f);
            var allW = M(64f);
            var dotGap = M(10f);
            var rx = 0f;
            rarityAllLabel = null!;
            for (var i = 0; i < RarityChips; i++)
            {
                var chipGo = Child(raritiesGo, $"Rarity{i}");
                var width = i == 0 ? allW : dot;
                var fill = chipGo.AddComponent<Image>();
                fill.sprite = i == 0 ? rounded : circle;
                if (i == 0)
                {
                    fill.type = Image.Type.Sliced;
                    fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(13f));
                }
                fill.color = design.GlassFill;
                Place(fill, new Vector2(rx + width * 0.5f, 0f), new Vector2(width, i == 0 ? dot : dot),
                    new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));

                var ringGo = Child(chipGo, "Ring");
                Stretch(ringGo, -M(3f));
                var ring = ringGo.AddComponent<Image>();
                ring.sprite = i == 0 ? LoadSprite("rounded-rect-outline") : circleOutline;
                if (i == 0)
                {
                    ring.type = Image.Type.Sliced;
                    ring.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
                }
                ring.color = design.TextPrimary;
                ring.raycastTarget = false;

                if (i == 0)
                {
                    rarityAllLabel = Label(chipGo, "Label", "Усі", design, font,
                        design.FontSizeCaption, design.TextPrimary, TextAlignmentOptions.Center);
                    Stretch(rarityAllLabel.gameObject);
                }

                var button = chipGo.AddComponent<Button>();
                button.targetGraphic = fill;
                rarityButtons[i] = button;
                rarityDots[i] = fill;
                rarityRings[i] = ring;
                rx += width + dotGap;
            }

            return headerBottom + rowHeight * 2f + M(14f);
        }

        // ── Сітка: вертикальний скрол, пул карток ──
        private static void BuildGrid(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, float top,
            out ScrollRect scroll, out RectTransform content, out CollectionCard[] cards,
            out TMP_Text emptyLabel, out TMP_Text hintLabel)
        {
            var go = Child(parent, "Grid");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(M(18f), M(12f));
            rect.offsetMax = new Vector2(-M(18f), -(top + M(6f)));

            scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 0f;

            var viewportGo = Child(go, "Viewport");
            Stretch(viewportGo);
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, M(600f));

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = content;

            var columns = Mathf.Max(1, design.CollectionColumns);
            cards = new CollectionCard[columns * PoolRows];
            var panel = K1Sprites.Panel;
            for (var i = 0; i < cards.Length; i++)
                cards[i] = BuildCard(contentGo, $"Card{i}", design, font, rounded, outline, panel);

            emptyLabel = Label(go, "Empty", "Домалюй картинку в Нескінченному — і вона з'явиться тут", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(emptyLabel, new Vector2(0f, M(40f)), new Vector2(M(280f), M(44f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            emptyLabel.textWrappingMode = TextWrappingModes.Normal;

            hintLabel = Label(parent, "Hint", "Усі копії цієї картинки вже стоять у слотах", design, font,
                design.FontSizeCaption, design.TextPrimary, TextAlignmentOptions.Center);
            Place(hintLabel, new Vector2(0f, M(14f)), new Vector2(M(340f), M(36f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            hintLabel.textWrappingMode = TextWrappingModes.Normal;
            hintLabel.gameObject.SetActive(false);
        }

        private static CollectionCard BuildCard(GameObject parent, string name, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite? panel)
        {
            var width = design.CollectionCardWidth;
            var height = design.CollectionCardHeight;
            var captionH = M(18f);
            var countH = M(14f);
            var plateH = height - captionH - countH - M(6f);
            var radius = M(12f);

            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);

            var group = go.AddComponent<CanvasGroup>();

            // Невидима площина під тап: картка — і картинка, і кнопка.
            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;

            var plateGo = Child(go, "Plate");
            var plate = plateGo.AddComponent<Image>();
            plate.sprite = panel != null ? panel : rounded;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = panel != null ? K1Sprites.PanelMultiplier(radius) : GlassPanel.PixelsPerUnitFor(radius);
            plate.raycastTarget = false;
            Place(plate, Vector2.zero, new Vector2(width, plateH), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var frameGo = Child(plateGo, "Frame");
            Stretch(frameGo);
            var frame = frameGo.AddComponent<Image>();
            frame.sprite = outline;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            frame.color = design.PicturePlateStroke;
            frame.raycastTarget = false;

            var pixelSide = Mathf.Min(width, plateH) - M(16f);
            var pixelsGo = Child(plateGo, "Pixels");
            var pixels = pixelsGo.AddComponent<RawImage>();
            pixels.raycastTarget = false;
            pixels.color = Color.white;
            Place(pixels, Vector2.zero, new Vector2(pixelSide, pixelSide), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var nameLabel = Label(go, "Name", "КИТ", design, font,
                design.FontSizeCollectionName, design.PictureTitleColor, TextAlignmentOptions.Center);
            Place(nameLabel, new Vector2(0f, -plateH - M(2f)), new Vector2(width, captionH), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var countLabel = Label(go, "Count", "×1", design, font,
                design.FontSizeCollectionCount, design.TextMuted, TextAlignmentOptions.Center);
            Place(countLabel, new Vector2(0f, -plateH - captionH - M(2f)), new Vector2(width, countH), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var card = go.AddComponent<CollectionCard>();
            Wire(card, ("group", group), ("plate", plate), ("frame", frame), ("pixels", pixels),
                ("nameLabel", nameLabel), ("countLabel", countLabel), ("button", button));
            go.SetActive(false);
            return card;
        }
    }
}
