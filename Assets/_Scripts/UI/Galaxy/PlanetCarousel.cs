using System;
using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Карусель планет: гортання пальцем із інерцією та прилипанням до центру.
    ///
    /// Слоти беруться з пулу і живуть увесь час роботи екрана — під час свайпу не
    /// створюється й не знищується нічого. Вікно — фокус ±2: далі планета вже
    /// повністю за межами кадру, і слот вимикається, щоб не платити за її шейдер.
    ///
    /// Щокадру рухаються лише localPosition, localScale і альфа CanvasRenderer.
    /// Перепризначення слота на іншу планету (єдине місце, що чіпає графіку)
    /// стається кілька разів за свайп, а не щокадру.
    ///
    /// Розмір планети під екран (U1Glaze S03/S12) — масштаб доріжки <see cref="SetFit"/>, який екран ставить на зміну
    /// розкладки, а не щокадру; рух пальця переводиться в одиниці доріжки, тож свайп однаковий на будь-якій ширині.
    /// Мітки комірок і бейдж (<see cref="PlanetView.FocusOnly"/>) гаснуть, поки планета їде з центру.
    /// </summary>
    public sealed class PlanetCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesignSystem design;
        [Tooltip("Межа відсікання — увесь екран (не карусель у стовпчику з полями: планети малюються й на полях).")]
        [SerializeField] private RectTransform viewport;
        [Tooltip("Доріжка зі слотами: її localScale — розмір планети під висоту каруселі.")]
        [SerializeField] private RectTransform track;
        [SerializeField] private PlanetView[] slots = Array.Empty<PlanetView>();

        private GalaxyProgress? _galaxy;
        private bool _readOnly;
        private Func<string, Color>? _pictureColor;

        private float _scroll;
        private float _target;
        private float _snapVelocity;
        private float _dragVelocity;
        private float _lastDragDelta;
        private bool _dragging;
        private int _windowCenter = int.MinValue;
        private int _focus = -1;
        private float _fit = 1f;

        // Рендерери кешуємо один раз: GetComponentsInChildren щокадру алокував би
        // масив на кожен слот — рівно те, чого в каруселі бути не повинно.
        private CanvasRenderer[][] _slotRenderers = Array.Empty<CanvasRenderer[]>();
        private CanvasRenderer[][] _focusRenderers = Array.Empty<CanvasRenderer[]>();

        /// <summary>Фокус змінився: екран оновлює назву, підпис, кнопку й пагінацію.</summary>
        public event Action<int>? FocusChanged;

        /// <summary>Індекс планети в центрі.</summary>
        public int Focus => _focus;

        /// <summary>Масштаб доріжки (1 — планета розміру токена planetSize).</summary>
        public float Fit => _fit;

#if UNITY_EDITOR
        /// <summary>Тести: в'юха планети з цим індексом, якщо вона зараз у вікні.</summary>
        public PlanetView? PreviewViewOf(int index)
        {
            foreach (var slot in slots)
                if (slot != null && slot.gameObject.activeSelf && slot.Planet != null && _galaxy != null &&
                    index >= 0 && index < _galaxy.Planets.Count && ReferenceEquals(slot.Planet, _galaxy.Planets[index]))
                    return slot;
            return null;
        }
#endif

        private void Start() => CacheRenderers();

        /// <summary>Знімає рендерери разом із неактивними: слоти вмикають і вимикають
        /// шари (кільце, замок, серпанок, мітки), і після кешування нові не з'являються.</summary>
        private void CacheRenderers()
        {
            if (_slotRenderers.Length == slots.Length && slots.Length > 0)
                return;

            _slotRenderers = new CanvasRenderer[slots.Length][];
            _focusRenderers = new CanvasRenderer[slots.Length][];
            for (var i = 0; i < slots.Length; i++)
            {
                _slotRenderers[i] = slots[i] != null
                    ? slots[i].GetComponentsInChildren<CanvasRenderer>(true)
                    : Array.Empty<CanvasRenderer>();
                var focusOnly = slots[i] != null ? slots[i].FocusOnly : null;
                _focusRenderers[i] = focusOnly != null
                    ? focusOnly.GetComponentsInChildren<CanvasRenderer>(true)
                    : Array.Empty<CanvasRenderer>();
            }
        }

        /// <param name="pictureColor">Колір мітки заповненої комірки за назвою картинки.</param>
        public void Bind(GalaxyProgress galaxy, bool readOnly, int startIndex, Func<string, Color>? pictureColor = null)
        {
            CacheRenderers();
            _galaxy = galaxy;
            _readOnly = readOnly;
            _pictureColor = pictureColor;
            _scroll = Mathf.Clamp(startIndex, 0, MaxIndex);
            _target = _scroll;
            _dragVelocity = 0f;
            _snapVelocity = 0f;
            _windowCenter = int.MinValue;
            _focus = -1;
            Rebuild();
        }

        /// <summary>
        /// Розмір планети під екран: масштаб доріжки. Ставить екран на зміну розкладки (не щокадру) — localScale,
        /// а не sizeDelta, тож графіка не перебудовується.
        /// </summary>
        public void SetFit(float scale)
        {
            _fit = Mathf.Max(0.05f, scale);
            if (track != null)
                track.localScale = new Vector3(_fit, _fit, 1f);
            if (_galaxy != null)
                Layout();
        }

        /// <summary>Останній допустимий індекс — остання планета. Прев'ю наступної галактики більше немає (S03_05 — окремий стан).</summary>
        private int MaxIndex => _galaxy == null ? 0 : Mathf.Max(0, _galaxy.Planets.Count - 1);

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragVelocity = 0f;
            _lastDragDelta = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_galaxy == null || design == null || track == null)
                return;

            var perIndex = design.PlanetCarouselStep;
            if (perIndex <= 0.001f)
                return;

            // Рух пальця — у одиниці доріжки: так свайп однаковий і при match 1 (iPad, канвас ширший за 1080),
            // і при будь-якому масштабі планети.
            var camera = eventData.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, camera, out var now) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position - eventData.delta, camera, out var before))
                return;
            var delta = -(now.x - before.x) / perIndex;

            _scroll += delta;
            _lastDragDelta = delta;

            // За край не пускаємо взагалі: гумовий хід тут лише плутав би —
            // порожнечі за останньою планетою немає.
            _scroll = Mathf.Clamp(_scroll, 0f, MaxIndex);
            Layout();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (design == null)
                return;

            // Швидкість беремо з останнього кадру перетягування, а не з
            // PointerEventData: там вона в пікселях екрана й іншого масштабу.
            _dragVelocity = Time.deltaTime > 0f ? _lastDragDelta / Time.deltaTime : 0f;

            // Змах перегортає рівно на одну планету. Пускати кілька за раз —
            // означає загубити місце, де ти був.
            var flick = 0;
            if (Mathf.Abs(_dragVelocity) > design.PlanetCarouselFlickVelocity)
                flick = _dragVelocity > 0f ? 1 : -1;

            _target = Mathf.Clamp(Mathf.RoundToInt(_scroll) + flick, 0, MaxIndex);
            _snapVelocity = _dragVelocity;
        }

        private void LateUpdate()
        {
            if (_galaxy == null || design == null || _dragging)
                return;

            if (Mathf.Abs(_scroll - _target) < 0.0005f && Mathf.Abs(_snapVelocity) < 0.0005f)
                return;

            _scroll = Mathf.SmoothDamp(_scroll, _target, ref _snapVelocity,
                design.PlanetCarouselSnapTime, Mathf.Infinity, Time.deltaTime);
            Layout();
        }

        /// <summary>Перепризначає слоти, коли вікно з'їхало, і розкладає їх.</summary>
        private void Rebuild()
        {
            _windowCenter = int.MinValue;
            Layout();
        }

        private void Layout()
        {
            if (_galaxy == null || design == null || slots.Length == 0)
                return;

            var center = Mathf.RoundToInt(_scroll);
            if (center != _windowCenter)
            {
                _windowCenter = center;
                AssignSlots(center);
            }

            var step = design.PlanetCarouselStep;
            // Межа кадру — у одиницях доріжки: доріжка масштабована під екран.
            var halfViewport = (viewport != null ? viewport.rect.width * 0.5f : 540f) / _fit;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                // Індекс поза галактикою — слот вимкнений (і не «привид» попередньої планети).
                var index = _windowCenter - HalfWindow + i;
                if (index < 0 || index > MaxIndex)
                {
                    if (slot.gameObject.activeSelf)
                        slot.gameObject.SetActive(false);
                    continue;
                }

                var d = index - _scroll;
                var x = d * step;
                var scale = Mathf.Max(design.PlanetCarouselMinScale,
                    1f - Mathf.Abs(d) * design.PlanetCarouselScaleFalloff);
                var alpha = Mathf.Max(design.PlanetCarouselMinAlpha,
                    1f - Mathf.Abs(d) * design.PlanetCarouselAlphaFalloff);

                // Планета цілком за краєм — вимикаємо: за межами кадру її шейдер
                // усе одно виконувався б для кожного пікселя квада. Межа — найширша частина (кільце, серпанок),
                // а не сама куля: інакше хвіст кільця й гало зникав би на екрані посеред свайпу.
                // Два аргументи, не params: Mathf.Max(a, b, c) виділяв би масив на кожен кадр свайпу.
                var widest = Mathf.Max(Mathf.Max(design.PlanetRingWidthScale, design.PlanetAtmosphereScale), 1f);
                var halfWidth = design.PlanetSizeFinale * widest * scale * 0.5f;
                var visible = Mathf.Abs(x) - halfWidth <= halfViewport;
                if (slot.gameObject.activeSelf != visible)
                    slot.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                var t = slot.transform;
                t.localPosition = new Vector3(x, 0f, 0f);
                t.localScale = new Vector3(scale, scale, 1f);
                SetAlpha(_slotRenderers[i], alpha);
                // Мітки комірок і бейдж — лише в центрі: гаснуть, поки планета їде вбік.
                SetAlpha(_focusRenderers[i], alpha * Mathf.Clamp01(1f - Mathf.Abs(d) * design.GalaxyFocusFade));
            }

            var focus = Mathf.Clamp(center, 0, MaxIndex);
            if (focus == _focus)
                return;
            _focus = focus;
            FocusChanged?.Invoke(focus);
        }

        /// <summary>Скільки слотів по кожен бік від центрального.</summary>
        private int HalfWindow => slots.Length / 2;

        private void AssignSlots(int center)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                var index = center - HalfWindow + i;
                if (_galaxy == null || index < 0 || index >= _galaxy.Planets.Count)
                {
                    slot.Release();
                    continue;
                }

                slot.gameObject.SetActive(true);
                slot.Bind(_galaxy.Planets[index], index == center, _readOnly, _pictureColor);
            }
        }

        /// <summary>
        /// Прозорість гілки — через CanvasRenderer кожної графіки: щокадрова
        /// зміна Image.color просила б перебудову й ламала прохід канваса.
        /// </summary>
        private static void SetAlpha(CanvasRenderer[] renderers, float alpha)
        {
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].SetAlpha(alpha);
        }
    }
}
