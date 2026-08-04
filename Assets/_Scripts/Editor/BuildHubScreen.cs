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

namespace InkFlow.Editor
{
    /// <summary>
    /// Збирає хаб-меню за розміткою макета «Ink Flow v2» (полотно 390×844).
    /// Меню: Ink Flow → Setup → Build Hub Screen.
    ///
    /// Кожне число нижче — це px макета × K, де K = 1080/390 ≈ 2.769. Джерело значень
    /// вказане в коментарі біля кожної групи, щоб при наступній звірці з макетом
    /// не доводилось шукати їх удруге.
    ///
    /// Прив'язки: шапка до верху SafeArea, навігація до низу SafeArea, середина
    /// розтягується. Тому на 4:3 і 20:9 порожній простір змінюється посередині,
    /// а блоки лишаються на своїх краях.
    /// </summary>
    public static class BuildHubScreen
    {
        private const string ScenePath = "Assets/Scenes/Hub.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string SpriteFolder = "Assets/_Sprites/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        /// <summary>Спрайт-асет іконок вішаємо на кожен напис явно: покладатись на
        /// TMP Settings ризиковано — одна забута галочка й ★ знову стає квадратом.</summary>
        private static TMP_SpriteAsset? _iconSprites;

        // ── Числа макета (px) → reference-одиниці ──
        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        [MenuItem("Ink Flow/Setup/Build Hub Screen")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            // Поки збираємо — жодних відкладених перечитувань стилю: ApplyScaler()
            // і Apply() кличемо самі, синхронно, перед SaveScene.
            StyleRefresh.Suspended = true;
            try { BuildScene(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildScene()
        {
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            if (design == null)
            {
                Debug.LogError($"[InkFlow] Немає {DesignSystemPath}. Спершу: Ink Flow → Setup → Build UI Kit.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Асети — строго після NewScene (див. коментар у InkFlowBootstrap).
            design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _iconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            if (_iconSprites == null)
                Debug.LogWarning($"[InkFlow] Немає {SpriteAssetPath} — ★ не відрендериться. " +
                                 "Спершу: Ink Flow → Setup → Build UI Kit.");
            var rounded = LoadSprite("rounded-rect");
            var outline = LoadSprite("rounded-rect-outline");
            var circle = LoadSprite("circle-soft");
            var gloss = LoadSprite("circle-gloss");
            var glowSprite = LoadSprite("glow");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/DropView.prefab");
            var currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CurrencyWidget.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (rounded == null) missing.Add($"{SpriteFolder}/rounded-rect.png");
            if (outline == null) missing.Add($"{SpriteFolder}/rounded-rect-outline.png");
            if (circle == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (gloss == null) missing.Add($"{SpriteFolder}/circle-gloss.png");
            if (glowSprite == null) missing.Add($"{SpriteFolder}/glow.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (dropPrefab == null) missing.Add($"{PrefabFolder}/DropView.prefab");
            if (currencyPrefab == null) missing.Add($"{PrefabFolder}/CurrencyWidget.prefab");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Хаб НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
                return;
            }

            CreateCamera(design!);

            var canvasGo = new GameObject("UI Root");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);
            BuildNebula(canvasGo, design!, circle!);

            var safe = Child(canvasGo, "SafeArea");
            Stretch(safe);
            var safeBinder = safe.AddComponent<SafeAreaBinder>();
            var navigation = safe.AddComponent<NavigationStack>();

            var uiRoot = canvasGo.AddComponent<UIRoot>();
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler),
                ("navigation", navigation), ("safeArea", safeBinder));

            var screenGo = Child(safe, "HubScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<HubScreen>();

            var header = BuildHeader(screenGo, design!, font, dropPrefab!, currencyPrefab!,
                out var avatar, out var nameLabel, out var titleLabel, out var currency);
            var nav = BuildNavBar(screenGo, design!, font, rounded!, outline!, out var navBar);
            var middle = BuildMiddle(screenGo, design!, font, rounded!, outline!, glowSprite!, circle!,
                header, nav, out var logo, out var tagline, out var levels, out var endless);
            _ = middle;

            Wire(screen,
                ("design", design!), ("avatar", avatar), ("playerName", nameLabel),
                ("playerTitle", titleLabel), ("currency", currency), ("logo", logo),
                ("tagline", tagline), ("levelsCard", levels), ("endlessCard", endless),
                ("navBar", navBar));

            // Пул краплин — над контентом, щоб краплі падали поверх карток.
            var dripsGo = Child(screenGo, "Drips");
            Stretch(dripsGo);
            var drips = dripsGo.AddComponent<DripPool>();
            Wire(drips, ("design", design!), ("dropSprite", circle!));
            dripsGo.transform.SetAsLastSibling();

            // Аватар капає; лого — теж, але рідше й у два кольори.
            var avatarSo = new SerializedObject(avatar);
            avatarSo.FindProperty("dripPool").objectReferenceValue = drips;
            avatarSo.ApplyModifiedPropertiesWithoutUndo();

            BuildLogoDrips(screenGo, design!, drips, logo);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Стилі застосовуємо тут, синхронно й перед збереженням. Покладатись на
            // OnValidate більше не можна: він відкладений на наступний кадр (див.
            // StyleRefresh), тож у файл сцени потрапили б дефолтні значення.
            uiRoot.ApplyScaler();
            screen.Apply();

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Хаб зібрано: {ScenePath} (коефіцієнт {K:0.000}).");
        }

        // ───────────────────────── Шапка ─────────────────────────
        // Макет: padding 54/20, аватар 44, gap 11, «Нова» 16/800, підпис 11/700 α.42,
        // капсула валюти r22 з padding 6/15/6/7 і gap 9, куля 32×34, число 17/800.

        private static RectTransform BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            GameObject dropPrefab, GameObject currencyPrefab,
            out DropView avatar, out TMP_Text nameLabel, out TMP_Text titleLabel, out CurrencyWidget currency)
        {
            var side = M(20f);          // 55
            var avatarSize = M(44f);    // 122

            var header = Child(parent, "Header");
            var rect = header.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(side, 0f);
            rect.offsetMax = new Vector2(-side, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, M(46f)); // висота капсули валюти
            rect.anchoredPosition = Vector2.zero;                    // до верху SafeArea, offset 0

            // Аватар-крапля (маджента з глянцем) — лівий край шапки.
            var avatarGo = (GameObject)PrefabUtility.InstantiatePrefab(dropPrefab, header.transform);
            avatarGo.name = "Avatar";
            var avatarRect = avatarGo.GetComponent<RectTransform>();
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = Vector2.zero;
            avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);
            avatar = avatarGo.GetComponent<DropView>();
            avatar.Show(InkColor.Magenta, 0); // 0 = без числа густоти
            avatar.Apply();

            // Два рядки тексту праворуч від аватара: gap 11 → 30.
            var textGo = Child(header, "Identity");
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(avatarSize + M(11f), 0f);
            textRect.sizeDelta = new Vector2(M(200f), avatarSize);

            // line-height 1.15 при 16 px → рядки на 44 і 30 з проміжком у 3 px макета.
            nameLabel = Label(textGo, "Name", "Нова", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.BottomLeft);
            Place(nameLabel, new Vector2(0f, M(2f)), new Vector2(M(200f), M(22f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            titleLabel = Label(textGo, "Title", "Художниця галактик", design, font,
                design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.TopLeft);
            Place(titleLabel, new Vector2(0f, -M(15f)), new Vector2(M(200f), M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            // Капсула валюти — правий край шапки.
            var currencyGo = (GameObject)PrefabUtility.InstantiatePrefab(currencyPrefab, header.transform);
            currencyGo.name = "CurrencyWidget";
            var currencyRect = currencyGo.GetComponent<RectTransform>();
            currencyRect.anchorMin = currencyRect.anchorMax = new Vector2(1f, 0.5f);
            currencyRect.pivot = new Vector2(1f, 0.5f);
            currencyRect.anchoredPosition = Vector2.zero;
            currencyRect.sizeDelta = new Vector2(M(118f), M(46f)); // 327×127
            currency = currencyGo.GetComponent<CurrencyWidget>();
            currency.SetPreviewAmount(1250);
            currency.Apply();

            return rect;
        }

        // ───────────────────────── Середина ─────────────────────────
        // Макет: flex:1, justify-content:center, gap 22 → 61.
        // Лого 58/800 (lh .9), таглайн 12/700 ls .18em, картки max-width 332 з gap 15.

        private static RectTransform BuildMiddle(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite glowSprite, Sprite circle, RectTransform header, RectTransform nav,
            out TMP_Text logo, out TMP_Text tagline, out ModeCard levels, out ModeCard endless)
        {
            var middle = Child(parent, "Middle");
            var rect = middle.GetComponent<RectTransform>();
            // Розтягнута між шапкою і навігацією — саме тут «дихає» вільний простір.
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(M(20f), nav.sizeDelta.y);
            rect.offsetMax = new Vector2(-M(20f), -header.sizeDelta.y);

            var cardH = M(84f);            // 52 плитка + 16×2 padding → 233
            var cardGap = M(15f);          // 42
            var logoH = M(58f * 0.9f);     // line-height .9 → 145
            var taglineH = M(17f);         // 47
            var groupGap = M(22f);         // 61
            var logoTaglineGap = M(6f);    // 17

            var totalH = logoH + logoTaglineGap + taglineH + groupGap + cardH * 2 + cardGap;

            var content = Child(middle, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(M(332f), totalH); // 919 — max-width макета

            var y = totalH * 0.5f;

            logo = Label(content, "Logo", "Ink Flow", design, font,
                design.FontSizeLogo, design.TextPrimary, TextAlignmentOptions.Center);
            Place(logo, new Vector2(0f, y - logoH * 0.5f), new Vector2(M(332f), logoH),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            y -= logoH + logoTaglineGap;

            tagline = Label(content, "Tagline", "ФАРБУЙ ГАЛАКТИКУ", design, font,
                design.FontSizeLabel, new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Center);
            Place(tagline, new Vector2(0f, y - taglineH * 0.5f), new Vector2(M(332f), taglineH),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            y -= taglineH + groupGap;

            levels = BuildModeCard(content, "LevelsCard", ModeCard.Tone.Levels, design, font,
                rounded, outline, glowSprite, circle, cardH);
            Place(levels, new Vector2(0f, y - cardH * 0.5f), new Vector2(M(332f), cardH),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            y -= cardH + cardGap;

            endless = BuildModeCard(content, "EndlessCard", ModeCard.Tone.Endless, design, font,
                rounded, outline, glowSprite, circle, cardH);
            Place(endless, new Vector2(0f, y - cardH * 0.5f), new Vector2(M(332f), cardH),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            return rect;
        }

        // Картка: padding 16/18 → 44/50, gap 15 → 42, плитка 52 → 144, шеврон 25 → 69.
        private static ModeCard BuildModeCard(GameObject parent, string name, ModeCard.Tone tone,
            DesignSystem design, TMP_FontAsset? font, Sprite rounded, Sprite outline, Sprite glowSprite,
            Sprite circle, float height)
        {
            var padX = M(18f);       // 50
            var tileSize = design.CardIconTileSize; // 144
            var gap = M(15f);        // 42

            var card = Child(parent, name);

            var glowGo = Child(card, "Glow");
            Stretch(glowGo, -design.CardGlowRadius);
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = glowSprite;
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            // Зона згасання спрайта — 56 px; розтягуємо її рівно на радіус гало,
            // щоб світіння плавно зникало, а не обривалось кантом.
            glow.pixelsPerUnitMultiplier = 56f / design.CardGlowRadius;

            var bgGo = Child(card, "Background");
            Stretch(bgGo);
            var background = bgGo.AddComponent<GradientImage>();
            background.sprite = rounded;
            background.type = Image.Type.Sliced;

            var strokeGo = Child(card, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline; // обведення, а не заповнений прямокутник
            stroke.type = Image.Type.Sliced;
            stroke.raycastTarget = false;

            // Плитка іконки — ліворуч, по центру вертикалі.
            var tile = Child(card, "IconTile");
            var tileRect = tile.GetComponent<RectTransform>();
            tileRect.anchorMin = tileRect.anchorMax = new Vector2(0f, 0.5f);
            tileRect.pivot = new Vector2(0f, 0.5f);
            tileRect.anchoredPosition = new Vector2(padX, 0f);
            tileRect.sizeDelta = new Vector2(tileSize, tileSize);

            var tileFillGo = Child(tile, "Fill");
            Stretch(tileFillGo);
            var tileFill = tileFillGo.AddComponent<Image>();
            tileFill.sprite = rounded;
            tileFill.type = Image.Type.Sliced;
            tileFill.raycastTarget = false;

            var tileStrokeGo = Child(tile, "Stroke");
            Stretch(tileStrokeGo);
            var tileStroke = tileStrokeGo.AddComponent<Image>();
            tileStroke.sprite = outline;
            tileStroke.type = Image.Type.Sliced;
            tileStroke.raycastTarget = false;

            if (tone == ModeCard.Tone.Levels)
                BuildFourDrops(tile, design, circle);
            else
                BuildInfinity(tile, design, font);

            // Текстовий блок: назва / підзаголовок / статистика, gap 2 → 6.
            var textX = padX + tileSize + gap;
            var textW = M(332f) - textX - padX - M(25f); // мінус шеврон
            var text = Child(card, "Text");
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(textX, 0f);
            textRect.sizeDelta = new Vector2(textW, height);

            var title = Label(text, "Title", "Рівні", design, font,
                design.FontSizeTitle, design.TextPrimary, TextAlignmentOptions.Left);
            Place(title, new Vector2(0f, M(17f)), new Vector2(textW, M(24f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var subtitle = Label(text, "Subtitle", "Розчисти сітку", design, font,
                design.FontSizeCardSubtitle, design.TextMuted, TextAlignmentOptions.Left);
            Place(subtitle, new Vector2(0f, -M(2f)), new Vector2(textW, M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            // Зірка тегом, а не символом: у сцені лишався літерал ★, і TMP щоразу
            // писав «not found in font asset», бо гліфа в Nunito немає.
            var stat = Label(text, "Stat", "Рівень 12 · <sprite name=\"star\"> 27", design, font,
                design.FontSizeLabel, design.AccentPrimary, TextAlignmentOptions.Left);
            Place(stat, new Vector2(0f, -M(19f)), new Vector2(textW, M(16f)),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            var chevron = Label(card, "Chevron", "›", design, font,
                design.FontSizeSubtitle * 1.47f, design.TextDim, TextAlignmentOptions.Right);
            Place(chevron, new Vector2(-padX, 0f), new Vector2(M(20f), M(30f)),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var component = card.AddComponent<ModeCard>();
            var so = new SerializedObject(component);
            so.FindProperty("tone").enumValueIndex = (int)tone;
            so.ApplyModifiedPropertiesWithoutUndo();

            Wire(component,
                ("design", design), ("background", background), ("stroke", stroke), ("glow", glow),
                ("iconTileFill", tileFill), ("iconTileStroke", tileStroke),
                ("titleLabel", title), ("subtitleLabel", subtitle), ("statLabel", stat),
                ("chevronLabel", chevron));
            component.Apply();
            return component;
        }

        /// <summary>Іконка «Рівні»: 4 краплі 2×2, padding 9 → 25, gap 5 → 14.</summary>
        private static void BuildFourDrops(GameObject tile, DesignSystem design, Sprite circle)
        {
            var pad = M(9f);
            var gap = M(5f);
            var size = (design.CardIconTileSize - pad * 2f - gap) * 0.5f;
            var colors = new[] { InkColor.Magenta, InkColor.Violet, InkColor.Cyan, InkColor.Lime };

            for (var i = 0; i < 4; i++)
            {
                var go = Child(tile, $"Drop{i}");
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                var col = i % 2;
                var row = i / 2;
                rect.anchoredPosition = new Vector2(pad + col * (size + gap), -(pad + row * (size + gap)));
                rect.sizeDelta = new Vector2(size, size);

                var image = go.AddComponent<Image>();
                image.sprite = circle;
                image.raycastTarget = false;
                image.color = design.Ink(colors[i]);
            }
        }

        /// <summary>Іконка «Нескінченний»: ∞ 34/800 бірюзовим.</summary>
        private static void BuildInfinity(GameObject tile, DesignSystem design, TMP_FontAsset? font)
        {
            var label = Label(tile, "Infinity", "∞", design, font,
                M(34f), design.AccentTeal, TextAlignmentOptions.Center);
            Stretch(label.gameObject);
        }

        // ───────────────────────── Навігація ─────────────────────────
        // Макет: left/right 16 → 44, r30 → 83, padding 10/8, gap 6, підпис 10.

        private static RectTransform BuildNavBar(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, out NavBar navBar)
        {
            var side = M(16f);      // 44
            var height = M(65f);    // 180

            var nav = Child(parent, "NavBar");
            var rect = nav.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(side, 0f);
            rect.offsetMax = new Vector2(-side, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = Vector2.zero; // до низу SafeArea, offset 0

            var fillGo = Child(nav, "Fill");
            Stretch(fillGo);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;

            var strokeGo = Child(nav, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.raycastTarget = false;

            navBar = nav.AddComponent<NavBar>();
            var so = new SerializedObject(navBar);
            so.FindProperty("capsuleFill").objectReferenceValue = fill;
            so.FindProperty("capsuleStroke").objectReferenceValue = stroke;
            so.FindProperty("design").objectReferenceValue = design;

            var titles = new[] { "Галактика", "Магазин", "Рейтинги", "Профіль" };
            var ids = new[] { "galaxy", "shop", "ranks", "profile" };
            var iconFiles = new[] { "icon-galaxy", "icon-shop", "icon-ranks", "icon-profile" };
            var accents = new[] { design.AccentSecondary, design.AccentPrimary, design.AccentGold, design.TextMuted };

            var tabs = so.FindProperty("tabs");
            tabs.arraySize = titles.Length;

            for (var i = 0; i < titles.Length; i++)
            {
                var tab = Child(nav, $"Tab_{ids[i]}");
                var tabRect = tab.GetComponent<RectTransform>();
                tabRect.anchorMin = new Vector2(i / 4f, 0f);
                tabRect.anchorMax = new Vector2((i + 1) / 4f, 1f);
                tabRect.offsetMin = Vector2.zero;
                tabRect.offsetMax = Vector2.zero;

                var button = tab.AddComponent<Button>();
                var hit = tab.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f); // прозора зона натискання
                button.targetGraphic = hit;

                // Іконка над підписом: gap 6 → 17.
                var icon = Child(tab, "Icon");
                var iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, M(9f));
                iconRect.sizeDelta = new Vector2(M(26f), M(26f));
                // Світіння під активною вкладкою — за іконкою, тому окремим об'єктом.
                var underGlowGo = Child(tab, "UnderGlow");
                var underRect = underGlowGo.GetComponent<RectTransform>();
                underRect.anchorMin = underRect.anchorMax = new Vector2(0.5f, 0.5f);
                underRect.anchoredPosition = new Vector2(0f, -M(4f));
                underRect.sizeDelta = new Vector2(M(34f), M(20f));
                var underGlow = underGlowGo.AddComponent<Image>();
                underGlow.sprite = LoadSprite("circle-soft");
                underGlow.raycastTarget = false;
                underGlowGo.transform.SetAsFirstSibling();

                var iconImage = icon.AddComponent<Image>();
                iconImage.sprite = LoadSprite(iconFiles[i]);
                iconImage.raycastTarget = false;
                iconImage.color = accents[i];

                // Кольорові деталі поверх силуету: саме вони роблять іконку живою,
                // а не монохромним знаком.
                var accentLayers = BuildIconAccents(icon, i, design);

                var label = Label(tab, "Label", titles[i], design, font,
                    design.FontSizeCaption,
                    i == 0 ? design.NavLabelActive : design.NavLabelInactive,
                    TextAlignmentOptions.Center);
                Place(label, new Vector2(0f, -M(15f)), new Vector2(M(80f), M(14f)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                var entry = tabs.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = ids[i];
                entry.FindPropertyRelative("label").objectReferenceValue = label;
                entry.FindPropertyRelative("icon").objectReferenceValue = iconRect;
                entry.FindPropertyRelative("button").objectReferenceValue = button;
                entry.FindPropertyRelative("activeColor").colorValue = accents[i];
                entry.FindPropertyRelative("underGlow").objectReferenceValue = underGlow;
                var accentArray = entry.FindPropertyRelative("accents");
                accentArray.arraySize = accentLayers.Length;
                for (var a = 0; a < accentLayers.Length; a++)
                    accentArray.GetArrayElementAtIndex(a).objectReferenceValue = accentLayers[a];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            navBar.Apply();
            return rect;
        }


        /// <summary>Туманність за зорями: два м'які кола, що дуже повільно дихають.</summary>
        private static void BuildNebula(GameObject canvas, DesignSystem design, Sprite circle)
        {
            var go = Child(canvas, "Nebula");
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, DesignSystem.ReferenceWidth * 0.12f);

            var image = go.AddComponent<Image>();
            image.sprite = circle;
            image.raycastTarget = false;

            var secondGo = Child(go, "Secondary");
            var secondRect = secondGo.GetComponent<RectTransform>();
            secondRect.anchorMin = secondRect.anchorMax = new Vector2(0.5f, 0.5f);
            secondRect.sizeDelta = new Vector2(DesignSystem.ReferenceWidth * 0.8f,
                DesignSystem.ReferenceWidth * 0.8f);
            secondRect.anchoredPosition = new Vector2(-DesignSystem.ReferenceWidth * 0.18f,
                -DesignSystem.ReferenceWidth * 0.15f);
            var second = secondGo.AddComponent<Image>();
            second.sprite = circle;
            second.raycastTarget = false;

            var nebula = go.AddComponent<NebulaGlow>();
            Wire(nebula, ("design", design), ("image", image), ("secondary", second));
            nebula.Apply();

            // Під зорі, але над градієнтом.
            go.transform.SetSiblingIndex(1);
        }

        /// <summary>Точки зриву краплин під «Ink» і під «Flow».</summary>
        private static void BuildLogoDrips(GameObject parent, DesignSystem design, DripPool pool, TMP_Text logo)
        {
            var go = Child(parent, "LogoDrips");
            Stretch(go);

            var left = Child(go, "InkSource").GetComponent<RectTransform>();
            var right = Child(go, "FlowSource").GetComponent<RectTransform>();
            var logoRect = logo.rectTransform;

            foreach (var (source, x) in new[] { (left, -0.22f), (right, 0.24f) })
            {
                source.anchorMin = source.anchorMax = new Vector2(0.5f, 0.5f);
                source.sizeDelta = Vector2.one;
                // Прив'язуємось до низу рядка логотипу — саме звідти стікає чорнило.
                source.position = logoRect.TransformPoint(
                    new Vector3(logoRect.rect.width * x, -logoRect.rect.height * 0.28f, 0f));
            }

            var drip = go.AddComponent<LogoDrip>();
            var so = new SerializedObject(drip);
            so.FindProperty("design").objectReferenceValue = design;
            so.FindProperty("pool").objectReferenceValue = pool;
            var sources = so.FindProperty("sources");
            sources.arraySize = 2;
            sources.GetArrayElementAtIndex(0).objectReferenceValue = left;
            sources.GetArrayElementAtIndex(1).objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        /// <summary>
        /// Кольорові шари іконки вкладки. У макеті кожна іконка складена з кількох
        /// фігур із власними кольорами — відтворюємо це накладанням дрібних спрайтів.
        /// </summary>
        private static Graphic[] BuildIconAccents(GameObject icon, int tabIndex, DesignSystem design)
        {
            var circle = LoadSprite("circle-soft");

            switch (tabIndex)
            {
                case 0: // Галактика — плями на планеті
                {
                    var spots = new[]
                    {
                        (new Vector2(-M(4f), M(3f)), M(8f), design.AccentPrimary),
                        (new Vector2(M(4f), M(5f)), M(6f), design.AccentLime),
                        (new Vector2(M(2f), -M(4f)), M(7f), design.AccentTeal)
                    };

                    var layers = new Graphic[spots.Length];
                    for (var i = 0; i < spots.Length; i++)
                    {
                        var go = Child(icon, $"Spot{i}");
                        var rect = go.GetComponent<RectTransform>();
                        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                        rect.anchoredPosition = spots[i].Item1;
                        rect.sizeDelta = new Vector2(spots[i].Item2, spots[i].Item2);
                        var image = go.AddComponent<Image>();
                        image.sprite = circle;
                        image.raycastTarget = false;
                        image.color = spots[i].Item3;
                        layers[i] = image;
                    }

                    return layers;
                }

                case 1: // Магазин — маджентова фарба у відрі з відблиском
                {
                    var paintGo = Child(icon, "Paint");
                    var paintRect = paintGo.GetComponent<RectTransform>();
                    paintRect.anchorMin = paintRect.anchorMax = new Vector2(0.5f, 0.5f);
                    paintRect.anchoredPosition = new Vector2(0f, M(2f));
                    paintRect.sizeDelta = new Vector2(M(13f), M(5f));
                    var paint = paintGo.AddComponent<Image>();
                    paint.sprite = circle;
                    paint.raycastTarget = false;
                    paint.color = design.AccentPrimary;

                    var shineGo = Child(icon, "Shine");
                    var shineRect = shineGo.GetComponent<RectTransform>();
                    shineRect.anchorMin = shineRect.anchorMax = new Vector2(0.5f, 0.5f);
                    shineRect.anchoredPosition = new Vector2(-M(3f), M(3f));
                    shineRect.sizeDelta = new Vector2(M(4f), M(2.5f));
                    var shine = shineGo.AddComponent<Image>();
                    shine.sprite = circle;
                    shine.raycastTarget = false;
                    shine.color = new Color(1f, 1f, 1f, 0.55f);

                    return new Graphic[] { paint, shine };
                }

                case 2: // Рейтинги — тепле світло в чаші кубка
                {
                    var lightGo = Child(icon, "CupLight");
                    var lightRect = lightGo.GetComponent<RectTransform>();
                    lightRect.anchorMin = lightRect.anchorMax = new Vector2(0.5f, 0.5f);
                    lightRect.anchoredPosition = new Vector2(0f, M(4f));
                    lightRect.sizeDelta = new Vector2(M(14f), M(10f));
                    var light = lightGo.AddComponent<Image>();
                    light.sprite = circle;
                    light.raycastTarget = false;
                    light.color = DesignSystem.WithAlpha(design.AccentGold, 0.5f);
                    lightGo.transform.SetAsFirstSibling();

                    return new Graphic[] { light };
                }

                default: // Профіль — силует у скляному крузі
                {
                    var glassGo = Child(icon, "GlassRing");
                    var glassRect = glassGo.GetComponent<RectTransform>();
                    glassRect.anchorMin = glassRect.anchorMax = new Vector2(0.5f, 0.5f);
                    glassRect.sizeDelta = new Vector2(M(24f), M(24f));
                    var glass = glassGo.AddComponent<Image>();
                    glass.sprite = circle;
                    glass.raycastTarget = false;
                    glass.color = design.GlassFillRaised;
                    glassGo.transform.SetAsFirstSibling();

                    return new Graphic[] { glass };
                }
            }
        }

        // ───────────────────────── Утиліти ─────────────────────────

        private static void CreateCamera(DesignSystem design)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = design.BackgroundEdge;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static Sprite LoadSprite(string file) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{file}.png");

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject Child(RectTransform parent, string name) => Child(parent.gameObject, name);

        private static void Stretch(GameObject go, float inset = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static TMP_Text Label(GameObject parent, string name, string text, DesignSystem design,
            TMP_FontAsset? font, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = Child(parent, name);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            if (font != null)
                label.font = font;
            if (_iconSprites != null)
                label.spriteAsset = _iconSprites;
            return label;
        }

        private static TMP_Text Label(RectTransform parent, string name, string text, DesignSystem design,
            TMP_FontAsset? font, float size, Color color, TextAlignmentOptions alignment) =>
            Label(parent.gameObject, name, text, design, font, size, color, alignment);

        private static void Place(Component component, Vector2 position, Vector2 size,
            Vector2 anchor, Vector2 pivot)
        {
            var rect = component.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                if (value == null)
                {
                    Debug.LogError($"[InkFlow] null у поле '{field}' на {target.GetType().Name}");
                    continue;
                }

                var property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[InkFlow] Поле '{field}' не знайдено на {target.GetType().Name}");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var check = new SerializedObject(target);
            foreach (var (field, value) in fields)
                if (value != null && check.FindProperty(field)?.objectReferenceValue == null)
                    Debug.LogError($"[InkFlow] Поле '{field}' на {target.GetType().Name} записалось як null.");
        }
    }
}
