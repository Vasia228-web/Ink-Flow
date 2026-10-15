using InkFlow.Core;
using InkFlow.Style;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static InkFlow.Editor.UiBuilder;

namespace InkFlow.Editor
{
    /// <summary>
    /// Фабрики спільних компонентів U1Glaze для збирачів екранів: панель із тінню, кнопка-цукерка,
    /// кнопка-іконка, бейдж, чип, перемикач, поле вводу, рамка рідкості, діалог, нижній лист і
    /// текст за ролями типографіки. Кожен екран збирає розкладку сам, але жоден не малює компонент
    /// по-своєму — інакше «один глянцевий матеріал на всі екрани» розійшовся б на першій правці.
    ///
    /// Усі розміри — px макета 402×874 через <see cref="Px"/>; спрайти — з маніфесту
    /// (<see cref="U1GlazeSprites"/>), 9-slice з межами маніфесту й множником 1.
    /// </summary>
    internal static class U1
    {
        internal const string SheenShaderPath = "Assets/_Shaders/InkFlowSheen.shader";

        /// <summary>Ролі типографіки tokens.json (Nunito 900 для заголовків/чисел/кнопок, 700 для тексту).</summary>
        internal enum Role
        {
            Display,
            H1,
            H2,
            H3,
            Button,
            ButtonSmall,
            Body,
            Label,
            Caption
        }

        internal static float Px(float designPx) => Mathf.Round(designPx * DesignSystem.MockupToReference);

        internal static Sprite S(string name) => U1GlazeSprites.Require(name);

        internal static Shader? SheenShader => AssetDatabase.LoadAssetAtPath<Shader>(SheenShaderPath);

        // ───────────────────────── Текст ─────────────────────────

        internal static TMP_Text Text(GameObject parent, string name, string text, DesignSystem design, Role role,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = Child(parent, name);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            Style(label, design, role);
            if (IconSprites != null)
                label.spriteAsset = IconSprites;
            return label;
        }

        /// <summary>Шрифт, кегль і розрядка ролі — одне місце на проєкт.</summary>
        internal static void Style(TMP_Text label, DesignSystem design, Role role)
        {
            var black = role != Role.Body && role != Role.Caption;
            label.font = black ? design.FontBlack : design.FontBody;
            label.fontSize = role switch
            {
                Role.Display => design.UiDisplay,
                Role.H1 => design.UiH1,
                Role.H2 => design.UiH2,
                Role.H3 => design.UiH3,
                Role.Button => design.UiButton,
                Role.ButtonSmall => design.UiButtonSmall,
                Role.Label => design.UiLabel,
                Role.Caption => design.UiCaption,
                _ => design.UiBody
            };
            label.characterSpacing = role == Role.Label ? design.UiLabelSpacing : 0f;
            if (role == Role.Label)
                label.text = label.text.ToUpperInvariant();
        }

        // ───────────────────────── Панелі ─────────────────────────

        /// <summary>
        /// Панель 9-slice (panel_card / panel_low / dialog / well) і, якщо треба, м'яка тінь під нею:
        /// окремий спрайт panel_shadow першим дочірнім (виступ 24 px, зсув 8 униз; SPEC: тінь — спрайт,
        /// не ефект), далі заливка «Fill». Повертає корінь — вміст кладіть у нього після заливки.
        /// Тінь і заливка позначені <c>ignoreLayout</c>, тож корінь може нести LayoutGroup.
        /// </summary>
        internal static GameObject Panel(GameObject parent, string name, string sprite = "panel_card", bool shadow = true)
        {
            var go = Child(parent, name);
            if (shadow)
                Shadow(go, Px(24f), -Px(8f));

            var fillGo = Child(go, "Fill");
            var image = fillGo.AddComponent<Image>();
            image.sprite = S(sprite);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Stretch(fillGo);
            fillGo.AddComponent<LayoutElement>().ignoreLayout = true;
            return go;
        }

        /// <summary>Тінь panel_shadow під панеллю: перший дочірній, поза розкладкою.</summary>
        internal static Image Shadow(GameObject panel, float outset, float shiftY)
        {
            var go = Child(panel, "Shadow");
            go.transform.SetAsFirstSibling();
            var image = go.AddComponent<Image>();
            image.sprite = S("panel_shadow");
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Stretch(go, -outset);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, shiftY);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        /// <summary>Тонкий роздільник рядків у картці (line-700), 1 px макета.</summary>
        internal static Image Divider(GameObject parent, string name, DesignSystem design)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite("white-quad");
            image.color = design.Line;
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(0f, Px(1f));
            return image;
        }

        // ───────────────────────── Кнопки ─────────────────────────

        internal static string BodySprite(CandyButton.Tone tone, bool small) => (tone, small) switch
        {
            (CandyButton.Tone.Coral, false) => "button_coral",
            (CandyButton.Tone.Coral, true) => "button_coral_small",
            (CandyButton.Tone.Teal, false) => "button_teal",
            (CandyButton.Tone.Teal, true) => "button_teal_small",
            (CandyButton.Tone.Glass, false) => "button_glass",
            (CandyButton.Tone.Glass, true) => "button_glass_small",
            (_, false) => "button_off",
            _ => "button_off_small"
        };

        /// <summary>
        /// Кнопка-цукерка: гало під тілом (лише коралова/бірюзова), 9-slice тіло, напис Nunito Black
        /// (коралова — білий з тінню), необов'язкова іконка ліворуч від тексту. Розмір задає викликач
        /// якорями/Place; висота — <see cref="DesignSystem.ButtonHeight"/> або мала.
        /// </summary>
        internal static CandyButton Button(GameObject parent, string name, string text, DesignSystem design,
            CandyButton.Tone tone, bool small = false, Sprite? icon = null)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Px(200f), small ? design.ButtonSmallHeight : design.ButtonHeight);

            var glowGo = Child(go, "Glow");
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = S(tone == CandyButton.Tone.Teal ? "button_glow_teal" : "button_glow_coral");
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            // Гало 140×100 намальовано під кнопку 96×56: виступ 22 px з кожного боку.
            Stretch(glowGo, -Px(22f));
            glowGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -Px(4f));

            var bodyGo = Child(go, "Body");
            var body = bodyGo.AddComponent<Image>();
            body.sprite = S(BodySprite(tone, small));
            body.type = Image.Type.Sliced;
            Stretch(bodyGo);

            var rowGo = Child(go, "Row");
            Stretch(rowGo);
            var row = rowGo.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = Px(8f);
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            Image? iconImage = null;
            if (icon != null)
            {
                var iconGo = Child(rowGo, "Icon");
                iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.raycastTarget = false;
                var size = Px(small ? 18f : 22f);
                iconGo.AddComponent<LayoutElement>().preferredWidth = size;
                iconGo.GetComponent<LayoutElement>().preferredHeight = size;
            }

            var label = Text(rowGo, "Label", text, design, small ? Role.ButtonSmall : Role.Button, Color.white);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;

            var candy = go.AddComponent<CandyButton>();
            Wire(candy, ("design", design), ("button", button), ("body", body), ("glow", glow), ("label", label),
                ("bodySprite", S(BodySprite(tone, small))), ("offSprite", S(small ? "button_off_small" : "button_off")));
            if (iconImage != null)
                Wire(candy, ("icon", iconImage));
            candy.Configure(tone, small, S(BodySprite(tone, small)), S(small ? "button_off_small" : "button_off"));
            return candy;
        }

        /// <summary>Чип ціни в нафті на кнопці: oil_price_chip + крапля 16 + число.</summary>
        internal static TMP_Text PriceChip(CandyButton button, long amount, DesignSystem design)
        {
            var row = button.transform.Find("Row").gameObject;
            var chipGo = Child(row, "Price");
            var chip = chipGo.AddComponent<Image>();
            chip.sprite = S("oil_price_chip");
            chip.type = Image.Type.Sliced;
            chip.raycastTarget = false;
            chip.color = button.CurrentTone == CandyButton.Tone.Teal ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            var layout = chipGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)Px(10f), (int)Px(10f), 0, 0);
            layout.spacing = Px(4f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            chipGo.AddComponent<LayoutElement>().preferredHeight = Px(30f);
            chipGo.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var dropGo = Child(chipGo, "Drop");
            var drop = dropGo.AddComponent<Image>();
            drop.sprite = S("oil_drop");
            drop.raycastTarget = false;
            var dropLayout = dropGo.AddComponent<LayoutElement>();
            dropLayout.preferredWidth = dropLayout.preferredHeight = Px(16f);

            var text = Text(chipGo, "Amount", ScoreFormat.Full(amount), design, Role.ButtonSmall,
                button.CurrentTone == CandyButton.Tone.Teal ? design.TealInk : Color.white);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>Текстова кнопка 44 у висоту («Скасувати», «Назад», «Завершити»).</summary>
        internal static UnityEngine.UI.Button TextButton(GameObject parent, string name, string text, DesignSystem design, Color color)
        {
            var go = Child(parent, name);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(Px(200f), design.TapMin);
            var catcher = go.AddComponent<Image>();
            catcher.color = Color.clear;
            var label = Text(go, "Label", text, design, Role.ButtonSmall, color);
            Stretch(label.gameObject);
            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = catcher;
            button.transition = Selectable.Transition.None;
            var press = go.AddComponent<PressScale>();
            press.Bind(design);
            return button;
        }

        /// <summary>Квадратна скляна кнопка-іконка 46 (назад, закрити, шестерня) з іконкою 24, тонованою ink.</summary>
        internal static UnityEngine.UI.Button IconButton(GameObject parent, string name, DesignSystem design, string iconSprite,
            Vector2 anchor, Vector2 offset)
        {
            var go = Child(parent, name);
            var body = go.AddComponent<Image>();
            body.sprite = S("icon_button");
            body.type = Image.Type.Sliced;
            Place(body, offset, new Vector2(design.IconButtonSize, design.IconButtonSize), anchor, anchor);

            var iconGo = Child(go, "Icon");
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = S(iconSprite);
            icon.color = design.InkText;
            icon.raycastTarget = false;
            var size = Px(24f);
            Place(icon, Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            var press = go.AddComponent<PressScale>();
            press.Bind(design);
            return button;
        }

        // ───────────────────────── Бейджі, чипи, перемикачі ─────────────────────────

        /// <summary>Бейдж-капсула з написом великими 12/900; ширина — за текстом, мінімум 60 px.</summary>
        internal static TMP_Text Badge(GameObject parent, string name, string text, DesignSystem design, string sprite, Color textColor)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = S(sprite);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)Px(10f), (int)Px(10f), 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = Px(26f);
            element.minWidth = Px(60f);
            go.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(Px(60f), Px(26f));

            var label = Text(go, "Label", text, design, Role.Label, textColor);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>Чип фільтра (теми, рідкості): скло / бірюзова цукерка, напис, необов'язкова мітка кольору.</summary>
        internal static ChipView Chip(GameObject parent, string name, string text, DesignSystem design, bool withMark = false)
        {
            var go = Child(parent, name);
            var body = go.AddComponent<Image>();
            body.sprite = S("chip");
            body.type = Image.Type.Sliced;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)Px(14f), (int)Px(14f), 0, 0);
            layout.spacing = Px(8f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = Px(40f);
            element.minHeight = design.TapMin;
            go.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(Px(72f), design.TapMin);

            Image? mark = null;
            if (withMark)
            {
                var markGo = Child(go, "Mark");
                mark = markGo.AddComponent<Image>();
                mark.sprite = S("pip_filled");
                mark.raycastTarget = false;
                var markLayout = markGo.AddComponent<LayoutElement>();
                markLayout.preferredWidth = markLayout.preferredHeight = Px(20f);
            }

            var label = Text(go, "Label", text, design, Role.Body, design.InkText);
            label.font = design.FontBlack;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;

            var chip = go.AddComponent<ChipView>();
            Wire(chip, ("design", design), ("button", button), ("body", body), ("label", label),
                ("offSprite", S("chip")), ("onSprite", S("chip_on")));
            if (mark != null)
                Wire(chip, ("mark", mark));
            chip.Configure(S("chip"), S("chip_on"));
            return chip;
        }

        /// <summary>Перемикач 56×34: доріжка й кружок — спрайти; стан — підміна спрайтів і позиція кружка.</summary>
        internal static (Image track, Image knob) Toggle(GameObject parent, string name, DesignSystem design, bool on)
        {
            var go = Child(parent, name);
            var track = go.AddComponent<Image>();
            track.sprite = S(on ? "toggle_track_on" : "toggle_track_off");
            track.raycastTarget = true;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(Px(56f), Px(34f));

            var knobGo = Child(go, "Knob");
            var knob = knobGo.AddComponent<Image>();
            knob.sprite = S(on ? "toggle_knob_on" : "toggle_knob_off");
            knob.raycastTarget = false;
            var size = Px(28f);
            Place(knob, new Vector2(on ? Px(11f) : -Px(11f), 0f), new Vector2(size, size), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _ = design;
            return (track, knob);
        }

        /// <summary>Поле вводу (нік): спрайт field, кільце фокусу/помилки навколо, текст 18/900.</summary>
        internal static (Image field, Image ring, TMP_Text text) Field(GameObject parent, string name, string value, DesignSystem design, bool error, bool focused)
        {
            var go = Child(parent, name);
            var field = go.AddComponent<Image>();
            field.sprite = S(error ? "field_error" : "field");
            field.type = Image.Type.Sliced;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(Px(300f), Px(54f));

            var ringGo = Child(go, "Ring");
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = S("field_focus_ring");
            ring.type = Image.Type.Sliced;
            ring.raycastTarget = false;
            Stretch(ringGo, -Px(3f));
            ring.color = error ? design.DangerLine : design.Teal;
            ringGo.SetActive(error || focused);

            var text = Text(go, "Text", value, design, Role.H3, design.InkText, TextAlignmentOptions.Left);
            Stretch(text.gameObject);
            text.margin = new Vector4(Px(16f), 0f, Px(16f), 0f);
            return (field, ring, text);
        }

        // ───────────────────────── Рамки рідкості ─────────────────────────

        internal static readonly string[] FrameNames = { "frame_rarity_1", "frame_rarity_2", "frame_rarity_3", "frame_rarity_4", "frame_rarity_5", "frame_rarity_6" };
        internal static readonly string[] PipNames = { "pips_1", "pips_2", "pips_3", "pips_4", "pips_5", "pips_6" };
        internal static readonly string[] GlowNames = { "frame_glow_epic", "frame_glow_legendary", "frame_glow_cosmic" };

        /// <summary>
        /// Рамка рідкості: гало під рамкою (Епічна+), сама рамка, вкладений «Picture» у вирізі
        /// (відступ 4 px для великої, 3 — для малої), мітки під нижнім краєм, іскри в кутах.
        /// Картинку викликач кладе в «Picture» (RawImage атласу чи полотно W4).
        /// </summary>
        internal static RarityFrameView RarityFrame(GameObject parent, string name, DesignSystem design, bool big, float sidePx, Rarity rarity)
        {
            var go = Child(parent, name);
            var side = Px(sidePx);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(side, side);

            var glowGo = Child(go, "Glow");
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = S("frame_glow_epic");
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            // Гало 164 намальовано під рамку 112: виступ 26 px з кожного боку (пропорційно до сторони).
            Stretch(glowGo, -side * 26f / 112f);

            var pictureGo = Child(go, "Picture");
            Stretch(pictureGo, Px(big ? 4f : 3f));

            var frameGo = Child(go, "Frame");
            var frame = frameGo.AddComponent<Image>();
            frame.sprite = S(big ? "frame_rarity_1" : "frame_rarity_1_small");
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            Stretch(frameGo);

            var pipsGo = Child(go, "Pips");
            var pips = pipsGo.AddComponent<Image>();
            pips.sprite = S("pips_1");
            pips.raycastTarget = false;
            Place(pips, new Vector2(0f, -Px(8f)), new Vector2(Px(20f), Px(14f)), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            var sparkles = new Image[4];
            var corners = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 0f) };
            for (var i = 0; i < 4; i++)
            {
                var sparkleGo = Child(go, $"Sparkle{i}");
                var sparkle = sparkleGo.AddComponent<Image>();
                sparkle.sprite = S("sparkle");
                sparkle.raycastTarget = false;
                var size = Px(24f);
                Place(sparkle, Vector2.zero, new Vector2(size, size), corners[i], new Vector2(0.5f, 0.5f));
                sparkles[i] = sparkle;
            }

            var view = go.AddComponent<RarityFrameView>();
            Wire(view, ("design", design), ("frame", frame), ("pips", pips), ("glow", glow));
            var sheen = SheenShader;
            if (sheen != null)
                Wire(view, ("sheenShader", sheen));
            WireArray(view, "sparkles", sparkles);
            WireArray(view, "frames", Sprites(FrameNames));
            WireArray(view, "framesSmall", Sprites(System.Array.ConvertAll(FrameNames, n => n + "_small")));
            WireArray(view, "pipSprites", Sprites(PipNames));
            WireArray(view, "glows", Sprites(GlowNames));
            view.Configure(big, Sprites(FrameNames), Sprites(System.Array.ConvertAll(FrameNames, n => n + "_small")), Sprites(PipNames), Sprites(GlowNames), sheen);
            view.SetRarity(rarity);
            return view;
        }

        private static Sprite[] Sprites(string[] names) => System.Array.ConvertAll(names, S);

        // ───────────────────────── Діалог і лист ─────────────────────────

        /// <summary>
        /// Діалог на весь екран: затемнення (scrim, альфа — токен) і непрозора панель dialog
        /// заданої ширини по центру (висота — за вмістом через VerticalLayoutGroup + fitter).
        /// Повертає компонент; вміст кладіть у <c>panel</c>.
        /// </summary>
        internal static DialogView Dialog(GameObject parent, string name, DesignSystem design, float widthPx, out GameObject panel)
        {
            var go = Child(parent, name);
            Stretch(go);
            var group = go.AddComponent<CanvasGroup>();

            var scrimGo = Child(go, "Scrim");
            Stretch(scrimGo);
            var scrim = scrimGo.AddComponent<Image>();
            scrim.sprite = LoadSprite("white-quad");
            scrim.color = DesignSystem.WithAlpha(design.Scrim, design.ScrimOpacity);

            panel = Child(go, "Panel");
            var image = panel.AddComponent<Image>();
            image.sprite = S("dialog");
            image.type = Image.Type.Sliced;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Px(widthPx), Px(200f));
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            var pad = (int)Px(20f);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = Px(12f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Shadow(panel, Px(24f), -Px(12f));

            var view = go.AddComponent<DialogView>();
            Wire(view, ("design", design), ("group", group), ("scrim", scrim), ("panel", rect));
            return view;
        }

        /// <summary>Нижній лист дій: верхні кути 30, притиснутий до низу, висота за вмістом.</summary>
        internal static GameObject Sheet(GameObject parent, string name, DesignSystem design, out GameObject content)
        {
            var go = Child(parent, name);
            Stretch(go);
            var scrimGo = Child(go, "Scrim");
            Stretch(scrimGo);
            var scrim = scrimGo.AddComponent<Image>();
            scrim.sprite = LoadSprite("white-quad");
            scrim.color = DesignSystem.WithAlpha(design.Scrim, design.SheetScrimOpacity);

            content = Child(go, "Sheet");
            var image = content.AddComponent<Image>();
            image.sprite = S("sheet");
            image.type = Image.Type.Sliced;
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, Px(260f));
            var layout = content.AddComponent<VerticalLayoutGroup>();
            var pad = (int)Px(20f);
            layout.padding = new RectOffset(pad, pad, pad, pad + (int)Px(12f));
            layout.spacing = Px(12f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        // ───────────────────────── Капсула нафти ─────────────────────────

        /// <summary>Капсула нафти — екземпляр префаба з UI Kit (спільний для всіх екранів).</summary>
        internal static CurrencyWidget Capsule(GameObject parent, Vector2 anchor, Vector2 offset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/UI/CurrencyWidget.prefab");
            if (prefab == null)
            {
                Debug.LogError("[InkFlow] Немає Assets/_Prefabs/UI/CurrencyWidget.prefab — спершу Build UI Kit.");
                return null!;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.name = "CurrencyWidget";
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            return go.GetComponent<CurrencyWidget>();
        }
    }
}
