using System;
using System.Collections.Generic;
using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Планета на екрані фарбування: обертається пальцем, тримає зони на своїх
    /// місцях на кулі й повідомляє, у яку з них влучив дотик.
    ///
    /// Зони — це плоскі маркери, спроєктовані на екран, а не геометрія на сфері.
    /// Так їх можна робити тапабельними й анімувати заливкою, лишаючись у межах
    /// UGUI. Проєкція щокадру чіпає лише localPosition, localScale і CanvasRenderer.
    ///
    /// Влучання рахує сам stage, а не Button на кожній зоні: на кулі зони
    /// перекриваються, і при попаданні треба вибрати ту, що ближча до глядача.
    /// </summary>
    public sealed class PlanetStage : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image disc;
        [SerializeField] private RectTransform zoneRoot;
        [SerializeField] private ZoneMarker[] markers = Array.Empty<ZoneMarker>();
        [SerializeField] private PlacementMarker[] placements = Array.Empty<PlacementMarker>();

        private static readonly int RotationId = Shader.PropertyToID("_Rotation");

        private const float DragReferenceWidth = 1080f;

        /// <summary>Зона зникає за лімб не рівно на 0: у макеті поріг −0.18.</summary>
        private const float VisibilityThreshold = -0.18f;

        private PlanetSurface? _surface;
        private RectTransform? _rect;
        private Material? _discMaterial;

        private float _rotation;      // градуси
        private float _velocity;      // градусів за секунду
        private bool _dragging;
        private float _lastDelta;
        private bool _rotatedByPlayer;

        /// <summary>Гравець тапнув по зоні. Другий аргумент — точка дотику в частках
        /// прямокутника зони: з неї починається розтікання фарби.</summary>
        public event Action<PlanetZone, Vector2>? ZoneTapped;

        /// <summary>Тап по кулі — довгота й широта точки (§10: сюди стає картинка). Іде ПЕРЕД зоною.</summary>
        public event Action<float, float>? SurfaceTapped;

        /// <summary>Накладки картинок (§10) — стільки, скільки слотів; решта не показується.</summary>
        public IReadOnlyList<PlacementMarker> Placements => placements;

        /// <summary>Гравець уперше крутнув планету — підказку можна прибрати.</summary>
        public event Action? RotatedByPlayer;

        /// <summary>Автообертання після завершення планети.</summary>
        public bool AutoSpin { get; set; }

        // Ліниво, а НЕ в Awake: у Edit Mode Awake не виконується взагалі, а
        // Apply() приходить із OnValidate — і поле лишалось би null.
        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public void Bind(PlanetSurface surface, Material discMaterial)
        {
            _surface = surface;
            _discMaterial = discMaterial;
            _rotation = 0f;
            _velocity = 0f;
            _rotatedByPlayer = false;

            var radius = Rect.rect.width * 0.5f;
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                if (i >= surface.Zones.Count)
                {
                    marker.Release();
                    continue;
                }

                var zone = surface.Zones[i];
                marker.gameObject.SetActive(true);
                marker.Bind(zone, radius * zone.Radius * design.PaintZoneSizeFactor);
            }

            Project();
        }

        /// <summary>Маркер конкретної зони — екрану він потрібен, щоб запустити заливку.</summary>
        public ZoneMarker? MarkerFor(PlanetZone zone)
        {
            for (var i = 0; i < markers.Length; i++)
                if (markers[i] != null && ReferenceEquals(markers[i].Zone, zone))
                    return markers[i];
            return null;
        }

        public void SetSelected(PlanetZone? zone)
        {
            for (var i = 0; i < markers.Length; i++)
                if (markers[i] != null && markers[i].Zone != null)
                    markers[i].SetSelected(ReferenceEquals(markers[i].Zone, zone));
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

            if (TryPickSurface(local, out var longitude, out var latitude))
                SurfaceTapped?.Invoke(longitude, latitude);

            var hit = Pick(local, out var originUv);
            if (hit != null)
                ZoneTapped?.Invoke(hit, originUv);
        }

        /// <summary>
        /// Обернена проєкція: точка на диску → довгота й широта на кулі з урахуванням
        /// поточного обертання. Поза кулею — false.
        /// </summary>
        public bool TryPickSurface(Vector2 local, out float longitude, out float latitude)
        {
            longitude = 0f;
            latitude = 0f;
            if (design == null)
                return false;
            var radius = Rect.rect.width * 0.5f * design.PaintZoneInset;
            if (radius <= 0f)
                return false;
            var nx = local.x / radius;
            var ny = local.y / radius;
            var sq = nx * nx + ny * ny;
            if (sq > 1f)
                return false;
            var depth = Mathf.Sqrt(1f - sq);
            latitude = Mathf.Asin(Mathf.Clamp(ny, -1f, 1f)) * Mathf.Rad2Deg;
            longitude = Mathf.Atan2(nx, depth) * Mathf.Rad2Deg + _rotation;
            return true;
        }

        /// <summary>Перепроєктувати накладки після зміни їхнього набору.</summary>
        public void RefreshPlacements() => Project();

        /// <summary>
        /// Шукає зону під точкою. З кількох накладених бере найближчу до глядача:
        /// на кулі перекриття — норма, і тапати треба по передній.
        /// </summary>
        private PlanetZone? Pick(Vector2 local, out Vector2 originUv)
        {
            originUv = new Vector2(0.5f, 0.5f);
            if (_surface == null || design == null)
                return null;

            var radius = Rect.rect.width * 0.5f;
            PlanetZone? best = null;
            var bestDepth = float.NegativeInfinity;

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.Zone == null)
                    continue;

                var zone = marker.Zone;
                Projection(zone, radius, out var position, out var scale, out var depth, out var visible);
                if (!visible || depth <= bestDepth)
                    continue;

                var half = marker.BaseSize * scale * 0.5f;
                var offset = local - position;
                if (offset.sqrMagnitude > half * half)
                    continue;

                best = zone;
                bestDepth = depth;
                // Точка дотику в частках прямокутника зони — звідси піде заливка.
                originUv = new Vector2(offset.x / (half * 2f) + 0.5f, offset.y / (half * 2f) + 0.5f);
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

        /// <summary>Рахує ракурс однієї зони: положення, стиснення й видимість.</summary>
        private void Projection(PlanetZone zone, float radius,
            out Vector2 position, out float scale, out float depth, out bool visible) =>
            Projection(zone.Longitude, zone.Latitude, radius, out position, out scale, out depth, out visible);

        private void Projection(float longitude, float latitude, float radius,
            out Vector2 position, out float scale, out float depth, out bool visible)
        {
            var lat = latitude * Mathf.Deg2Rad;
            var lon = (longitude - _rotation) * Mathf.Deg2Rad;

            var cosLat = Mathf.Cos(lat);
            var x = cosLat * Mathf.Sin(lon);
            var y = Mathf.Sin(lat);
            depth = cosLat * Mathf.Cos(lon);

            // 0.94 — зони не доходять до самого лімба, інакше вони «злизуються»
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
                if (marker == null || marker.Zone == null)
                    continue;

                Projection(marker.Zone, radius, out var position, out var scale, out var depth, out var visible);

                // Замість вимикання об'єкта гасимо альфу: SetActive щокадру
                // смикав би графіку на кожному перетині лімба.
                var alpha = !visible ? 0f
                    : depth > 0.04f ? 1f
                    : Mathf.Max(0f, (depth + 0.18f) / 0.22f);

                marker.Project(position, scale, alpha);
            }

            for (var i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                if (placement == null || !placement.IsBound)
                    continue;

                Projection(placement.Longitude, placement.Latitude, radius,
                    out var position, out var scale, out var depth, out var visible);
                var alpha = !visible ? 0f
                    : depth > 0.04f ? 1f
                    : Mathf.Max(0f, (depth + 0.18f) / 0.22f);
                placement.Project(position, scale, alpha);
            }
        }
    }
}
