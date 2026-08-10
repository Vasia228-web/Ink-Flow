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
    /// Збирає профіль за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Profile Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Екран скролиться цілком,
    /// разом із шапкою — так у макеті. Усі елементи створюються тут один раз.
    /// </summary>
    public static class BuildProfileScreen
    {
        private const string ScenePath = "Assets/Scenes/Profile.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string PlanetShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(18f);

        /// <summary>Радіус великих карток профілю: 26 px макета.</summary>
        private static readonly float CardRadius = M(26f);

        [MenuItem("Ink Flow/Setup/Build Profile Screen")]
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
            var glow = LoadSprite("glow");
            var star = LoadSprite("icon-star");
            var check = LoadSprite("icon-check");
            var gear = LoadSprite("icon-gear");
            var pencil = LoadSprite("icon-pencil");
            var planetShader = AssetDatabase.LoadAssetAtPath<Shader>(PlanetShaderPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/DropView.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"), (circle, "circle-soft"),
                (circleOutline, "circle-outline"), (gloss, "circle-gloss"), (quad, "white-quad"),
                (glow, "glow"), (star, "icon-star"), (check, "icon-check"), (gear, "icon-gear"), (pencil, "icon-pencil")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (planetShader == null) missing.Add(PlanetShaderPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (dropPrefab == null) missing.Add($"{PrefabFolder}/DropView.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Профіль НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "ProfileScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<ProfileScreen>();

            // Шапка скролиться разом із рештою — тому вона всередині вмісту.
            BuildScroll(screenGo, out var content);

            var y = 0f;
            BuildHeader(content, design!, font, circle!, circleOutline!, gear!, ref y,
                out var backButton, out var title, out var settingsButton);

            BuildIdentity(content, design!, font, rounded!, outline!, circle!, glow!, pencil!,
                dropPrefab!, ref y, out var avatar, out var editButton, out var editFill,
                out var nick, out var rankCapsule, out var rankGlow, out var rankLabel,
                out var oilValue, out var oilWord);

            BuildLadder(content, design!, font, rounded!, outline!, circle!, glow!,
                star!, check!, ref y, out var ladderCaption, out var ladderSteps);

            BuildStats(content, design!, font, rounded!, outline!, star!, ref y,
                out var statValues, out var statLabels, out var statStars);

            BuildPalette(content, design!, font, rounded!, outline!, circle!, gloss!, ref y,
                out var paletteCaption, out var paletteSpent, out var paletteSlots,
                out var shopButton, out var shopLabel);

            BuildShowcaseAndBadges(content, design!, font, rounded!, outline!, circle!, quad!,
                glow!, planetShader!, ref y,
                out var showcaseCaption, out var showcasePlanet, out var showcaseName,
                out var thumbButtons, out var thumbPlanets, out var thumbRings,
                out var achievementsCaption, out var badges, out var toast, out var toastLabel);

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y + M(24f));

            Wire(screen,
                ("design", design!), ("planetShader", planetShader!),
                ("backButton", backButton), ("title", title), ("settingsButton", settingsButton),
                ("avatar", avatar), ("editAvatarButton", editButton), ("editAvatarFill", editFill),
                ("nickLabel", nick), ("rankCapsule", rankCapsule), ("rankCapsuleGlow", rankGlow),
                ("rankLabel", rankLabel), ("oilValue", oilValue), ("oilWord", oilWord),
                ("ladderCaption", ladderCaption),
                ("paletteCaption", paletteCaption), ("paletteSpent", paletteSpent),
                ("paletteShopButton", shopButton), ("paletteShopLabel", shopLabel),
                ("showcaseCaption", showcaseCaption), ("showcasePlanet", showcasePlanet),
                ("showcaseName", showcaseName),
                ("achievementsCaption", achievementsCaption),
                ("toast", toast), ("toastLabel", toastLabel));

            WireArray(screen, "ladder", ladderSteps);
            WireArray(screen, "statValues", statValues);
            WireArray(screen, "statLabels", statLabels);
            WireArray(screen, "statStars", statStars);
            WireArray(screen, "paletteSlots", paletteSlots);
            WireArray(screen, "thumbButtons", thumbButtons);
            WireArray(screen, "thumbPlanets", thumbPlanets);
            WireArray(screen, "thumbRings", thumbRings);
            WireArray(screen, "badges", badges);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Профіль зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        private static void BuildScroll(GameObject parent, out GameObject content)
        {
            var go = Child(parent, "Scroll");
            Stretch(go);

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
        }

        // ── Шапка: padding 0 18 12; «ПРОФІЛЬ» 15/800 ls .18em; шестерня 40 ──
        private static void BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, Sprite gear, ref float y,
            out Button backButton, out TMP_Text title, out Button settingsButton)
        {
            var height = M(46f);
            var go = Section(parent, "Header", ref y, height, spacing: 0f);
            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(SideMargin, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-SideMargin, rect.offsetMax.y);

            backButton = RoundButton(go, design, font, circle, circleOutline, null, "‹",
                new Vector2(0f, 0.5f), Vector2.zero, out _);
            settingsButton = RoundButton(go, design, font, circle, circleOutline, gear, null,
                new Vector2(1f, 0.5f), Vector2.zero, out _);

            // Тут праворуч не капсула валюти, а кругла кнопка — вільний проміжок
            // симетричний, тож заголовок центрується по шапці.
            title = Label(go, "Title", "ПРОФІЛЬ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(240f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private static Button RoundButton(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, Sprite? icon, string? glyph,
            Vector2 anchor, Vector2 offset, out Image fill)
        {
            var size = M(40f);
            var go = Child(parent, glyph != null ? "Back" : "Settings");
            fill = go.AddComponent<Image>();
            fill.sprite = circle;
            fill.color = design.CircleButtonFill;
            Place(fill, offset, new Vector2(size, size), anchor, anchor);

            var ringGo = Child(go, "Ring");
            Stretch(ringGo);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = circleOutline;
            ring.color = design.GlassStroke;
            ring.raycastTarget = false;

            if (glyph != null)
            {
                var label = Label(go, "Glyph", glyph, design, font,
                    M(22f), design.TextPrimary, TextAlignmentOptions.Center);
                Stretch(label.gameObject);
            }
            else if (icon != null)
            {
                var iconGo = Child(go, "Icon");
                var image = iconGo.AddComponent<Image>();
                image.sprite = icon;
                image.color = design.TextMuted;
                image.raycastTarget = false;
                Place(image, Vector2.zero, new Vector2(M(20f), M(20f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }

        // ── Візитка ──
        private static void BuildIdentity(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite glowSprite, Sprite pencil,
            GameObject dropPrefab, ref float y, out DropView avatar, out Button editButton, out GradientImage editFill,
            out TMP_Text nick, out GradientImage rankCapsule, out Image rankGlow,
            out TMP_Text rankLabel, out TMP_Text oilValue, out TMP_Text oilWord)
        {
            var height = M(269f);
            var go = Card(parent, "Identity", rounded, outline, design, ref y, height);

            var avatarSize = M(110f);
            var avatarGo = (GameObject)PrefabUtility.InstantiatePrefab(dropPrefab, go.transform);
            avatar = avatarGo.GetComponent<DropView>();
            Place(avatar, new Vector2(0f, -M(16f)), new Vector2(avatarSize, avatarSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));

            // Олівець сидить на самій краплі, правіше-нижче її центру.
            var editSize = M(30f);
            var editGo = Child(avatarGo, "EditAvatar");
            editFill = editGo.AddComponent<GradientImage>();
            editFill.sprite = circle;
            editFill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(editFill, new Vector2(-M(14f), 0f), new Vector2(editSize, editSize),
                new Vector2(1f, 0f), new Vector2(1f, 0.5f));

            // Олівець — спрайт, не символ: ✎ (U+270E) у Nunito немає, і TMP
            // підставляв би порожній квадрат.
            var pencilGo = Child(editGo, "Glyph");
            var pencilIcon = pencilGo.AddComponent<Image>();
            pencilIcon.sprite = pencil;
            pencilIcon.color = design.TextPrimary;
            pencilIcon.raycastTarget = false;
            Place(pencilIcon, Vector2.zero, new Vector2(M(15f), M(15f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            editButton = editGo.AddComponent<Button>();
            editButton.targetGraphic = editFill;

            nick = Label(go, "Nick", "Нова", design, font,
                design.FontSizeProfileNick, design.TextPrimary, TextAlignmentOptions.Center);
            Place(nick, new Vector2(0f, -M(16f) - avatarSize - M(8f)), new Vector2(M(300f), M(40f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var capsuleGo = Child(go, "RankCapsule");
            rankCapsule = capsuleGo.AddComponent<GradientImage>();
            rankCapsule.sprite = rounded;
            rankCapsule.type = Image.Type.Sliced;
            rankCapsule.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            Place(rankCapsule, new Vector2(0f, -M(16f) - avatarSize - M(52f)),
                new Vector2(M(190f), M(32f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var rankGlowGo = Child(capsuleGo, "Glow");
            Stretch(rankGlowGo, -M(6f));
            rankGlow = rankGlowGo.AddComponent<Image>();
            rankGlow.sprite = glowSprite;
            rankGlow.type = Image.Type.Sliced;
            rankGlow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(6f);
            rankGlow.raycastTarget = false;
            rankGlowGo.transform.SetAsFirstSibling();

            rankLabel = Label(capsuleGo, "Label", "Художниця галактик", design, font,
                design.FontSizeShopCard, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(rankLabel.gameObject);

            // Капсула валюти тут інша, ніж CurrencyWidget: із додатковим словом
            // «нафти» і без глянцевої кулі — так у макеті саме на цьому екрані.
            var oilGo = Child(go, "Oil");
            var oilFill = oilGo.AddComponent<Image>();
            oilFill.sprite = rounded;
            oilFill.type = Image.Type.Sliced;
            oilFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
            oilFill.color = new Color(1f, 1f, 1f, 0.05f);
            oilFill.raycastTarget = false;
            Place(oilFill, new Vector2(0f, -M(16f) - avatarSize - M(96f)),
                new Vector2(M(150f), M(34f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var oilStrokeGo = Child(oilGo, "Stroke");
            Stretch(oilStrokeGo);
            var oilStroke = oilStrokeGo.AddComponent<Image>();
            oilStroke.sprite = outline;
            oilStroke.type = Image.Type.Sliced;
            oilStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
            oilStroke.color = design.GlassStroke;
            oilStroke.raycastTarget = false;

            var oilDropGo = Child(oilGo, "Drop");
            var oilDrop = oilDropGo.AddComponent<GradientImage>();
            oilDrop.sprite = circle;
            oilDrop.SetGradient(design.AccentSecondary, design.BackgroundEdge);
            oilDrop.raycastTarget = false;
            Place(oilDrop, new Vector2(M(9f), 0f), new Vector2(M(26f), M(28f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            oilValue = Label(oilGo, "Value", "1 250", design, font,
                design.FontSizeShopPrice, design.TextPrimary, TextAlignmentOptions.Left);
            Place(oilValue, new Vector2(M(43f), 0f), new Vector2(M(56f), M(20f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            oilWord = Label(oilGo, "Word", "нафти", design, font,
                design.FontSizeCardSubtitle, design.TextFaint, TextAlignmentOptions.Left);
            Place(oilWord, new Vector2(M(99f), 0f), new Vector2(M(46f), M(18f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        }

        // ── Драбина звань: лінія зліва, п'ять сходинок ──
        private static void BuildLadder(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite glowSprite,
            Sprite star, Sprite check, ref float y,
            out TMP_Text caption, out RankStepView[] steps)
        {
            const int count = 5;
            var stepHeight = M(48f);
            var height = M(16f) + M(22f) + count * stepHeight + M(10f);
            var go = Card(parent, "Ladder", rounded, outline, design, ref y, height);

            caption = Caption(go, design, font, "ЗВАННЯ");

            // Лінія, що з'єднує кружки. Іде під ними, тому додається першою.
            var lineGo = Child(go, "Line");
            var line = lineGo.AddComponent<Image>();
            line.color = new Color(1f, 1f, 1f, 0.12f);
            line.raycastTarget = false;
            Place(line, new Vector2(M(35f), -M(52f)), new Vector2(M(2f), count * stepHeight - M(30f)),
                new Vector2(0f, 1f), new Vector2(0.5f, 1f));

            steps = new RankStepView[count];
            for (var i = 0; i < count; i++)
                steps[i] = BuildLadderStep(go, design, font, circle, glowSprite, star, check,
                    -M(38f) - i * stepHeight, stepHeight);
        }

        private static RankStepView BuildLadderStep(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite glowSprite, Sprite star, Sprite check,
            float top, float height)
        {
            var nodeSize = M(30f);
            var go = Child(parent, "Step");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, top - height);
            rect.offsetMax = new Vector2(0f, top);

            var nodeGo = Child(go, "Node");
            var node = nodeGo.AddComponent<GradientImage>();
            node.sprite = circle;
            node.raycastTarget = false;
            Place(node, new Vector2(M(20f), 0f), new Vector2(nodeSize, nodeSize),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var nodeGlowGo = Child(nodeGo, "Glow");
            Stretch(nodeGlowGo, -M(7f));
            var nodeGlow = nodeGlowGo.AddComponent<Image>();
            nodeGlow.sprite = glowSprite;
            nodeGlow.type = Image.Type.Sliced;
            nodeGlow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(7f);
            nodeGlow.raycastTarget = false;
            nodeGlowGo.transform.SetAsFirstSibling();

            var strokeGo = Child(nodeGo, "Stroke");
            Stretch(strokeGo);
            var nodeStroke = strokeGo.AddComponent<Image>();
            nodeStroke.sprite = LoadSprite("circle-outline");
            nodeStroke.raycastTarget = false;

            var checkGo = Child(nodeGo, "Check");
            var checkIcon = checkGo.AddComponent<Image>();
            checkIcon.sprite = check;
            checkIcon.color = design.TextMuted;
            checkIcon.raycastTarget = false;
            Place(checkIcon, Vector2.zero, new Vector2(M(13f), M(13f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var starGo = Child(nodeGo, "Star");
            var starIcon = starGo.AddComponent<Image>();
            starIcon.sprite = star;
            starIcon.color = design.TextPrimary;
            starIcon.raycastTarget = false;
            Place(starIcon, Vector2.zero, new Vector2(M(15f), M(15f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var titleLabel = Label(go, "Title", "Звання", design, font,
                design.FontSizeRankRow, design.TextPrimary, TextAlignmentOptions.Left);
            Place(titleLabel, new Vector2(M(62f), M(7f)), new Vector2(M(250f), M(22f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var subtitleLabel = Label(go, "Subtitle", "", design, font,
                design.FontSizeSmall, design.AccentTeal, TextAlignmentOptions.Left);
            Place(subtitleLabel, new Vector2(M(62f), -M(10f)), new Vector2(M(250f), M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var view = go.AddComponent<RankStepView>();
            Wire(view,
                ("design", design), ("node", node), ("nodeStroke", nodeStroke), ("nodeGlow", nodeGlow),
                ("checkIcon", checkIcon), ("starIcon", starIcon),
                ("titleLabel", titleLabel), ("subtitleLabel", subtitleLabel));
            return view;
        }

        // ── Статистика: сітка 2×2 ──
        private static void BuildStats(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite star, ref float y,
            out TMP_Text[] values, out TMP_Text[] labels, out Image[] stars)
        {
            var tileHeight = M(84f);
            var gap = M(10f);
            var height = tileHeight * 2f + gap;

            var go = Child(parent, "Stats");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, y - height);
            rect.offsetMax = new Vector2(-SideMargin, y);
            y -= height + M(12f);

            values = new TMP_Text[4];
            labels = new TMP_Text[4];
            stars = new Image[4];

            for (var i = 0; i < 4; i++)
            {
                var column = i % 2;
                var row = i / 2;

                var tileGo = Child(go, $"Tile{i}");
                var tileRect = tileGo.GetComponent<RectTransform>();
                tileRect.anchorMin = new Vector2(column * 0.5f, 1f);
                tileRect.anchorMax = new Vector2((column + 1) * 0.5f, 1f);
                tileRect.pivot = new Vector2(0.5f, 1f);
                tileRect.offsetMin = new Vector2(column == 0 ? 0f : gap * 0.5f,
                    -row * (tileHeight + gap) - tileHeight);
                tileRect.offsetMax = new Vector2(column == 0 ? -gap * 0.5f : 0f,
                    -row * (tileHeight + gap));

                var fill = tileGo.AddComponent<Image>();
                fill.sprite = rounded;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
                fill.color = new Color(1f, 1f, 1f, 0.045f);
                fill.raycastTarget = false;

                var strokeGo = Child(tileGo, "Stroke");
                Stretch(strokeGo);
                var stroke = strokeGo.AddComponent<Image>();
                stroke.sprite = outline;
                stroke.type = Image.Type.Sliced;
                stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
                stroke.color = new Color(1f, 1f, 1f, 0.09f);
                stroke.raycastTarget = false;

                // ★ перед числом — спрайт, не символ: у Nunito його немає.
                var starGo = Child(tileGo, "Star");
                var starImage = starGo.AddComponent<Image>();
                starImage.sprite = star;
                starImage.raycastTarget = false;
                Place(starImage, new Vector2(M(16f), -M(18f)), new Vector2(M(22f), M(22f)),
                    new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                stars[i] = starImage;

                values[i] = Label(tileGo, "Value", "0", design, font,
                    design.FontSizeProfileStat, design.AccentTeal, TextAlignmentOptions.Left);
                Place(values[i], new Vector2(M(16f), -M(18f)), new Vector2(M(120f), M(34f)),
                    new Vector2(0f, 1f), new Vector2(0f, 0.5f));

                labels[i] = Label(tileGo, "Label", "Підпис", design, font,
                    design.FontSizeLabel, design.TextMuted, TextAlignmentOptions.Left);
                Place(labels[i], new Vector2(M(16f), -M(50f)), new Vector2(M(150f), M(30f)),
                    new Vector2(0f, 1f), new Vector2(0f, 1f));
            }
        }

        // ── Палітра: горизонтальний скрол ──
        private static void BuildPalette(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite gloss, ref float y,
            out TMP_Text caption, out TMP_Text spent, out PaletteSlot[] slots,
            out Button shopButton, out TMP_Text shopLabel)
        {
            var height = M(158f);
            var go = Card(parent, "Palette", rounded, outline, design, ref y, height);

            caption = Caption(go, design, font, "МОЯ ПАЛІТРА");
            spent = Label(go, "Spent", "Витрачено всього: 340 л", design, font,
                design.FontSizeCaption, design.TextFaint, TextAlignmentOptions.Right);
            Place(spent, new Vector2(-M(18f), -M(16f)), new Vector2(M(180f), M(16f)),
                new Vector2(1f, 1f), new Vector2(1f, 1f));

            var rowGo = Child(go, "Row");
            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = new Vector2(0f, -height + M(12f));
            rowRect.offsetMax = new Vector2(0f, -M(42f));

            var scroll = rowGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.scrollSensitivity = 0f;

            var catcher = rowGo.AddComponent<Image>();
            catcher.color = Color.clear;

            var viewportGo = Child(rowGo, "Viewport");
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

            var order = PlayerProfile.PaletteOrder;
            var slotWidth = M(52f);
            var slotGap = M(14f);
            var x = M(18f);

            slots = new PaletteSlot[order.Length];
            for (var i = 0; i < order.Length; i++)
            {
                slots[i] = BuildPaletteSlot(contentGo, design, font, rounded, outline, circle, gloss,
                    new Vector2(x + slotWidth * 0.5f, 0f), slotWidth);
                x += slotWidth + slotGap;
            }

            // «+ Магазин» у кінці — той самий короткий шлях, що на екрані фарбування.
            var shopW = M(66f);
            var shopGo = Child(contentGo, "Shop");
            var shopFill = shopGo.AddComponent<Image>();
            shopFill.sprite = rounded;
            shopFill.type = Image.Type.Sliced;
            shopFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(18f));
            shopFill.color = new Color(1f, 1f, 1f, 0.05f);
            Place(shopFill, new Vector2(x + shopW * 0.5f, 0f), new Vector2(shopW, M(96f)),
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));

            var plusGo = Child(shopGo, "Plus");
            var plus = plusGo.AddComponent<GradientImage>();
            plus.sprite = circle;
            plus.SetGradient(design.AccentGold, design.AccentPrimary);
            plus.raycastTarget = false;
            Place(plus, new Vector2(0f, M(12f)), new Vector2(M(34f), M(34f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var plusGlyph = Label(plusGo, "Sign", "+", design, font,
                M(22f), design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(plusGlyph.gameObject);

            shopLabel = Label(shopGo, "Label", "Магазин", design, font,
                design.FontSizeSmall, design.TextMuted, TextAlignmentOptions.Center);
            Place(shopLabel, new Vector2(0f, -M(22f)), new Vector2(shopW, M(16f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            shopButton = shopGo.AddComponent<Button>();
            shopButton.targetGraphic = shopFill;

            content.sizeDelta = new Vector2(x + shopW + M(18f), 0f);
        }

        private static PaletteSlot BuildPaletteSlot(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle, Sprite gloss,
            Vector2 position, float width)
        {
            var go = Child(parent, "PaletteSlot");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, M(96f));

            var dropGo = Child(go, "Drop");
            var drop = dropGo.AddComponent<GradientImage>();
            drop.sprite = circle;
            drop.raycastTarget = false;
            Place(drop, new Vector2(0f, -M(2f)), new Vector2(M(34f), M(34f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));

            var glossGo = Child(dropGo, "Gloss");
            Stretch(glossGo);
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = gloss;
            glossImage.raycastTarget = false;

            // Мензурка 22×34 — той самий компонент, що в картці магазину.
            var tubeGo = Child(go, "Beaker");
            var tubeBackground = tubeGo.AddComponent<Image>();
            tubeBackground.sprite = rounded;
            tubeBackground.type = Image.Type.Sliced;
            tubeBackground.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(6f));
            tubeBackground.raycastTarget = false;
            Place(tubeBackground, new Vector2(0f, -M(42f)), new Vector2(M(22f), M(34f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var tubeStrokeGo = Child(tubeGo, "Stroke");
            Stretch(tubeStrokeGo);
            var tubeStroke = tubeStrokeGo.AddComponent<Image>();
            tubeStroke.sprite = outline;
            tubeStroke.type = Image.Type.Sliced;
            tubeStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(6f));
            tubeStroke.raycastTarget = false;

            var clipGo = Child(tubeGo, "Clip");
            Stretch(clipGo, design.ShopBeakerInset);
            clipGo.AddComponent<RectMask2D>();

            var fillGo = Child(clipGo, "Fill");
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.sprite = rounded;
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(5f));
            fillImage.raycastTarget = false;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            var beaker = tubeGo.AddComponent<BeakerGauge>();
            Wire(beaker,
                ("design", design), ("background", tubeBackground), ("stroke", tubeStroke),
                ("fill", fillRect), ("fillImage", fillImage));

            var nameLabel = Label(go, "Name", "Фарба", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(nameLabel, new Vector2(0f, -M(80f)), new Vector2(width + M(10f), M(14f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var liters = Label(go, "Liters", "0 л", design, font,
                design.FontSizeCaption, design.TextFaint, TextAlignmentOptions.Center);
            Place(liters, new Vector2(0f, -M(94f)), new Vector2(width, M(14f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var slot = go.AddComponent<PaletteSlot>();
            Wire(slot,
                ("design", design), ("drop", drop), ("gloss", glossImage), ("beaker", beaker),
                ("nameLabel", nameLabel), ("litersLabel", liters));
            return slot;
        }

        // ── Вітрина й досягнення (у макеті це одна картка з роздільником) ──
        private static void BuildShowcaseAndBadges(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite circle, Sprite quad,
            Sprite glowSprite, Shader planetShader, ref float y,
            out TMP_Text showcaseCaption, out Image showcasePlanet, out TMP_Text showcaseName,
            out Button[] thumbButtons, out Image[] thumbPlanets, out Image[] thumbRings,
            out TMP_Text achievementsCaption, out AchievementBadge[] badges,
            out RectTransform toast, out TMP_Text toastLabel)
        {
            var height = M(430f);
            var go = Card(parent, "Showcase", rounded, outline, design, ref y, height);

            showcaseCaption = Caption(go, design, font, "ВІТРИНА");

            var planetSize = M(150f);
            var planetGo = Child(go, "Planet");
            showcasePlanet = planetGo.AddComponent<Image>();
            showcasePlanet.sprite = quad;
            showcasePlanet.raycastTarget = false;
            Place(showcasePlanet, new Vector2(0f, -M(42f)), new Vector2(planetSize, planetSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));

            showcaseName = Label(go, "Name", "Моя гордість · Аквіла", design, font,
                design.FontSizeShopPrice, design.TextPrimary, TextAlignmentOptions.Center);
            Place(showcaseName, new Vector2(0f, -M(204f)), new Vector2(M(300f), M(20f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            const int thumbCount = 3;
            var thumbSize = M(44f);
            var thumbGap = M(12f);
            thumbButtons = new Button[thumbCount];
            thumbPlanets = new Image[thumbCount];
            thumbRings = new Image[thumbCount];

            var startX = -(thumbCount - 1) * 0.5f * (thumbSize + thumbGap);
            for (var i = 0; i < thumbCount; i++)
            {
                var thumbGo = Child(go, $"Thumb{i}");
                var hit = thumbGo.AddComponent<Image>();
                hit.color = Color.clear;
                Place(hit, new Vector2(startX + i * (thumbSize + thumbGap), -M(232f)),
                    new Vector2(thumbSize, thumbSize), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

                var planetImageGo = Child(thumbGo, "Planet");
                Stretch(planetImageGo, M(2f));
                var planetImage = planetImageGo.AddComponent<Image>();
                planetImage.sprite = quad;
                planetImage.raycastTarget = false;

                var ringGo = Child(thumbGo, "Ring");
                Stretch(ringGo);
                var ring = ringGo.AddComponent<Image>();
                ring.sprite = LoadSprite("circle-outline");
                ring.raycastTarget = false;

                thumbButtons[i] = thumbGo.AddComponent<Button>();
                thumbButtons[i].targetGraphic = hit;
                thumbPlanets[i] = planetImage;
                thumbRings[i] = ring;
            }

            // Роздільник між вітриною й досягненнями.
            var dividerGo = Child(go, "Divider");
            var divider = dividerGo.AddComponent<Image>();
            divider.color = new Color(1f, 1f, 1f, 0.09f);
            divider.raycastTarget = false;
            Place(divider, new Vector2(0f, -M(272f)), new Vector2(M(318f), M(1f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            achievementsCaption = Label(go, "AchievementsCaption", "ДОСЯГНЕННЯ", design, font,
                design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.Left);
            Place(achievementsCaption, new Vector2(M(18f), -M(288f)), new Vector2(M(200f), M(16f)),
                new Vector2(0f, 1f), new Vector2(0f, 1f));

            const int badgeCount = 4;
            var badgeWidth = M(64f);
            var badgeGap = M(10f);
            badges = new AchievementBadge[badgeCount];
            var badgeStart = -(badgeCount - 1) * 0.5f * (badgeWidth + badgeGap);
            for (var i = 0; i < badgeCount; i++)
                badges[i] = BuildBadge(go, design, font, circle, glowSprite, i,
                    new Vector2(badgeStart + i * (badgeWidth + badgeGap), -M(316f)), badgeWidth);

            // Підказка про умову — над бейджами, з'являється тапом по замкненому.
            var toastGo = Child(go, "Toast");
            var toastFill = toastGo.AddComponent<Image>();
            toastFill.sprite = rounded;
            toastFill.type = Image.Type.Sliced;
            toastFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(14f));
            toastFill.color = design.GlassFillRaised;
            toastFill.raycastTarget = false;
            toast = toastGo.GetComponent<RectTransform>();
            Place(toastFill, new Vector2(0f, -M(404f)), new Vector2(M(318f), M(34f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            toastLabel = Label(toastGo, "Label", "", design, font,
                design.FontSizeSmall, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(toastLabel.gameObject, M(8f));
        }

        private static AchievementBadge BuildBadge(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite glowSprite, int index,
            Vector2 position, float width)
        {
            // Іконки беремо з наявних: своїх спрайтів під кожне досягнення
            // поки немає, а ці за змістом близькі.
            var iconNames = new[] { "icon-galaxy", "circle-soft", "icon-shop", "icon-ranks" };
            var circleSize = M(52f);

            var go = Child(parent, $"Badge{index}");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, M(78f));

            var circleGo = Child(go, "Circle");
            var badgeCircle = circleGo.AddComponent<GradientImage>();
            badgeCircle.sprite = circle;
            Place(badgeCircle, Vector2.zero, new Vector2(circleSize, circleSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));

            var glowGo = Child(circleGo, "Glow");
            Stretch(glowGo, -M(8f));
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = glowSprite;
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = GenerateUISprites.GlowFalloff / M(8f);
            glow.raycastTarget = false;
            glowGo.transform.SetAsFirstSibling();

            var iconGo = Child(circleGo, "Icon");
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = LoadSprite(iconNames[index % iconNames.Length]);
            icon.raycastTarget = false;
            Place(icon, Vector2.zero, new Vector2(M(26f), M(26f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var lockGo = Child(circleGo, "Lock");
            var lockFill = lockGo.AddComponent<Image>();
            lockFill.sprite = circle;
            lockFill.color = new Color(0.051f, 0.027f, 0.086f, 1f);
            lockFill.raycastTarget = false;
            Place(lockFill, Vector2.zero, new Vector2(M(19f), M(19f)),
                new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));

            // Замок малюємо тими самими прямокутниками, що й на замкненій планеті
            // в Галактиці: жодних символів, які може не мати шрифт.
            var lockBodyGo = Child(lockGo, "Body");
            var lockBody = lockBodyGo.AddComponent<Image>();
            lockBody.sprite = LoadSprite("rounded-rect");
            lockBody.type = Image.Type.Sliced;
            lockBody.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(2f));
            lockBody.color = design.TextMuted;
            lockBody.raycastTarget = false;
            Place(lockBody, new Vector2(0f, -M(1.5f)), new Vector2(M(9f), M(6f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var shackleGo = Child(lockGo, "Shackle");
            var shackle = shackleGo.AddComponent<Image>();
            shackle.sprite = LoadSprite("rounded-rect-outline");
            shackle.type = Image.Type.Sliced;
            shackle.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(3f));
            shackle.color = design.TextMuted;
            shackle.raycastTarget = false;
            Place(shackle, new Vector2(0f, M(3f)), new Vector2(M(6f), M(7f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var nameLabel = Label(go, "Name", "Досягнення", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(nameLabel, new Vector2(0f, -M(56f)), new Vector2(width + M(8f), M(28f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var button = go.AddComponent<Button>();
            button.targetGraphic = badgeCircle;

            var badge = go.AddComponent<AchievementBadge>();
            Wire(badge,
                ("design", design), ("button", button), ("circle", badgeCircle), ("glow", glow),
                ("icon", icon), ("lockBadge", lockGo.GetComponent<RectTransform>()),
                ("nameLabel", nameLabel));
            return badge;
        }

        // ── Спільні дрібниці ──

        /// <summary>Секція в потоці вмісту: посуває курсор y на свою висоту.</summary>
        private static GameObject Section(GameObject parent, string name, ref float y,
            float height, float spacing)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, y - height);
            rect.offsetMax = new Vector2(0f, y);
            y -= height + spacing;
            return go;
        }

        /// <summary>Велика скляна картка профілю з обведенням.</summary>
        private static GameObject Card(GameObject parent, string name, Sprite rounded,
            Sprite outline, DesignSystem design, ref float y, float height)
        {
            var go = Section(parent, name, ref y, height, M(12f));
            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(SideMargin, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-SideMargin, rect.offsetMax.y);

            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(CardRadius);
            fill.color = new Color(1f, 1f, 1f, 0.045f);
            fill.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(CardRadius);
            stroke.color = new Color(1f, 1f, 1f, 0.09f);
            stroke.raycastTarget = false;
            _ = design;
            return go;
        }

        /// <summary>Дрібний розріджений заголовок секції: 11/800 ls .14em.</summary>
        private static TMP_Text Caption(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, string text)
        {
            var label = Label(parent, "Caption", text, design, font,
                design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.Left);
            Place(label, new Vector2(M(18f), -M(16f)), new Vector2(M(220f), M(16f)),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            return label;
        }
    }
}
