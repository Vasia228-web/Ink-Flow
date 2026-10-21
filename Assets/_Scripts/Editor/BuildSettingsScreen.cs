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
    /// Збирає налаштування U1Glaze (S11SettingsSoon_01, S12SmallScreen_11) фабриками <see cref="U1"/>: шапка 56 («‹» і
    /// назва h2 по центру) → вертикальний скрол: «ЗВУК І ВІБРАЦІЯ» + картка з трьома рядками-перемикачами, ряд
    /// «Додому» (бірюзова) + «Колекція» (скло), «Почати заново» (скло, лише в забігу), «ІНШЕ» + картка (приховати
    /// профіль з поясненням, мова — схована, політика й підтримка з бейджем «Скоро» або «›»); внизу екрана — версія.
    /// Рядки картки — 57 px, роздільник line-700 зверху кожного, крім першого. Меню: Ink Flow → Setup → Build Settings Screen.
    /// </summary>
    public static class BuildSettingsScreen
    {
        private const string ScenePath = "Assets/Scenes/Settings.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        /// <summary>Висота рядка картки (S11_01: 57 px), поля рядка й відступ роздільника від краю картки.</summary>
        private const float RowPx = 57f;
        private const float RowPaddingPx = 20f;
        private const float DividerInsetPx = 15f;

        [MenuItem("Ink Flow/Setup/Build Settings Screen")]
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
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (U1GlazeSprites.Load("toggle_track_on") == null) missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Налаштування НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "SettingsScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<SettingsScreen>();

            var column = Child(screenGo, "Column");
            Stretch(column);
            var col = column.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, (int)U1.Px(8f));
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            var back = BuildHeader(column, design!);

            // ── Скрол: на SE у забігу все разом не вміщається, на високих телефонах — без прокрутки ──
            var scrollGo = Child(column, "Scroll");
            scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            var catcher = scrollGo.AddComponent<Image>();
            catcher.color = Color.clear;
            var viewportGo = Child(scrollGo, "Viewport");
            Stretch(viewportGo);
            // Маска виходить за поля стовпчика: тіні карток не зрізаються по краях.
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.offsetMin = new Vector2(-U1.Px(16f), 0f);
            viewportRect.offsetMax = new Vector2(U1.Px(16f), 0f);
            viewportGo.AddComponent<RectMask2D>();
            var contentGo = Child(viewportGo, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var stack = contentGo.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), (int)U1.Px(6f), (int)U1.Px(20f));
            stack.spacing = 0f;
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = content;

            // ── ЗВУК І ВІБРАЦІЯ ──
            SectionLabel(contentGo, "SoundCaption", "Звук і вібрація", design!);
            var soundCard = Card(contentGo, "SoundCard");
            var sound = ToggleRow(soundCard, "Sound", "Звук", null, design!, first: true);
            var music = ToggleRow(soundCard, "Music", "Музика", null, design!);
            var vibration = ToggleRow(soundCard, "Vibration", "Вібрація", null, design!);

            // ── Дії: «Додому» + «Колекція», під ними в забігу «Почати заново» ──
            U1.Gap(contentGo, 16f);
            var actions = Child(contentGo, "Actions");
            var actionsLayout = actions.AddComponent<VerticalLayoutGroup>();
            actionsLayout.spacing = U1.Px(12f);
            actionsLayout.childControlWidth = actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = true;
            actionsLayout.childForceExpandHeight = false;
            actions.AddComponent<LayoutElement>().flexibleHeight = 0f;

            var pair = Child(actions, "Pair");
            var pairLayout = pair.AddComponent<HorizontalLayoutGroup>();
            pairLayout.spacing = U1.Px(12f);
            pairLayout.childControlWidth = pairLayout.childControlHeight = true;
            pairLayout.childForceExpandWidth = true;
            pairLayout.childForceExpandHeight = false;
            U1.Size(pair, 0f, 56f).flexibleHeight = 0f;
            var home = U1.Button(pair, "Home", "Додому", design!, CandyButton.Tone.Teal);
            ActionSize(home);
            var collection = U1.Button(pair, "Collection", "Колекція", design!, CandyButton.Tone.Glass);
            ActionSize(collection);

            var restart = U1.Button(actions, "Restart", "Почати заново", design!, CandyButton.Tone.Glass);
            ActionSize(restart);
            restart.gameObject.SetActive(false);

            // ── ІНШЕ ──
            U1.Gap(contentGo, 8f);
            var othersCaption = SectionLabel(contentGo, "OthersCaption", "Інше", design!);
            var othersCard = Card(contentGo, "OthersCard");
            var hideProfile = ToggleRow(othersCard, "HideProfile", "Приховати профіль у рейтингах",
                "Інші бачитимуть тебе як «Гравець-інкогніто»", design!, first: true);
            var language = LinkRow(othersCard, "Language", "Мова", design!, out var languageValue, out var languageSoon, out var languageChevron);
            languageValue.gameObject.SetActive(true);
            languageValue.text = "Українська";
            languageSoon.SetActive(false);
            languageChevron.SetActive(true);
            // Рядок мови в префабі схований: екран вмикає його лише з двома й більше локалями.
            language.gameObject.SetActive(false);
            var privacy = LinkRow(othersCard, "Privacy", "Політика приватності", design!, out _, out var privacySoon, out var privacyChevron);
            var support = LinkRow(othersCard, "Support", "Написати в підтримку", design!, out _, out var supportSoon, out var supportChevron);

            // ── Версія — внизу екрана, поза скролом ──
            var version = U1.Text(column, "Version", "SpacePaint · версія 1.0", design!, U1.Role.Caption, design!.InkDim);
            version.textWrappingMode = TextWrappingModes.NoWrap;
            U1.Size(version.gameObject, 0f, 32f).flexibleHeight = 0f;

            Wire(screen,
                ("design", design), ("backButton", back),
                ("soundToggle", sound), ("musicToggle", music), ("vibrationToggle", vibration),
                ("homeButton", home), ("collectionButton", collection), ("restartButton", restart),
                ("content", content), ("othersCaption", othersCaption), ("hideProfileToggle", hideProfile),
                ("languageButton", language), ("languageValue", languageValue),
                ("privacyButton", privacy), ("privacySoon", privacySoon), ("privacyChevron", privacyChevron),
                ("supportButton", support), ("supportSoon", supportSoon), ("supportChevron", supportChevron),
                ("versionLabel", version));

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Налаштування зібрано: {ScenePath} (U1Glaze, коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        /// <summary>Шапка 56 (S11_01): «‹» icon_button 46 ліворуч, назва h2 по центру шапки (не по залишку ряду).</summary>
        private static Button BuildHeader(GameObject column, DesignSystem design)
        {
            var row = Child(column, "Header");
            U1.Size(row, 0f, 56f).flexibleHeight = 0f;

            var back = U1.IconButton(row, "Back", design, "icon_back", new Vector2(0f, 0.5f), Vector2.zero);

            var title = U1.Text(row, "Title", "Налаштування", design, U1.Role.H2, design.InkText);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            // Поля під «‹» з обох боків: назва по центру й не залазить під кнопку.
            var side = design.IconButtonSize + U1.Px(8f);
            titleRect.offsetMin = new Vector2(side, 0f);
            titleRect.offsetMax = new Vector2(-side, 0f);
            return back;
        }

        /// <summary>Підпис секції («ЗВУК І ВІБРАЦІЯ», «ІНШЕ»): label великими, ink-muted, з відступом 6 від краю картки.</summary>
        private static TMP_Text SectionLabel(GameObject parent, string name, string text, DesignSystem design)
        {
            var label = U1.Text(parent, name, text, design, U1.Role.Label, design.InkMuted, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = new Vector4(U1.Px(6f), 0f, 0f, 0f);
            U1.Size(label.gameObject, 0f, 26f).flexibleHeight = 0f;
            U1.Gap(parent, 6f);
            return label;
        }

        /// <summary>Картка panel_card: рядки один під одним, поля 4 згори й знизу.</summary>
        private static GameObject Card(GameObject parent, string name)
        {
            var card = U1.Panel(parent, name, "panel_card");
            var layout = card.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(4f);
            layout.padding = new RectOffset(0, 0, pad, pad);
            layout.spacing = 0f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            card.AddComponent<LayoutElement>().flexibleHeight = 0f;
            return card;
        }

        private static void ActionSize(CandyButton button)
        {
            var size = U1.Size(button.gameObject, 0f, 56f);
            size.flexibleWidth = 1f;
            size.flexibleHeight = 0f;
        }

        /// <summary>
        /// Рядок картки: зона тапу на весь рядок, роздільник зверху (крім першого), вертикальна група з «лінією»
        /// (напис ліворуч + праве) і, якщо є, поясненням під нею. Напис переноситься (S12_11: «Приховати профіль
        /// у рейтингах» на SE — два рядки), рядок росте разом із ним.
        /// </summary>
        private static (GameObject row, GameObject line, TMP_Text label, Button button) Row(GameObject card, string name, string text,
            string? note, DesignSystem design, bool first)
        {
            var row = Child(card, name);
            var hit = row.AddComponent<Image>();
            hit.color = Color.clear;
            var layout = row.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(RowPaddingPx);
            layout.padding = new RectOffset(pad, pad, 0, note != null ? (int)U1.Px(12f) : 0);
            layout.spacing = 0f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().flexibleHeight = 0f;

            if (!first)
            {
                var divider = U1.Divider(row, "Divider", design);
                divider.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var rect = divider.rectTransform;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(U1.Px(DividerInsetPx), -U1.Px(1f));
                rect.offsetMax = new Vector2(-U1.Px(DividerInsetPx), 0f);
            }

            var line = Child(row, "Line");
            var lineLayout = line.AddComponent<HorizontalLayoutGroup>();
            // Проміжок 4, не 12: напис вирівняний ліворуч і до перемикача майже не доходить, а на 402 «Приховати
            // профіль у рейтингах» потребує ~263 з 266 px — з 12 переносився б (S11_01 — один рядок).
            lineLayout.spacing = U1.Px(4f);
            lineLayout.childAlignment = TextAnchor.MiddleLeft;
            lineLayout.childControlWidth = lineLayout.childControlHeight = true;
            lineLayout.childForceExpandWidth = lineLayout.childForceExpandHeight = false;
            var lineSize = line.AddComponent<LayoutElement>();
            lineSize.minHeight = U1.Px(note != null ? 50f : RowPx);
            lineSize.flexibleHeight = 0f;

            // S11_01: напис рядка — 16/900 (кегль body, Nunito Black, як у чипа), а не h3 18: на 402 «Приховати профіль
            // у рейтингах» стає в один рядок поруч із перемикачем, на SE переноситься (S12_11).
            var label = U1.Text(line, "Label", text, design, U1.Role.Body, design.InkText, TextAlignmentOptions.Left);
            label.font = design.FontBlack;
            label.textWrappingMode = TextWrappingModes.Normal;
            var labelSize = label.gameObject.AddComponent<LayoutElement>();
            labelSize.preferredWidth = 0f;
            labelSize.minWidth = 0f;
            labelSize.flexibleWidth = 1f;

            if (note != null)
            {
                var caption = U1.Text(row, "Note", note, design, U1.Role.Caption, design.InkMuted, TextAlignmentOptions.Left);
                caption.textWrappingMode = TextWrappingModes.Normal;
            }

            var button = row.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            return (row, line, label, button);
        }

        /// <summary>Рядок із перемикачем U1 56×34 праворуч; тап по всьому рядку.</summary>
        private static SettingsToggle ToggleRow(GameObject card, string name, string text, string? note, DesignSystem design, bool first = false)
        {
            var (row, line, label, button) = Row(card, name, text, note, design, first);
            var (track, knob) = U1.Toggle(line, "Toggle", design, true);
            track.raycastTarget = false;
            U1.Size(track.gameObject, 56f, 34f);

            var toggle = row.AddComponent<SettingsToggle>();
            Wire(toggle, ("label", label), ("button", button), ("track", track), ("knob", knob),
                ("trackOn", U1.S("toggle_track_on")), ("trackOff", U1.S("toggle_track_off")),
                ("knobOn", U1.S("toggle_knob_on")), ("knobOff", U1.S("toggle_knob_off")));
            var so = new SerializedObject(toggle);
            so.FindProperty("knobTravel").floatValue = U1.Px(U1.ToggleKnobTravelPx);
            so.ApplyModifiedPropertiesWithoutUndo();
            return toggle;
        }

        /// <summary>Рядок-посилання: напис ліворуч, праворуч — значення (мова), сірий бейдж «Скоро» або «›».</summary>
        private static Button LinkRow(GameObject card, string name, string text, DesignSystem design,
            out TMP_Text value, out GameObject soon, out GameObject chevron)
        {
            var (_, line, _, button) = Row(card, name, text, null, design, first: false);

            value = U1.Text(line, "Value", string.Empty, design, U1.Role.Body, design.InkMuted, TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.gameObject.SetActive(false);

            soon = U1.Badge(line, "Soon", "Скоро", design, "badge_grey", design.InkText).transform.parent.gameObject;

            var chevronGo = Child(line, "Chevron");
            var icon = chevronGo.AddComponent<Image>();
            icon.sprite = U1.S("icon_chev");
            icon.color = design.InkMuted;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            U1.Size(chevronGo, 20f, 20f);
            chevronGo.SetActive(false);
            chevron = chevronGo;
            return button;
        }
    }
}
