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
    /// </summary>
    public sealed class PlanetCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private PlanetView[] slots = Array.Empty<PlanetView>();
        [SerializeField] private RectTransform? nextGalaxyPreview;

        /// <summary>Скільки індексів пробігає палець на всю ширину екрана.</summary>
        private const float DragReferenceWidth = 1080f;

        private GalaxyProgress? _galaxy;
        private bool _readOnly;

        private float _scroll;
        private float _target;
        private float _snapVelocity;
        private float _dragVelocity;
        private float _lastDragDelta;
        private bool _dragging;
        private int _windowCenter = int.MinValue;
        private int _focus = -1;

        // Рендерери кешуємо один раз: GetComponentsInChildren щокадру алокував би
        // масив на кожен слот — рівно те, чого в каруселі бути не повинно.
        private CanvasRenderer[][] _slotRenderers = Array.Empty<CanvasRenderer[]>();
        private CanvasRenderer[] _previewRenderers = Array.Empty<CanvasRenderer>();

        /// <summary>Фокус змінився: екран оновлює назву, підпис, кнопку й пагінацію.</summary>
        public event Action<int>? FocusChanged;

        /// <summary>Індекс планети в центрі. Дорівнює кількості планет, якщо це прев'ю наступної галактики.</summary>
        public int Focus => _focus;

        /// <summary>Чи стоїть у центрі прев'ю наступної галактики.</summary>
        public bool OnPreview => _galaxy != null && _focus >= _galaxy.Planets.Count;

#if UNITY_EDITOR
        /// <summary>Стенд і тести: чи видно прев'ю наступної галактики (у режимі перегляду його немає).</summary>
        public bool PreviewShown => nextGalaxyPreview != null && nextGalaxyPreview.gameObject.activeSelf;
#endif

        private void Start() => CacheRenderers();

        /// <summary>Знімає рендерери разом із неактивними: слоти вмикають і вимикають
        /// шари (кільце, замок, серпанок), і після кешування нові не з'являються.</summary>
        private void CacheRenderers()
        {
            if (_slotRenderers.Length == slots.Length && slots.Length > 0)
                return;

            _slotRenderers = new CanvasRenderer[slots.Length][];
            for (var i = 0; i < slots.Length; i++)
                _slotRenderers[i] = slots[i] != null
                    ? slots[i].GetComponentsInChildren<CanvasRenderer>(true)
                    : Array.Empty<CanvasRenderer>();

            _previewRenderers = nextGalaxyPreview != null
                ? nextGalaxyPreview.GetComponentsInChildren<CanvasRenderer>(true)
                : Array.Empty<CanvasRenderer>();
        }

        public void Bind(GalaxyProgress galaxy, bool readOnly, int startIndex)
        {
            CacheRenderers();
            _galaxy = galaxy;
            _readOnly = readOnly;
            _scroll = Mathf.Clamp(startIndex, 0, MaxIndex);
            _target = _scroll;
            _dragVelocity = 0f;
            _snapVelocity = 0f;
            _windowCenter = int.MinValue;
            _focus = -1;
            Rebuild();
        }

        /// <summary>
        /// Останній допустимий індекс. Прев'ю наступної галактики — це зайвий
        /// «віртуальний» індекс у кінці, і його немає в режимі перегляду чужої
        /// галактики: там нема чого відкривати.
        /// </summary>
        private int MaxIndex
        {
            get
            {
                if (_galaxy == null)
                    return 0;
                return _readOnly ? _galaxy.Planets.Count - 1 : _galaxy.Planets.Count;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragVelocity = 0f;
            _lastDragDelta = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_galaxy == null || design == null)
                return;

            var perIndex = design.PlanetCarouselStep;
            if (perIndex <= 0.001f)
                return;

            // Переводимо рух пальця в екранних пікселях у reference-одиниці канваса.
            var scale = DragReferenceWidth / Mathf.Max(1f, Screen.width);
            var delta = -eventData.delta.x * scale / perIndex;

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
            var halfViewport = viewport != null ? viewport.rect.width * 0.5f : DragReferenceWidth * 0.5f;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

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
                // усе одно виконувався б для кожного пікселя квада.
                var halfWidth = design.PlanetSizeFinale * scale * 0.5f;
                var visible = Mathf.Abs(x) - halfWidth <= halfViewport;
                if (slot.gameObject.activeSelf != visible)
                    slot.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                var t = slot.transform;
                t.localPosition = new Vector3(x, 0f, 0f);
                t.localScale = new Vector3(scale, scale, 1f);
                SetAlpha(_slotRenderers[i], alpha);
            }

            LayoutPreview(step, halfViewport);

            var focus = Mathf.Clamp(center, 0, MaxIndex);
            if (focus == _focus)
                return;
            _focus = focus;
            FocusChanged?.Invoke(focus);
        }

        private void LayoutPreview(float step, float halfViewport)
        {
            if (nextGalaxyPreview == null)
                return;

            // Прев'ю живе на «зайвому» індексі за останньою планетою.
            var show = !_readOnly && _galaxy != null;
            if (!show)
            {
                if (nextGalaxyPreview.gameObject.activeSelf)
                    nextGalaxyPreview.gameObject.SetActive(false);
                return;
            }

            var index = _galaxy!.Planets.Count;
            var d = index - _scroll;
            var x = d * step;
            var scale = Mathf.Max(design.PlanetCarouselMinScale,
                1f - Mathf.Abs(d) * design.PlanetCarouselScaleFalloff);
            var alpha = Mathf.Max(design.PlanetCarouselMinAlpha,
                1f - Mathf.Abs(d) * design.PlanetCarouselAlphaFalloff);

            var visible = Mathf.Abs(x) - design.PlanetSizeFinale * scale * 0.5f <= halfViewport;
            if (nextGalaxyPreview.gameObject.activeSelf != visible)
                nextGalaxyPreview.gameObject.SetActive(visible);
            if (!visible)
                return;

            nextGalaxyPreview.localPosition = new Vector3(x, 0f, 0f);
            nextGalaxyPreview.localScale = new Vector3(scale, scale, 1f);
            SetAlpha(_previewRenderers, alpha);
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
                slot.Bind(_galaxy.Planets[index], index == center, _readOnly);
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
