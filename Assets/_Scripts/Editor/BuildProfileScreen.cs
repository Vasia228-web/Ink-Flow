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
    /// Збирає профіль за майстер-доком §14: візитка (аватар-крапля, нік, олівець), набір аватарів,
    /// вітринна картинка з кнопкою «Обрати з колекції». Меню: Ink Flow → Setup → Build Profile Screen.
    ///
    /// Числа — px макета × K (K = 1080/390 ≈ 2.769). Екран скролиться цілком, разом із шапкою — так
    /// у макеті. Крапля-аватар висить ПІД верхом картки (півот зверху), а не над ним: стара збірка
    /// ставила півот знизу, крапля стирчала на 94 px над карткою, і маска скролу зрізала їй маківку.
    /// </summary>
    public static class BuildProfileScreen
    {
        private const string ScenePath = "Assets/Scenes/Profile.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string FontPath = "Assets/_Fonts/Nunito ExtraBold SDF.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";

        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        private static readonly float SideMargin = M(18f);

        /// <summary>Радіус великих карток профілю: 26 px макета.</summary>
        private static readonly float CardRadius = M(26f);

        /// <summary>Аватар на візитці: 110 px макета; крапля в наборі — 44.</summary>
        private const float AvatarMockup = 110f;
        private const float OptionMockup = 44f;

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
            var nebula = LoadSprite("nebula");
            var gear = LoadSprite("icon-gear");
            var pencil = LoadSprite("icon-pencil");
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");
            var dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/DropView.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            foreach (var (sprite, name) in new[]
            {
                (rounded, "rounded-rect"), (outline, "rounded-rect-outline"), (circle, "circle-soft"),
                (circleOutline, "circle-outline"), (gloss, "circle-gloss"), (nebula, "nebula"), (gear, "icon-gear"), (pencil, "icon-pencil")
            })
                if (sprite == null) missing.Add($"{SpriteFolder}/{name}.png");
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

            BuildIdentity(content, design!, font, rounded!, outline!, circle!, pencil!, dropPrefab!, ref y,
                out var identityCard, out var avatar, out var editButton, out var editFill, out var nick, out var nickHint);
            _ = cosmic;

            BuildAvatars(content, design!, font, rounded!, outline!, circle!, gloss!, circleOutline!, ref y,
                out var avatarCaption, out var options, out var optionButtons, out var rings);

            BuildShowcase(content, design!, font, rounded!, outline!, circle!, nebula!, ref y,
                out var showcaseCaption, out var showcasePicture, out var showcaseEmpty, out var showcaseEmptyFrame,
                out var showcaseEmptyGlyph, out var showcaseHint, out var showcaseButton, out var showcaseFill, out var showcaseLabel);

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y + M(24f));

            Wire(screen,
                ("design", design!),
                ("backButton", backButton), ("title", title), ("settingsButton", settingsButton),
                ("identityCard", identityCard), ("avatar", avatar), ("editNickButton", editButton), ("editNickFill", editFill),
                ("nickLabel", nick), ("nickHint", nickHint),
                ("avatarCaption", avatarCaption),
                ("showcaseCaption", showcaseCaption), ("showcasePicture", showcasePicture), ("showcaseHint", showcaseHint),
                ("showcaseEmpty", showcaseEmpty), ("showcaseEmptyFrame", showcaseEmptyFrame), ("showcaseEmptyGlyph", showcaseEmptyGlyph),
                ("showcaseButton", showcaseButton), ("showcaseButtonFill", showcaseFill), ("showcaseButtonLabel", showcaseLabel));
            WireArray(screen, "avatarOptions", options);
            WireArray(screen, "avatarButtons", optionButtons);
            WireArray(screen, "avatarRings", rings);

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

            content = Child(viewportGo, "Content");
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = rect;
        }

        private static void BuildHeader(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, Sprite gear, ref float y,
            out Button backButton, out TMP_Text title, out Button settingsButton)
        {
            var height = M(46f);
            var go = Section(parent, "Header", ref y, height, spacing: M(12f));
            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(SideMargin, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-SideMargin, rect.offsetMax.y);

            backButton = RoundButton(go, design, font, circle, circleOutline, null, "‹", new Vector2(0f, 0.5f));
            settingsButton = RoundButton(go, design, font, circle, circleOutline, gear, null, new Vector2(1f, 0.5f));

            // Праворуч не капсула валюти, а кругла кнопка — проміжок симетричний, заголовок по центру шапки.
            title = Label(go, "Title", "ПРОФІЛЬ", design, font,
                design.FontSizePaintTitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, Vector2.zero, new Vector2(M(240f), M(24f)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private static Button RoundButton(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite circle, Sprite circleOutline, Sprite? icon, string? glyph, Vector2 anchor)
        {
            var size = M(40f);
            var go = Child(parent, glyph != null ? "Back" : "Settings");
            var fill = go.AddComponent<Image>();
            fill.sprite = circle;
            fill.color = design.CircleButtonFill;
            Place(fill, Vector2.zero, new Vector2(size, size), anchor, anchor);

            var ringGo = Child(go, "Ring");
            Stretch(ringGo);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = circleOutline;
            ring.color = design.GlassStroke;
            ring.raycastTarget = false;

            if (glyph != null)
            {
                var label = Label(go, "Glyph", glyph, design, font, M(22f), design.TextPrimary, TextAlignmentOptions.Center);
                Stretch(label.gameObject);
            }
            else if (icon != null)
            {
                var iconGo = Child(go, "Icon");
                var image = iconGo.AddComponent<Image>();
                image.sprite = icon;
                image.color = design.TextMuted;
                image.raycastTarget = false;
                Place(image, Vector2.zero, new Vector2(M(20f), M(20f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = fill;
            return button;
        }

        // ── Візитка: крапля-аватар, олівець, нік ──
        private static void BuildIdentity(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite pencil, GameObject dropPrefab, ref float y,
            out RectTransform card, out DropView avatar, out Button editButton, out GradientImage editFill,
            out TMP_Text nick, out TMP_Text nickHint)
        {
            var top = M(20f);
            var avatarSize = M(AvatarMockup);
            var nickTop = top + avatarSize + M(10f);
            var hintTop = nickTop + M(40f);
            var height = hintTop + M(18f) + M(18f);
            var go = Card(parent, "Identity", rounded, outline, design, ref y, height);
            card = go.GetComponent<RectTransform>();

            // Якір зверху, півот по центру: верх краплі = верх картки − відступ, тож вона ніколи не вилазить
            // за картку (маска скролу не зріже маківку), а дихання й приземлення масштабують її навколо
            // центру, а не навколо маківки, як маятник.
            var avatarGo = (GameObject)PrefabUtility.InstantiatePrefab(dropPrefab, go.transform);
            avatarGo.name = "Avatar";
            avatar = avatarGo.GetComponent<DropView>();
            Place(avatar, new Vector2(0f, -(top + avatarSize * 0.5f)), new Vector2(avatarSize, avatarSize),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            SetDrop(avatar, density: 0, idleWobble: true);

            // Олівець сидить на самій краплі, правіше-нижче її центру.
            var editSize = M(30f);
            var editGo = Child(avatarGo, "EditNick");
            editFill = editGo.AddComponent<GradientImage>();
            editFill.sprite = circle;
            editFill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(editFill, new Vector2(-M(14f), 0f), new Vector2(editSize, editSize),
                new Vector2(1f, 0f), new Vector2(1f, 0.5f));

            // Олівець — спрайт, не символ: ✎ у Nunito немає, TMP підставляв би порожній квадрат.
            var pencilGo = Child(editGo, "Glyph");
            var pencilIcon = pencilGo.AddComponent<Image>();
            pencilIcon.sprite = pencil;
            pencilIcon.color = design.TextPrimary;
            pencilIcon.raycastTarget = false;
            Place(pencilIcon, Vector2.zero, new Vector2(M(15f), M(15f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            editButton = editGo.AddComponent<Button>();
            editButton.targetGraphic = editFill;

            nick = Label(go, "Nick", "Нова", design, font,
                design.FontSizeProfileNick, design.TextPrimary, TextAlignmentOptions.Center);
            // Найдовший дозволений нік (16 широких літер) стискається до одного рядка, а не переноситься на підказку.
            nick.textWrappingMode = TextWrappingModes.NoWrap;
            nick.overflowMode = TextOverflowModes.Ellipsis;
            nick.enableAutoSizing = true;
            nick.fontSizeMax = design.FontSizeProfileNick;
            nick.fontSizeMin = design.FontSizeProfileNick * design.NickMinScale;
            Place(nick, new Vector2(0f, -nickTop), new Vector2(M(300f), M(40f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            nickHint = Label(go, "NickHint", "Нік і аватар бачать інші гравці", design, font,
                design.FontSizeLabel, design.TextMuted, TextAlignmentOptions.Center);
            Place(nickHint, new Vector2(0f, -hintTop), new Vector2(M(300f), M(18f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        }

        // ── Набір аватарів: ряд крапель, обрана обведена ──
        // Кожна крапля — градієнтне коло з глянцем, як у рядку рейтингу, а не DropView: шість DropView
        // дали б шість LateUpdate і перехопили б тап (IPointerClickHandler на краплі з'їдав клік слота).
        private static void BuildAvatars(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite gloss, Sprite circleOutline, ref float y,
            out TMP_Text caption, out GradientImage[] options, out Button[] buttons, out Image[] rings)
        {
            var count = AvatarSet.Count;
            var option = M(OptionMockup);
            var ringPad = M(4f);
            var rowTop = M(40f);
            var height = rowTop + option + M(20f);
            var go = Card(parent, "Avatars", rounded, outline, design, ref y, height);
            caption = Caption(go, design, font, "АВАТАР");

            // Ряд рівномірно по ширині картки між бічними полями 18: позиції — частками ширини, щоб
            // ряд тримався на будь-якому пристрої, а не лише на макеті 390.
            options = new GradientImage[count];
            buttons = new Button[count];
            rings = new Image[count];
            for (var i = 0; i < count; i++)
            {
                var slotGo = Child(go, $"Option{i}");
                var slot = slotGo.GetComponent<RectTransform>();
                var t = count > 1 ? i / (float)(count - 1) : 0.5f;
                slot.anchorMin = slot.anchorMax = new Vector2(t, 1f);
                slot.pivot = new Vector2(t, 1f);
                slot.anchoredPosition = new Vector2(0f, -rowTop);
                slot.sizeDelta = new Vector2(option + ringPad * 2f, option + ringPad * 2f);
                // Крайні відступають від країв картки на бічне поле: півот tягне їх усередину.
                slot.anchoredPosition = new Vector2(Mathf.Lerp(SideMargin, -SideMargin, t), -rowTop);

                var ringGo = Child(slotGo, "Ring");
                Stretch(ringGo);
                var ring = ringGo.AddComponent<Image>();
                ring.sprite = circleOutline;
                ring.color = design.TextPrimary;
                ring.raycastTarget = false;
                ringGo.SetActive(i == 0);
                rings[i] = ring;

                var dropGo = Child(slotGo, "Drop");
                var drop = dropGo.AddComponent<GradientImage>();
                drop.sprite = circle;
                design.AvatarGradient(AvatarSet.InkOf(i), false, out var inkFrom, out var inkTo);
                drop.SetGradient(inkFrom, inkTo);
                Place(drop, Vector2.zero, new Vector2(option, option), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                options[i] = drop;

                var glossGo = Child(dropGo, "Gloss");
                Stretch(glossGo);
                var glossImage = glossGo.AddComponent<Image>();
                glossImage.sprite = gloss;
                glossImage.color = new Color(1f, 1f, 1f, design.DropGlossAlpha);
                glossImage.raycastTarget = false;

                // Ловець тапу на весь слот — влучати по краплі з кільцем, не лише по тілу; клік по тілу
                // спливає до кнопки слота, бо на самому колі обробника немає.
                var hit = slotGo.AddComponent<Image>();
                hit.color = Color.clear;
                var button = slotGo.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                buttons[i] = button;
            }
        }

        // ── Вітрина: картинка з колекції і кнопка вибору ──
        private static void BuildShowcase(GameObject parent, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite circle, Sprite nebula, ref float y,
            out TMP_Text caption, out PictureView picture, out RectTransform empty, out Image emptyFrame, out TMP_Text emptyGlyph,
            out TMP_Text hint, out Button button, out GradientImage fill, out TMP_Text label)
        {
            var side = M(150f);
            var pictureTop = M(40f);
            var hintTop = pictureTop + side + M(12f);
            var hintHeight = M(36f);
            var buttonTop = hintTop + hintHeight + M(12f);
            var buttonHeight = M(46f);
            var height = buttonTop + buttonHeight + M(18f);
            var go = Card(parent, "Showcase", rounded, outline, design, ref y, height);
            caption = Caption(go, design, font, "ВІТРИНА");

            var pictureGo = Child(go, "Picture");
            var pictureRect = pictureGo.GetComponent<RectTransform>();
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 1f);
            pictureRect.pivot = new Vector2(0.5f, 1f);
            pictureRect.anchoredPosition = new Vector2(0f, -pictureTop);
            pictureRect.sizeDelta = new Vector2(side, side);
            picture = PictureViewBuilder.MakePictureView(pictureGo, design, font, rounded, outline, nebula,
                side, side, M(128f), withTitle: false, captionHeight: 0f, particle: circle);

            // Порожній стан: та сама рамка без картинки, всередині знак питання — місце чекає на першу картинку.
            var emptyGo = Child(go, "Empty");
            empty = emptyGo.GetComponent<RectTransform>();
            empty.anchorMin = empty.anchorMax = new Vector2(0.5f, 1f);
            empty.pivot = new Vector2(0.5f, 1f);
            empty.anchoredPosition = new Vector2(0f, -pictureTop);
            empty.sizeDelta = new Vector2(side, side);
            emptyFrame = emptyGo.AddComponent<Image>();
            emptyFrame.sprite = outline;
            emptyFrame.type = Image.Type.Sliced;
            emptyFrame.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(design.BoardPanelRadius);
            emptyFrame.color = design.GlassStroke;
            emptyFrame.raycastTarget = false;
            emptyGlyph = Label(emptyGo, "Glyph", "?", design, font, design.FontSizeProfileNick, design.TextDim, TextAlignmentOptions.Center);
            Stretch(emptyGlyph.gameObject);
            emptyGo.SetActive(false);

            hint = Label(go, "Hint", "Домалюй першу картинку в забігу — вона стане вітриною", design, font,
                design.FontSizeLabel, design.TextMuted, TextAlignmentOptions.Center);
            hint.textWrappingMode = TextWrappingModes.Normal;
            Place(hint, new Vector2(0f, -hintTop), new Vector2(M(300f), hintHeight), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var buttonGo = Child(go, "Pick");
            fill = buttonGo.AddComponent<GradientImage>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(M(23f));
            fill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(fill, new Vector2(0f, -buttonTop), new Vector2(M(220f), buttonHeight), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            label = Label(buttonGo, "Label", "Обрати з колекції", design, font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(label.gameObject);
            button = buttonGo.AddComponent<Button>();
            button.targetGraphic = fill;
        }

        /// <summary>Серіалізовані поля краплі з префаба: число густоти (аватар — без «1») і дихання.</summary>
        private static void SetDrop(DropView drop, int density, bool idleWobble)
        {
            var so = new SerializedObject(drop);
            so.FindProperty("density").intValue = density;
            so.FindProperty("idleWobble").boolValue = idleWobble;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Section(GameObject parent, string name, ref float y, float height, float spacing)
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
        private static GameObject Card(GameObject parent, string name, Sprite rounded, Sprite outline, DesignSystem design, ref float y, float height)
        {
            var go = Section(parent, name, ref y, height, M(12f));
            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(SideMargin, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-SideMargin, rect.offsetMax.y);

            // Скло — з токенів, як у налаштуваннях: біла заливка з макета в UGUI читалась сірим пластиком.
            var fill = go.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(CardRadius);
            fill.color = design.GlassFill;
            fill.raycastTarget = false;

            var strokeGo = Child(go, "Stroke");
            Stretch(strokeGo);
            var stroke = strokeGo.AddComponent<Image>();
            stroke.sprite = outline;
            stroke.type = Image.Type.Sliced;
            stroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(CardRadius);
            stroke.color = design.GlassStroke;
            stroke.raycastTarget = false;
            return go;
        }

        /// <summary>Дрібний розріджений заголовок секції: 11/800 ls .14em.</summary>
        private static TMP_Text Caption(GameObject parent, DesignSystem design, TMP_FontAsset? font, string text)
        {
            var label = Label(parent, "Caption", text, design, font,
                design.FontSizeSmall, design.TextFaint, TextAlignmentOptions.Left);
            Place(label, new Vector2(M(18f), -M(16f)), new Vector2(M(220f), M(16f)),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            return label;
        }
    }
}
