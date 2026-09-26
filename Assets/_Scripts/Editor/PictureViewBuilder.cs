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
    /// Панель картинки — одна збірка на всі місця: над полем, у картці перед забігом, у картці
    /// завершення, у галереї фіналу, у шухляді колекції й на планеті. Панель — той самий матеріал,
    /// що поле K1Candy (спрайт із градієнтом і обвідкою, тінь під нею), рамка рідкості поверх,
    /// полотно — RawImage із шейдером W4DarkCanvas `InkFlow/DarkCanvas` (матеріал створює
    /// PictureView у Play Mode). Розійшлися б на першій правці.
    /// </summary>
    internal static class PictureViewBuilder
    {
        private const float K = 1080f / 390f;
        internal const string DarkCanvasShaderPath = "Assets/_Shaders/InkFlowDarkCanvas.shader";

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        internal static PictureView MakePictureView(GameObject go, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula,
            float width, float plateHeight, float canvasSide, bool withTitle, float captionHeight, bool bare = false,
            Sprite? particle = null)
        {
            var panel = K1Sprites.Panel;
            var shadow = LoadSprite("card-glow");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DarkCanvasShaderPath);
            if (shader == null) Debug.LogError($"[InkFlow] Немає {DarkCanvasShaderPath}");

            var plateGo = Child(go, "Plate");
            var plate = plateGo.GetComponent<RectTransform>();
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 1f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = new Vector2(0f, -plateHeight * 0.5f);
            plate.sizeDelta = new Vector2(width, plateHeight);

            var radius = Mathf.Min(design.BoardPanelRadius, plateHeight * 0.25f);

            // Тінь панелі (K1: чорний ~60 %, зсув униз, розмиття) — м'яке гало картки, тоноване чорним.
            var shadowGo = Child(plateGo, "Shadow");
            Stretch(shadowGo, -design.PanelShadowBlur);
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchoredPosition = new Vector2(0f, -design.PanelShadowOffset);
            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = shadow;
            shadowImage.type = Image.Type.Sliced;
            shadowImage.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius + design.PanelShadowBlur);
            shadowImage.color = bare ? Color.clear : design.PanelShadow;
            shadowImage.raycastTarget = false;

            var glowGo = Child(plateGo, "Glow");
            Stretch(glowGo, -M(18f));
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = nebula;
            glow.color = design.AccentGold;
            glow.raycastTarget = false;

            var fillGo = Child(plateGo, "Fill");
            Stretch(fillGo);
            var plateFill = fillGo.AddComponent<Image>();
            plateFill.sprite = panel != null ? panel : rounded;
            plateFill.type = Image.Type.Sliced;
            plateFill.pixelsPerUnitMultiplier = panel != null ? K1Sprites.PanelMultiplier(radius) : GlassPanel.PixelsPerUnitFor(radius);
            plateFill.color = bare ? Color.clear : Color.white;
            plateFill.raycastTarget = false;

            // Рамка рідкості (§6): обвідка поверх панелі в колір рідкості, світіння, для космічної — частинки.
            var strokeGo = Child(plateGo, "Stroke");
            Stretch(strokeGo);
            var plateStroke = strokeGo.AddComponent<Image>();
            plateStroke.sprite = outline;
            plateStroke.type = Image.Type.Sliced;
            plateStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            plateStroke.color = bare ? Color.clear : design.PicturePlateStroke;
            plateStroke.raycastTarget = false;

            var particlesGo = Child(plateGo, "Particles");
            Stretch(particlesGo);
            var particleRoot = particlesGo.GetComponent<RectTransform>();
            const int particleCount = 6;
            var particles = new Image[particleCount];
            for (var i = 0; i < particleCount; i++)
            {
                var dotGo = Child(particlesGo, $"Particle{i}");
                var dot = dotGo.AddComponent<Image>();
                dot.sprite = particle != null ? particle : nebula;
                dot.raycastTarget = false;
                dot.color = Color.white;
                Place(dot, Vector2.zero, new Vector2(M(7f), M(7f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                dotGo.SetActive(false);
                particles[i] = dot;
            }
            var frame = plateGo.AddComponent<RarityFrame>();
            Wire(frame, ("design", design), ("stroke", plateStroke), ("glow", glow), ("particleRoot", particleRoot));
            WireArray(frame, "particles", particles);

            TMP_Text? title = null;
            if (withTitle)
            {
                // Над полотном — назва великими літерами з розрядкою (#c9cbe8).
                title = Label(plateGo, "Title", "КИТ", design, font,
                    design.FontSizePictureName, design.PictureTitleColor, TextAlignmentOptions.Center);
                Place(title, new Vector2(0f, -M(5f)), new Vector2(width, M(14f)),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            }

            // Полотно — знизу панелі; без назви — по центру.
            var canvasGo = Child(plateGo, "Canvas");
            var canvas = canvasGo.GetComponent<RectTransform>();
            if (withTitle)
            {
                canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0f);
                canvas.pivot = new Vector2(0.5f, 0f);
                canvas.anchoredPosition = new Vector2(0f, M(7f));
            }
            else
            {
                canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0.5f);
                canvas.pivot = new Vector2(0.5f, 0.5f);
                canvas.anchoredPosition = Vector2.zero;
            }
            canvas.sizeDelta = new Vector2(canvasSide, canvasSide);

            var pixelsGo = Child(canvasGo, "Pixels");
            var pixels = pixelsGo.AddComponent<RawImage>();
            pixels.raycastTarget = false;
            pixels.color = Color.white;
            Place(pixels, Vector2.zero, new Vector2(canvasSide, canvasSide),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            TMP_Text? caption = null;
            if (captionHeight > 0f)
            {
                caption = Label(go, "Caption", "0 / 60", design, font,
                    design.FontSizePictureCaption, design.TextMuted, TextAlignmentOptions.Center);
                Place(caption, new Vector2(0f, 0f), new Vector2(width, captionHeight),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            }

            var view = go.AddComponent<PictureView>();
            Wire(view, ("design", design), ("plate", plate), ("plateFill", plateFill),
                ("plateStroke", plateStroke), ("glow", glow), ("frame", frame), ("canvas", canvas), ("pixels", pixels));
            if (shader != null) Wire(view, ("darkCanvasShader", shader));
            if (title != null) Wire(view, ("title", title));
            if (caption != null) Wire(view, ("caption", caption));
            view.Apply();
            return view;
        }

        /// <summary>
        /// Картинка над полем (§11): лише полотно W4DarkCanvas — без панелі, рамки, назви й лічильника
        /// (рішення автора; назва лишається на картках і в колекції). Рідкість — гало з-під полотна
        /// (<see cref="RarityHalo"/>, два шари для плавної зміни кольору з хвилею перефарбування).
        /// Корінь — блок розкладки (<see cref="RunLayout"/>), усередині по центру «Plate» зі стороною
        /// полотна: його масштабує спалах завершення. Сторону полотна виставляє розкладка екрана.
        /// </summary>
        internal static PictureView MakeRunPicture(GameObject go, DesignSystem design, float canvasSide)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DarkCanvasShaderPath);
            if (shader == null) Debug.LogError($"[InkFlow] Немає {DarkCanvasShaderPath}");
            var cardGlow = LoadSprite("card-glow");

            var plateGo = Child(go, "Plate");
            var plate = plateGo.GetComponent<RectTransform>();
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = Vector2.zero;
            plate.sizeDelta = new Vector2(canvasSide, canvasSide);

            // Два шари гало під полотном: поточне й наступне (перехід кольору з хвилею).
            Image HaloLayer(string name)
            {
                var layerGo = Child(plateGo, name);
                var image = layerGo.AddComponent<Image>();
                image.sprite = cardGlow;
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                image.color = Color.clear;
                return image;
            }
            var haloBack = HaloLayer("HaloBack");
            var haloFront = HaloLayer("HaloFront");

            var canvasGo = Child(plateGo, "Canvas");
            var canvas = canvasGo.GetComponent<RectTransform>();
            canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0.5f);
            canvas.pivot = new Vector2(0.5f, 0.5f);
            canvas.anchoredPosition = Vector2.zero;
            canvas.sizeDelta = new Vector2(canvasSide, canvasSide);

            var pixelsGo = Child(canvasGo, "Pixels");
            var pixels = pixelsGo.AddComponent<RawImage>();
            pixels.raycastTarget = false;
            pixels.color = Color.white;
            Place(pixels, Vector2.zero, new Vector2(canvasSide, canvasSide),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var halo = plateGo.AddComponent<RarityHalo>();
            // Гало обгортає САМУ картинку (RawImage, вписаний у квадрат за пропорціями), а не квадрат:
            // інакше в невисокої чи невузької картинки відкривається яскрава серцевина гало суцільною смугою.
            Wire(halo, ("design", design), ("canvas", pixels.rectTransform), ("front", haloFront), ("back", haloBack));
            var so = new SerializedObject(halo);
            so.FindProperty("spriteFalloffPx").floatValue = GenerateUISprites.GlowFalloff;
            so.ApplyModifiedPropertiesWithoutUndo();

            var view = go.AddComponent<PictureView>();
            Wire(view, ("design", design), ("plate", plate), ("canvas", canvas), ("pixels", pixels), ("halo", halo));
            if (shader != null) Wire(view, ("darkCanvasShader", shader));
            view.Apply();
            return view;
        }
    }

    /// <summary>
    /// Еталонні спрайти тривоги поля A1Breathe (`Assets/_Sprites/A1Breathe`, скопійовані з
    /// docs/StyleRef/A1Breathe/sprites). Усі 864×864; поле в них — 704×704 з відступом 80 px.
    /// Шар кладеться на панель тим самим центром і тягнеться разом із нею: якорі виходять за
    /// панель на 80/704 з кожного боку, тож масштаб спрайта завжди дорівнює масштабу поля.
    /// </summary>
    internal static class A1Sprites
    {
        private const string Folder = "Assets/_Sprites/A1Breathe";
        internal const float SpritePx = 864f;
        internal const float BoardPx = 704f;
        internal const float MarginPx = 80f;

        internal static readonly string[] Names =
            { "glow_outer_calm", "glow_outer_critical", "glow_inner_calm", "glow_inner_critical", "frame_line" };

        internal static Sprite? Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        internal static string PathOf(string name) => $"{Folder}/{name}.png";

        /// <summary>Шар сяйва на всю панель із запасом під спрайт; альфа 0 до першої тривоги.</summary>
        internal static Image Layer(GameObject panel, string name, string sprite)
        {
            var go = UiBuilder.Child(panel, name);
            var rect = go.GetComponent<RectTransform>();
            var margin = MarginPx / BoardPx;
            rect.anchorMin = new Vector2(-margin, -margin);
            rect.anchorMax = new Vector2(1f + margin, 1f + margin);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = Load(sprite);
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            if (image.sprite == null)
                Debug.LogError($"[InkFlow] Немає {PathOf(sprite)} — дай редактору імпортувати Assets/_Sprites/A1Breathe.");
            return image;
        }

        internal static bool AllPresent(System.Collections.Generic.List<string> missing)
        {
            var ok = true;
            foreach (var name in Names)
                if (Load(name) == null)
                {
                    missing.Add($"{PathOf(name)} (спрайт A1Breathe; скопійовано з docs/StyleRef — дай редактору імпортувати)");
                    ok = false;
                }
            return ok;
        }
    }

    /// <summary>
    /// Еталонні спрайти K1Candy (`Assets/_Sprites/K1Candy`, скопійовані з docs/StyleRef/) і
    /// їхня геометрія в пікселях: радіус кута панелі й слота потрібен, щоб перерахувати
    /// потрібний радіус із DesignSystem у pixelsPerUnitMultiplier 9-slice.
    /// </summary>
    internal static class K1Sprites
    {
        private const string Folder = "Assets/_Sprites/K1Candy";

        /// <summary>Радіус кута в пікселях спрайта: панель ≈ 49 px із 256, слот ≈ 41 px із 128 (виміряно з еталона).</summary>
        private const float PanelCornerPx = 49f;
        private const float SlotCornerPx = 41f;

        internal static Sprite? Panel => Load("panel");
        internal static Sprite? TraySlot => Load("tray-slot");
        internal static Sprite? Socket => Load("socket");
        internal static Sprite? Block => Load("block-base");
        internal static Sprite? Highlight => Load("block-highlight");
        internal static Sprite? Glow => Load("glow");

        internal static Sprite? Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        /// <summary>PPU 100 і referencePixelsPerUnit 100: межа в одиницях = px / множник, тож множник = px кута / бажаний радіус.</summary>
        internal static float PanelMultiplier(float radiusUnits) => PanelCornerPx / Mathf.Max(radiusUnits, 1f);
        internal static float SlotMultiplier(float radiusUnits) => SlotCornerPx / Mathf.Max(radiusUnits, 1f);

        internal static bool AllPresent(System.Collections.Generic.List<string> missing)
        {
            var ok = true;
            foreach (var name in new[] { "panel", "tray-slot", "socket", "block-base", "block-highlight", "glow" })
                if (Load(name) == null)
                {
                    missing.Add($"{Folder}/{name}.png (спрайт K1Candy; скопійовано з docs/StyleRef — дай редактору імпортувати)");
                    ok = false;
                }
            return ok;
        }
    }
}
