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
    /// Планета «Горизонт» (U1Glaze S05, від порогу + 1 комірок): велика планета внизу, комірки — рядами по дугах її
    /// поверхні, усі видно завжди. Розкладка — <see cref="HorizonLayout"/> (правило з макетів, числа з токенів).
    /// Сцена статична: розкладається на зміну розміру ділянки, а не щокадру; ні свайпу, ні самообертання.
    /// Вигляд комірки — той самий <see cref="SlotCell"/>, що й на «Сфері».
    /// </summary>
    public sealed class PlanetHorizon : MonoBehaviour, IPointerClickHandler, IDragHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;
        [Tooltip("Ділянка між рядком лічильника й кнопкою: її висота — простір сцени.")]
        [SerializeField] private RectTransform area;
        [Tooltip("Екран: ширина сцени — на весь екран, як у макеті (ділянка вужча на поля стовпчика 16).")]
        [SerializeField] private RectTransform widthSource;
        [Tooltip("Велика планета: квад шейдера, діаметр і місце ставить Layout.")]
        [SerializeField] private Image disc;
        [SerializeField] private RectTransform cellsRoot;
        [SerializeField] private SlotCell[] cells = Array.Empty<SlotCell>();
        [Tooltip("Іскри «планета ожила» в небі над горизонтом (S05_04); місця — від верху планети з масштабом сцени.")]
        [SerializeField] private RectTransform[] sparkles = Array.Empty<RectTransform>();

        private PlanetSurface? _surface;
        private HorizonScene _scene;
        private Vector2 _laidOutFor = new Vector2(-1f, -1f);
        private Material? _discMaterial;

        public event Action<PlanetSlot>? SlotTapped;

        public IReadOnlyList<SlotCell> Cells => cells;

        /// <summary>Сцена останньої розкладки (тести: масштаб, радіус, комірки).</summary>
        public HorizonScene Scene => _scene;

        public void Bind(PlanetSurface surface)
        {
            _surface = surface;
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
            SetupMaterial();
            _laidOutFor = new Vector2(-1f, -1f);
            Layout();
        }

        public SlotCell? CellFor(PlanetSlot slot)
        {
            foreach (var cell in cells)
                if (cell != null && ReferenceEquals(cell.Slot, slot))
                    return cell;
            return null;
        }

        public void SetSelected(PlanetSlot? slot)
        {
            foreach (var cell in cells)
                if (cell != null && cell.Slot != null)
                    cell.SetSelected(ReferenceEquals(cell.Slot, slot));
            _laidOutFor = new Vector2(-1f, -1f);
            Layout();
        }

        // Розмір ділянки змінився (поворот, split-screen) — розкладаємо в LateUpdate, а не посеред перебудови розкладки:
        // писати розміри дітей у цьому колбеку — шлях до «graphic rebuild loop».
        private bool _dirty;

        private void OnRectTransformDimensionsChange() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty)
                return;
            _dirty = false;
            Layout();
        }

        /// <summary>Розкладка під поточний розмір ділянки (px макета → одиниці канваса через коефіцієнт макета).</summary>
        public void Layout()
        {
            if (design == null || area == null || _surface == null)
                return;
            var width = widthSource != null ? widthSource.rect.width : area.rect.width;
            var size = new Vector2(width, area.rect.height);
            if (size.x <= 0f || size.y <= 0f || size == _laidOutFor)
                return;
            _laidOutFor = size;

            var k = DesignSystem.MockupToReference;
            var p = new HorizonParams(design.HorizonRadius, design.HorizonSlotGap, design.HorizonRowStep, design.HorizonTopInset,
                design.HorizonNearSlot, design.HorizonPerspective, design.HorizonSideInset, design.HorizonDepth,
                design.HorizonSkyMin, design.HorizonBottomGap, design.HorizonMaxRow);
            _scene = HorizonLayout.Layout(_surface.Slots.Count, size.x / k, size.y / k, p);

            // Центр планети в координатах ділянки (півот по центру): верх ділянки +h/2, верх планети нижче на TopBelowAreaTop.
            var top = size.y * 0.5f - (float)_scene.TopBelowAreaTop * k;
            var radius = (float)_scene.Radius * k;
            var centre = new Vector2(0f, top - radius);
            if (disc != null)
            {
                var rect = disc.rectTransform;
                rect.anchoredPosition = centre;
                rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            }

            var slots = _scene.Slots;
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == null || cell.Slot == null || i >= slots.Length)
                    continue;
                var slot = slots[i];
                var box = (float)slot.Size * k * SlotCell.BoxPerSide;
                var scale = cell.BoxSide > 0f ? box / cell.BoxSide : 1f;
                cell.Pose(centre + new Vector2((float)slot.X * k, (float)slot.Y * k), (float)slot.TiltDeg, scale, scale, 0f, 1f, 0f);
            }

            // Іскри — у небі над верхом планети (px макета від верхньої точки, Y угору), разом із масштабом сцени.
            var sceneScale = (float)_scene.Scale;
            for (var i = 0; i < sparkles.Length && i < SparkleSky.Length; i++)
                if (sparkles[i] != null)
                    sparkles[i].anchoredPosition = new Vector2(SparkleSky[i].x * k * sceneScale, top + SparkleSky[i].y * k * sceneScale);
        }

        /// <summary>Іскри S05_04: зсув від верхньої точки планети, px макета за масштабу сцени 1 (Y угору).</summary>
        private static readonly Vector2[] SparkleSky =
        {
            new Vector2(0f, 107f), new Vector2(-141f, 81f), new Vector2(141f, 91f),
            new Vector2(-172f, 0f), new Vector2(171f, 10f), new Vector2(76f, 160f),
        };

        /// <summary>«Планета ожила»: іскри 0…1 (поява — через CanvasRenderer, рахує екран).</summary>
        public void SetAlive(float amount)
        {
            foreach (var sparkle in sparkles)
                if (sparkle != null)
                {
                    if (sparkle.gameObject.activeSelf != amount > 0f)
                        sparkle.gameObject.SetActive(amount > 0f);
                    var image = sparkle.GetComponent<Image>();
                    if (image != null)
                        image.canvasRenderer.SetAlpha(Mathf.Clamp01(amount));
                }
        }

        /// <summary>
        /// Порожній обробник: з ним модуль вводу починає перетягування, і свайп по сцені не закінчується «тапом» по
        /// комірці під пальцем (сцена статична — рухати нічого).
        /// </summary>
        public void OnDrag(PointerEventData eventData) { }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging || cellsRoot == null || design == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(cellsRoot, eventData.position, eventData.pressEventCamera, out var point))
                return;
            var slot = Pick(point);
            if (slot != null)
                SlotTapped?.Invoke(slot);
        }

        /// <summary>
        /// Комірка під точкою (локальні одиниці комірок): зона тапу — видима лунка разом із пів проміжку з кожного боку,
        /// тож і дрібні дальні ряди не менші за палець; де зони сусідів сходяться, перемагає найближча комірка.
        /// </summary>
        public PlanetSlot? Pick(Vector2 point)
        {
            if (design == null)
                return null;
            PlanetSlot? best = null;
            var bestDistance = float.MaxValue;
            var gapPx = design.HorizonSlotGap * (float)_scene.Scale;
            foreach (var cell in cells)
            {
                if (cell == null || cell.Slot == null)
                    continue;
                var boxPx = cell.BoxSide * cell.transform.localScale.x / DesignSystem.MockupToReference;
                if (boxPx <= 0f)
                    continue;
                // Межа зони в частках пів бокса: видима лунка (64 з 72) + пів проміжку.
                var reach = 1f / SlotCell.BoxPerSide + gapPx / boxPx;
                var distance = cell.Distance(point);
                if (distance <= reach && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cell.Slot;
                }
            }
            return best;
        }

        private void SetupMaterial()
        {
            if (design == null || planetShader == null || disc == null || _surface == null)
                return;
            if (_discMaterial == null)
            {
                _discMaterial = new Material(planetShader) { name = "HorizonDisc" };
                _discMaterial.SetFloat(Shader.PropertyToID("_Mode"), 0f);
                disc.material = _discMaterial;
            }
            var palette = design.Planet(_surface.Type);
            _discMaterial.SetFloat(Shader.PropertyToID("_Type"), (float)_surface.Type);
            _discMaterial.SetFloat(Shader.PropertyToID("_Spin"), 0f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Painted"), 1f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Locked"), 0f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Seed"), Mathf.Abs(_surface.Name.GetHashCode() % 997) * 0.031f);
            _discMaterial.SetColor(Shader.PropertyToID("_Base"), palette.Base);
            _discMaterial.SetColor(Shader.PropertyToID("_Land"), palette.Land);
            _discMaterial.SetColor(Shader.PropertyToID("_Atmo"), palette.Atmosphere);
        }

        private void OnDestroy()
        {
            if (_discMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_discMaterial);
            else
                DestroyImmediate(_discMaterial);
        }
    }
}
