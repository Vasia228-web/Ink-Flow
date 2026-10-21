using InkFlow.Core;
using InkFlow.Style;
using InkFlow.UI;
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
        internal const string DarkCanvasShaderPath = "Assets/_Shaders/InkFlowDarkCanvas.shader";

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
            // Не квадратна картинка — вліво-вгору: край врівень із «‹», верх — із панелями рахунку (S02Run_01).
            var fit = new SerializedObject(view);
            fit.FindProperty("fitAnchor").vector2Value = new Vector2(0f, 1f);
            fit.ApplyModifiedPropertiesWithoutUndo();
            view.Apply();
            return view;
        }

        /// <summary>
        /// Картинка всередині рамки рідкості U1Glaze (мініатюра на головному, далі — картки й комірки): лише
        /// полотно W4DarkCanvas по центру квадрата <paramref name="canvasSide"/>, без панелі, назви й гало —
        /// рідкість каже рамка (<see cref="RarityFrameView"/>), а не світло з-під полотна.
        /// </summary>
        internal static PictureView MakeFramedPicture(GameObject go, DesignSystem design, float canvasSide)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DarkCanvasShaderPath);
            if (shader == null) Debug.LogError($"[InkFlow] Немає {DarkCanvasShaderPath}");

            // Підкладка кольору полотна на все вікно рамки: картинки здебільшого не квадратні (стего 33×25…), а
            // полотно вписується за пропорціями — без неї в смугах збоку просвічувала б картка (кути накриває кільце).
            var backGo = Child(go, "Back");
            Stretch(backGo);
            var back = backGo.AddComponent<Image>();
            back.sprite = LoadSprite("white-quad");
            back.color = design.PictureCanvasColor;
            back.raycastTarget = false;

            var plateGo = Child(go, "Plate");
            var plate = plateGo.GetComponent<RectTransform>();
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = Vector2.zero;
            plate.sizeDelta = new Vector2(canvasSide, canvasSide);

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

            var view = go.AddComponent<PictureView>();
            Wire(view, ("design", design), ("plate", plate), ("canvas", canvas), ("pixels", pixels));
            if (shader != null) Wire(view, ("darkCanvasShader", shader));
            view.Apply();
            return view;
        }
    }

    /// <summary>
    /// Шари тривоги поля A1Breathe: геометрія — як у спрайтів еталона (квадрат 864 px, поле 704 px із
    /// відступом 80 px, <see cref="BoardAlarmGlow"/>). Шар кладеться на панель тим самим центром і
    /// тягнеться разом із нею: якорі виходять за панель на 80/704 з кожного боку. Спрайт шару
    /// будує BoardPulse з формули еталона й токенів товщини та яскравості — у префаб він не потрапляє.
    /// </summary>
    internal static class A1Layers
    {
        /// <summary>Шар на всю панель із запасом під сяйво; альфа 0 до першої тривоги.</summary>
        internal static Image Layer(GameObject panel, string name)
        {
            var go = UiBuilder.Child(panel, name);
            var rect = go.GetComponent<RectTransform>();
            var margin = BoardAlarmGlow.MarginPx / BoardAlarmGlow.BoardPx;
            rect.anchorMin = new Vector2(-margin, -margin);
            rect.anchorMax = new Vector2(1f + margin, 1f + margin);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
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
