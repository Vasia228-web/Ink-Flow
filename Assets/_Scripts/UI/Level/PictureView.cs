using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картинка на папері (документ §4, P2Watercolor): арт у текстурі, маска зафарбованості —
    /// у другій; шейдер <c>InkFlow/Watercolor</c> кладе папір, ескіз незафарбованого й
    /// акварель зафарбованих кроків. Одна в'юха на всі місця: над полем, картка перед
    /// забігом, картка завершення, галерея фіналу, шухляда колекції, накладки на планеті.
    ///
    /// Стан приходить із <see cref="PictureProgress"/>; окремий крок (<see cref="PlayPixel"/>)
    /// проявляється, коли крапля долетіла, — маска анімується 0 → 1 з одного LateUpdate
    /// (текстуру можна оновлювати щокадру: SetPixels32/Apply не чіпає графіку канваса).
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
        [SerializeField] private Shader watercolorShader;
        [SerializeField] private Texture2D paper;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text caption;

        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int PaperId = Shader.PropertyToID("_Paper");
        private static readonly int GridId = Shader.PropertyToID("_Grid");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int PaperColorId = Shader.PropertyToID("_PaperColor");
        private static readonly int GrainId = Shader.PropertyToID("_Grain");
        private static readonly int PaperScaleId = Shader.PropertyToID("_PaperScale");
        private static readonly int PencilId = Shader.PropertyToID("_Pencil");
        private static readonly int PencilWidthId = Shader.PropertyToID("_PencilWidth");
        private static readonly int PaintAlphaId = Shader.PropertyToID("_PaintAlpha");
        private static readonly int EdgeDarkId = Shader.PropertyToID("_EdgeDark");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int DisplaceId = Shader.PropertyToID("_Displace");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int VignetteWidthId = Shader.PropertyToID("_VignetteWidth");

        /// <summary>Знімки екрана з редактора: дозволити матеріали поза Play Mode (вони не потрапляють у префаб).</summary>
        public static bool EditorPreview { get; set; }

        private PixelPicture? _picture;
        private PictureProgress? _progress;
        private Texture2D? _texture;
        private Texture2D? _mask;
        private Material? _material;
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

        /// <summary>Картка перед забігом: лише контур і олівцевий ескіз — «ось що малюватимеш», без спойлера кольорів.</summary>
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
            var local = new Vector3(
                rect.xMin + rect.width * x / _picture.Width,
                rect.yMax - rect.height * y / _picture.Height,
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
                var flipped = (h - 1 - i / w) * w + i % w; // рядок 0 картинки — верхній, текстури — нижній
                _maskPixels[flipped].r = (byte)Mathf.RoundToInt(_reveal[i] * 255f);
                if (_reveal[i] >= 1f)
                {
                    _revealing[i] = false;
                    _revealingCount--;
                }
            }
            UploadMask();
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
            if (_texture == null || _texture.width != picture.Width || _texture.height != picture.Height)
            {
                if (_texture != null) Release(_texture);
                if (_mask != null) Release(_mask);
                _texture = NewTexture(picture, "Picture art");
                _mask = NewTexture(picture, "Picture mask", linear: true);
                _art = new Color32[picture.Width * picture.Height];
                _maskPixels = new Color32[picture.Width * picture.Height];
                _reveal = new float[picture.Width * picture.Height];
                _revealing = new bool[picture.Width * picture.Height];
                _revealingCount = 0;
                if (pixels != null)
                    pixels.texture = _texture;
                _material?.SetTexture(MaskId, _mask);
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
        /// Арт — кольори, тож sRGB; маска — ДАНІ (зафарбованість у R, код родини в G), тож
        /// linear: у лінійному просторі sRGB-текстура з кодом 1/255 декодується в ~0.0003, і
        /// шейдер не бачив жодної родини — на першому знімку акварель не з'явилась узагалі.
        /// </summary>
        private static Texture2D NewTexture(PixelPicture picture, string name, bool linear = false) =>
            new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, false, linear)
            {
                filterMode = FilterMode.Point,
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

        private void WriteMaskImmediately()
        {
            if (_picture == null)
                return;
            var w = _picture.Width;
            var h = _picture.Height;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var flipped = (h - 1 - y) * w + x;
                    _maskPixels[flipped].r = (byte)Mathf.RoundToInt(_reveal[i] * 255f);
                }
            UploadMask();
        }

        private void UploadMask()
        {
            if (_mask == null)
                return;
            _mask.SetPixels32(_maskPixels);
            _mask.Apply(false, false);
        }

        /// <summary>Матеріал — лише в Play Mode (або для знімків з редактора): у префаб він не потрапляє.</summary>
        private void ApplyMaterial()
        {
            if (design == null || pixels == null)
                return;
            if (!Application.isPlaying && !EditorPreview)
                return;
            if (watercolorShader == null)
                return;
            if (_material == null)
            {
                _material = new Material(watercolorShader) { name = "Watercolor (instance)", hideFlags = HideFlags.HideAndDontSave };
                pixels.material = _material;
            }
            var rect = ((RectTransform)pixels.transform).rect;
            var width = Mathf.Max(rect.width, 1f);
            var height = Mathf.Max(rect.height, 1f);
            _material.SetTexture(MaskId, _mask);
            if (paper != null)
                _material.SetTexture(PaperId, paper);
            _material.SetVector(GridId, new Vector4(_picture?.Width ?? 1, _picture?.Height ?? 1, 0f, 0f));
            _material.SetVector(SizeId, new Vector4(width, height, 0f, 0f));
            _material.SetFloat(RadiusId, Mathf.Min(design.PictureCanvasRadius, Mathf.Min(width, height) * 0.4f));
            _material.SetColor(PaperColorId, design.PaperColor);
            _material.SetFloat(GrainId, design.PaperGrain);
            _material.SetFloat(PaperScaleId, 1f / 256f);
            _material.SetColor(PencilId, DesignSystem.WithAlpha(design.PencilColor, design.PencilAlpha));
            _material.SetFloat(PencilWidthId, design.PencilWidth);
            _material.SetFloat(PaintAlphaId, design.PaintAlpha);
            _material.SetFloat(EdgeDarkId, design.PaintEdgeDark);
            _material.SetFloat(EdgeWidthId, design.PaintEdgeWidth);
            _material.SetFloat(DisplaceId, design.PaintDisplace);
            _material.SetFloat(VignetteId, design.PictureVignette);
            _material.SetFloat(VignetteWidthId, design.PictureVignetteWidth);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_material != null)
                ApplyMaterial();
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
            var max = Mathf.Max(_picture.Width, _picture.Height);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(side * _picture.Width / max, side * _picture.Height / max);
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
