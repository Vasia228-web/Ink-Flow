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
    /// Збирає екран колекції U1Glaze (S06Collection_01…03, S12SmallScreen_07) фабриками <see cref="U1"/>:
    /// стовпчик — шапка 56 (вкладка: «Колекція» h1, капсула, шестерня; вибір: «×», «Обери картинку») →
    /// підпис «Зібрано …» або смуга «Комірка N · планета …» → ряд чипів тем → ряд чипів рідкості з мітками →
    /// гнучка ділянка сітки (віртуалізований пул карток у рамках рідкості) або картка «порожньо» →
    /// [вибір: підказка й коралова «Поставити «…»»] → навігація 76. Меню: Ink Flow → Setup → Build Collection Screen.
    ///
    /// Карток у пулі — на в'юпорт із запасом у два ряди; розмір картки, колонки й проміжок — токени DesignSystem.
    /// Пікселі всіх карток — із одного атласу (SlotAtlas), тож сітка малюється кількома викликами.
    /// </summary>
    public static class BuildCollectionScreen
    {
        private const string ScenePath = "Assets/Scenes/Collection.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        /// <summary>Чипів рідкості («Усі» + шість). Фільтра тем немає з 2026-10-23 (рішення автора) — лише рідкість.</summary>
        private const int RarityChips = 7;

        /// <summary>Рядків карток у пулі: в'юпорт найвищого телефона вміщає ~3.5, запас — два.</summary>
        private const int PoolRows = 6;

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

            // Асети — строго після NewScene: закриття сцени вивантажує незакорінені асети.
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var quad = LoadSprite("white-quad");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (U1GlazeSprites.Load("frame_rarity_1") == null) missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
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

            // ── Стовпчик ──
            var column = Child(screenGo, "Column");
            Stretch(column);
            var col = column.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, (int)U1.Px(8f));
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            // Шапка вкладки / підекрана (S06_01) і шапка вибору (S06_02) — дві шапки на одному місці.
            var browseHeader = U1.ScreenHeader(column, design!, out var back, out var title, out var currency, out var gear);
            title.text = "Колекція";
            var pickHeader = BuildPickHeader(column, design!, out var close, out var pickTitle);

            var subtitle = U1.Text(column, "Subtitle", "Зібрано 0 різних · 0 усього", design!, U1.Role.Body, design!.InkMuted, TextAlignmentOptions.Left);
            subtitle.font = design!.FontBlack;
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            U1.Size(subtitle.gameObject, 0f, 24f);

            var pickInfo = BuildPickInfo(column, design!, out var pickIcon, out var pickText);

            U1.Gap(column, 6f);
            var raritiesRow = ChipRow(column, "Rarities", design!, RarityChips, withMarks: true, out var raritiesScroll, out var rarityChips);
            _ = raritiesRow;
            U1.Gap(column, 8f);

            // ── Гнучка ділянка: сітка або «порожньо» ──
            var area = Child(column, "Area");
            area.AddComponent<LayoutElement>().flexibleHeight = 1f;
            BuildGrid(area, design!, quad!, out var scroll, out var content, out var cards);
            var empty = BuildEmpty(area, design!, out var emptyTitle, out var emptyBody, out var emptyPlay);

            // ── Низ вибору: підказка й коралова кнопка ──
            var footer = Child(column, "Footer");
            var footerCol = footer.AddComponent<VerticalLayoutGroup>();
            footerCol.spacing = U1.Px(6f);
            footerCol.padding = new RectOffset(0, 0, (int)U1.Px(8f), 0);
            footerCol.childAlignment = TextAnchor.LowerCenter;
            footerCol.childControlWidth = footerCol.childControlHeight = true;
            footerCol.childForceExpandWidth = true;
            footerCol.childForceExpandHeight = false;
            var hint = U1.Text(footer, "Hint", "Усі копії цієї картинки вже стоять у слотах — домалюй її ще раз", design!, U1.Role.Caption, design!.Coral300);
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.gameObject.SetActive(false);
            var place = U1.Button(footer, "Place", "Обери картинку", design!, CandyButton.Tone.Coral);
            U1.Size(place.gameObject, 0f, 56f);
            var placeLabel = place.transform.Find("Row/Label").GetComponent<TMP_Text>();
            // Довга назва в кнопці стискається до 80 % і далі обрізається «…», а не вилазить за кнопку.
            placeLabel.enableAutoSizing = true;
            placeLabel.fontSizeMax = design!.UiButton;
            placeLabel.fontSizeMin = design!.UiButton * 0.8f;
            placeLabel.overflowMode = TextOverflowModes.Ellipsis;
            var placeRow = place.transform.Find("Row").GetComponent<HorizontalLayoutGroup>();
            placeRow.padding = new RectOffset((int)U1.Px(18f), (int)U1.Px(18f), 0, 0);

            var navBar = U1.NavBar(column, "NavBar", design!, NavBar.Collection);

            Wire(screen,
                ("design", design!),
                ("browseHeader", browseHeader), ("backButton", back), ("settingsButton", gear), ("title", title),
                ("currency", currency), ("subtitle", subtitle),
                ("pickHeader", pickHeader), ("closeButton", close), ("pickTitle", pickTitle),
                ("pickInfo", pickInfo), ("pickInfoIcon", pickIcon), ("pickInfoText", pickText),
                ("planetIcon", U1.S("icon_planet")), ("showcaseIcon", U1.S("icon_grid")),
                ("raritiesScroll", raritiesScroll),
                ("scroll", scroll), ("content", content),
                ("emptyCard", empty), ("emptyTitle", emptyTitle), ("emptyBody", emptyBody), ("emptyPlay", emptyPlay),
                ("footer", footer), ("hintLabel", hint), ("placeButton", place),
                ("placeSprite", U1.S("button_coral")), ("offSprite", U1.S("button_off")),
                ("navBar", navBar));
            WireArray(screen, "rarityChips", rarityChips);
            WireArray(screen, "cards", cards);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Колекцію зібрано: {ScenePath} (U1Glaze, коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        // ── Шапка вибору (S06_02): «×» icon_button 46, «Обери картинку» h1 по центру, справа — порожнє місце 46 ──
        private static GameObject BuildPickHeader(GameObject parent, DesignSystem design, out Button close, out TMP_Text title)
        {
            var row = Child(parent, "PickHeader");
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = U1.Px(10f);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            U1.Size(row, 0f, 56f);

            close = U1.IconButton(row, "Close", design, "icon_close", new Vector2(0.5f, 0.5f), Vector2.zero);
            U1.Size(close.gameObject, 46f, 46f);

            title = U1.Text(row, "Title", "Обери картинку", design, U1.Role.H1, design.InkText);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            var titleSize = title.gameObject.AddComponent<LayoutElement>();
            titleSize.preferredWidth = 0f;
            titleSize.minWidth = 0f;
            titleSize.flexibleWidth = 1f;

            // Противага «×»: назва центрується відносно екрана, а не залишку ряду.
            var spacer = Child(row, "Spacer");
            U1.Size(spacer, 46f, 46f);
            row.SetActive(false);
            return row;
        }

        // ── Смуга вибору (S06_02): panel_low 56, іконка планети бірюзова 24, «Комірка 6 · планета Кріос» ──
        private static GameObject BuildPickInfo(GameObject parent, DesignSystem design, out Image icon, out TMP_Text text)
        {
            var card = U1.Panel(parent, "PickInfo", "panel_low", shadow: false);
            U1.Size(card, 0f, 52f);
            var row = card.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, 0);
            row.spacing = U1.Px(12f);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            var iconGo = Child(card, "Icon");
            icon = iconGo.AddComponent<Image>();
            icon.sprite = U1.S("icon_planet");
            icon.color = design.Teal;
            icon.raycastTarget = false;
            U1.Size(iconGo, 24f, 24f);

            text = U1.Text(card, "Text", "Комірка 1 · планета", design, U1.Role.Body, design.InkText, TextAlignmentOptions.Left);
            text.font = design.FontBlack;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            var textSize = text.gameObject.AddComponent<LayoutElement>();
            textSize.preferredWidth = 0f;
            textSize.minWidth = 0f;
            textSize.flexibleWidth = 1f;
            card.SetActive(false);
            return card;
        }

        // ── Ряд чипів: горизонтальний скрол без смуги, чипи U1.Chip у HorizontalLayoutGroup ──
        private static GameObject ChipRow(GameObject parent, string name, DesignSystem design, int count, bool withMarks,
            out ScrollRect scroll, out ChipView[] chips)
        {
            var row = Child(parent, name);
            U1.Size(row, 0f, 48f);
            scroll = row.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0f;
            var catcher = row.AddComponent<Image>();
            catcher.color = Color.clear; // дотик до проміжків між чипами теж тягне ряд

            var viewportGo = Child(row, "Viewport");
            Stretch(viewportGo);
            // Ряд виходить за поля стовпчика до краю екрана (S06: чипи «зникають» за правим краєм).
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.offsetMax = new Vector2(U1.Px(16f), 0f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var layout = contentGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = U1.Px(8f);
            layout.padding = new RectOffset(0, (int)U1.Px(16f), 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = content;

            chips = new ChipView[count];
            for (var i = 0; i < count; i++)
            {
                var label = i == 0 ? "Усі" : "Рідкість";
                chips[i] = U1.Chip(contentGo, $"Chip{i}", label, design, withMark: withMarks && i > 0);
            }
            return row;
        }

        // ── Сітка: вертикальний скрол, пул карток ──
        private static void BuildGrid(GameObject parent, DesignSystem design, Sprite quad,
            out ScrollRect scroll, out RectTransform content, out CollectionCard[] cards)
        {
            var go = Child(parent, "Grid");
            Stretch(go);
            // Сітка заходить на поля стовпчика на висоту гало й кільця вибору, щоб рамки не зрізало маскою.
            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(-U1.Px(8f), 0f);
            rect.offsetMax = new Vector2(U1.Px(8f), 0f);

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
            content.sizeDelta = new Vector2(-U1.Px(16f), U1.Px(600f));

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = content;

            var columns = Mathf.Max(1, design.CollectionColumns);
            cards = new CollectionCard[columns * PoolRows];
            for (var i = 0; i < cards.Length; i++)
                cards[i] = BuildCard(contentGo, $"Card{i}", design, quad);
        }

        /// <summary>
        /// Картка (S06): рамка рідкості на всю ширину картки (велика, ≥ 70) з мітками й гало; у вирізі — полотно W4
        /// (#403C78) і пікселі з атласу з полем; кільце вибору slot_selected і бірюзова галочка; назва h3 у два рядки
        /// з «…» і лічильник caption. Розміри — токени картки колекції (px макета).
        /// </summary>
        private static CollectionCard BuildCard(GameObject parent, string name, DesignSystem design, Sprite quad)
        {
            var width = design.CollectionCardWidth;
            var height = design.CollectionCardHeight;
            var sidePx = width / DesignSystem.MockupToReference;

            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);

            var group = go.AddComponent<CanvasGroup>();

            // Невидима площина під тап: картка — і картинка, і кнопка.
            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;

            var frame = U1.RarityFrame(go, "Frame", design, big: true, sidePx, Rarity.Common);
            var frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 1f);
            frameRect.pivot = new Vector2(0.5f, 1f);
            frameRect.anchoredPosition = Vector2.zero;

            var pictureGo = frame.transform.Find("Picture").gameObject;
            var canvasImage = pictureGo.AddComponent<Image>();
            canvasImage.sprite = quad;
            canvasImage.color = design.PictureCanvasColor;
            canvasImage.raycastTarget = false;
            var pixelsGo = Child(pictureGo, "Pixels");
            var pixels = pixelsGo.AddComponent<RawImage>();
            pixels.raycastTarget = false;
            pixels.color = Color.white;
            Stretch(pixelsGo, U1.Px(12f));

            // Кільце вибору поверх рамки (кільце slot_selected лягає на 2–3 px назовні від її краю) і бірюзова
            // галочка 30 з іконкою на правому верхньому куті; вмикаються разом.
            var selectionGo = Child(go, "Selection");
            var selectionRect = selectionGo.GetComponent<RectTransform>();
            selectionRect.anchorMin = selectionRect.anchorMax = new Vector2(0.5f, 1f);
            selectionRect.pivot = new Vector2(0.5f, 0.5f);
            selectionRect.anchoredPosition = new Vector2(0f, -width * 0.5f);
            selectionRect.sizeDelta = new Vector2(width, width);
            var ring = selectionGo.AddComponent<Image>();
            ring.sprite = U1.S("slot_selected");
            ring.raycastTarget = false;
            selectionRect.sizeDelta = new Vector2(width * 1.55f, width * 1.55f);

            var checkGo = Child(selectionGo, "Check");
            var check = checkGo.AddComponent<Image>();
            check.sprite = U1.S("toggle_knob_on");
            check.color = design.Teal;
            check.raycastTarget = false;
            var checkSide = U1.Px(30f);
            Place(check, new Vector2(width * 0.5f - U1.Px(4f), width * 0.5f - U1.Px(4f)), new Vector2(checkSide, checkSide),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var tickGo = Child(checkGo, "Tick");
            var tick = tickGo.AddComponent<Image>();
            tick.sprite = U1.S("icon_check");
            tick.color = design.TealInk;
            tick.raycastTarget = false;
            Stretch(tickGo, U1.Px(6f));
            selectionGo.SetActive(false);

            // Назва — до двох рядків під мітками (щільний інтервал, як на S06: рядок ≈ 1 em); лічильник — одразу під
            // останнім рядком назви: позиції для одного й двох рядків рахує збирач, картка лише обирає.
            var nameTop = width + U1.Px(14f);
            var nameLabel = U1.Text(go, "Name", "Назва", design, U1.Role.H3, design.InkText);
            Place(nameLabel, new Vector2(0f, -nameTop), new Vector2(width + U1.Px(8f), U1.Px(48f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            nameLabel.textWrappingMode = TextWrappingModes.Normal;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.lineSpacing = -30f;
            nameLabel.alignment = TextAlignmentOptions.Top;

            var countLabel = U1.Text(go, "Count", "×1", design, U1.Role.Caption, design.InkMuted);
            var countOneLine = -(nameTop + U1.Px(26f));
            var countTwoLines = -(nameTop + U1.Px(46f));
            Place(countLabel, new Vector2(0f, countTwoLines), new Vector2(width + U1.Px(8f), U1.Px(20f)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            countLabel.font = design.FontBlack;
            countLabel.textWrappingMode = TextWrappingModes.NoWrap;
            countLabel.overflowMode = TextOverflowModes.Ellipsis;

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            // Без PressScale: масштаб картки — це масштаб сітки на вузькому екрані (localScale), натиск його перебив би.

            var card = go.AddComponent<CollectionCard>();
            Wire(card, ("design", design), ("group", group), ("frame", frame), ("pixels", pixels),
                ("nameLabel", nameLabel), ("countLabel", countLabel), ("button", button), ("selection", selectionGo));
            var so = new SerializedObject(card);
            so.FindProperty("countOneLineY").floatValue = countOneLine;
            so.FindProperty("countTwoLinesY").floatValue = countTwoLines;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return card;
        }

        // ── Порожньо (S06_03): panel_card, три цукеркові кубики, заголовок h2, пояснення, бірюзова «Грати» ──
        private static GameObject BuildEmpty(GameObject parent, DesignSystem design,
            out TMP_Text title, out TMP_Text body, out CandyButton play)
        {
            var card = U1.Panel(parent, "Empty", "panel_card");
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, U1.Px(260f));
            var layout = card.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(24f);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = U1.Px(12f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cubes = Child(card, "Cubes");
            var cubesRow = cubes.AddComponent<HorizontalLayoutGroup>();
            cubesRow.spacing = U1.Px(10f);
            cubesRow.childAlignment = TextAnchor.MiddleCenter;
            cubesRow.childControlWidth = cubesRow.childControlHeight = true;
            cubesRow.childForceExpandWidth = cubesRow.childForceExpandHeight = false;
            U1.Size(cubes, 0f, 44f);
            var colors = new[] { design.Teal, design.Pink, design.Amber };
            for (var i = 0; i < colors.Length; i++)
            {
                var (pip, _) = U1.Pip(cubes, $"Cube{i}", filled: true, colors[i], 32f);
                U1.Size(pip.gameObject, 32f, 32f);
            }

            title = U1.Text(card, "Title", "Ще немає космічних картинок", design, U1.Role.H2, design.InkText);
            title.textWrappingMode = TextWrappingModes.Normal;
            body = U1.Text(card, "Body", "Вони трапляються найрідше. Грай далі — і одна з них твоя.", design, U1.Role.Body, design.InkMuted);
            body.textWrappingMode = TextWrappingModes.Normal;
            U1.Gap(card, 4f);
            play = U1.Button(card, "Play", "Грати", design, CandyButton.Tone.Teal);
            U1.Size(play.gameObject, 0f, 56f);
            card.SetActive(false);
            return card;
        }
    }
}
