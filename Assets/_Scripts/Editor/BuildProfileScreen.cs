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
    /// Збирає профіль U1Glaze (S10Profile_01, S12SmallScreen_10) фабриками <see cref="U1"/>: шапка 56 («‹», капсула,
    /// шестерня) → вертикальний скрол із картками: візитка (аватар 96, нік h1 + бірюзова кнопка-олівець 46, підпис),
    /// «АВАТАР» (сітка 4 × 2 спрайтів avatar_N 56, обраний — у кільці avatar_ring_selected), «ВІТРИНА» (картинка в
    /// рамці рідкості 150 з мітками, назва h2, бейдж рідкості, скляна «Обрати з колекції»). Діалог ніка — у головній
    /// сцені (<see cref="BuildMainScene.BuildNickPrompt"/>). Меню: Ink Flow → Setup → Build Profile Screen.
    /// </summary>
    public static class BuildProfileScreen
    {
        private const string ScenePath = "Assets/Scenes/Profile.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

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
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (U1GlazeSprites.Load("avatar_ring_selected") == null) missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
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

            var column = Child(screenGo, "Column");
            Stretch(column);
            var col = column.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, 0);
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            U1.ScreenHeader(column, design!, out var back, out var title, out var currency, out var gear);
            title.text = string.Empty;

            // ── Скрол із картками: на SE все не вміщається (S12_10), на високих телефонах — без прокрутки ──
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
            var cards = contentGo.AddComponent<VerticalLayoutGroup>();
            cards.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), (int)U1.Px(4f), (int)U1.Px(20f));
            cards.spacing = U1.Px(12f);
            cards.childAlignment = TextAnchor.UpperCenter;
            cards.childControlWidth = cards.childControlHeight = true;
            cards.childForceExpandWidth = true;
            cards.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = content;

            BuildIdentity(contentGo, design!, out var avatar, out var nick, out var edit);
            BuildAvatars(contentGo, design!, out var avatarButtons, out var avatarRings, out var avatarSprites, out var avatarPick);
            BuildShowcase(contentGo, design!, out var frame, out var picture, out var empty, out var pictureName, out var badge, out var pick);

            Wire(screen,
                ("design", design!), ("backButton", back), ("settingsButton", gear), ("currency", currency),
                ("avatar", avatar), ("nickLabel", nick), ("editNickButton", edit),
                ("showcaseFrame", frame), ("showcasePicture", picture), ("showcaseEmpty", empty), ("showcaseName", pictureName),
                ("showcaseBadge", badge), ("showcaseButton", pick), ("avatarPickButton", avatarPick));
            WireArray(screen, "avatarButtons", avatarButtons);
            WireArray(screen, "avatarRings", avatarRings);
            WireArray(screen, "avatarSprites", avatarSprites);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Профіль зібрано: {ScenePath} (U1Glaze, коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        private static GameObject Card(GameObject parent, string name, int topPx = 20)
        {
            var card = U1.Panel(parent, name, "panel_card");
            var layout = card.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(16f);
            layout.padding = new RectOffset(pad, pad, (int)U1.Px(topPx), pad);
            layout.spacing = U1.Px(10f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        // ── Візитка (S10_01): аватар 96, нік h1 + бірюзова кнопка-олівець 46, підпис ──
        private static void BuildIdentity(GameObject parent, DesignSystem design, out Image avatar, out TMP_Text nick, out Button edit)
        {
            var card = Card(parent, "Identity", 16);

            var avatarGo = Child(card, "Avatar");
            avatar = avatarGo.AddComponent<Image>();
            avatar.sprite = U1.S("avatar_1");
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            U1.Size(avatarGo, 0f, 86f);

            var row = Child(card, "NickRow");
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = U1.Px(12f);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            U1.Size(row, 0f, 48f);

            nick = U1.Text(row, "Nick", "Гравець", design, U1.Role.H1, design.InkText);
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            // Нік не довший за ряд мінус олівець: з «…» лише після автостиску до nickMinScale.
            var nickSize = nick.gameObject.AddComponent<LayoutElement>();
            nickSize.minWidth = 0f;
            nickSize.preferredWidth = -1f;
            nickSize.flexibleWidth = 0f;

            var editGo = Child(row, "Edit");
            var body = editGo.AddComponent<Image>();
            body.sprite = U1.S("button_teal_small");
            body.type = Image.Type.Sliced;
            U1.Size(editGo, 46f, 46f);
            var iconGo = Child(editGo, "Icon");
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = U1.S("icon_pencil");
            icon.color = design.TealInk;
            icon.raycastTarget = false;
            Place(icon, Vector2.zero, new Vector2(U1.Px(22f), U1.Px(22f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            edit = editGo.AddComponent<Button>();
            edit.targetGraphic = body;
            edit.transition = Selectable.Transition.None;
            editGo.AddComponent<PressScale>().Bind(design);

            var note = U1.Text(card, "Note", "Нік і аватар бачать інші гравці", design, U1.Role.Body, design.InkMuted);
            note.textWrappingMode = TextWrappingModes.Normal;
        }

        // ── «АВАТАР» (S10_01): сітка 4 × 2, аватар 56, обраний — у кільці (кільце намальоване з відступом 3: ×1.125) ──
        private static void BuildAvatars(GameObject parent, DesignSystem design, out Button[] buttons, out Image[] rings, out Sprite[] sprites,
            out CandyButton pick)
        {
            var card = Card(parent, "Avatars", 16);
            var label = U1.Text(card, "Label", "Аватар", design, U1.Role.Label, design.InkMuted, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var gridGo = Child(card, "Grid");
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            var cell = U1.Px(56f);
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(U1.Px(22f), U1.Px(8f));
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            var gridSize = gridGo.AddComponent<LayoutElement>();
            gridSize.preferredHeight = gridSize.minHeight = cell * 2f + U1.Px(8f);

            buttons = new Button[8];
            rings = new Image[8];
            sprites = new Sprite[8];
            for (var i = 0; i < 8; i++)
            {
                sprites[i] = U1.S($"avatar_{i + 1}");
                var go = Child(gridGo, $"Avatar{i}");
                var hit = go.AddComponent<Image>();
                hit.color = Color.clear;

                var ringGo = Child(go, "Ring");
                var ring = ringGo.AddComponent<Image>();
                ring.sprite = U1.S("avatar_ring_selected");
                ring.raycastTarget = false;
                Stretch(ringGo);
                ringGo.SetActive(i == 0);

                var faceGo = Child(go, "Face");
                var face = faceGo.AddComponent<Image>();
                face.sprite = sprites[i];
                face.preserveAspect = true;
                face.raycastTarget = false;
                Stretch(faceGo, cell * (1f - 1f / 1.125f) * 0.5f);

                var button = go.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                go.AddComponent<PressScale>().Bind(design);
                buttons[i] = button;
                rings[i] = ring;
            }

            // Аватаром може стати й зібрана картинка (2026-10-24): скляна кнопка під сіткою, як «Обрати з колекції» вітрини.
            U1.Gap(card, 2f);
            pick = U1.Button(card, "Pick", "З колекції", design, CandyButton.Tone.Glass);
            U1.Size(pick.gameObject, 0f, 48f);
        }

        // ── «ВІТРИНА» (S10_01): рамка рідкості 150 з мітками, назва h2, бейдж рідкості, скляна «Обрати з колекції» ──
        private static void BuildShowcase(GameObject parent, DesignSystem design, out RarityFrameView frame, out PictureView picture,
            out GameObject empty, out TMP_Text name, out RarityBadgeView badge, out CandyButton pick)
        {
            var card = Card(parent, "Showcase", 16);
            var label = U1.Text(card, "Label", "Вітрина", design, U1.Role.Label, design.InkMuted, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            frame = U1.FramedPicture(card, "Picture", design, big: true, 146f, 16f, out picture, glowRoomPx: 4f);
            // Порожня вітрина — бліде питання на полотні рамки: картка не западає, а чекає першої картинки.
            var emptyGo = Child(frame.transform.Find("Picture").gameObject, "Empty");
            Stretch(emptyGo);
            var glyph = U1.Text(emptyGo, "Glyph", "?", design, U1.Role.Display, design.InkDim);
            Stretch(glyph.gameObject);
            emptyGo.SetActive(false);
            empty = emptyGo;

            name = U1.Text(card, "Name", "Кактус у сомбреро", design, U1.Role.H2, design.InkText);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.overflowMode = TextOverflowModes.Ellipsis;

            var badgeRow = Child(card, "BadgeRow");
            var badgeLayout = badgeRow.AddComponent<HorizontalLayoutGroup>();
            badgeLayout.childAlignment = TextAnchor.MiddleCenter;
            badgeLayout.childControlWidth = badgeLayout.childControlHeight = true;
            badgeLayout.childForceExpandWidth = badgeLayout.childForceExpandHeight = false;
            U1.Size(badgeRow, 0f, 30f).flexibleHeight = 0f;
            badge = U1.RarityBadge(badgeRow, "Badge", design, Rarity.Rare);

            U1.Gap(card, 2f);
            pick = U1.Button(card, "Pick", "Обрати з колекції", design, CandyButton.Tone.Glass);
            U1.Size(pick.gameObject, 0f, 56f);
        }
    }
}
