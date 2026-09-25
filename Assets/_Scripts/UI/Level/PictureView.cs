using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Піксельна картинка (документ §4, §11): текстура «піксель у піксель» на RawImage,
    /// контур видно одразу, заливка проявляється пікселями; рамка — у колір рідкості.
    /// Одна в'юха на всі місця: над полем, картка перед забігом, галерея фіналу,
    /// шухляда колекції, накладки на планеті.
    ///
    /// Стан приходить із <see cref="PictureProgress"/>; окремий піксель
    /// (<see cref="PlayPixel"/>) кладеться в момент, коли крапля долетіла, — не тоді,
    /// коли модель уже все порахувала.
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
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text attempts;

        private PixelPicture? _picture;
        private PictureProgress? _progress;
        private Texture2D? _texture;
        private Color32[] _buffer = System.Array.Empty<Color32>();
        private Coroutine? _flash;

        public PixelPicture? Picture => _picture;

        public void Apply()
        {
            if (design == null)
                return;
            if (plateFill != null) plateFill.color = design.PicturePlateFill;
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
            Font(title, design.FontSizePictureName, design.TextPrimary, design.LetterSpacingWide);
            Font(caption, design.FontSizePictureCaption, design.TextMuted, design.LetterSpacingWide);
            Font(attempts, design.FontSizePictureCaption, design.AccentGold, design.LetterSpacingWide);
            if (attempts != null && Application.isPlaying == false) attempts.gameObject.SetActive(false);
            if (plate != null) plate.localScale = Vector3.one;
        }

        /// <summary>§9 п.5: «Спроб лишилось: N» видно завжди, поки картинка перенесена; 0 — напису немає.</summary>
        public void ShowAttempts(int attemptsLeft)
        {
            if (attempts == null)
                return;
            var show = attemptsLeft > 0;
            if (attempts.gameObject.activeSelf != show)
                attempts.gameObject.SetActive(show);
            if (show)
                attempts.text = $"СПРОБ · {attemptsLeft}";
        }

        /// <summary>Показує стан картинки як є: контур, заповнені пікселі, решта — ледь помітний силует.</summary>
        public void Show(PictureProgress progress)
        {
            _progress = progress;
            Bind(progress.Picture);
            for (var i = 0; i < _buffer.Length; i++)
                _buffer[i] = ColorFor(i, progress.IsFilled(i));
            Upload();
            ApplyTexts();
        }

        /// <summary>Готова картинка — галерея фіналу, колекція, планета: усе залито, назва в колір рідкості.</summary>
        public void ShowCompleted(PixelPicture picture, string captionText)
        {
            _progress = null;
            ShowAttempts(0);
            Bind(picture);
            for (var i = 0; i < _buffer.Length; i++)
                _buffer[i] = ColorFor(i, filled: true);
            Upload();
            if (title != null)
            {
                title.text = picture.Name;
                if (design != null)
                    title.color = design.RarityColor(picture.Rarity);
            }
            if (caption != null)
                caption.text = captionText;
        }

        /// <summary>Картка перед забігом: лише контур-силует — «ось що малюватимеш», без спойлера кольорів.</summary>
        public void ShowOutline(PixelPicture picture)
        {
            _progress = null;
            Bind(picture);
            for (var i = 0; i < _buffer.Length; i++)
                _buffer[i] = ColorFor(i, filled: false);
            Upload();
            if (title != null)
            {
                title.text = picture.Name;
                if (design != null)
                    title.color = design.RarityColor(picture.Rarity);
            }
            if (caption != null)
                caption.text = string.Empty;
        }

        /// <summary>Один піксель ліг (крапля долетіла). Індекс — з події PixelFilled.</summary>
        public void PlayPixel(int index)
        {
            if (_picture == null || index < 0 || index >= _buffer.Length)
                return;
            _buffer[index] = ColorFor(index, filled: true);
            Upload();
            ApplyTexts();
        }

        /// <summary>Світова точка пікселя — ціль для краплі з поля.</summary>
        public Vector3 PixelWorldPosition(int index)
        {
            var target = pixels != null ? (RectTransform)pixels.transform : canvas;
            if (target == null || _picture == null || index < 0)
                return transform.position;
            var rect = target.rect;
            var x = index % _picture.Width;
            var y = index / _picture.Width;
            var local = new Vector3(
                rect.xMin + rect.width * (x + 0.5f) / _picture.Width,
                rect.yMax - rect.height * (y + 0.5f) / _picture.Height,
                0f);
            return target.TransformPoint(local);
        }

        /// <summary>Спалах завершення (§8), після нього — <paramref name="then"/> (зазвичай Show наступної).</summary>
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

        // ── Текстура ──

        private void Bind(PixelPicture picture)
        {
            var changed = _picture != picture;
            _picture = picture;
            if (_texture == null || _texture.width != picture.Width || _texture.height != picture.Height)
            {
                if (_texture != null)
                    Destroy(_texture);
                _texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "Picture"
                };
                _buffer = new Color32[picture.Width * picture.Height];
                if (pixels != null)
                    pixels.texture = _texture;
            }
            if (changed)
                FitAspect();
            if (frame != null)
                frame.Bind(picture.Rarity);
            else if (plateStroke != null && design != null)
                plateStroke.color = design.RarityColor(picture.Rarity);
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

        private Color32 ColorFor(int index, bool filled)
        {
            if (_picture == null || design == null)
                return new Color32(0, 0, 0, 0);
            // Рядок 0 картинки — верхній; рядок 0 текстури — нижній.
            var x = index % _picture.Width;
            var y = index / _picture.Width;
            var color = _picture[x, y];
            if (color == MasterPalette.Empty)
                return new Color32(0, 0, 0, 0);
            if (color == _picture.Outline)
                return MasterPalette.ColorOf(color).ToColor();
            if (filled)
                return MasterPalette.ColorOf(color).ToColor();
            return design.PictureUnfilledTint;
        }

        private void Upload()
        {
            if (_texture == null || _picture == null)
                return;
            // Текстура читається знизу вгору — перевертаємо рядки.
            var w = _picture.Width;
            var h = _picture.Height;
            var flipped = new Color32[_buffer.Length];
            for (var y = 0; y < h; y++)
                System.Array.Copy(_buffer, y * w, flipped, (h - 1 - y) * w, w);
            _texture.SetPixels32(flipped);
            _texture.Apply(false, false);
        }

        private void ApplyTexts()
        {
            if (_progress == null)
                return;
            if (title != null)
            {
                title.text = _progress.Picture.Name;
                if (design != null)
                    title.color = design.RarityColor(_progress.Picture.Rarity);
            }
            if (caption != null)
                caption.text = _progress.IsComplete ? "ГОТОВО" : $"{_progress.FilledCount} / {_progress.Total}";
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
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
