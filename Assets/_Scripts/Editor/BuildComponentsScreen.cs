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
    /// Сцена-вітрина спільних компонентів U1Glaze — дзеркало preview/S00Components.png: кожен
    /// компонент зібрано тими самими фабриками <see cref="U1"/>, якими далі збираються екрани.
    /// Якщо на вітрині щось не схоже на еталон — лагодиться фабрика, і всі екрани отримують правку.
    /// Меню: Ink Flow → Setup → Build Components Showcase. Знімки — стан components-N у MetaScreenshots.
    /// </summary>
    public static class BuildComponentsScreen
    {
        private const string ScenePath = "Assets/Scenes/Components.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private static DesignSystem design = null!;

        [MenuItem("Ink Flow/Setup/Build Components Showcase")]
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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            if (design == null || cosmic == null || U1GlazeSprites.Load("panel_card") == null)
            {
                Debug.LogError("[InkFlow] Вітрину НЕ зібрано: потрібні DesignSystem.asset, CosmicBackground.prefab і спрайти U1Glaze (Build UI Kit).");
                return;
            }

            CreateCamera(design);
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
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler), ("navigation", navigation), ("safeArea", safeBinder));

            var screenGo = Child(safe, "ComponentsShowcase");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<ComponentsShowcase>();

            var scroll = VerticalScroll(screenGo, "Scroll", 0f, out var content);
            // Новий RectTransform має sizeDelta 100×100: при розтягнутих якорях вміст був би на 100 од. ширший за в'юпорт.
            content.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var column = content.AddComponent<VerticalLayoutGroup>();
            var gutter = (int)design.SideGutter;
            column.padding = new RectOffset(gutter, gutter, (int)U1.Px(12f), (int)U1.Px(24f));
            column.spacing = U1.Px(10f);
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Headers(content);
            Oil(content);
            Buttons(content);
            Controls(content);
            Badges(content);
            CardsAndDialog(content);
            States(content);
            Navigation(content);
            Slots(content);
            Frames(content);

            Wire(screen, ("scroll", scroll), ("content", content.GetComponent<RectTransform>()));

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
            uiRoot.ApplyScaler();

            SaveScreenPrefab(screenGo);
            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Вітрину компонентів зібрано: {ScenePath}.");
        }

        // ───────────────────────── Секції ─────────────────────────

        private static void Headers(GameObject content)
        {
            var section = Section(content, "ШАПКА");

            var main = Row(section, "Main", 8f);
            var avatar = Img(main, "Avatar", "avatar_1", 44f);
            _ = avatar;
            var nick = U1.Text(main, "Nick", "Космічний_Маляр2", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            Fix(nick.gameObject, 60f, 44f);
            Spacer(main);
            Capsule(main, 999_999_999);
            IconButton(main, "Gear", "icon_gear");
            Caption(section, "головний: аватар, нік (обрізається «…»), нафта до 999\u00A0999\u00A0999, шестерня");

            var tab = Row(section, "Tab", 8f);
            var title = U1.Text(tab, "Title", "Колекція", design, U1.Role.H1, design.InkText, TextAlignmentOptions.Left);
            Flex(title.gameObject);
            Capsule(tab, 1240);
            IconButton(tab, "Gear", "icon_gear");
            Caption(section, "вкладка: назва зліва");

            var sub = Row(section, "Sub", 8f);
            IconButton(sub, "Back", "icon_back");
            var subTitle = U1.Text(sub, "Title", "Налаштування", design, U1.Role.H2, design.InkText);
            Flex(subTitle.gameObject);
            Fix(Child(sub, "Spacer"), 46f, 46f);
            Caption(section, "підекран: «назад», назва по центру");

            var subOil = Row(section, "SubOil", 8f);
            IconButton(subOil, "Back", "icon_back");
            Spacer(subOil);
            Capsule(subOil, 1240);
            IconButton(subOil, "Gear", "icon_gear");
            Caption(section, "підекран із нафтою");
        }

        private static void Oil(GameObject content)
        {
            var section = Section(content, "НАФТА");
            var drops = Row(section, "Drops", 14f);
            foreach (var size in new[] { 16f, 24f, 30f, 48f, 72f })
                Img(drops, $"Drop{size:0}", "oil_drop", size);
            Caption(section, "крапля нафти: 16 · 24 · 30 · 48 · 72");

            var capsules = Row(section, "Capsules", 10f);
            Capsule(capsules, 0);
            Capsule(capsules, 1240);
            var wide = Row(section, "CapsuleWide", 10f);
            Capsule(wide, 999_999_999);
            Caption(section, "капсула: крапля й число; ширина росте за вмістом");
        }

        private static void Buttons(GameObject content)
        {
            var section = Section(content, "КНОПКИ");
            var play = U1.Button(section, "Button_Coral", "Грати", design, CandyButton.Tone.Coral, icon: U1.S("icon_play"));
            Fix(play.gameObject, 0f, 56f, flexWidth: true);
            Caption(section, "головна — коралова цукерка, одна на екран");

            var teal = U1.Button(section, "Button_Teal", "Поставити картинку", design, CandyButton.Tone.Teal);
            Fix(teal.gameObject, 0f, 56f, flexWidth: true);
            Caption(section, "позитивна дія / підтвердження — бірюзова");

            var glass = U1.Button(section, "Button_Glass", "Повернути в колекцію", design, CandyButton.Tone.Glass);
            Fix(glass.gameObject, 0f, 56f, flexWidth: true);
            Caption(section, "другорядна — скляна");

            var off = U1.Button(section, "Button_Off", "Відкрити", design, CandyButton.Tone.Off);
            Fix(off.gameObject, 0f, 56f, flexWidth: true);
            off.Interactable = false;
            Caption(section, "вимкнена — без блиску й світіння");

            var priced = U1.Button(section, "Button_Price", "Домалювати", design, CandyButton.Tone.Teal);
            Fix(priced.gameObject, 0f, 56f, flexWidth: true);
            U1.PriceChip(priced, 500, design);
            var small = U1.Button(section, "Button_Small", string.Empty, design, CandyButton.Tone.Glass, small: true);
            Fix(small.gameObject, 110f, 46f);
            U1.PriceChip(small, 300, design);
            Caption(section, "з ціною в нафті · мала (46)");

            var text = U1.TextButton(section, "Button_Text", "Скасувати", design, design.InkMuted);
            Fix(text.gameObject, 0f, 44f, flexWidth: true);
            Caption(section, "текстова — 44 у висоту");
        }

        private static void Controls(GameObject content)
        {
            var section = Section(content, "ПЕРЕМИКАЧІ, ТАБИ, ЧИПИ");
            var toggles = Row(section, "Toggles", 12f);
            foreach (var (name, on, disabled) in new[] { ("On", true, false), ("Off", false, false), ("Disabled", false, true) })
            {
                var (track, _) = U1.Toggle(toggles, $"Toggle{name}", design, on);
                Fix(track.gameObject, 56f, 34f);
                if (disabled)
                    track.gameObject.AddComponent<CanvasGroup>().alpha = design.DisabledOpacity;
            }
            Caption(section, "перемикач: увімк · вимк · недоступний");

            var segment = Child(section, "Segment");
            var track2 = segment.AddComponent<Image>();
            track2.sprite = U1.S("segment_track");
            track2.type = Image.Type.Sliced;
            Fix(segment, 0f, 48f, flexWidth: true);
            var segRow = segment.AddComponent<HorizontalLayoutGroup>();
            var pad = (int)U1.Px(4f);
            segRow.padding = new RectOffset(pad, pad, pad, pad);
            segRow.childControlWidth = true;
            segRow.childControlHeight = true;
            segRow.childForceExpandWidth = true;
            segRow.childForceExpandHeight = true;
            var onGo = Child(segment, "On");
            var onImage = onGo.AddComponent<Image>();
            onImage.sprite = U1.S("segment_on");
            onImage.type = Image.Type.Sliced;
            var onLabel = U1.Text(onGo, "Label", "Планети", design, U1.Role.H3, design.TealInk);
            Stretch(onLabel.gameObject);
            var offGo = Child(segment, "Off");
            var offLabel = U1.Text(offGo, "Label", "Галактики", design, U1.Role.H3, design.InkMuted);
            Stretch(offLabel.gameObject);
            Caption(section, "сегментний перемикач");

            var tabs = Row(section, "Tabs", 24f);
            foreach (var (name, on) in new[] { ("Цей тиждень", true), ("За весь час", false) })
            {
                var tabGo = Child(tabs, name);
                var col = tabGo.AddComponent<VerticalLayoutGroup>();
                col.spacing = U1.Px(6f);
                col.childControlWidth = true;
                col.childControlHeight = true;
                col.childForceExpandWidth = false;
                col.childForceExpandHeight = false;
                var label = U1.Text(tabGo, "Label", name, design, U1.Role.H3, on ? design.InkText : design.InkMuted);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var line = Img(tabGo, "Underline", "tab_underline", 4f);
                var lineImage = line.GetComponent<Image>();
                lineImage.type = Image.Type.Sliced;
                line.GetComponent<LayoutElement>().preferredWidth = U1.Px(60f);
                line.SetActive(on);
                tabGo.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            Caption(section, "таби");

            var chips = Row(section, "Chips", 8f);
            foreach (var (name, on) in new[] { ("Усі теми", true), ("Космос", false), ("Їжа", false) })
            {
                var chip = U1.Chip(chips, name, name, design);
                chip.SetOn(on);
            }
            var rarityChips = Row(section, "RarityChips", 8f);
            var all = U1.Chip(rarityChips, "Усі", "Усі", design);
            all.SetOn(true);
            foreach (var (name, rarity) in new[] { ("Звичайна", Rarity.Common), ("Незвичайна", Rarity.Uncommon) })
            {
                var chip = U1.Chip(rarityChips, name, name, design, withMark: true);
                chip.SetMark(design.RarityFrame(rarity));
            }
            Caption(section, "чипи фільтрів: теми · рідкість (з міткою рамки)");
        }

        private static void Badges(GameObject content)
        {
            var section = Section(content, "БЕЙДЖІ");
            // Два ряди: п'ять бейджів в один ряд не вміщаються на 402 — ряд стиснув би їх, і написи налізли б.
            var row = Row(section, "Badges", 8f);
            // Слова пакетів магазину («Найвигідніше», «Популярне») — у запасі разом із магазином; кольори — ті самі.
            U1.Badge(row, "Record", "Рекорд", design, "badge_amber", design.AmberInk);
            U1.Badge(row, "New", "Нова", design, "badge_pink", design.PinkInk);
            U1.Badge(row, "Open", "Відкрито", design, "badge_teal", design.TealInk);
            var row2 = Row(section, "Badges2", 8f);
            U1.Badge(row2, "Soon", "Скоро", design, "badge_grey", design.InkText);
            U1.Badge(row2, "Burn", "Згорить", design, "badge_coral", Color.white);
            Caption(section, "бурштин · рожевий · бірюза · сірий · кораловий");

            // Як в еталоні — по одному в рядок: три бейджі рідкості з мітками в ряд на 402 не вміщаються,
            // і ряд стискав їх так, що вони налазили один на одного.
            for (var r = 0; r < 6; r++)
            {
                var rarityRow = Row(section, $"RarityBadge{r + 1}", 8f);
                U1.Badge(rarityRow, $"Rarity{r + 1}", RarityNames.Of((Rarity)r), design, $"badge_rarity_{r + 1}", design.PanelBottom,
                    marks: $"pips_{r + 1}");
            }
            Caption(section, "рідкість: колір + кількість міток 1–6");
        }

        private static void CardsAndDialog(GameObject content)
        {
            var section = Section(content, "КАРТКА І ДІАЛОГ");
            var card = TextPanel(section, "Card", "panel_card", "Картка", "Панель K1Candy: градієнт, світла рамка, тінь. Кути 24.");
            _ = card;
            var low = TextPanel(section, "CardLow", "panel_low", "Картка нижчого рівня", "Для рядків списків і вкладених блоків.", shadow: false);
            _ = low;

            var dialog = U1.Panel(section, "Dialog", "dialog");
            var col = dialog.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(20f);
            col.padding = new RectOffset(pad, pad, pad, pad);
            col.spacing = U1.Px(12f);
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            dialog.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            U1.Text(dialog, "Title", "Вийти додому?", design, U1.Role.H1, design.InkText);
            var body = U1.Text(dialog, "Body", "Забіг збережеться — продовжиш пізніше.", design, U1.Role.Body, design.InkMuted);
            body.textWrappingMode = TextWrappingModes.Normal;
            var stay = U1.Button(dialog, "Stay", "Грати далі", design, CandyButton.Tone.Teal);
            Fix(stay.gameObject, 0f, 56f, flexWidth: true);
            var leave = U1.Button(dialog, "Leave", "Вийти", design, CandyButton.Tone.Glass);
            Fix(leave.gameObject, 0f, 56f, flexWidth: true);
            Caption(section, "діалог непрозорий, під ним затемнення 78 %");
        }

        private static void States(GameObject content)
        {
            var section = Section(content, "СТАНИ");

            var loading = StatePanel(section, "Loading");
            var dots = Row(loading, "Dots", 8f);
            dots.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var (name, color) in new[] { ("A", design.Teal), ("B", design.Pink), ("C", design.Amber) })
                Img(dots, name, "pip_filled", 18f).GetComponent<Image>().color = color;
            U1.Text(loading, "Title", "Завантаження…", design, U1.Role.H2, design.InkText);

            var offline = StatePanel(section, "Offline");
            var wifi = Img(offline, "Icon", "icon_wifi_off", 48f);
            wifi.GetComponent<Image>().color = design.InkMuted;
            U1.Text(offline, "Title", "Немає з’єднання", design, U1.Role.H2, design.InkText);
            var hint = U1.Text(offline, "Body", "Рейтинги оновляться, щойно з’явиться інтернет.", design, U1.Role.Body, design.InkMuted);
            hint.textWrappingMode = TextWrappingModes.Normal;
            var retry = U1.Button(offline, "Retry", "Спробувати ще", design, CandyButton.Tone.Glass, small: true);
            Fix(retry.gameObject, 220f, 46f);

            var empty = StatePanel(section, "Empty");
            var tileGo = Child(empty, "Tile");
            var tile = tileGo.AddComponent<Image>();
            tile.sprite = U1.S("well");
            tile.type = Image.Type.Sliced;
            Fix(tileGo, 72f, 72f);
            var heart = Img(tileGo, "Icon", "icon_grid", 32f);
            heart.GetComponent<Image>().color = design.InkMuted;
            heart.GetComponent<LayoutElement>().ignoreLayout = true;
            Place(heart.GetComponent<Image>(), Vector2.zero, new Vector2(U1.Px(32f), U1.Px(32f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            U1.Text(empty, "Title", "Тут поки порожньо", design, U1.Role.H2, design.InkText);
            var emptyHint = U1.Text(empty, "Body", "Домалюй першу картинку — вона з’явиться в колекції.", design, U1.Role.Body, design.InkMuted);
            emptyHint.textWrappingMode = TextWrappingModes.Normal;
        }

        private static void Navigation(GameObject content)
        {
            var section = Section(content, "НИЖНЯ НАВІГАЦІЯ");
            NavBar(section, "NavPlay", 0);
            Caption(section, "4 вкладки, активна — бірюзова цукерка");
            NavBar(section, "NavRanks", 3);
        }

        private static void Slots(GameObject content)
        {
            var section = Section(content, "ЛУНКИ, КОМІРКИ, МІТКИ ПРОГРЕСУ");
            var row = Row(section, "Slots", 16f);
            Img(row, "Empty", "slot_empty", 72f);
            FilledSlot(row, "Filled", Rarity.Uncommon, selected: false);
            FilledSlot(row, "Selected", Rarity.Legendary, selected: true);
            Caption(section, "комірка: порожня · заповнена (рамка кольору рідкості) · вибрана");

            var pips = Row(section, "Pips", 4f);
            var colors = new[] { design.Pink, design.InkText, design.Coral300, design.Teal, design.Amber, design.Pink300, design.Teal100, design.Coral };
            for (var i = 0; i < 8; i++)
                Img(pips, $"Filled{i}", "pip_filled", 16f).GetComponent<Image>().color = colors[i];
            for (var i = 0; i < 8; i++)
                Img(pips, $"Empty{i}", "pip_empty", 16f);
            Caption(section, "мітки прогресу: заповнена — колір картинки");
        }

        private static void Frames(GameObject content)
        {
            var section = Section(content, "6 РАМОК РІДКОСТІ");
            // Великі — по три в ряд, 70 px, як в еталоні: шість у ряд на 402 стискались до 58 px, назви — до різних
            // кеглів, і назви налазили на мітки (мітки сидять поза розкладкою, на 8 px нижче рамки).
            for (var half = 0; half < 2; half++)
            {
                var big = Row(section, $"Big{half + 1}", 8f);
                for (var r = half * 3; r < half * 3 + 3; r++)
                {
                    var cell = Child(big, $"Cell{r + 1}");
                    var column = cell.AddComponent<VerticalLayoutGroup>();
                    column.spacing = U1.Px(18f); // місце під мітки: центр на 8 нижче + пів висоти 14 + повітря
                    column.childAlignment = TextAnchor.UpperCenter;
                    column.childControlWidth = column.childControlHeight = true;
                    column.childForceExpandWidth = column.childForceExpandHeight = false;
                    Fix(cell, 116f, 70f + 18f + 20f);
                    var frame = U1.RarityFrame(cell, $"Frame{r + 1}", design, big: true, sidePx: 70f, (Rarity)r);
                    Fix(frame.gameObject, 70f, 70f);
                    Placeholder(frame);
                    var name = RarityNames.Of((Rarity)r);
                    var label = U1.Text(cell, $"Name{r + 1}", name.Substring(0, 1) + name.Substring(1).ToLowerInvariant(),
                        design, U1.Role.Caption, design.InkMuted, TextAlignmentOptions.Center);
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
            var small = Row(section, "Small", 12f);
            for (var r = 0; r < 6; r++)
            {
                var frame = U1.RarityFrame(small, $"Small{r + 1}", design, big: false, sidePx: 40f, (Rarity)r);
                Fix(frame.gameObject, 40f, 40f);
                Placeholder(frame);
            }
            Fix(Child(section, "PipsRoom"), 1f, 12f); // мітки малих рамок — під рядом, підпис не має на них лізти
            Caption(section, "Розрізняються не лише кольором: товщина, подвійна лінія (Епічна), іскри (Легендарна), перелив (Космічна) та мітки 1–6.");
        }

        // ───────────────────────── Помічники ─────────────────────────

        private static GameObject Section(GameObject content, string caption)
        {
            var go = Child(content, caption);
            var col = go.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(0, 0, (int)U1.Px(10f), (int)U1.Px(6f));
            col.spacing = U1.Px(10f);
            col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            var title = U1.Text(go, "Caption", caption, design, U1.Role.Label, design.Amber, TextAlignmentOptions.Left);
            Flex(title.gameObject); // висота — від самого напису (TMP як ILayoutElement), не фіксована
            return go;
        }

        private static GameObject Row(GameObject parent, string name, float spacingPx)
        {
            var go = Child(parent, name);
            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.spacing = U1.Px(spacingPx);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return go;
        }

        private static void Caption(GameObject parent, string text)
        {
            var label = U1.Text(parent, "Note", text, design, U1.Role.Caption, design.InkDim, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
        }

        private static GameObject Img(GameObject parent, string name, string sprite, float sizePx)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = U1.S(sprite);
            image.raycastTarget = false;
            Fix(go, sizePx, sizePx);
            return go;
        }

        private static void Fix(GameObject go, float widthPx, float heightPx, bool flexWidth = false)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredHeight = U1.Px(heightPx);
            element.minHeight = U1.Px(heightPx);
            if (flexWidth)
                element.flexibleWidth = 1f;
            else
            {
                element.preferredWidth = U1.Px(widthPx);
                element.minWidth = U1.Px(widthPx);
            }
        }

        private static void Flex(GameObject go)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
        }

        private static void Spacer(GameObject row) => Flex(Child(row, "Spacer"));

        private static void Capsule(GameObject row, long amount)
        {
            var widget = U1.Capsule(row, new Vector2(0.5f, 0.5f), Vector2.zero);
            if (widget == null)
                return;
            widget.SetPreviewAmount(amount);
            var element = widget.gameObject.GetComponent<LayoutElement>() ?? widget.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = U1.Px(46f);
        }

        private static void IconButton(GameObject row, string name, string icon)
        {
            var button = U1.IconButton(row, name, design, icon, new Vector2(0.5f, 0.5f), Vector2.zero);
            Fix(button.gameObject, 46f, 46f);
        }

        private static GameObject TextPanel(GameObject parent, string name, string sprite, string title, string body, bool shadow = true)
        {
            var panel = U1.Panel(parent, name, sprite, shadow);
            var col = panel.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(16f);
            col.padding = new RectOffset(pad, pad, pad, pad);
            col.spacing = U1.Px(6f);
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            U1.Text(panel, "Title", title, design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
            var text = U1.Text(panel, "Body", body, design, U1.Role.Body, design.InkMuted, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.Normal;
            return panel;
        }

        private static GameObject StatePanel(GameObject parent, string name)
        {
            var panel = U1.Panel(parent, name, "panel_card");
            var col = panel.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(20f);
            col.padding = new RectOffset(pad, pad, pad, pad);
            col.spacing = U1.Px(10f);
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        private static void NavBar(GameObject parent, string name, int active)
        {
            var bar = U1.Panel(parent, name, "panel_card");
            Fix(bar, 0f, 76f, flexWidth: true);
            var row = bar.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            var items = new[] { ("Грати", "icon_play"), ("Галактика", "icon_planet"), ("Колекція", "icon_grid"), ("Рейтинги", "icon_trophy") };
            for (var i = 0; i < items.Length; i++)
            {
                var (label, icon) = items[i];
                var item = Child(bar, label);
                var col = item.AddComponent<VerticalLayoutGroup>();
                col.childAlignment = TextAnchor.MiddleCenter;
                col.spacing = U1.Px(4f);
                col.childControlWidth = true;
                col.childControlHeight = true;
                col.childForceExpandWidth = false;
                col.childForceExpandHeight = false;
                var tileGo = Child(item, "Tile");
                var tile = tileGo.AddComponent<Image>();
                tile.sprite = U1.S("nav_active");
                tile.type = Image.Type.Sliced;
                tile.enabled = i == active;
                Fix(tileGo, 46f, 36f);
                var iconGo = Img(tileGo, "Icon", icon, 24f);
                iconGo.GetComponent<LayoutElement>().ignoreLayout = true;
                var iconImage = iconGo.GetComponent<Image>();
                iconImage.color = i == active ? design.TealInk : design.InkMuted;
                Place(iconImage, Vector2.zero, new Vector2(U1.Px(24f), U1.Px(24f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                var text = U1.Text(item, "Label", label, design, U1.Role.Caption, i == active ? design.InkText : design.InkMuted);
                text.font = design.FontBlack;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        private static void FilledSlot(GameObject row, string name, Rarity rarity, bool selected)
        {
            var go = Child(row, name);
            Fix(go, 72f, 72f);
            if (selected)
            {
                var ringGo = Child(go, "Ring");
                var ring = ringGo.AddComponent<Image>();
                ring.sprite = U1.S("slot_selected");
                ring.raycastTarget = false;
                Place(ring, Vector2.zero, new Vector2(U1.Px(124f), U1.Px(124f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }
            var baseGo = Child(go, "Base");
            var baseImage = baseGo.AddComponent<Image>();
            baseImage.sprite = U1.S("slot_frame_base");
            Stretch(baseGo);
            var pictureGo = Child(go, "Picture");
            var picture = pictureGo.AddComponent<Image>();
            picture.sprite = LoadSprite("white-quad");
            picture.color = design.PanelLowBottom;
            Stretch(pictureGo, U1.Px(6f));
            var strokeGo = Child(go, "Stroke");
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = U1.S("slot_frame_stroke");
            stroke.color = design.RarityFrame(rarity);
            stroke.raycastTarget = false;
            Stretch(strokeGo);
            if (selected)
                go.transform.localScale = Vector3.one * 1.1f;
        }

        private static void Placeholder(RarityFrameView frame)
        {
            var picture = frame.transform.Find("Picture");
            var image = picture.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite("white-quad");
            image.color = design.PanelLowBottom;
            image.raycastTarget = false;
        }
    }
}
