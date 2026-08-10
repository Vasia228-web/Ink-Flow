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
    /// Збирає екран партії за розміткою макета «Ink Flow v2».
    /// Меню: Ink Flow → Setup → Build Level Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Полотно поля — квадрат 358,
    /// вписаний у ширину екрана; клітинку рахує <see cref="BoardGeometry"/>, тож
    /// 4×4 і 7×7 займають однакове місце й HUD між рівнями не стрибає.
    /// </summary>
    public static class BuildLevelScreen
    {
        private const string ScenePath = "Assets/Scenes/Level.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        /// <summary>Бічні поля екрана партії — 16, вужчі за решту екранів заради поля.</summary>
        private static readonly float SideMargin = M(16f);

        [MenuItem("Ink Flow/Setup/Build Level Screen")]
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
            var retry = LoadSprite("icon-retry");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var dropPrefab = AssetDatabase.LoadAssetAtPath<DropView>($"{PrefabFolder}/DropView.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"),
                (circle, "circle-soft"), (circleOutline, "circle-outline"),
                (nebula, "nebula"), (retry, "icon-retry")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (dropPrefab == null) missing.Add($"{PrefabFolder}/DropView.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Екран партії НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "LevelScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<LevelScreen>();

            var headerHeight = M(40f);
            var statsTop = headerHeight + M(12f);
            var statsHeight = M(62f);

            BuildHeader(screenGo, design!, font, circle!, circleOutline!, retry!,
                headerHeight, out var backButton, out var title, out var restartButton);

            BuildStats(screenGo, design!, font, rounded!, outline!, nebula!,
                statsTop, statsHeight,
                out var movesCapsule, out var movesStroke, out var movesGlow,
                out var movesLabel, out var movesNumber,
                out var goalCapsule, out var goalStroke, out var goalLabel, out var goalText);

            var board = BuildBoard(screenGo, design!, dropPrefab!,
                statsTop + statsHeight + M(10f));

            BuildOutcome(screenGo, design!, font, rounded!, outline!, circle!,
                out var outcomeCard, out var outcomeFill, out var outcomeTitle, out var outcomeNote,
                out var outcomeStars, out var primary, out var primaryLabel,
                out var secondary, out var secondaryLabel);

            Wire(screen,
                ("design", design!),
                ("backButton", backButton), ("title", title), ("restartButton", restartButton),
                ("movesCapsule", movesCapsule), ("movesCapsuleStroke", movesStroke),
                ("movesCapsuleGlow", movesGlow), ("movesLabel", movesLabel),
                ("movesNumber", movesNumber),
                ("goalCapsule", goalCapsule), ("goalCapsuleStroke", goalStroke),
                ("goalLabel", goalLabel), ("goalText", goalText),
                ("board", board),
                ("outcomeCard", outcomeCard), ("outcomeFill", outcomeFill),
                ("outcomeTitle", outcomeTitle), ("outcomeNote", outcomeNote),
                ("outcomePrimary", primary), ("outcomePrimaryLabel", primaryLabel),
                ("outcomeSecondary", secondary), ("outcomeSecondaryLabel", secondaryLabel));
            WireArray(screen, "outcomeStars", outcomeStars);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Екран партії зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: ‹ · РІВЕНЬ N · ↺ ──
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
            // ↺ — спрайт, не гліф: у Nunito його немає, і TMP мовчки ставив би квадрат.
            var retryGo = Child(restartRoot, "Glyph");
            var retryIcon = retryGo.AddComponent<Image>();
            retryIcon.sprite = retry;
            retryIcon.color = design.TextPrimary;
            retryIcon.raycastTarget = false;
            Place(retryIcon, Vector2.zero, new Vector2(M(20f), M(20f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // Кнопки симетричні, тож заголовок центрується по всій шапці —
            // на відміну від екранів із капсулою валюти праворуч.
            title = Label(go, "Title", "РІВЕНЬ 1", design, font,
                design.FontSizeGameTitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(220f), M(20f)),
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

        // ── Дві капсули: ХОДИ (flex 1) і ЦІЛЬ (flex 1.4), проміжок 10 ──
        private static void BuildStats(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, float top, float height,
            out Image movesCapsule, out Image movesStroke, out Image movesGlow,
            out TMP_Text movesLabel, out TMP_Text movesNumber,
            out Image goalCapsule, out Image goalStroke, out TMP_Text goalLabel, out TMP_Text goalText)
        {
            var go = Child(parent, "Stats");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SideMargin, -top - height);
            rect.offsetMax = new Vector2(-SideMargin, -top);

            // Ширини рахуємо самі, а не через LayoutGroup: розкладка статична,
            // а layout-компонент коштував би перебудови на кожен показ числа.
            var total = M(390f) - SideMargin * 2f;
            var gap = M(10f);
            var movesWidth = Mathf.Round((total - gap) / 2.4f);
            var goalWidth = total - gap - movesWidth;

            movesCapsule = Capsule(go, "Moves", design, rounded, outline, nebula,
                new Vector2(0f, 0.5f), movesWidth, height, out movesStroke, out movesGlow);

            movesLabel = Label(movesCapsule.gameObject, "Label", "ХОДИ", design, font,
                design.FontSizeStatLabel, design.TextDim, TextAlignmentOptions.Center);
            Place(movesLabel, new Vector2(0f, -M(9f)), new Vector2(movesWidth, M(14f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            movesNumber = Label(movesCapsule.gameObject, "Number", "12", design, font,
                design.FontSizeMovesNumber, design.TextPrimary, TextAlignmentOptions.Center);
            Place(movesNumber, new Vector2(0f, M(9f)), new Vector2(movesWidth, M(30f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            goalCapsule = Capsule(go, "Goal", design, rounded, outline, null,
                new Vector2(1f, 0.5f), goalWidth, height, out goalStroke, out _);

            goalLabel = Label(goalCapsule.gameObject, "Label", "ЦІЛЬ", design, font,
                design.FontSizeStatLabel, design.TextDim, TextAlignmentOptions.Center);
            Place(goalLabel, new Vector2(0f, -M(9f)), new Vector2(goalWidth, M(14f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            goalText = Label(goalCapsule.gameObject, "Text", "Розчисти сітку", design, font,
                design.FontSizeGoal, design.TextPrimary, TextAlignmentOptions.Center);
            Place(goalText, new Vector2(0f, M(9f)), new Vector2(goalWidth - M(16f), M(30f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        }

        private static Image Capsule(GameObject parent, string name, DesignSystem design,
            Sprite rounded, Sprite outline, Sprite? glowSprite, Vector2 anchor,
            float width, float height, out Image stroke, out Image glow)
        {
            var go = Child(parent, name);
            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            fill.color = design.StatCapsuleFill;
            fill.raycastTarget = false;
            Place(fill, Vector2.zero, new Vector2(width, height), anchor, anchor);

            // Гало ПІД капсулою: воно радіальне, тож для прямокутної капсули
            // розтягується ширше за неї й читається як м'яка тінь-підсвітка.
            glow = null!;
            if (glowSprite != null)
            {
                var glowGo = Child(go, "Glow");
                Stretch(glowGo, -M(14f));
                glow = glowGo.AddComponent<Image>();
                glow.sprite = glowSprite;
                glow.color = design.MovesCalmGlow;
                glow.raycastTarget = false;
                glowGo.transform.SetAsFirstSibling();
            }

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            stroke.color = design.StatCapsuleStroke;
            stroke.raycastTarget = false;

            return fill;
        }

        // ── Поле: квадрат BoardGeometry.Canvas, вписаний у ширину ──
        private static BoardView BuildBoard(GameObject parent, DesignSystem design,
            DropView dropPrefab, float top)
        {
            var go = Child(parent, "Board");
            var rect = go.GetComponent<RectTransform>();
            var side = M(BoardGeometry.Canvas);

            // У макеті поле стоїть у `flex:1; align-items:center` — центрується
            // у вільному місці під HUD, а не притискається до нього. Стрибків
            // між рівнями це не дає: полотно завжди 358, незалежно від сітки —
            // за це відповідає BoardGeometry.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-side * 0.5f, 0f);
            rect.offsetMax = new Vector2(side * 0.5f, -top);

            // Прозорий ловець: жест веде дошка цілком, а не кожна крапля окремо.
            var catcher = go.AddComponent<Image>();
            catcher.color = Color.clear;

            var canvasGo = Child(go, "Canvas");
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            // Півот у ЛІВОМУ ВЕРХНЬОМУ куті: BoardGeometry рахує центри клітинок
            // саме звідти, тож координати лягають без жодного перерахунку.
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0f, 1f);
            canvasRect.pivot = new Vector2(0f, 1f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = new Vector2(side, side);

            var audioGo = Child(go, "Feedback");
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var feedback = audioGo.AddComponent<BoardFeedback>();
            Wire(feedback, ("source", audio));

            var pool = go.AddComponent<DropPool>();
            Wire(pool, ("prefab", dropPrefab), ("contentRoot", canvasRect));

            var board = go.AddComponent<BoardView>();
            Wire(board, ("design", design), ("pool", pool), ("canvasRect", canvasRect),
                ("feedback", feedback));
            return board;
        }

        // ── Картка підсумку ──
        private static void BuildOutcome(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle,
            out RectTransform card, out GradientImage fill, out TMP_Text title, out TMP_Text note,
            out Image[] stars, out Button primary, out TMP_Text primaryLabel,
            out Button secondary, out TMP_Text secondaryLabel)
        {
            var go = Child(parent, "Outcome");
            Stretch(go);
            card = go.GetComponent<RectTransform>();

            var scrim = go.AddComponent<Image>();
            scrim.color = new Color(0.031f, 0.016f, 0.071f, 0.62f);

            var panelGo = Child(go, "Panel");
            fill = panelGo.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            Place(fill, new Vector2(0f, M(24f)), new Vector2(M(340f), M(230f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            var strokeGo = Child(panelGo, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(28f));
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;

            title = Label(panelGo, "Title", "Рівень пройдено", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -M(20f)), new Vector2(M(300f), M(26f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var starsGo = Child(panelGo, "Stars");
            stars = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var pipGo = Child(starsGo, $"Pip{i}");
                var pip = pipGo.AddComponent<Image>();
                pip.sprite = circle;
                pip.raycastTarget = false;
                Place(pip, new Vector2((i - 1) * M(28f), 0f), new Vector2(M(20f), M(24f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                stars[i] = pip;
            }
            var starsRect = starsGo.GetComponent<RectTransform>();
            starsRect.anchorMin = starsRect.anchorMax = new Vector2(0.5f, 1f);
            starsRect.pivot = new Vector2(0.5f, 0.5f);
            starsRect.anchoredPosition = new Vector2(0f, -M(58f));
            starsRect.sizeDelta = new Vector2(M(110f), M(26f));

            note = Label(panelGo, "Note", "", design, font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
            Place(note, new Vector2(0f, -M(96f)), new Vector2(M(300f), M(36f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var primaryGo = Child(panelGo, "Primary");
            var primaryFill = primaryGo.AddComponent<GradientImage>();
            primaryFill.sprite = rounded;
            primaryFill.type = Image.Type.Sliced;
            primaryFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(22f));
            primaryFill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(primaryFill, new Vector2(0f, M(58f)), new Vector2(M(290f), M(50f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            primaryLabel = Label(primaryGo, "Label", "Далі", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(primaryLabel.gameObject);

            primary = primaryGo.AddComponent<Button>();
            primary.targetGraphic = primaryFill;

            var secondaryGo = Child(panelGo, "Secondary");
            var secondaryFill = secondaryGo.AddComponent<Image>();
            secondaryFill.color = Color.clear;
            Place(secondaryFill, new Vector2(0f, M(18f)), new Vector2(M(290f), M(34f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            secondaryLabel = Label(secondaryGo, "Label", "До карти", design, font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
            Stretch(secondaryLabel.gameObject);

            secondary = secondaryGo.AddComponent<Button>();
            secondary.targetGraphic = secondaryFill;

            go.SetActive(false);
        }
    }
}
