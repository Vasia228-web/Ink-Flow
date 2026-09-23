using InkFlow.Style;
using InkFlow.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InkFlow.Editor.UiBuilder;

namespace InkFlow.Editor
{
    /// <summary>
    /// Плитка з картинкою — одна збірка на всі місця: над полем, у картці перед забігом,
    /// у галереї фіналу, у шухляді колекції й на планеті. Розійшлися б на першій правці.
    /// </summary>
    internal static class PictureViewBuilder
    {
        private const float K = 1080f / 390f;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        /// <summary>Скільки зон може показати картинка: легендарна — 15+ (§6).</summary>
        internal const int ZoneSlots = 20;

        /// <summary>
        /// Плитка з картинкою: скло, гало завершення, назва згори, квадрат зон знизу і,
        /// за потреби, підпис під плиткою. Одна збірка на три місця — картинка над полем,
        /// картка перед забігом, мініатюри галереї, — щоб вони не розійшлися на першій правці.
        /// </summary>
        internal static PictureView MakePictureView(GameObject go, DesignSystem design, TMP_FontAsset? font,
            Sprite rounded, Sprite outline, Sprite nebula, Shader zoneShader, PictureArtCatalog art,
            float width, float plateHeight, float canvasSide, bool withTitle, float captionHeight, bool bare = false)
        {
            var plateGo = Child(go, "Plate");
            var plate = plateGo.GetComponent<RectTransform>();
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 1f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = new Vector2(0f, -plateHeight * 0.5f);
            plate.sizeDelta = new Vector2(width, plateHeight);

            var radius = Mathf.Min(M(18f), plateHeight * 0.25f);

            var glowGo = Child(plateGo, "Glow");
            Stretch(glowGo, -M(18f));
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = nebula;
            glow.color = design.AccentGold;
            glow.raycastTarget = false;

            var fillGo = Child(plateGo, "Fill");
            Stretch(fillGo);
            var plateFill = fillGo.AddComponent<Image>();
            plateFill.sprite = rounded;
            plateFill.type = Image.Type.Sliced;
            plateFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            plateFill.color = bare ? Color.clear : design.PicturePlateFill;
            plateFill.raycastTarget = false;

            var strokeGo = Child(plateGo, "Stroke");
            Stretch(strokeGo);
            var plateStroke = strokeGo.AddComponent<Image>();
            plateStroke.sprite = outline;
            plateStroke.type = Image.Type.Sliced;
            plateStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(radius);
            plateStroke.color = bare ? Color.clear : design.PicturePlateStroke;
            plateStroke.raycastTarget = false;

            TMP_Text? title = null;
            TMP_Text? attempts = null;
            if (withTitle)
            {
                title = Label(plateGo, "Title", "КИТ · 0/5", design, font,
                    design.FontSizePictureName, design.TextPrimary, TextAlignmentOptions.Center);
                Place(title, new Vector2(0f, -M(5f)), new Vector2(width, M(14f)),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

                // §7 п.5: «Спроб лишилось» видно завжди — під назвою, золотом, лише коли є що рятувати.
                attempts = Label(plateGo, "Attempts", "СПРОБ · 3", design, font,
                    design.FontSizePictureCaption, design.AccentGold, TextAlignmentOptions.Center);
                Place(attempts, new Vector2(0f, -M(19f)), new Vector2(width, M(12f)),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                attempts.gameObject.SetActive(false);
            }

            // Квадрат зон — знизу плитки; без назви — по центру.
            var canvasGo = Child(plateGo, "Canvas");
            var canvas = canvasGo.GetComponent<RectTransform>();
            if (withTitle)
            {
                canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0f);
                canvas.pivot = new Vector2(0.5f, 0f);
                canvas.anchoredPosition = new Vector2(0f, M(6f));
            }
            else
            {
                canvas.anchorMin = canvas.anchorMax = new Vector2(0.5f, 0.5f);
                canvas.pivot = new Vector2(0.5f, 0.5f);
                canvas.anchoredPosition = Vector2.zero;
            }
            canvas.sizeDelta = new Vector2(canvasSide, canvasSide);

            var zones = new PictureZoneView[ZoneSlots];
            for (var i = 0; i < ZoneSlots; i++)
            {
                var zoneGo = Child(canvasGo, $"Zone_{i}");
                Stretch(zoneGo);
                var image = zoneGo.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                var zone = zoneGo.AddComponent<PictureZoneView>();
                Wire(zone, ("design", design), ("zoneShader", zoneShader), ("image", image));
                zoneGo.SetActive(false);
                zones[i] = zone;
            }

            TMP_Text? caption = null;
            if (captionHeight > 0f)
            {
                caption = Label(go, "Caption", "ТІЛО · СИНІЙ", design, font,
                    design.FontSizePictureCaption, design.TextMuted, TextAlignmentOptions.Center);
                Place(caption, new Vector2(0f, 0f), new Vector2(width, captionHeight),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            }

            var view = go.AddComponent<PictureView>();
            Wire(view, ("design", design), ("art", art), ("plate", plate), ("plateFill", plateFill),
                ("plateStroke", plateStroke), ("glow", glow), ("canvas", canvas));
            if (title != null) Wire(view, ("title", title));
            if (attempts != null) Wire(view, ("attempts", attempts));
            if (caption != null) Wire(view, ("caption", caption));
            WireArray(view, "zones", zones);
            view.Apply();
            return view;
        }

    }
}
