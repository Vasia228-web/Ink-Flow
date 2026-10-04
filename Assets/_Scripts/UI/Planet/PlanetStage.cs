using System;
using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Планета на екрані вітрини: обертається пальцем, тримає слоти на своїх місцях на кулі
    /// й повідомляє, у який слот влучив дотик.
    ///
    /// Слоти — плоскі маркери, спроєктовані на екран, а не геометрія на сфері. Так їх можна
    /// робити тапабельними, лишаючись у межах UGUI. Проєкція щокадру чіпає лише
    /// localPosition, localScale і CanvasRenderer.
    ///
    /// Влучання рахує сам stage, а не Button на кожному слоті: на кулі слоти перекриваються,
    /// і при попаданні треба вибрати той, що ближчий до глядача. Знак обертання збігається з
    /// шейдером планети: там точку вибірки крутять на +_Rotation, тож поверхня на екрані їде
    /// на −_Rotation — і слот, прив'язаний до поверхні, теж.
    /// </summary>
    public sealed class PlanetStage : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image disc;
        [SerializeField] private SlotMarker[] markers = Array.Empty<SlotMarker>();

        private static readonly int RotationId = Shader.PropertyToID("_Rotation");

        private const float DragReferenceWidth = 1080f;

        /// <summary>Слот зникає за лімб не рівно на 0: у макеті поріг −0.18.</summary>
        private const float VisibilityThreshold = -0.18f;

        private PlanetSurface? _surface;
        private RectTransform? _rect;
        private Material? _discMaterial;

        private float _rotation;      // градуси
        private float _velocity;      // градусів за секунду
        private bool _dragging;
        private float _lastDelta;
        private bool _rotatedByPlayer;

        /// <summary>Гравець тапнув по слоту.</summary>
        public event Action<PlanetSlot>? SlotTapped;

        /// <summary>Гравець уперше крутнув планету — підказку можна прибрати.</summary>
        public event Action? RotatedByPlayer;

        /// <summary>Автообертання після того, як планета ожила.</summary>
        public bool AutoSpin { get; set; }

        /// <summary>Маркери слотів — екрану, щоб покласти в них картинки.</summary>
        public SlotMarker[] Markers => markers;

        // Ліниво, а НЕ в Awake: у Edit Mode Awake не виконується взагалі, а
        // Apply() приходить із OnValidate — і поле лишалось би null.
        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>Розмір слота на цій планеті (одиниці канваса): радіус у px макета — з токенів за кількістю слотів.</summary>
        public float SlotBaseSize(PlanetSurface surface)
        {
            var radius = Rect.rect.width * 0.5f;
            return radius * design.SlotRadiusFor(surface.Slots.Count) * design.PaintZoneSizeFactor;
        }

        public void Bind(PlanetSurface surface, Material discMaterial)
        {
            _surface = surface;
            _discMaterial = discMaterial;
            _rotation = 0f;
            _velocity = 0f;
            _rotatedByPlayer = false;

            var baseSize = SlotBaseSize(surface);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                if (i >= surface.Slots.Count)
                {
                    marker.Release();
                    continue;
                }

                marker.Bind(surface.Slots[i], baseSize);
            }

            Project();
        }

        /// <summary>Маркер конкретного слота — екрану він потрібен, щоб покласти картинку.</summary>
        public SlotMarker? MarkerFor(PlanetSlot slot)
        {
            for (var i = 0; i < markers.Length; i++)
                if (markers[i] != null && ReferenceEquals(markers[i].Slot, slot))
                    return markers[i];
            return null;
        }

        public void SetSelected(PlanetSlot? slot)
        {
            for (var i = 0; i < markers.Length; i++)
                if (markers[i] != null && markers[i].Slot != null)
                    markers[i].SetSelected(ReferenceEquals(markers[i].Slot, slot));
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _velocity = 0f;
            _lastDelta = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (design == null)
                return;

            // Палець рухається в пікселях екрана — переводимо в reference-одиниці,
            // інакше на планшеті планета крутилась би вдвічі повільніше.
            var scale = DragReferenceWidth / Mathf.Max(1f, Screen.width);
            var delta = -eventData.delta.x * scale * design.PaintRotationPerUnit;

            _rotation += delta;
            _lastDelta = delta;

            if (!_rotatedByPlayer && Mathf.Abs(delta) > 0.01f)
            {
                _rotatedByPlayer = true;
                RotatedByPlayer?.Invoke();
            }

            Project();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _velocity = Time.deltaTime > 0f ? _lastDelta / Time.deltaTime : 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Сюди EventSystem доводить лише те, що не стало перетягуванням,
            // тож окремий поріг «тап чи не тап» тут не потрібен.
            if (_surface == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    Rect, eventData.position, eventData.pressEventCamera, out var local))
                return;

            var hit = Pick(local);
            if (hit != null)
                SlotTapped?.Invoke(hit);
        }

        /// <summary>Перепроєктувати після зміни вмісту слотів.</summary>
        public void Refresh() => Project();

        /// <summary>
        /// Шукає слот під точкою. З кількох накладених бере найближчий до глядача:
        /// на кулі перекриття — норма, і тапати треба по передньому.
        /// </summary>
        public PlanetSlot? Pick(Vector2 local)
        {
            if (_surface == null || design == null)
                return null;

            var radius = Rect.rect.width * 0.5f;
            PlanetSlot? best = null;
            var bestDepth = float.NegativeInfinity;

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.Slot == null)
                    continue;

                var slot = marker.Slot;
                Projection(slot.Longitude, slot.Latitude, radius, out var position, out var scale, out var depth, out var visible);
                if (!visible || depth <= bestDepth)
                    continue;

                var half = marker.BaseSize * scale * 0.5f;
                var offset = local - position;
                if (offset.sqrMagnitude > half * half)
                    continue;

                best = slot;
                bestDepth = depth;
            }

            return best;
        }

        private void LateUpdate()
        {
            if (_surface == null || design == null)
                return;

            var moved = false;

            if (AutoSpin)
            {
                _rotation += design.PaintAutoSpinSpeed * Time.deltaTime;
                moved = true;
            }
            else if (!_dragging && Mathf.Abs(_velocity) > 0.5f)
            {
                // Легкий вибіг після відпускання: планета не завмирає різко.
                _rotation += _velocity * Time.deltaTime;
                _velocity *= Mathf.Exp(-design.PaintRotationDamping * Time.deltaTime);
                moved = true;
            }

            if (moved)
                Project();
        }

        /// <summary>Ракурс точки на кулі: положення, стиснення, глибина й видимість.</summary>
        public void Projection(float longitude, float latitude, float radius,
            out Vector2 position, out float scale, out float depth, out bool visible)
        {
            var lat = latitude * Mathf.Deg2Rad;
            var lon = (longitude - _rotation) * Mathf.Deg2Rad;

            var cosLat = Mathf.Cos(lat);
            var x = cosLat * Mathf.Sin(lon);
            var y = Mathf.Sin(lat);
            depth = cosLat * Mathf.Cos(lon);

            // 0.94 — слоти не доходять до самого лімба, інакше вони «злизуються»
            // з краю кулі й виглядають приклеєними ззовні.
            var k = design.PaintZoneInset;
            position = new Vector2(radius * x * k, radius * y * k);
            scale = 0.66f + 0.44f * Mathf.Max(depth, -0.1f);
            visible = depth > VisibilityThreshold;
        }

        private void Project()
        {
            if (_surface == null || design == null)
                return;

            if (_discMaterial != null)
                _discMaterial.SetFloat(RotationId, _rotation * Mathf.Deg2Rad);

            var radius = Rect.rect.width * 0.5f;
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.Slot == null)
                    continue;

                Projection(marker.Slot.Longitude, marker.Slot.Latitude, radius,
                    out var position, out var scale, out var depth, out var visible);

                // Замість вимикання об'єкта гасимо альфу: SetActive щокадру
                // смикав би графіку на кожному перетині лімба.
                var alpha = !visible ? 0f
                    : depth > 0.04f ? 1f
                    : Mathf.Max(0f, (depth + 0.18f) / 0.22f);

                marker.Project(position, scale, alpha);
            }
        }
    }
}
