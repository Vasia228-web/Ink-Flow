using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картинка над полем (документ §5): скляна плитка з силуетом, зони стосом в одному
    /// квадраті, назва з лічильником зон і підпис активної зони — «ФОНТАН · ЗЕЛЕНИЙ»:
    /// це і є ціль гравця для змішувача (§4: «зривай лінії потрібного кольору»).
    ///
    /// Стан приходить із <see cref="PictureProgress"/>; мазок (<see cref="PlayFill"/>)
    /// грає в момент, коли виплеск долітає до зони, а не коли модель уже все порахувала.
    /// </summary>
    public sealed class PictureView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private PictureArtCatalog art;
        [SerializeField] private RectTransform plate;
        [SerializeField] private Image plateFill;
        [SerializeField] private Image plateStroke;
        [SerializeField] private Image glow;
        [SerializeField] private RectTransform canvas;
        [SerializeField] private PictureZoneView[] zones = System.Array.Empty<PictureZoneView>();
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text caption;

        private PictureProgress? _progress;
        private PictureArtCatalog.PictureArt? _art;
        private Coroutine? _flash;

        public void Apply()
        {
            if (design == null)
                return;
            if (plateFill != null) plateFill.color = design.PicturePlateFill;
            if (plateStroke != null) plateStroke.color = design.PicturePlateStroke;
            if (glow != null)
            {
                glow.color = design.AccentGold;
                glow.canvasRenderer.SetAlpha(0f);
            }
            Font(title, design.FontSizePictureName, design.TextPrimary, design.LetterSpacingWide);
            Font(caption, design.FontSizePictureCaption, design.TextMuted, design.LetterSpacingWide);
            if (plate != null) plate.localScale = Vector3.one;
        }

        /// <summary>Показує стан картинки як є. Спрайти — з каталогу за id; без них лишається назва.</summary>
        public void Show(PictureProgress progress)
        {
            _progress = progress;
            _art = art != null ? art.Find(progress.Def.Id) : null;

            var active = progress.ActiveZone;
            for (var i = 0; i < zones.Length; i++)
            {
                var view = zones[i];
                if (view == null)
                    continue;
                if (_art == null || i >= progress.ZoneCount || i >= _art.zones.Length || _art.zones[i] == null)
                {
                    view.Release();
                    continue;
                }
                view.Bind(_art.zones[i], Origin(i), Extent(i), progress.Def.Zones[i].Hue,
                    progress.Fraction(i), i == active);
            }

            ApplyTexts();
        }

        /// <summary>Мазок у зону: частка після виплеску, фронт — з точки, куди він упав.</summary>
        public void PlayFill(int zone, float fraction, Vector2 originUv)
        {
            if (zone < 0 || zone >= zones.Length || zones[zone] == null || !zones[zone].IsBound || design == null)
                return;
            zones[zone].PlayFill(fraction, originUv, design.ZoneFillDuration);
            RefreshSelection();
            ApplyTexts();
        }

        /// <summary>Світова точка зони — ціль для виплеску.</summary>
        public Vector3 ZoneWorldPosition(int zone)
        {
            if (canvas == null)
                return transform.position;
            var uv = zone >= 0 && _art != null && zone < _art.origins.Length ? _art.origins[zone] : new Vector2(0.5f, 0.5f);
            var rect = canvas.rect;
            var local = new Vector3(rect.xMin + rect.width * uv.x, rect.yMin + rect.height * uv.y, 0f);
            return canvas.TransformPoint(local);
        }

        /// <summary>UV точки, куди прилетів виплеск, — щоб мазок ішов саме звідти.</summary>
        public Vector2 UvOf(Vector3 worldPosition)
        {
            if (canvas == null)
                return new Vector2(0.5f, 0.5f);
            var local = canvas.InverseTransformPoint(worldPosition);
            var rect = canvas.rect;
            return new Vector2(
                Mathf.Clamp01((local.x - rect.xMin) / Mathf.Max(1f, rect.width)),
                Mathf.Clamp01((local.y - rect.yMin) / Mathf.Max(1f, rect.height)));
        }

        /// <summary>Спалах завершення (§5), після нього — <paramref name="then"/> (зазвичай Show наступної).</summary>
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
            if (glow != null) glow.canvasRenderer.SetAlpha(0f);
        }

        private IEnumerator CompletedRoutine(System.Action? then)
        {
            var duration = design.PictureCompleteDuration;
            var pop = design.PictureCompletePop;
            if (caption != null)
                caption.text = "ГОТОВО";
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                var wave = Mathf.Sin(k * Mathf.PI);
                if (plate != null)
                    plate.localScale = Vector3.one * (1f + pop * design.CurveBackOut.Evaluate(Mathf.Min(1f, k * 2f)) * (1f - Mathf.Max(0f, k - 0.5f) * 2f));
                if (glow != null)
                    glow.canvasRenderer.SetAlpha(design.PictureGlowAlpha * wave);
                yield return null;
            }

            if (plate != null) plate.localScale = Vector3.one;
            if (glow != null) glow.canvasRenderer.SetAlpha(0f);
            _flash = null;
            then?.Invoke();
        }

        private void RefreshSelection()
        {
            if (_progress == null)
                return;
            var active = _progress.ActiveZone;
            for (var i = 0; i < zones.Length && i < _progress.ZoneCount; i++)
                zones[i]?.SetSelected(i == active);
        }

        private void ApplyTexts()
        {
            if (_progress == null)
                return;
            var done = 0;
            for (var i = 0; i < _progress.ZoneCount; i++)
                if (_progress.IsZoneComplete(i))
                    done++;
            if (title != null)
                title.text = $"{_progress.Def.Name} · {done}/{_progress.ZoneCount}";

            if (caption == null)
                return;
            var active = _progress.ActiveZone;
            caption.text = active < 0
                ? "ГОТОВО"
                : $"{_progress.Def.Zones[active].Label.ToUpperInvariant()} · {HueNames.Of(_progress.Def.Zones[active].Hue)}";
        }

        private Vector2 Origin(int zone) =>
            _art != null && zone < _art.origins.Length ? _art.origins[zone] : new Vector2(0.5f, 0.5f);

        private float Extent(int zone) =>
            _art != null && zone < _art.extents.Length ? _art.extents[zone] : 0.75f;

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
