using System;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Планета «Сфера» (U1Glaze S04, до порогу комірок): куля, що крутиться, комірки на ній, кільце міток навколо й
    /// бірюзова дуга видимої половини. Математика — порт еталона <see cref="GlobeSlots"/> (розкладка за seed планети,
    /// проєкція з нахилом осі, глибина, прозорість, затінення, кут мітки).
    ///
    /// Крутиться комірками й кільцем: свайп по горизонталі з інерцією, без дотику — повільне самообертання (повзунок,
    /// 0 вимикає). Поверхня кулі не крутиться: планети поки заглушки, і SPEC дозволяє «крутити лише комірки й
    /// кільце» — так щокадру не пишеться жоден матеріал. Тап по комірці біля краю (d &lt; поріг) спершу довертає її до
    /// центру, потім віддає екрану.
    ///
    /// Щокадру — лише трансформи комірок і міток, альфа CanvasGroup і колір CanvasRenderer (затінення); розмір кулі
    /// під ділянку — масштаб на зміну розкладки (<see cref="SetFit"/>).
    /// </summary>
    public sealed class PlanetGlobe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;
        [Tooltip("Ділянка, у якій ловиться дотик і рахується рух пальця (без масштабу кулі).")]
        [SerializeField] private RectTransform area;
        [Tooltip("Квадрат кулі: його ширина — діаметр за масштабу 1; масштаб ставить SetFit.")]
        [SerializeField] private RectTransform globe;
        [SerializeField] private Image disc;
        [SerializeField] private Image halo;
        [SerializeField] private Image aliveHalo;
        [SerializeField] private Image arc;
        [SerializeField] private SlotCell[] cells = Array.Empty<SlotCell>();
        [SerializeField] private Image[] pips = Array.Empty<Image>();
        [SerializeField] private Image[] pipShines = Array.Empty<Image>();
        [SerializeField] private Sprite pipEmpty;
        [SerializeField] private Sprite pipFilled;
        [SerializeField] private RectTransform[] sparkles = Array.Empty<RectTransform>();

        // Розкладка дорога (до 12 × 30 спроб по 200 точок) — раз на пару (кількість, seed) за запуск.
        private static readonly Dictionary<(int n, int seed), GlobeSlot[]> LayoutCache = new Dictionary<(int, int), GlobeSlot[]>();

        private PlanetSurface? _surface;
        private GlobeSlot[] _layout = Array.Empty<GlobeSlot>();
        private PlanetType _type;
        private bool _bound;
        private float _rotation;
        private float _velocity;
        private bool _dragging;
        private float _lastDragTime;
        private bool _hold;
        private bool _rotatedByPlayer;
        private float _fit = 1f;
        private bool _turning;
        private float _turnFrom;
        private float _turnTo;
        private float _turnTime;
        private PlanetSlot? _turnSlot;
        private readonly float[] _depth = new float[24];

        private Material? _discMaterial;
        private Material? _haloMaterial;
        private Material? _aliveMaterial;
        private Material? _arcMaterial;

        /// <summary>Тап по комірці (після доворота, якщо вона була біля краю).</summary>
        public event Action<PlanetSlot>? SlotTapped;

        /// <summary>Гравець уперше крутнув кулю пальцем.</summary>
        public event Action? RotatedByPlayer;

        /// <summary>Поворот кулі, градуси (стенд знімків ставить той самий, що на макеті).</summary>
        public float Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                _velocity = 0f;
                _turning = false;
                Pose();
            }
        }

        /// <summary>Масштаб кулі (1 — діаметр квадрата <see cref="globe"/>).</summary>
        public float Fit => _fit;

        /// <summary>Діаметр кулі за масштабу 1, одиниці канваса.</summary>
        public float BaseDiameter => globe != null ? globe.rect.width : 0f;

        public IReadOnlyList<SlotCell> Cells => cells;

        /// <summary>
        /// Прив'язує поверхню: розкладка за seed типу планети. Та сама планета (повернення з колекції) лишає
        /// поворот — гравець бачить, як картинка лягла в комірку, а не як куля знову стала на нуль.
        /// </summary>
        public void Bind(PlanetSurface surface)
        {
            var samePlanet = _bound && _type == surface.Type && _surface != null && _surface.Slots.Count == surface.Slots.Count;
            _surface = surface;
            _type = surface.Type;
            _bound = true;
            _layout = LayoutFor(surface.Slots.Count, PlanetLooks.SeedOf(surface.Type));
            if (!samePlanet)
            {
                _rotation = 0f;
                _velocity = 0f;
            }
            _turning = false;
            _turnSlot = null;
            _dragging = false;
            _hold = false;

            var selectedScale = design != null ? design.SlotSelectedScale : 1f;
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null)
                    continue;
                if (i < surface.Slots.Count)
                    cells[i].Bind(surface.Slots[i], selectedScale);
                else
                    cells[i].Release();
            }
            for (var i = 0; i < pips.Length; i++)
                Toggle(pips[i], i < surface.Slots.Count);

            SetupMaterials();
            Pose();
        }

        /// <summary>Мітка комірки: заповнена — кольором картинки з блиском, порожня — <c>pip_empty</c>.</summary>
        public void SetPip(int index, bool filled, Color color)
        {
            if (index < 0 || index >= pips.Length || pips[index] == null)
                return;
            pips[index].sprite = filled ? pipFilled : pipEmpty;
            pips[index].color = filled ? color : Color.white;
            if (index < pipShines.Length && pipShines[index] != null)
                Toggle(pipShines[index], filled);
        }

        public SlotCell? CellFor(PlanetSlot slot)
        {
            foreach (var cell in cells)
                if (cell != null && ReferenceEquals(cell.Slot, slot))
                    return cell;
            return null;
        }

        /// <summary>
        /// Вибрана комірка (відкритий лист) — куля стоїть: інакше самообертання за кілька секунд відносило б комірку,
        /// з якою гравець щойно працює, за край кулі (S04_04: вибрана — у центрі).
        /// </summary>
        public void SetSelected(PlanetSlot? slot)
        {
            _hold = slot != null;
            if (_hold)
                _velocity = 0f;
            foreach (var cell in cells)
                if (cell != null && cell.Slot != null)
                    cell.SetSelected(ReferenceEquals(cell.Slot, slot));
            Pose();
        }

        /// <summary>Розмір кулі під ділянку — на зміну розкладки, не щокадру (localScale, не sizeDelta).</summary>
        public void SetFit(float scale)
        {
            _fit = Mathf.Max(0.05f, scale);
            if (globe != null)
                globe.localScale = new Vector3(_fit, _fit, 1f);
        }

        /// <summary>Гало «планета ожила»: 0…1 (поява — через CanvasRenderer, рахує екран).</summary>
        public void SetAlive(float amount)
        {
            Toggle(aliveHalo, amount > 0f);
            if (aliveHalo != null)
                aliveHalo.canvasRenderer.SetAlpha(Mathf.Clamp01(amount));
            foreach (var sparkle in sparkles)
                if (sparkle != null)
                {
                    Toggle(sparkle, amount > 0f);
                    var image = sparkle.GetComponent<Image>();
                    if (image != null)
                        image.canvasRenderer.SetAlpha(Mathf.Clamp01(amount));
                }
        }

        private static GlobeSlot[] LayoutFor(int n, int seed)
        {
            if (n < 1)
                return Array.Empty<GlobeSlot>();
            if (!LayoutCache.TryGetValue((n, seed), out var layout))
            {
                layout = GlobeSlots.Layout(n, seed);
                LayoutCache[(n, seed)] = layout;
            }
            return layout;
        }

        private void SetupMaterials()
        {
            if (design == null || planetShader == null || _surface == null)
                return;
            var palette = design.Planet(_surface.Type);

            _discMaterial ??= Make(disc, "PlanetDisc", 0f);
            if (_discMaterial != null)
            {
                _discMaterial.SetFloat(TypeId, (float)_surface.Type);
                _discMaterial.SetFloat(SpinId, 0f); // поверхня стоїть: крутяться комірки й кільце (SPEC)
                _discMaterial.SetFloat(PaintedId, 1f);
                _discMaterial.SetFloat(LockedId, 0f);
                // Той самий узор, що в каруселі галактики (PlanetView): зерно — від назви планети.
                _discMaterial.SetFloat(SeedId, Mathf.Abs(_surface.Name.GetHashCode() % 997) * 0.031f);
                _discMaterial.SetColor(BaseId, palette.Base);
                _discMaterial.SetColor(LandId, palette.Land);
                _discMaterial.SetColor(AtmoId, palette.Atmosphere);
            }

            _haloMaterial ??= Make(halo, "PlanetHalo", 4f);
            if (_haloMaterial != null)
            {
                _haloMaterial.SetColor(AtmoId, palette.Atmosphere);
                _haloMaterial.SetFloat(RimId, 1f / design.SphereHalo);
            }
            _aliveMaterial ??= Make(aliveHalo, "PlanetAliveHalo", 4f);
            if (_aliveMaterial != null)
            {
                _aliveMaterial.SetColor(AtmoId, Color.Lerp(palette.Atmosphere, design.Teal300, 0.5f));
                _aliveMaterial.SetFloat(RimId, 1f / design.SphereAliveHalo);
            }
            _arcMaterial ??= Make(arc, "PlanetArc", 3f);
            if (_arcMaterial != null)
            {
                _arcMaterial.SetColor(AtmoId, design.Teal);
                _arcMaterial.SetFloat(ArcInnerId, design.SphereArcInner / design.SphereArcOuter);
                _arcMaterial.SetFloat(ArcAlphaId, design.SphereArcAlpha);
            }
        }

        private Material? Make(Image? target, string name, float mode)
        {
            if (target == null || planetShader == null)
                return null;
            var material = new Material(planetShader) { name = name };
            material.SetFloat(ModeId, mode);
            target.material = material;
            return material;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _turning = false;
            _turnSlot = null;
            _velocity = 0f;
            _lastDragTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (design == null || area == null)
                return;
            // Рух пальця — у px макета ділянки (без масштабу кулі): свайп однаковий на будь-якому екрані.
            var camera = eventData.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, camera, out var now) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position - eventData.delta, camera, out var before))
                return;
            var px = (now.x - before.x) / DesignSystem.MockupToReference;
            var delta = -px * design.SphereSwipeRate; // палець управо — комірки йдуть управо
            _rotation += delta;
            _velocity = Time.deltaTime > 0f ? delta / Time.deltaTime : 0f;
            _lastDragTime = Time.unscaledTime;
            if (!_rotatedByPlayer && Mathf.Abs(delta) > 0.01f)
            {
                _rotatedByPlayer = true;
                RotatedByPlayer?.Invoke();
            }
            Pose();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            // Палець зупинився й постояв — вибігу немає: модуль вводу не кличе OnDrag без руху, і швидкість лишилась би
            // з останнього кадру руху (куля розкручувалась би після того, як гравець її поставив).
            if (design != null && Time.unscaledTime - _lastDragTime > design.SphereFlingHold)
                _velocity = 0f;
        }

        // Екран сховали посеред перетягування (другий палець на «‹») — OnEndDrag не прийде, куля не мусить завмерти.
        private void OnDisable() => _dragging = false;

        public void OnPointerClick(PointerEventData eventData)
        {
            // Під час доворота тапи не приймаються: інакше відкладений тап (по комірці біля краю) спрацював би вже після
            // іншого, і лист відкрився б не на тій комірці.
            if (eventData.dragging || _turning || globe == null || _surface == null || design == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(globe, eventData.position, eventData.pressEventCamera, out var point))
                return;
            var slot = Pick(point, out var depth);
            if (slot == null)
                return;
            if (depth < design.SphereTurnDepth)
                TurnTo(slot);
            else
                SlotTapped?.Invoke(slot);
        }

        /// <summary>Передня комірка під точкою (локальні одиниці кулі): серед видимих — з найбільшою глибиною.</summary>
        public PlanetSlot? Pick(Vector2 point, out float depth)
        {
            depth = -1f;
            PlanetSlot? best = null;
            for (var i = 0; i < cells.Length && i < _layout.Length; i++)
            {
                var cell = cells[i];
                if (cell == null || cell.Slot == null || _depth[i] <= GlobeHide)
                    continue;
                if (!cell.Contains(point, 0f) || _depth[i] <= depth)
                    continue;
                depth = _depth[i];
                best = cell.Slot;
            }
            return best;
        }

        /// <summary>Довертає комірку до центру (найкоротшим шляхом), потім віддає тап екрану.</summary>
        public void TurnTo(PlanetSlot slot)
        {
            if (slot.Index < 0 || slot.Index >= _layout.Length)
                return;
            var target = (float)_layout[slot.Index].Lon;
            var delta = Mathf.DeltaAngle(_rotation, target);
            _turnFrom = _rotation;
            _turnTo = _rotation + delta;
            _turnTime = 0f;
            _turnSlot = slot;
            _turning = true;
            _velocity = 0f;
        }

        private void LateUpdate()
        {
            if (design == null || _surface == null)
                return;
            var dt = Time.deltaTime;
            if (_turning)
            {
                _turnTime += dt;
                var k = Mathf.Clamp01(_turnTime / Mathf.Max(0.01f, design.SphereTurnDuration));
                _rotation = Mathf.LerpUnclamped(_turnFrom, _turnTo, design.CurveEaseInOut.Evaluate(k));
                if (k >= 1f)
                {
                    _turning = false;
                    var slot = _turnSlot;
                    _turnSlot = null;
                    Pose();
                    if (slot != null)
                        SlotTapped?.Invoke(slot);
                    return;
                }
            }
            else if (!_dragging && !_hold)
            {
                if (Mathf.Abs(_velocity) > 0.5f)
                {
                    _rotation += _velocity * dt;
                    _velocity *= Mathf.Exp(-design.SphereSwipeDamping * dt);
                }
                else
                {
                    _velocity = 0f;
                    _rotation += design.SphereAutoSpin * dt;
                }
            }
            else
                return; // під пальцем позу ставить OnDrag
            Pose();
        }

        /// <summary>Глибина, з якої комірку видно (SPEC: d ≤ 0.05 — сховати).</summary>
        private const float GlobeHide = 0.05f;

        private void Pose()
        {
            if (design == null || globe == null || _surface == null)
                return;
            var radius = globe.rect.width * 0.5f;
            var tilt = design.SphereAxisTilt;
            var ring = globe.rect.width * design.SpherePipRing;
            for (var i = 0; i < _layout.Length; i++)
            {
                var slot = _layout[i];
                var p = GlobeSlots.Project(slot, _rotation, tilt);
                if (i < _depth.Length)
                    _depth[i] = (float)p.Depth;
                if (i < cells.Length && cells[i] != null && cells[i].Slot != null)
                {
                    var cell = cells[i];
                    var svd = GlobeSlots.Svd2(p);
                    var box = (float)slot.Size * radius * SlotCell.BoxPerSide;
                    var k = cell.BoxSide > 0f ? box / cell.BoxSide : 1f;
                    cell.Pose(new Vector2((float)p.X * radius, (float)p.Y * radius), (float)svd.T1,
                        (float)svd.Sx * k, (float)svd.Sy * k, (float)svd.T2,
                        (float)GlobeSlots.Alpha(p.Depth), (float)GlobeSlots.Shade(p.Depth));
                }
                if (i < pips.Length && pips[i] != null)
                {
                    var a = (float)GlobeSlots.PipAngle(slot, _rotation) * Mathf.Deg2Rad;
                    pips[i].rectTransform.localPosition = new Vector3(Mathf.Cos(a) * ring, Mathf.Sin(a) * ring, 0f);
                }
            }
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private void OnDestroy()
        {
            ReleaseMaterial(_discMaterial);
            ReleaseMaterial(_haloMaterial);
            ReleaseMaterial(_aliveMaterial);
            ReleaseMaterial(_arcMaterial);
        }

        private static void ReleaseMaterial(Material? material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int TypeId = Shader.PropertyToID("_Type");
        private static readonly int SpinId = Shader.PropertyToID("_Spin");
        private static readonly int PaintedId = Shader.PropertyToID("_Painted");
        private static readonly int LockedId = Shader.PropertyToID("_Locked");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int BaseId = Shader.PropertyToID("_Base");
        private static readonly int LandId = Shader.PropertyToID("_Land");
        private static readonly int AtmoId = Shader.PropertyToID("_Atmo");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int ArcInnerId = Shader.PropertyToID("_ArcInner");
        private static readonly int ArcAlphaId = Shader.PropertyToID("_ArcAlpha");
    }
}
