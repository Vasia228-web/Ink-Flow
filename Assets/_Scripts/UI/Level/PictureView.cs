using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картинка на темному полотні (документ §4, W4DarkCanvas, `docs/StyleRef/W4DarkCanvas/`):
    /// арт у текстурі, покриття й коди родин — у масці, гало фарби — у третій текстурі; шейдер
    /// <c>InkFlow/DarkCanvas</c> кладе полотно з плетінням, світлі контури незафарбованого, гало
    /// й рівні квадратні пікселі зі світлом зверху й тінню знизу. Одна в'юха на всі місця: над
    /// полем, картка перед забігом, картка завершення, галерея фіналу, шухляда колекції, планети.
    ///
    /// Стан приходить із <see cref="PictureProgress"/>; окремий крок (<see cref="PlayPixel"/>)
    /// проявляється, коли крапля долетіла, — маска анімується 0 → 1 з одного LateUpdate
    /// (текстуру можна оновлювати щокадру: SetPixels32/Apply не чіпає графіку канваса). Що видно
    /// на кожному пікселі (контур — разом із сусідньою фарбою) і де світиться гало, рахує Core
    /// (<see cref="PictureCanvas"/>) — тому «готова картинка = арт піксель у піксель» тримає тест.
    /// </summary>
    public sealed class PictureView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private RectTransform plate;
        [SerializeField] private Image plateFill;
        [SerializeField] private Image plateStroke;
        [SerializeField] private Image glow;
        [SerializeField] private RarityFrame frame;
        [SerializeField] private RectTransform canvas;
        [SerializeField] private RawImage pixels;
        [SerializeField] private Shader darkCanvasShader;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text caption;

        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int HaloId = Shader.PropertyToID("_Halo");
        private static readonly int GridId = Shader.PropertyToID("_Grid");
        private static readonly int MarginId = Shader.PropertyToID("_Margin");
        private static readonly int CanvasColorId = Shader.PropertyToID("_CanvasColor");
        private static readonly int WeaveId = Shader.PropertyToID("_Weave");
        private static readonly int LightId = Shader.PropertyToID("_Light");
        private static readonly int SketchColorId = Shader.PropertyToID("_SketchColor");
        private static readonly int SketchWidthId = Shader.PropertyToID("_SketchWidth");
        private static readonly int SketchAllTonesId = Shader.PropertyToID("_SketchAllTones");
        private static readonly int CornerId = Shader.PropertyToID("_Corner");
        private static readonly int BorderId = Shader.PropertyToID("_Border");
        private static readonly int PopId = Shader.PropertyToID("_Pop");

        /// <summary>Знімки екрана з редактора: дозволити матеріали поза Play Mode (вони не потрапляють у префаб).</summary>
        public static bool EditorPreview { get; set; }

        private PixelPicture? _picture;
        private PictureProgress? _progress;
        private Texture2D? _texture;
        private Texture2D? _mask;
        private Texture2D? _halo;
        private Material? _material;
        private int _margin = -1;
        private float[] _coverage = System.Array.Empty<float>();
        private float[] _coverageScratch = System.Array.Empty<float>();
        private float[] _haloValues = System.Array.Empty<float>();
        private float[] _haloScratch = System.Array.Empty<float>();
        private Color32[] _haloPixels = System.Array.Empty<Color32>();
        private Color32[] _art = System.Array.Empty<Color32>();
        private Color32[] _maskPixels = System.Array.Empty<Color32>();
        private float[] _reveal = System.Array.Empty<float>();   // 0..1 на піксель — куди анімується маска
        private bool[] _revealing = System.Array.Empty<bool>();
        private int _revealingCount;
        private bool _maskDirty;
        private Coroutine? _flash;

        public PixelPicture? Picture => _picture;

        public void Apply()
        {
            if (design == null)
                return;
            if (plateFill != null) plateFill.color = Color.white; // панель K1Candy — спрайт із запеченим градієнтом
            if (plateStroke != null && frame == null) plateStroke.color = _picture != null ? design.RarityColor(_picture.Rarity) : design.PicturePlateStroke;
            if (glow != null && frame == null)
            {
                glow.color = design.AccentGold;
                glow.canvasRenderer.SetAlpha(0f);
            }
            if (pixels != null)
            {
                pixels.color = Color.white;
                pixels.raycastTarget = false;
            }
            Font(title, design.FontSizePictureName, design.PictureTitleColor, design.LetterSpacingWide);
            Font(caption, design.FontSizePictureCaption, design.TextMuted, design.LetterSpacingWide);
            if (plate != null) plate.localScale = Vector3.one;
            ApplyMaterial();
        }

        /// <summary>Показує стан картинки як є: контур, зафарбовані кроки, решта — ескіз.</summary>
        public void Show(PictureProgress progress)
        {
            _progress = progress;
            Bind(progress.Picture);
            for (var i = 0; i < _reveal.Length; i++)
                _reveal[i] = progress.IsPixelFilled(i) ? 1f : 0f;
            WriteMaskImmediately();
            ApplyTexts();
        }

        /// <summary>Готова картинка — галерея фіналу, колекція, планета: усе зафарбовано, назва в колір рідкості.</summary>
        public void ShowCompleted(PixelPicture picture, string captionText)
        {
            _progress = null;
            Bind(picture);
            for (var i = 0; i < _reveal.Length; i++)
                _reveal[i] = 1f;
            WriteMaskImmediately();
            if (title != null)
            {
                title.text = picture.Name;
                if (design != null)
                    title.color = design.RarityColor(picture.Rarity);
            }
            if (caption != null)
                caption.text = captionText;
        }

        /// <summary>Картка перед забігом: лише світлі контури зон — «ось що малюватимеш», без спойлера кольорів.</summary>
        public void ShowOutline(PixelPicture picture)
        {
            _progress = null;
            Bind(picture);
            for (var i = 0; i < _reveal.Length; i++)
                _reveal[i] = 0f;
            WriteMaskImmediately();
            if (title != null)
            {
                title.text = picture.Name;
                if (design != null)
                    title.color = design.RarityColor(picture.Rarity);
            }
            if (caption != null)
                caption.text = string.Empty;
        }

        /// <summary>Один крок ліг (крапля долетіла): його пікселі проявляються плавно. Індекс — з події PixelFilled.</summary>
        public void PlayPixel(int step)
        {
            if (_picture == null || step < 0 || step >= _picture.FillCount)
                return;
            var n = _picture.StepLength(step);
            for (var k = 0; k < n; k++)
            {
                var i = _picture.StepPixel(step, k);
                if (i < 0 || i >= _reveal.Length || _reveal[i] >= 1f)
                    continue;
                if (!_revealing[i])
                {
                    _revealing[i] = true;
                    _revealingCount++;
                }
            }
            // Поза Play Mode (знімок з редактора) або на вимкненій в'юсі — без анімації, одразу.
            if (!Application.isPlaying || !isActiveAndEnabled)
                FinishReveal();
            ApplyTexts();
        }

        /// <summary>Світова точка кроку — ціль для краплі з поля (центр його пікселів).</summary>
        public Vector3 PixelWorldPosition(int step)
        {
            var target = pixels != null ? (RectTransform)pixels.transform : canvas;
            if (target == null || _picture == null || step < 0 || step >= _picture.FillCount)
                return transform.position;
            var rect = target.rect;
            _picture.StepCenter(step, out var x, out var y);
            // Квад — усе полотно: арт плюс порожнє поле з кожного боку.
            var margin = Mathf.Max(_margin, 0);
            var local = new Vector3(
                rect.xMin + rect.width * (x + margin) / (_picture.Width + margin * 2),
                rect.yMax - rect.height * (y + margin) / (_picture.Height + margin * 2),
                0f);
            return target.TransformPoint(local);
        }

        /// <summary>Спалах завершення (§8), після нього — <paramref name="then"/>.</summary>
        public void PlayCompleted(System.Action? then)
        {
            if (_flash != null)
                StopCoroutine(_flash);
            if (!isActiveAndEnabled || design == null)
            {
                then?.Invoke();
                return;
            }
            _flash = StartCoroutine(CompletedRoutine(then));
        }

        public void StopAll()
        {
            if (_flash != null)
                StopCoroutine(_flash);
            _flash = null;
            FinishReveal();
            if (plate != null) plate.localScale = Vector3.one;
            if (glow != null) glow.canvasRenderer.SetAlpha(frame != null ? glow.color.a : 0f);
        }

        private IEnumerator CompletedRoutine(System.Action? then)
        {
            var duration = design.PictureCompleteDuration;
            var pop = design.PictureCompletePop;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                var wave = Mathf.Sin(k * Mathf.PI);
                if (plate != null)
                    plate.localScale = Vector3.one * (1f + pop * design.CurveBackOut.Evaluate(Mathf.Min(1f, k * 2f)) * (1f - Mathf.Max(0f, k - 0.5f) * 2f));
                if (glow != null)
                    glow.canvasRenderer.SetAlpha(Mathf.Max(glow.color.a, design.PictureGlowAlpha * wave));
                yield return null;
            }

            if (plate != null) plate.localScale = Vector3.one;
            if (glow != null) glow.canvasRenderer.SetAlpha(frame != null ? glow.color.a : 0f);
            _flash = null;
            then?.Invoke();
        }

        // ── Проявлення: маска анімується з одного LateUpdate ──

        private void LateUpdate()
        {
            if (_revealingCount == 0 || design == null || _mask == null)
                return;
            var speed = Time.deltaTime / Mathf.Max(design.PictureRevealDuration, 0.05f);
            var w = _picture != null ? _picture.Width : 1;
            var h = _picture != null ? _picture.Height : 1;
            for (var i = 0; i < _reveal.Length; i++)
            {
                if (!_revealing[i])
                    continue;
                _reveal[i] = Mathf.Min(1f, _reveal[i] + speed);
                if (_reveal[i] >= 1f)
                {
                    _revealing[i] = false;
                    _revealingCount--;
                }
            }
            WriteMaskImmediately();
        }

        private void FinishReveal()
        {
            if (_revealingCount == 0)
                return;
            for (var i = 0; i < _reveal.Length; i++)
                if (_revealing[i])
                {
                    _reveal[i] = 1f;
                    _revealing[i] = false;
                }
            _revealingCount = 0;
            WriteMaskImmediately();
        }

        // ── Текстури ──

        private void Bind(PixelPicture picture)
        {
            var changed = _picture != picture;
            _picture = picture;
            var margin = design != null ? Mathf.Max(design.PictureMargin, 0) : 2;
            if (_texture == null || _texture.width != picture.Width || _texture.height != picture.Height || margin != _margin)
            {
                if (_texture != null) Release(_texture);
                if (_mask != null) Release(_mask);
                if (_halo != null) Release(_halo);
                _margin = margin;
                var n = picture.Width * picture.Height;
                var canvasCells = (picture.Width + margin * 2) * (picture.Height + margin * 2);
                _texture = NewTexture(picture.Width, picture.Height, "Picture art");
                _mask = NewTexture(picture.Width, picture.Height, "Picture mask", linear: true);
                _halo = NewTexture(picture.Width + margin * 2, picture.Height + margin * 2, "Picture halo", bilinear: true);
                _art = new Color32[n];
                _maskPixels = new Color32[n];
                _reveal = new float[n];
                _revealing = new bool[n];
                _revealingCount = 0;
                _coverage = new float[n];
                _coverageScratch = new float[n];
                _haloValues = new float[canvasCells * 4];
                _haloScratch = new float[canvasCells * 4];
                _haloPixels = new Color32[canvasCells];
                if (pixels != null)
                    pixels.texture = _texture;
                _material?.SetTexture(MaskId, _mask);
                _material?.SetTexture(HaloId, _halo);
                changed = true;
            }
            if (changed)
            {
                UploadArt();
                FitAspect();
                ApplyMaterial();
            }
            if (frame != null)
                frame.Bind(picture.Rarity);
            else if (plateStroke != null && design != null)
                plateStroke.color = design.RarityColor(picture.Rarity);
        }

        /// <summary>
        /// Арт і гало — кольори, тож sRGB; маска — ДАНІ (покриття в R, код родини в G), тож
        /// linear: у лінійному просторі sRGB-текстура з кодом 1/255 декодується в ~0.0003, і
        /// шейдер не бачив би жодної родини. Арт і маска — точкові (пікселі рівні, коди дискретні),
        /// гало — білінійне: світіння наростає м'яко.
        /// </summary>
        private static Texture2D NewTexture(int width, int height, string name, bool linear = false, bool bilinear = false) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false, linear)
            {
                filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };

        /// <summary>Арт: тон кожного пікселя (A = 1 де є піксель); маска: G = код родини (0 порожньо, 255 контур).</summary>
        private void UploadArt()
        {
            if (_texture == null || _picture == null)
                return;
            var w = _picture.Width;
            var h = _picture.Height;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var tone = _picture[x, y];
                    var flipped = (h - 1 - y) * w + x; // рядок 0 картинки — верхній, рядок 0 текстури — нижній
                    if (tone == MasterPalette.Empty)
                    {
                        _art[flipped] = new Color32(0, 0, 0, 0);
                        _maskPixels[flipped] = new Color32(0, 0, 0, 255);
                        continue;
                    }
                    var color = MasterPalette.ColorOf(tone).ToColor();
                    _art[flipped] = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), 255);
                    var family = _picture.FamilyAt(i);
                    byte code = 255;
                    if (family != MasterPalette.Empty)
                    {
                        var rank = 0;
                        for (var f = 0; f < _picture.FillColors.Count; f++)
                            if (_picture.FillColors[f] == family) { rank = f + 1; break; }
                        code = (byte)rank;
                    }
                    _maskPixels[flipped] = new Color32(0, code, 0, 255);
                }
            _texture.SetPixels32(_art);
            _texture.Apply(false, false);
        }

        /// <summary>Покриття (контур — разом із сусідньою фарбою) у маску й гало фарби — з поточного проявлення.</summary>
        private void WriteMaskImmediately()
        {
            if (_picture == null)
                return;
            var w = _picture.Width;
            var h = _picture.Height;
            PictureCanvas.Coverage(_picture, _reveal, _coverage, _coverageScratch);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var flipped = (h - 1 - y) * w + x; // рядок 0 картинки — верхній, текстури — нижній
                    _maskPixels[flipped].r = (byte)Mathf.RoundToInt(_coverage[y * w + x] * 255f);
                }
            if (_mask != null)
            {
                _mask.SetPixels32(_maskPixels);
                _mask.Apply(false, false);
            }
            WriteHalo();
        }

        private void WriteHalo()
        {
            if (_picture == null || _halo == null || design == null)
                return;
            PictureCanvas.Halo(_picture, _coverage, _margin, design.PictureHaloSigma, design.PictureHaloGain,
                design.PictureHaloAlpha, _haloValues, _haloScratch);
            var cw = _halo.width;
            var ch = _halo.height;
            for (var y = 0; y < ch; y++)
                for (var x = 0; x < cw; x++)
                {
                    var k = (y * cw + x) * 4;
                    _haloPixels[(ch - 1 - y) * cw + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(_haloValues[k]) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(_haloValues[k + 1]) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(_haloValues[k + 2]) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(_haloValues[k + 3]) * 255f));
                }
            _halo.SetPixels32(_haloPixels);
            _halo.Apply(false, false);
        }

        /// <summary>Матеріал — лише в Play Mode (або для знімків з редактора): у префаб він не потрапляє.</summary>
        private void ApplyMaterial()
        {
            if (design == null || pixels == null)
                return;
            if (!Application.isPlaying && !EditorPreview)
                return;
            if (darkCanvasShader == null)
                return;
            if (_material == null)
            {
                _material = new Material(darkCanvasShader) { name = "DarkCanvas (instance)", hideFlags = HideFlags.HideAndDontSave };
                pixels.material = _material;
            }
            _material.SetTexture(MaskId, _mask);
            _material.SetTexture(HaloId, _halo);
            _material.SetVector(GridId, new Vector4(_picture?.Width ?? 1, _picture?.Height ?? 1, 0f, 0f));
            _material.SetFloat(MarginId, Mathf.Max(_margin, 0));
            _material.SetColor(CanvasColorId, design.PictureCanvasColor);
            _material.SetVector(WeaveId, new Vector4(design.PictureWeavePitch, design.PictureWeaveAlpha, design.PictureWeaveOnPaint, 0f));
            _material.SetVector(LightId, new Vector4(design.PictureLightTop, design.PictureShadeBottom, 0f, 0f));
            _material.SetColor(SketchColorId, DesignSystem.WithAlpha(design.PictureSketchColor, design.PictureSketchAlpha));
            _material.SetVector(SketchWidthId, new Vector4(design.PictureSketchWidth, 1f, 0f, 0f));
            _material.SetFloat(SketchAllTonesId, design.PictureSketchAllTones ? 1f : 0f);
            _material.SetFloat(CornerId, design.PictureCornerRadius);
            _material.SetVector(BorderId, new Vector4(design.PictureBorderAlpha, design.PictureBorderWidth, 1f, 0f));
            _material.SetFloat(PopId, design.PictureRevealPop);
        }

        /// <summary>Картинка не мусить бути квадратною — вписуємо її в квадрат полотна, зберігаючи пропорції.</summary>
        private void FitAspect()
        {
            if (pixels == null || canvas == null || _picture == null)
                return;
            var rect = (RectTransform)pixels.transform;
            var side = Mathf.Min(canvas.rect.width, canvas.rect.height);
            if (side <= 1f)
                side = canvas.sizeDelta.x > 1f ? canvas.sizeDelta.x : 100f;
            // Полотно — арт плюс порожнє поле з кожного боку; пропорції — полотна.
            var margin = Mathf.Max(_margin, 0);
            var cw = _picture.Width + margin * 2;
            var ch = _picture.Height + margin * 2;
            var max = Mathf.Max(cw, ch);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(side * cw / max, side * ch / max);
        }

        private void ApplyTexts()
        {
            if (_progress == null)
                return;
            if (title != null)
            {
                title.text = _progress.Picture.Name;
                if (design != null)
                    title.color = design.PictureTitleColor;
            }
            if (caption != null)
                caption.text = _progress.IsComplete ? "ГОТОВО" : $"{_progress.FilledCount} / {_progress.Total}";
        }

        private static void Release(Object asset)
        {
            if (Application.isPlaying)
                Destroy(asset);
            else
                DestroyImmediate(asset);
        }

        private void OnDestroy()
        {
            if (_texture != null) Release(_texture);
            if (_mask != null) Release(_mask);
            if (_halo != null) Release(_halo);
            if (_material != null) Release(_material);
        }

        private void Font(TMP_Text? label, float size, Color color, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
