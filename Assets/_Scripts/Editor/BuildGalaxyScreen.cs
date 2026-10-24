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
    /// Збирає екран галактики U1Glaze (S03Galaxy, S09ForeignGalaxy_01, S12SmallScreen_04) фабриками <see cref="U1"/>:
    /// стовпчик (VerticalLayoutGroup) — шапка 56 → картка гостя → рядок галактики зі стрілками циклів → карусель
    /// (гнучка: зайва висота — їй, планета меншає лише на низьких екранах) → назва й підпис планети → коралова
    /// «Відкрити» з полями 40 → пагінація → навігація 76. Поверх — діалог «Галактику завершено» (S03_04) і екран
    /// нової галактики (S03_05). Планети — шейдер InkFlow/Planet (SPEC: планети на макетах — заглушки), мітки
    /// комірок, бейдж і замок — спрайти U1Glaze. Меню: Ink Flow → Setup → Build Galaxy Screen.
    /// </summary>
    public static class BuildGalaxyScreen
    {
        private const string ScenePath = "Assets/Scenes/Galaxy.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string ShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";

        /// <summary>Слотів у пулі каруселі: фокус ±2. Далі планета вже поза кадром.</summary>
        private const int SlotCount = 5;

        /// <summary>Міток комірок на планету: стеля слотів у GalaxyConfig ([Range(1, 24)]).</summary>
        private const int PipPool = 24;

        /// <summary>Крапок пагінації й планет у дузі «Галактику завершено» — з запасом над конфігом (9).</summary>
        private const int PlanetPool = 16;

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
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var quad = LoadSprite("white-quad");
            var moonSprite = LoadSprite("circle-soft");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (moonSprite == null) missing.Add($"{SpriteFolder}/circle-soft.png");
            if (shader == null) missing.Add(ShaderPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (U1GlazeSprites.Load("pip_filled") == null) missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
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

            // ── Стовпчик ──
            var column = Child(screenGo, "Column");
            Stretch(column);
            var col = column.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, (int)U1.Px(8f));
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            U1.ScreenHeader(column, design!, out var back, out var title, out var currency, out var gear);
            title.text = "Галактика";

            var visitor = BuildVisitorCard(column, design!, out var visitorAvatar, out var visitorNick, out var visitorHint);

            U1.Gap(column, 4f);
            BuildGalaxyRow(column, design!, out var galaxyName, out var galaxyProgress, out var prev, out var next);
            // Нагорода в кінці галактики — видно заздалегідь (2026-10-23); вигляд галактик — окремою задачею автора.
            var galaxyReward = U1.Text(column, "GalaxyReward", "У кінці галактики: Епічна+ картинка і 300 нафти", design!, U1.Role.Caption, design!.Amber);
            galaxyReward.font = design!.FontBody;
            galaxyReward.textWrappingMode = TextWrappingModes.NoWrap;
            galaxyReward.overflowMode = TextOverflowModes.Ellipsis;
            U1.Size(galaxyReward.gameObject, 0f, 20f);

            var area = Child(column, "Carousel");
            area.AddComponent<LayoutElement>().flexibleHeight = 1f;
            // Прозора площина для перетягування: без графіки з raycastTarget EventSystem не доставить дотику.
            var catcher = area.AddComponent<Image>();
            catcher.color = Color.clear;
            var carousel = area.AddComponent<PlanetCarousel>();
            var track = Child(area, "Track");
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.sizeDelta = Vector2.zero;
            var slots = new PlanetView[SlotCount];
            for (var i = 0; i < SlotCount; i++)
                slots[i] = BuildSlot(track, design!, quad!, moonSprite!, shader!, $"Slot{i}", withMarks: true);
            // Межа відсікання — увесь екран: стовпчик має поля 16, а планети малюються й на них.
            Wire(carousel, ("design", design!), ("viewport", screenGo.GetComponent<RectTransform>()), ("track", trackRect));
            WireArray(carousel, "slots", slots);

            var planetName = U1.Text(column, "PlanetName", "Рудокам", design!, U1.Role.H1, design!.InkText);
            planetName.textWrappingMode = TextWrappingModes.NoWrap;
            planetName.overflowMode = TextOverflowModes.Ellipsis;
            U1.Size(planetName.gameObject, 0f, 40f);
            var planetZones = U1.Text(column, "PlanetCaption", "3 з 8 комірок заповнено", design!, U1.Role.Body, design!.InkMuted);
            planetZones.textWrappingMode = TextWrappingModes.NoWrap;
            planetZones.overflowMode = TextOverflowModes.Ellipsis;
            U1.Size(planetZones.gameObject, 0f, 24f);

            U1.Gap(column, design!.GalaxyOpenGap);
            var openRow = Child(column, "OpenRow");
            var openLayout = openRow.AddComponent<HorizontalLayoutGroup>();
            // Кнопка вужча за стовпчик — поля 40 від краю екрана (S03: 40 і на SE).
            openLayout.padding = new RectOffset((int)U1.Px(24f), (int)U1.Px(24f), 0, 0);
            openLayout.childControlWidth = openLayout.childControlHeight = true;
            openLayout.childForceExpandWidth = openLayout.childForceExpandHeight = true;
            // flexibleHeight 0 явно: childForceExpandHeight робить ряд гнучким, і він ділив би залишок із каруселлю.
            U1.Size(openRow, 0f, 56f).flexibleHeight = 0f;
            var open = U1.Button(openRow, "Open", "Відкрити", design!, CandyButton.Tone.Coral);

            U1.Gap(column, design!.GalaxyDotsGapTop);
            var pagination = BuildPagination(column, design!, out var dots);
            U1.Gap(column, design!.GalaxyDotsGapBottom);
            var navBar = U1.NavBar(column, "NavBar", design!, NavBar.Galaxy);

            // ── Поверх: «Галактику завершено» і нова галактика ──
            var complete = BuildCompleteDialog(screenGo, design!, shader!, quad!, out var arc, out var arcPlanets,
                out var completeKicker, out var completeBody, out var completeNext);
            var reveal = BuildReveal(screenGo, design!, quad!, moonSprite!, shader!, out var revealBack, out var revealCurrency,
                out var revealGear, out var revealTitle, out var revealSubtitle, out var revealArea, out var hero,
                out var heroPlanet, out var fly);

            var avatars = new Sprite[8];
            for (var i = 0; i < avatars.Length; i++)
                avatars[i] = U1.S($"avatar_{i + 1}");

            Wire(screen,
                ("design", design!), ("backButton", back), ("title", title), ("currency", currency), ("settingsButton", gear),
                ("visitorCard", visitor), ("visitorAvatar", visitorAvatar), ("visitorNick", visitorNick), ("visitorHint", visitorHint),
                ("incognitoAvatar", U1.S("avatar_incognito")),
                ("galaxyName", galaxyName), ("galaxyProgress", galaxyProgress), ("galaxyReward", galaxyReward),
                ("prevGalaxyButton", prev), ("nextGalaxyButton", next),
                ("column", column.GetComponent<RectTransform>()), ("carouselArea", area.GetComponent<RectTransform>()),
                ("carousel", carousel),
                ("planetName", planetName), ("planetZones", planetZones),
                ("openRow", openRow.GetComponent<RectTransform>()), ("openButton", open),
                ("openCoralSprite", U1.S("button_coral")), ("openGlassSprite", U1.S("button_glass")),
                ("pagination", pagination), ("dotSprite", U1.S("toggle_knob_off")), ("pillSprite", U1.S("progress_fill")),
                ("navBar", navBar),
                ("completeDialog", complete), ("completeArc", arc), ("completeKicker", completeKicker),
                ("completeBody", completeBody), ("completeNext", completeNext),
                ("reveal", reveal), ("revealBack", revealBack), ("revealCurrency", revealCurrency), ("revealSettings", revealGear),
                ("revealTitle", revealTitle), ("revealSubtitle", revealSubtitle), ("revealArea", revealArea),
                ("revealHero", hero), ("revealPlanet", heroPlanet), ("revealFly", fly));
            WireArray(screen, "avatarSprites", avatars);
            WireArray(screen, "dots", dots);
            WireArray(screen, "arcPlanets", arcPlanets);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Синхронно й перед збереженням: OnValidate відкладений, і дефолти
            // потрапили б у файл сцени замість справжніх значень.
            uiRoot.ApplyScaler();
            screen.Apply();

            // Префаб — те, з чого BuildMainScene збирає застосунок.
            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Галактику зібрано: {ScenePath} (U1Glaze, коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        // ── Картка гостя (S09_01): panel_low 56, аватар 36, нік h3 і «галактика гравця», бейдж «ПЕРЕГЛЯД» ──
        private static RectTransform BuildVisitorCard(GameObject parent, DesignSystem design,
            out Image avatar, out TMP_Text nick, out TMP_Text hint)
        {
            var card = U1.Panel(parent, "VisitorCard", "panel_low", shadow: false);
            U1.Size(card, 0f, 56f);
            var row = card.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)U1.Px(13f), (int)U1.Px(13f), 0, 0);
            row.spacing = U1.Px(11f);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            var avatarGo = Child(card, "Avatar");
            avatar = avatarGo.AddComponent<Image>();
            avatar.sprite = U1.S("avatar_1");
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            U1.Size(avatarGo, 36f, 36f);

            var texts = Child(card, "Texts");
            var textCol = texts.AddComponent<VerticalLayoutGroup>();
            textCol.childAlignment = TextAnchor.MiddleLeft;
            textCol.childControlWidth = textCol.childControlHeight = true;
            textCol.childForceExpandWidth = true;
            textCol.childForceExpandHeight = false;
            var textSize = texts.AddComponent<LayoutElement>();
            textSize.flexibleWidth = 1f;
            textSize.minWidth = 0f;
            nick = U1.Text(texts, "Nick", "Вега", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            hint = U1.Text(texts, "Hint", "галактика гравця", design, U1.Role.Caption, design.InkMuted, TextAlignmentOptions.Left);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.overflowMode = TextOverflowModes.Ellipsis;

            U1.Badge(card, "Viewing", "Перегляд", design, "badge_grey", design.InkMuted);

            card.SetActive(false);
            return card.GetComponent<RectTransform>();
        }

        // ── Рядок галактики: «‹  ГАЛАКТИКА I · ПЕРВІСНА / 2 / 9 планет  ›» — стрілки циклів (зона тапу 44) ──
        private static void BuildGalaxyRow(GameObject parent, DesignSystem design,
            out TMP_Text name, out TMP_Text progress, out Button prev, out Button next)
        {
            var row = Child(parent, "GalaxyRow");
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            U1.Size(row, 0f, 44f);

            prev = Arrow(row, "Prev", design, "icon_back");

            var labels = Child(row, "Labels");
            var labelCol = labels.AddComponent<VerticalLayoutGroup>();
            labelCol.childAlignment = TextAnchor.MiddleCenter;
            labelCol.childControlWidth = labelCol.childControlHeight = true;
            labelCol.childForceExpandWidth = true;
            labelCol.childForceExpandHeight = false;
            var labelSize = labels.AddComponent<LayoutElement>();
            labelSize.flexibleWidth = 1f;
            labelSize.minWidth = 0f;
            name = U1.Text(labels, "Name", "ГАЛАКТИКА I · ПЕРВІСНА", design, U1.Role.Label, design.InkMuted);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            progress = U1.Text(labels, "Progress", "12 / 33 комірок · 2 / 6 планет", design, U1.Role.Caption, design.InkMuted);
            progress.font = design.FontBody;
            progress.textWrappingMode = TextWrappingModes.NoWrap;

            next = Arrow(row, "Next", design, "icon_chev");
            prev.gameObject.SetActive(false);
            next.gameObject.SetActive(false);
        }

        /// <summary>Стрілка циклів: прозора зона тапу 44 з іконкою 20 (неактивна — тьмяна).</summary>
        private static Button Arrow(GameObject parent, string name, DesignSystem design, string icon)
        {
            var go = Child(parent, name);
            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            U1.Size(go, 44f, 44f);
            var iconGo = Child(go, "Icon");
            iconGo.transform.SetAsFirstSibling();
            var image = iconGo.AddComponent<Image>();
            image.sprite = U1.S(icon);
            image.color = design.InkText;
            image.raycastTarget = false;
            Place(image, Vector2.zero, new Vector2(U1.Px(20f), U1.Px(20f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            go.AddComponent<PressScale>().Bind(design);
            return button;
        }

        // ── Пагінація: крапка на планету, активна — бірюзова пігулка ──
        private static RectTransform BuildPagination(GameObject parent, DesignSystem design, out Image[] dots)
        {
            var row = Child(parent, "Pagination");
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = U1.Px(design.GalaxyDotGap);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            U1.Size(row, 0f, design.GalaxyDot);
            dots = new Image[PlanetPool];
            for (var i = 0; i < PlanetPool; i++)
            {
                var dotGo = Child(row, $"Dot{i}");
                var dot = dotGo.AddComponent<Image>();
                // Кругла крапка (S03): кружок toggle_knob_off під непрозорим тоном — не pip_filled (квадратик).
                dot.sprite = U1.S("toggle_knob_off");
                dot.type = Image.Type.Sliced;
                dot.color = design.GalaxyDotInactive;
                dot.raycastTarget = false;
                U1.Size(dotGo, design.GalaxyDot, design.GalaxyDot);
                dots[i] = dot;
            }
            return row.GetComponent<RectTransform>();
        }

        // ── Одна планета: шари шейдера, місяці, плитка із замком, мітки комірок і бейдж-галочка ──
        private static PlanetView BuildSlot(GameObject parent, DesignSystem design, Sprite quad, Sprite moonSprite, Shader shader,
            string name, bool withMarks, float hazeScale = 0f)
        {
            var size = design.PlanetSize;
            var go = Child(parent, name);
            var body = go.GetComponent<RectTransform>();
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(size, size);

            // Порядок шарів: заднє кільце → серпанок → диск → переднє кільце → місяці → [замок → мітки й бейдж].
            var ringBack = Quad(go, "RingBack", quad, size * design.PlanetRingWidthScale, size * design.PlanetRingHeightScale);
            var atmosphere = Quad(go, "Atmosphere", quad, size * design.PlanetAtmosphereScale, size * design.PlanetAtmosphereScale);
            var disc = Quad(go, "Disc", quad, size, size);
            var ringFront = Quad(go, "RingFront", quad, size * design.PlanetRingWidthScale, size * design.PlanetRingHeightScale);

            var moons = new RectTransform[2];
            for (var m = 0; m < moons.Length; m++)
            {
                var moonSize = U1.Px(m == 0 ? 16f : 11f);
                var moonGo = Child(go, $"Moon{m}");
                var moon = moonGo.AddComponent<Image>();
                moon.sprite = moonSprite;
                moon.color = design.InkText;
                moon.raycastTarget = false;
                Place(moon, Vector2.zero, new Vector2(moonSize, moonSize), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                moons[m] = moonGo.GetComponent<RectTransform>();
            }

            // Мітки, бейдж і замок — лише в планети у фокусі (S03_03: у замкненої сусідки плитки немає);
            // карусель гасить цю гілку альфою, поки планета їде з центру.
            RectTransform? focusRect = null;
            if (withMarks)
            {
                focusRect = Child(go, "FocusOnly").GetComponent<RectTransform>();
                focusRect.anchorMin = focusRect.anchorMax = new Vector2(0.5f, 0.5f);
                focusRect.sizeDelta = Vector2.zero;
            }

            // Замок: скляна плитка icon_button з icon_lock (S03_03); розміри ставить PlanetView від діаметра.
            var lockGo = Child(focusRect != null ? focusRect.gameObject : go, "Lock");
            var lockTile = lockGo.AddComponent<Image>();
            lockTile.sprite = U1.S("icon_button");
            lockTile.type = Image.Type.Sliced;
            lockTile.raycastTarget = false;
            Place(lockTile, Vector2.zero, new Vector2(size * design.GalaxyLockTile, size * design.GalaxyLockTile),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var lockIconGo = Child(lockGo, "Icon");
            var lockIcon = lockIconGo.AddComponent<Image>();
            lockIcon.sprite = U1.S("icon_lock");
            lockIcon.color = design.InkMuted;
            lockIcon.raycastTarget = false;
            Place(lockIcon, Vector2.zero, new Vector2(size * design.GalaxyLockIcon, size * design.GalaxyLockIcon),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            lockGo.SetActive(false);

            var view = go.AddComponent<PlanetView>();
            Wire(view,
                ("design", design), ("planetShader", shader), ("body", body),
                ("disc", disc), ("atmosphere", atmosphere),
                ("ringBack", ringBack), ("ringFront", ringFront),
                ("lockIcon", lockGo.GetComponent<RectTransform>()), ("lockIconImage", lockIconGo.GetComponent<RectTransform>()));
            WireArray(view, "moons", moons);

            if (focusRect != null)
            {
                var focusOnly = focusRect.gameObject;
                var pips = new Image[PipPool];
                var shines = new Image[PipPool];
                for (var i = 0; i < PipPool; i++)
                {
                    var (pip, shine) = U1.Pip(focusOnly, $"Pip{i}", filled: false, Color.white);
                    var rect = pip.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    pip.gameObject.SetActive(false);
                    pips[i] = pip;
                    shines[i] = shine;
                }

                // Бейдж-галочка ожилої планети (S03_02): бірюзове коло зі світлим обідком і icon_check кольору teal-ink.
                // Обідок — світлий кружок toggle_knob_on; коло — пігулка badge_teal, нарізана так, що її півкола-краї
                // (межа = пів висоти спрайта) більші за пів сторони бейджа за будь-якого розміру планети: Unity стискає
                // їх пропорційно — виходить рівне коло (button_teal_small давав квадратик із заокругленнями).
                var badgeGo = Child(focusOnly, "DoneBadge");
                var badge = badgeGo.AddComponent<Image>();
                badge.sprite = U1.S("toggle_knob_on");
                badge.preserveAspect = true;
                badge.raycastTarget = false;
                Place(badge, Vector2.zero, new Vector2(size * design.GalaxyBadgeSize, size * design.GalaxyBadgeSize),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                var fillGo = Child(badgeGo, "Fill");
                Stretch(fillGo, U1.Px(design.GalaxyBadgeRim));
                var fill = fillGo.AddComponent<Image>();
                fill.sprite = U1.S("badge_teal");
                fill.type = Image.Type.Sliced;
                fill.raycastTarget = false;
                var borderUnits = fill.sprite.border.x / fill.sprite.pixelsPerUnit * 100f; // reference PPU канваса — 100
                fill.pixelsPerUnitMultiplier = borderUnits / (design.PlanetSizeFinale * design.GalaxyBadgeSize * 0.5f);
                var checkGo = Child(badgeGo, "Check");
                var check = checkGo.AddComponent<Image>();
                check.sprite = U1.S("icon_check");
                check.color = design.TealInk;
                check.raycastTarget = false;
                Place(check, Vector2.zero, new Vector2(size * design.GalaxyBadgeIcon, size * design.GalaxyBadgeIcon),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                badgeGo.SetActive(false);

                Wire(view, ("focusOnly", focusRect), ("pipEmpty", U1.S("pip_empty")), ("pipFilled", U1.S("pip_filled")),
                    ("doneBadge", badgeGo.GetComponent<RectTransform>()), ("doneBadgeIcon", checkGo.GetComponent<RectTransform>()));
                WireArray(view, "pips", pips);
                WireArray(view, "pipShines", shines);
            }

            if (hazeScale > 0f)
            {
                var so = new SerializedObject(view);
                so.FindProperty("hazeScale").floatValue = hazeScale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return view;
        }

        // ── «Галактику завершено» (S03_04): дуга планет з іскрами, назва, «Галактику завершено!», «Далі» ──
        private static DialogView BuildCompleteDialog(GameObject parent, DesignSystem design, Shader shader, Sprite quad,
            out RectTransform arc, out PlanetDisc[] planets, out TMP_Text kicker, out TMP_Text body, out CandyButton next)
        {
            var dialog = U1.Dialog(parent, "GalaxyComplete", design, U1.DialogWidthPx, out var panel, U1.DialogPaddingPx);

            var arcGo = Child(panel, "Arc");
            // Дуга: розмах і радіус — токени; висота — від найнижчих планет на краях дуги до верхньої й іскор над нею.
            var lowest = Mathf.Sin((90f - design.GalaxyArcSpread * 0.5f) * Mathf.Deg2Rad);
            U1.Size(arcGo, 0f, (1f - lowest) * design.GalaxyArcRadius + design.GalaxyArcPlanet + 26f);
            arc = arcGo.GetComponent<RectTransform>();
            planets = new PlanetDisc[PlanetPool];
            for (var i = 0; i < PlanetPool; i++)
            {
                var planetGo = Child(arcGo, $"Planet{i}");
                var rect = planetGo.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(U1.Px(design.GalaxyArcPlanet), U1.Px(design.GalaxyArcPlanet));
                var disc = planetGo.AddComponent<Image>();
                disc.sprite = quad;
                disc.raycastTarget = false;
                var view = planetGo.AddComponent<PlanetDisc>();
                Wire(view, ("design", design), ("planetShader", shader), ("disc", disc));
                planetGo.SetActive(false);
                planets[i] = view;
            }
            // Іскри: три над дугою, дві малі — на планетах (S03_04).
            Sparkle(arcGo, new Vector2(-126f, -22f), 14f, top: true);
            Sparkle(arcGo, new Vector2(0f, 0f), 16f, top: true);
            Sparkle(arcGo, new Vector2(126f, -14f), 12f, top: true);
            Sparkle(arcGo, new Vector2(-103f, -93f), 9f, top: true);
            Sparkle(arcGo, new Vector2(102f, -85f), 9f, top: true);
            // Повітря між найнижчими планетами дуги й написом (S03_04 ≈ 61 px разом зі spacing панелі).
            U1.Gap(panel, design.GalaxyArcBottomGap);

            kicker = U1.Text(panel, "Kicker", "ГАЛАКТИКА I · ПЕРВІСНА", design, U1.Role.Label, design.InkMuted);
            var title = U1.Text(panel, "Title", "Галактику завершено!", design, U1.Role.H1, design.InkText);
            title.textWrappingMode = TextWrappingModes.Normal;
            body = U1.Text(panel, "Body", "Усі 9 планет ожили. Тебе чекає нова галактика.", design, U1.Role.Body, design.InkMuted);
            body.textWrappingMode = TextWrappingModes.Normal;
            U1.Gap(panel, 4f);
            next = U1.Button(panel, "Next", "Далі", design, CandyButton.Tone.Coral);
            U1.Size(next.gameObject, 0f, 56f);

            dialog.gameObject.SetActive(false);
            return dialog;
        }

        /// <summary>Іскра sparkle (тонується білим): позиція — px макета від верху-центру (top) або центру батька.</summary>
        private static void Sparkle(GameObject parent, Vector2 positionPx, float sidePx, bool top)
        {
            var go = Child(parent, "Sparkle");
            var image = go.AddComponent<Image>();
            image.sprite = U1.S("sparkle");
            image.color = Color.white;
            image.raycastTarget = false;
            var anchor = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
            Place(image, new Vector2(U1.Px(positionPx.x), U1.Px(positionPx.y) - (top ? U1.Px(sidePx * 0.5f) : 0f)),
                new Vector2(U1.Px(sidePx), U1.Px(sidePx)), anchor, new Vector2(0.5f, 0.5f));
        }

        // ── Нова галактика (S03_05): «‹» + нафта + шестерня, «ВІДКРИТО», назва, підпис, планета-герой з іскрами, «Летіти» ──
        private static RectTransform BuildReveal(GameObject parent, DesignSystem design, Sprite quad, Sprite moonSprite, Shader shader,
            out Button back, out CurrencyWidget currency, out Button gear, out TMP_Text title, out TMP_Text subtitle,
            out RectTransform area, out RectTransform hero, out PlanetView planet, out CandyButton fly)
        {
            var root = Child(parent, "Reveal");
            Stretch(root);
            var col = root.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, (int)U1.Px(24f));
            col.spacing = U1.Px(6f);
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            U1.ScreenHeader(root, design, out back, out var headerTitle, out currency, out gear);
            headerTitle.text = string.Empty;

            var badgeRow = Child(root, "BadgeRow");
            var badgeLayout = badgeRow.AddComponent<HorizontalLayoutGroup>();
            badgeLayout.childAlignment = TextAnchor.MiddleCenter;
            badgeLayout.childControlWidth = badgeLayout.childControlHeight = true;
            badgeLayout.childForceExpandWidth = badgeLayout.childForceExpandHeight = false;
            U1.Size(badgeRow, 0f, 26f);
            U1.Badge(badgeRow, "Opened", "Відкрито", design, "badge_teal", design.TealInk);

            title = U1.Text(root, "Title", "Галактика II · Туманна", design, U1.Role.H1, design.InkText);
            title.textWrappingMode = TextWrappingModes.Normal;
            subtitle = U1.Text(root, "Subtitle", "9 планет · до 12 комірок на планеті", design, U1.Role.Body, design.InkMuted);
            subtitle.textWrappingMode = TextWrappingModes.Normal;

            var areaGo = Child(root, "HeroArea");
            areaGo.AddComponent<LayoutElement>().flexibleHeight = 1f;
            area = areaGo.GetComponent<RectTransform>();
            var heroGo = Child(areaGo, "Hero");
            hero = heroGo.GetComponent<RectTransform>();
            hero.anchorMin = hero.anchorMax = new Vector2(0.5f, 0.5f);
            hero.pivot = new Vector2(0.5f, 0.5f);
            hero.sizeDelta = new Vector2(design.PlanetSize, design.PlanetSize);
            planet = BuildSlot(heroGo, design, quad, moonSprite, shader, "Planet", withMarks: false, hazeScale: design.GalaxyRevealHaze);
            // Іскри навколо героя — частки діаметра з S03_05 (чотири, різного розміру).
            foreach (var (x, y, side) in new[] { (-0.58f, 0.50f, 0.09f), (0.59f, 0.54f, 0.072f), (0.67f, -0.42f, 0.095f), (-0.62f, -0.55f, 0.063f) })
            {
                var sparkleGo = Child(heroGo, "Sparkle");
                var sparkle = sparkleGo.AddComponent<Image>();
                sparkle.sprite = U1.S("sparkle");
                sparkle.color = Color.white;
                sparkle.raycastTarget = false;
                var d = design.PlanetSize;
                Place(sparkle, new Vector2(x * d, y * d), new Vector2(side * d, side * d), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }

            var flyRow = Child(root, "FlyRow");
            var flyLayout = flyRow.AddComponent<HorizontalLayoutGroup>();
            flyLayout.padding = new RectOffset((int)U1.Px(8f), (int)U1.Px(8f), 0, 0);
            flyLayout.childControlWidth = flyLayout.childControlHeight = true;
            flyLayout.childForceExpandWidth = flyLayout.childForceExpandHeight = true;
            U1.Size(flyRow, 0f, 56f).flexibleHeight = 0f;
            fly = U1.Button(flyRow, "Fly", "Летіти", design, CandyButton.Tone.Coral);

            root.SetActive(false);
            return root.GetComponent<RectTransform>();
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
