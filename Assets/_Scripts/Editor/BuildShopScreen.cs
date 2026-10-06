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
    /// Збирає магазин (майстер-док §13): один екран, чотири пакети нафти сіткою 2×2, без вкладок,
    /// банерів і наборів. Меню: Ink Flow → Setup → Build Shop Screen.
    ///
    /// Кожне число — px макета × K, K = 1080/390 ≈ 2.769. Картки пакетів: крапля зростає з пакетом
    /// (токени `oilDropSizes`), на кнопці — ціна зі стору (у префабі риска: ціни в грі немає).
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

        private static readonly float SideMargin = M(18f);

        /// <summary>Карток у пулі: чотири пакети §13 плюс запас на випадок, якщо конфіг додасть п'ятий.</summary>
        private const int PackCards = 6;

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
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (circleOutline == null) missing.Add($"{SpriteFolder}/circle-outline.png");
            if (gloss == null) missing.Add($"{SpriteFolder}/circle-gloss.png");
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

            var headerHeight = BuildHeader(screenGo, design!, font, circle!, circleOutline!, currencyPrefab!,
                out var backButton, out var title, out var currency, out var settingsButton);
            BuildPacks(screenGo, design!, font, rounded!, outline!, circle!, gloss!, headerHeight,
                out var promise, out var packs, out var status);

            Wire(screen,
                ("design", design!), ("backButton", backButton), ("title", title), ("settingsButton", settingsButton),
                ("currency", currency), ("promise", promise), ("statusLabel", status));
            WireArray(screen, "packs", packs);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Магазин зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ── Шапка: padding 0 18 12; «МАГАЗИН» 15/800 ls .18em; капсула валюти праворуч ──
        private static float BuildHeader(GameObject parent, DesignSystem design,
            TMP_FontAsset? font, Sprite circle, Sprite circleOutline, GameObject currencyPrefab,
            out Button backButton, out TMP_Text title, out CurrencyWidget currency, out Button settingsButton)
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

            // Заголовок центруємо у ВІЛЬНОМУ проміжку між кнопкою «‹» і капсулою валюти, а не в усій
            // шапці: інакше він з'їжджає праворуч і лізе під капсулу. Числа — з ширини цих блоків.
            title = Label(go, "Title", "МАГАЗИН", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(-M(53f), 0f), new Vector2(M(160f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, go.transform);
            currency = currencyGo.GetComponent<CurrencyWidget>();
            // Шестерня (§15) — крайня праворуч, капсула валюти зсувається ліворуч і трохи вужчає.
            Place(currency, new Vector2(-GearSlot, 0f), new Vector2(M(104f), M(40f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            settingsButton = GearButton(go, design, circle, circleOutline, LoadSprite("icon-gear"));

            return height;
        }

        // ── Пакети: обіцянка, сітка 2×2 (картка 165×160, проміжок 12), рядок стану ──
        private static void BuildPacks(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite gloss, float headerHeight,
            out TMP_Text promise, out OilPackCard[] packs, out TMP_Text status)
        {
            var go = Child(parent, "Packs");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -(headerHeight + M(14f)));

            var y = -M(2f);
            promise = Label(go, "Promise",
                "Нафта прискорює красу, а не силу.", design, font,
                design.FontSizeCardSubtitle, design.TextMuted, TextAlignmentOptions.Center);
            Place(promise, new Vector2(0f, y), new Vector2(M(340f), M(44f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            promise.textWrappingMode = TextWrappingModes.Normal;
            y -= M(60f);

            // Картка 165×190: крапля найбільшого пакета (88 px) висить під бейджем і не доходить до суми.
            var packWidth = (M(354f) - M(12f)) * 0.5f;
            var packHeight = M(190f);
            packs = new OilPackCard[PackCards];
            for (var i = 0; i < PackCards; i++)
            {
                var column = i % 2;
                var row = i / 2;
                var x = SideMargin + column * (packWidth + M(12f));
                packs[i] = BuildOilPack(go, design, font, rounded, outline, circle, gloss,
                    new Vector2(x, y - row * (packHeight + M(12f))), new Vector2(packWidth, packHeight));
                // Запасні картки сплять: їх вмикає екран, якщо конфіг дасть більше пакетів.
                packs[i].gameObject.SetActive(i < OilPack.Defaults().Length);
            }
            y -= 2f * (packHeight + M(12f)) + M(8f);

            status = Label(go, "Status", "Магазин недоступний — спробуй пізніше", design, font,
                design.FontSizeCaption, design.TextMuted, TextAlignmentOptions.Center);
            Place(status, new Vector2(0f, y), new Vector2(M(340f), M(40f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            status.textWrappingMode = TextWrappingModes.Normal;
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
            Place(badgeFill, new Vector2(0f, M(9f)), new Vector2(M(110f), M(20f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var badgeLabel = Label(badgeGo, "Label", "Популярне", design, font,
                design.FontSizeCaption, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(badgeLabel.gameObject);

            // Крапля висить ВСЕРЕДИНІ картки під бейджем (pivot згори): з pivot знизу вона стирчала б над
            // карткою й накривала суму сусідньої. Розмір ставить екран з токенів `oilDropSizes`.
            var dropGo = Child(go, "Drop");
            var dropImage = dropGo.AddComponent<GradientImage>();
            dropImage.sprite = circle;
            dropImage.raycastTarget = false;
            dropImage.SetGradient(design.AccentSecondary, design.BackgroundEdge);
            Place(dropImage, new Vector2(0f, -M(16f)), new Vector2(M(58f), M(63f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            var drop = dropGo.GetComponent<RectTransform>();

            var glossGo = Child(dropGo, "Gloss");
            Stretch(glossGo);
            var glossImage = glossGo.AddComponent<Image>();
            glossImage.sprite = gloss;
            glossImage.raycastTarget = false;

            var amount = Label(go, "Amount", "1 000", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            // Сума — над кнопкою з запасом: кнопка сягає 50 px макета, нижній край суми — 51.
            Place(amount, new Vector2(0f, M(62f)), new Vector2(size.x, M(22f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            var buyGo = Child(go, "Buy");
            var buyFill = buyGo.AddComponent<GradientImage>();
            buyFill.sprite = rounded;
            buyFill.type = Image.Type.Sliced;
            buyFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(16f));
            Place(buyFill, new Vector2(0f, M(14f)), new Vector2(size.x - M(48f), M(36f)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            // Ціна в префабі — риска: справжню дає стор у рантаймі.
            var priceLabel = Label(buyGo, "Price", "—", design, font,
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
    }
}
