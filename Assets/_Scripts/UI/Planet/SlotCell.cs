using InkFlow.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна комірка планети (U1Glaze SPEC «Комірки на планеті»): порожня — лунка <c>slot_empty</c> з бірюзовим «+»,
    /// заповнена — панель <c>slot_frame_base</c>, картинка з атласу <see cref="SlotAtlas"/> і рамка
    /// <c>slot_frame_stroke</c> кольору рідкості, вибрана — бірюзове кільце <c>slot_selected</c> і масштаб.
    ///
    /// Форма — два вкладені RectTransform (SPEC): зовнішній (цей об'єкт) — позиція, поворот t1 і масштаб (sx, sy),
    /// внутрішній — поворот t2; так квадрат на кулі стискається й нахиляється (<see cref="GlobeSlots.Svd2"/>).
    /// Щокадру — лише localPosition / localRotation / localScale, прозорість через CanvasGroup і затінення біля
    /// краю через CanvasRenderer: графіка не перебудовується. Спрайти міняються лише на подію.
    /// </summary>
    public sealed class SlotCell : MonoBehaviour
    {
        /// <summary>
        /// Спрайти комірок U1Glaze — 72 px макета, лунка чи картинка в них — 64 (sprites.json). Сторона комірки в
        /// розкладці — це видима лунка, тож бокс спрайта — сторона × 72/64.
        /// </summary>
        public const float BoxPerSide = 72f / 64f;

        [SerializeField] private RectTransform inner;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image empty;
        [SerializeField] private Image frameBase;
        [SerializeField] private RawImage pixels;
        [SerializeField] private Image stroke;
        [SerializeField] private Image selected;

        [Tooltip("Порожня лунка своєї планети (з «+») і чужої (без «+», S09_02/03) — ставить збирач.")]
        [SerializeField] private Sprite? emptyOwn;
        [SerializeField] private Sprite? emptyReadOnly;

        private CanvasRenderer[] _renderers = System.Array.Empty<CanvasRenderer>();
        private float _shade = -1f;
        private bool _filled;
        private bool _showsPixels;
        private bool _isSelected;
        private float _selectedScale = 1f;

        /// <summary>Слот поверхні, який показує комірка (null — комірка з пулу не зайнята).</summary>
        public PlanetSlot? Slot { get; private set; }

        public bool IsFilled => _filled;
        public bool ShowsPixels => _showsPixels;
        public bool IsSelected => _isSelected;

        /// <summary>Сторона бокса в локальних одиницях (без масштабу) — від неї рахується масштаб у розкладці.</summary>
        public float BoxSide => ((RectTransform)transform).rect.width;

        private void CacheRenderers()
        {
            if (_renderers.Length > 0)
                return;
            var list = new System.Collections.Generic.List<CanvasRenderer>(4);
            foreach (var graphic in new Graphic?[] { empty, frameBase, pixels, stroke })
                if (graphic != null)
                    list.Add(graphic.canvasRenderer);
            _renderers = list.ToArray();
        }

        public void Bind(PlanetSlot slot, float selectedScale)
        {
            Slot = slot;
            _selectedScale = selectedScale;
            // Кільце slot_selected намальоване під уже збільшену комірку (124 / 72, «комірка ×1.1»): масштаб вибраної
            // множить і його — компенсуємо, щоб гало прилягало до рамки, а не стояло окремим обідком.
            if (selected != null)
                selected.rectTransform.localScale = Vector3.one / Mathf.Max(0.01f, selectedScale);
            _shade = -1f;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            SetSelected(false);
        }

        public void Release()
        {
            Slot = null;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        /// <param name="readOnly">Чужа планета (перегляд): темна лунка без «+», бо ставити туди нічого не можна.</param>
        public void ShowEmpty(bool readOnly = false)
        {
            var sprite = readOnly ? emptyReadOnly : emptyOwn;
            if (empty != null && sprite != null && empty.sprite != sprite)
                empty.sprite = sprite;
            _shade = -1f; // перебудова графіки скидає колір CanvasRenderer — наступна поза покладе затінення знову
            _filled = false;
            _showsPixels = false;
            Toggle(empty, true);
            Toggle(frameBase, false);
            Toggle(pixels, false);
            Toggle(stroke, false);
        }

        /// <summary>Картинка з атласу в рамці кольору рідкості.</summary>
        public void ShowPicture(Texture atlas, Rect uv, Color rarity)
        {
            _shade = -1f;
            _filled = true;
            _showsPixels = true;
            Toggle(empty, false);
            Toggle(frameBase, true);
            Toggle(stroke, true);
            Toggle(pixels, true);
            if (pixels != null)
            {
                pixels.texture = atlas;
                pixels.uvRect = uv;
            }
            if (stroke != null)
                stroke.color = rarity;
        }

        /// <summary>Слот зайнятий картинкою, якої бібліотека не знає: рамка без пікселів — зайнятий і видимий.</summary>
        public void ShowUnknown(Color neutral)
        {
            _shade = -1f;
            _filled = true;
            _showsPixels = false;
            Toggle(empty, false);
            Toggle(frameBase, true);
            Toggle(stroke, true);
            Toggle(pixels, false);
            if (stroke != null)
                stroke.color = neutral;
        }

        public void SetSelected(bool on)
        {
            _isSelected = on;
            Toggle(selected, on);
        }

        /// <summary>
        /// Поза комірки: центр, форма (поворот t1 · масштаб · поворот t2 — у частках бокса), прозорість і затінення.
        /// Щокадрово — лише трансформи, альфа групи й колір CanvasRenderer (затінення пишеться, коли змінилось).
        /// </summary>
        public void Pose(Vector2 position, float t1, float sx, float sy, float t2, float alpha, float shade)
        {
            var k = _isSelected ? _selectedScale : 1f;
            transform.localPosition = new Vector3(position.x, position.y, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, t1);
            transform.localScale = new Vector3(sx * k, sy * k, 1f);
            if (inner != null)
                inner.localRotation = Quaternion.Euler(0f, 0f, t2);
            if (group != null)
                group.alpha = alpha;
            if (Mathf.Abs(shade - _shade) < 0.004f)
                return;
            _shade = shade;
            CacheRenderers();
            var tint = new Color(1f - shade, 1f - shade, 1f - shade, 1f);
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].SetColor(tint);
        }

        /// <summary>
        /// Чи влучає точка (у координатах батька комірки) у комірку — через обернену форму, з запасом
        /// <paramref name="padding"/> (частка бокса) з кожного боку: тапнути можна й у проміжок.
        /// </summary>
        public bool Contains(Vector2 point, float padding) => Distance(point) <= 1f + padding * 2f;

        /// <summary>
        /// Відстань від центру комірки до точки (координати батька) у частках пів бокса за найбільшою віссю квадрата:
        /// 1 — на краю бокса. Для вибору найближчої, коли зони тапу сусідів перекриваються.
        /// </summary>
        public float Distance(Vector2 point)
        {
            var local = inner != null
                ? inner.InverseTransformPoint(transform.parent.TransformPoint(point))
                : transform.InverseTransformPoint(transform.parent.TransformPoint(point));
            var half = BoxSide * 0.5f;
            return half > 0f ? Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) / half : float.MaxValue;
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }
    }
}
