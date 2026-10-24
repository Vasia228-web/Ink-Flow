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
    /// Збирає рейтинги «Ліга» U1Glaze (S08Ratings_01…04, S12SmallScreen_09) фабриками <see cref="U1"/>: стовпчик —
    /// шапка 56 («Рейтинги» h1, капсула, шестерня) → картка «Ти» (panel_card з бірюзовою обводкою: місце, аватар,
    /// «Ти · нік», своє число, смужка й «ще N до №M») → сегменти «Планети / Галактики» → таби періоду з бурштиновим
    /// підкресленням → гнучка картка таблиці (віртуалізований пул рядків 56 з медалями топ-3) або картка стану
    /// («Немає з'єднання» з «Спробувати ще») → навігація 76. Меню: Ink Flow → Setup → Build Rankings Screen.
    /// </summary>
    public static class BuildRankingsScreen
    {
        private const string ScenePath = "Assets/Scenes/Rankings.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        /// <summary>Рядків у пулі: картка таблиці на найвищому телефоні вміщає ~9, запас — два.</summary>
        private const int PoolRows = 12;

        [MenuItem("Ink Flow/Setup/Build Rankings Screen")]
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
            var quad = LoadSprite("white-quad");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (U1GlazeSprites.Load("medal_1") == null) missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Рейтинги НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
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

            var screenGo = Child(safe, "RankingsScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<RankingsScreen>();

            var column = Child(screenGo, "Column");
            Stretch(column);
            var col = column.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, (int)U1.Px(8f));
            col.spacing = U1.Px(10f);
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            U1.ScreenHeader(column, design!, out var back, out var title, out var currency, out var gear);
            title.text = "Рейтинги";
            back.gameObject.SetActive(false);

            var you = BuildYouCard(column, design!, out var youGroup, out var youRing, out var youPosition, out var youAvatar,
                out var youNick, out var youSubtitle, out var progressRow, out var progressFill, out var youGap);
            _ = you;

            // Одна таблиця «планети за весь час» (2026-10-24, рішення автора): без «Цей тиждень» і «Планети / Галактики».

            var area = Child(column, "Area");
            area.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var table = BuildTable(area, design!, quad!, out var scroll, out var listContent, out var rows);
            var status = BuildStatus(area, design!, out var statusIcon, out var statusTitle, out var statusHint, out var retry);

            var navBar = U1.NavBar(column, "NavBar", design!, NavBar.Ranks);

            var avatars = new Sprite[8];
            for (var i = 0; i < avatars.Length; i++)
                avatars[i] = U1.S($"avatar_{i + 1}");

            Wire(screen,
                ("design", design!), ("backButton", back), ("settingsButton", gear), ("title", title), ("currency", currency),
                ("youGroup", youGroup), ("youRing", youRing), ("youPosition", youPosition), ("youAvatar", youAvatar),
                ("youNick", youNick), ("youSubtitle", youSubtitle), ("youProgressRow", progressRow),
                ("youProgressFill", progressFill), ("youGap", youGap),
                ("tableCard", table), ("scroll", scroll), ("listContent", listContent),
                ("statusCard", status), ("statusIcon", statusIcon), ("statusTitle", statusTitle), ("statusHint", statusHint),
                ("retryButton", retry), ("offlineIcon", U1.S("icon_wifi_off")), ("emptyIcon", U1.S("icon_trophy")),
                ("navBar", navBar), ("incognitoAvatar", U1.S("avatar_incognito")));
            WireArray(screen, "rows", rows);
            WireArray(screen, "avatarSprites", avatars);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Рейтинги зібрано: {ScenePath} (U1Glaze «Ліга», коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        // ── Картка «Ти» (S08_01): panel_card + бірюзова обводка; ряд «#19 · аватар 64 · ім'я й число»; смужка; «ще N до №M» ──
        private static GameObject BuildYouCard(GameObject parent, DesignSystem design, out CanvasGroup group, out Image ring,
            out TMP_Text position, out Image avatar, out TMP_Text nick, out TMP_Text subtitle,
            out GameObject progressRow, out RectTransform progressFill, out TMP_Text gap)
        {
            var card = U1.Panel(parent, "YouCard", "panel_card");
            group = card.AddComponent<CanvasGroup>();
            var ringGo = Child(card, "Ring");
            ringGo.transform.SetSiblingIndex(2); // над заливкою, під вмістом
            ring = ringGo.AddComponent<Image>();
            ring.sprite = U1.S("field_focus_ring");
            ring.type = Image.Type.Sliced;
            ring.raycastTarget = false;
            ring.color = DesignSystem.WithAlpha(design.Teal, design.YouCardRingAlpha);
            Stretch(ringGo, -U1.Px(3f));
            ringGo.AddComponent<LayoutElement>().ignoreLayout = true;

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), (int)U1.Px(14f), (int)U1.Px(14f));
            layout.spacing = U1.Px(10f);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var top = Child(card, "Top");
            var topRow = top.AddComponent<HorizontalLayoutGroup>();
            topRow.spacing = U1.Px(12f);
            topRow.childAlignment = TextAnchor.MiddleLeft;
            topRow.childControlWidth = topRow.childControlHeight = true;
            topRow.childForceExpandWidth = topRow.childForceExpandHeight = false;
            U1.Size(top, 0f, 64f);

            position = U1.Text(top, "Position", "#19", design, U1.Role.H1, design.Teal300, TextAlignmentOptions.Left);
            position.fontSize = design.UiH1 * 1.3f;
            position.textWrappingMode = TextWrappingModes.NoWrap;
            position.enableAutoSizing = true;
            position.fontSizeMax = design.UiH1 * 1.3f;
            position.fontSizeMin = design.UiH1 * 0.8f;
            U1.Size(position.gameObject, 76f, 0f);

            var avatarGo = Child(top, "Avatar");
            avatar = avatarGo.AddComponent<Image>();
            avatar.sprite = U1.S("avatar_4");
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            U1.Size(avatarGo, 56f, 56f);

            var texts = Child(top, "Texts");
            var textCol = texts.AddComponent<VerticalLayoutGroup>();
            textCol.childAlignment = TextAnchor.MiddleLeft;
            textCol.childControlWidth = textCol.childControlHeight = true;
            textCol.childForceExpandWidth = true;
            textCol.childForceExpandHeight = false;
            var textsSize = texts.AddComponent<LayoutElement>();
            textsSize.preferredWidth = 0f;
            textsSize.minWidth = 0f;
            textsSize.flexibleWidth = 1f;
            nick = U1.Text(texts, "Nick", "Ти · Гравець", design, U1.Role.H2, design.InkText, TextAlignmentOptions.Left);
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            subtitle = U1.Text(texts, "Subtitle", "2 планети · 0 гал. · рекорд 12 340", design, U1.Role.Body, design.InkMuted, TextAlignmentOptions.Left);
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            // Планети, галактики й рекорд в один рядок: на iPhone SE кегль стискається до 80 %, далі — «…».
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMax = design.UiBody;
            subtitle.fontSizeMin = design.UiBody * 0.8f;

            progressRow = Child(card, "Progress");
            var progressCol = progressRow.AddComponent<VerticalLayoutGroup>();
            progressCol.spacing = U1.Px(8f);
            progressCol.childControlWidth = progressCol.childControlHeight = true;
            progressCol.childForceExpandWidth = true;
            progressCol.childForceExpandHeight = false;

            var trackGo = Child(progressRow, "Track");
            var track = trackGo.AddComponent<Image>();
            track.sprite = U1.S("progress_track");
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;
            U1.Size(trackGo, 0f, 12f);
            var fillGo = Child(trackGo, "Fill");
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = U1.S("progress_fill");
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;
            Stretch(fillGo);
            progressFill = fillGo.GetComponent<RectTransform>();
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.localScale = new Vector3(0.2f, 1f, 1f);

            gap = U1.Text(progressRow, "Gap", "ще 9 планет до №18", design, U1.Role.Body, design.Teal300, TextAlignmentOptions.Left);
            gap.font = design.FontBlack;
            gap.textWrappingMode = TextWrappingModes.NoWrap;
            gap.overflowMode = TextOverflowModes.Ellipsis;
            return card;
        }

        // ── Картка таблиці: panel_card, вертикальний скрол, пул рядків 56 ──
        private static GameObject BuildTable(GameObject parent, DesignSystem design, Sprite quad,
            out ScrollRect scroll, out RectTransform content, out RankingRow[] rows)
        {
            var card = U1.Panel(parent, "Table", "panel_card");
            Stretch(card);
            scroll = card.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 0f;

            var viewportGo = Child(card, "Viewport");
            Stretch(viewportGo, U1.Px(4f));
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = Child(viewportGo, "Content");
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, U1.Px(600f));

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = content;

            var medals = new[] { U1.S("medal_1"), U1.S("medal_2"), U1.S("medal_3") };
            var avatars = new Sprite[8];
            for (var i = 0; i < avatars.Length; i++)
                avatars[i] = U1.S($"avatar_{i + 1}");

            rows = new RankingRow[PoolRows];
            for (var i = 0; i < rows.Length; i++)
                rows[i] = BuildRow(contentGo, $"Row{i}", design, quad, medals, avatars);
            return card;
        }

        /// <summary>
        /// Рядок 56 (S08): підсвітка (перше місце — тепла, свій — бірюзова), місце 44 (медаль 34 з цифрою або число
        /// ink-muted), аватар 40, нік h3 з «…», значення h3 праворуч без переносу (автостиск до 70 %), роздільник.
        /// </summary>
        private static RankingRow BuildRow(GameObject parent, string name, DesignSystem design, Sprite quad, Sprite[] medals, Sprite[] avatars)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, design.RankRowHeight);

            var highlightGo = Child(go, "Highlight");
            Stretch(highlightGo);
            var highlight = highlightGo.AddComponent<Image>();
            highlight.sprite = U1.S("panel_low");
            highlight.type = Image.Type.Sliced;
            highlight.raycastTarget = false;
            highlightGo.SetActive(false);

            var hitGo = Child(go, "Hit");
            Stretch(hitGo);
            var hit = hitGo.AddComponent<Image>();
            hit.color = Color.clear;

            var rowGo = Child(go, "Row");
            Stretch(rowGo);
            var row = rowGo.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)U1.Px(10f), (int)U1.Px(10f), 0, 0);
            row.spacing = U1.Px(12f);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            var placeGo = Child(rowGo, "Place");
            U1.Size(placeGo, 36f, 36f);
            var medalGo = Child(placeGo, "Medal");
            Stretch(medalGo);
            var medal = medalGo.AddComponent<Image>();
            medal.sprite = medals[0];
            medal.raycastTarget = false;
            var place = U1.Text(placeGo, "Number", "1", design, U1.Role.H3, design.MedalInk);
            Stretch(place.gameObject);
            place.textWrappingMode = TextWrappingModes.NoWrap;

            var avatarGo = Child(rowGo, "Avatar");
            var avatar = avatarGo.AddComponent<Image>();
            avatar.sprite = avatars[0];
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            U1.Size(avatarGo, 40f, 40f);

            // Нік і під ним «2 галактики · рекорд 12 340» — стовпчиком, що забирає ширину між аватаром і числом.
            var names = Child(rowGo, "Names");
            var namesCol = names.AddComponent<VerticalLayoutGroup>();
            namesCol.childAlignment = TextAnchor.MiddleLeft;
            namesCol.childControlWidth = namesCol.childControlHeight = true;
            namesCol.childForceExpandWidth = true;
            namesCol.childForceExpandHeight = false;
            var namesSize = names.AddComponent<LayoutElement>();
            namesSize.preferredWidth = 0f;
            namesSize.minWidth = 0f;
            namesSize.flexibleWidth = 1f;
            var nick = U1.Text(names, "Nick", "Вега", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Left);
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            var detail = U1.Text(names, "Detail", "2 гал. · рекорд 12 340", design, U1.Role.Caption, design.InkMuted, TextAlignmentOptions.Left);
            detail.font = design.FontBody;
            detail.textWrappingMode = TextWrappingModes.NoWrap;
            detail.overflowMode = TextOverflowModes.Ellipsis;

            var value = U1.Text(rowGo, "Value", "56", design, U1.Role.H3, design.InkText, TextAlignmentOptions.Right);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.enableAutoSizing = true;
            value.fontSizeMax = design.UiH3;
            value.fontSizeMin = design.UiH3 * 0.7f;
            // Планет — до 4–5 цифр, тож 72 вистачає (автостиск до 70 %); ширина йде другому рядку ніка.
            U1.Size(value.gameObject, 72f, 0f);

            var divider = U1.Divider(go, "Divider", design);
            var dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(1f, 0f);
            dividerRect.pivot = new Vector2(0.5f, 0f);
            dividerRect.offsetMin = new Vector2(U1.Px(14f), 0f);
            dividerRect.offsetMax = new Vector2(-U1.Px(14f), U1.Px(1f));

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var component = go.AddComponent<RankingRow>();
            Wire(component, ("design", design), ("button", button), ("highlight", highlight), ("medal", medal), ("place", place),
                ("avatar", avatar), ("nick", nick), ("detail", detail), ("value", value), ("divider", divider), ("incognito", U1.S("avatar_incognito")));
            WireArray(component, "medals", medals);
            WireArray(component, "avatars", avatars);
            _ = quad;
            go.SetActive(false);
            return component;
        }

        // ── Картка стану (S08_04): іконка 64, заголовок h2, пояснення, скляна «Спробувати ще» ──
        private static GameObject BuildStatus(GameObject parent, DesignSystem design, out Image icon, out TMP_Text title,
            out TMP_Text hint, out CandyButton retry)
        {
            var card = U1.Panel(parent, "Status", "panel_card");
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

            var iconGo = Child(card, "Icon");
            icon = iconGo.AddComponent<Image>();
            icon.sprite = U1.S("icon_wifi_off");
            icon.color = design.InkMuted;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            U1.Size(iconGo, 0f, 60f);

            title = U1.Text(card, "Title", "Немає з'єднання", design, U1.Role.H2, design.InkText);
            title.textWrappingMode = TextWrappingModes.Normal;
            hint = U1.Text(card, "Hint", "Рейтинги оновляться, щойно з'явиться інтернет.", design, U1.Role.Body, design.InkMuted);
            hint.textWrappingMode = TextWrappingModes.Normal;
            U1.Gap(card, 4f);
            retry = U1.Button(card, "Retry", "Спробувати ще", design, CandyButton.Tone.Glass);
            U1.Size(retry.gameObject, 0f, 56f);
            card.SetActive(false);
            return card;
        }
    }
}
