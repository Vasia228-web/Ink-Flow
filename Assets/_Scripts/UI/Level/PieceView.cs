using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Фігура в лотку (документ §2, K1Candy): до п'яти зменшених блоків того ж стилю, що й на
    /// полі — гало, тонований блок, блиск — у слоті з кутами 20. Кожна фігура повністю
    /// вміщається у свій слот: довга п'ятірка масштабується під ширину, нічого не виходить за край.
    ///
    /// Сама фігура за пальцем не їде — на полі з'являється привид, а слот лише тьмяніє.
    /// Куди ставити, вирішує дошка; слот знає лише свій індекс.
    /// </summary>
    public sealed class PieceView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image slot;
        [SerializeField] private RectTransform cellsRoot;
        [SerializeField] private Image[] glows = System.Array.Empty<Image>();
        [SerializeField] private Image[] blocks = System.Array.Empty<Image>();
        [SerializeField] private Image[] overlays = System.Array.Empty<Image>();
        [SerializeField] private CanvasGroup group;

        [Tooltip("Крок клітинок фігури в одиницях канваса, коли місця вдосталь (частка кроку поля).")]
        [SerializeField] private float cellStep = 69f;

        private int _index;
        private PieceDef _piece;

        public int Index => _index;
        public PieceDef Piece => _piece;
        public bool IsEmpty => _piece.IsEmpty;

        /// <summary>Палець ліг на фігуру / тягне / відпустив. Аргумент — індекс слота.</summary>
        public System.Action<int, PointerEventData>? DragBegan;
        public System.Action<int, PointerEventData>? Dragged;
        public System.Action<int, PointerEventData>? DragEnded;

        public void Bind(int index) => _index = index;

        public void Apply()
        {
            if (design == null)
                return;
            if (slot != null)
                slot.color = Color.white; // заливка й обвідка запечені у спрайті слота (білий 3.5 % / 10 %)
        }

        /// <summary>Показує фігуру: блоки в клітинки форми, колір — родина картинки.</summary>
        public void Show(PieceDef piece)
        {
            _piece = piece;
            if (design == null)
                return;

            if (piece.IsEmpty)
            {
                for (var i = 0; i < blocks.Length; i++)
                    SetCellActive(i, false);
                SetDragging(false);
                return;
            }

            var shape = piece.Shape!;
            var area = cellsRoot != null ? cellsRoot.rect : ((RectTransform)transform).rect;
            var w = Mathf.Max(area.width, 1f);
            var h = Mathf.Max(area.height, 1f);

            // Фігура мусить уміститись у слот разом із гало: крок стискається під ширину й висоту.
            var pad = cellStep * (design.BlockFraction * (design.BlockGlowScale - 1f) * 0.5f + 0.08f);
            var step = Mathf.Min(cellStep, (w - pad * 2f) / shape.Width, (h - pad * 2f) / shape.Height);
            step = Mathf.Max(step, cellStep * 0.35f);
            var side = step * design.BlockFraction;
            var glowSide = side * design.BlockGlowScale;
            var offsetX = (shape.Width - 1) * 0.5f;
            var offsetY = (shape.Height - 1) * 0.5f;

            var baseColor = DesignSystem.PaletteColor(piece.Color);
            var tint = design.BlockTint(baseColor);
            var glow = DesignSystem.WithAlpha(baseColor, design.BlockGlowAlpha);
            for (var i = 0; i < blocks.Length; i++)
            {
                var show = i < shape.Cells.Length;
                SetCellActive(i, show);
                if (!show)
                    continue;
                var cell = shape.Cells[i];
                var centre = new Vector2((cell.X - offsetX) * step, (cell.Y - offsetY) * step);
                PlaceCell(glows, i, centre, glowSide, glow);
                PlaceCell(blocks, i, centre, side, tint);
                PlaceCell(overlays, i, centre, side, new Color(1f, 1f, 1f, design.BlockHighlightAlpha));
            }
            SetDragging(false);
        }

        /// <summary>Перефарбування (§5): нова родина — нові кольори тих самих блоків.</summary>
        public void Recolor(byte color)
        {
            _piece = _piece.WithColor(color);
            if (design == null || _piece.IsEmpty)
                return;
            var baseColor = DesignSystem.PaletteColor(color);
            var tint = design.BlockTint(baseColor);
            var glow = DesignSystem.WithAlpha(baseColor, design.BlockGlowAlpha);
            for (var i = 0; i < blocks.Length && i < _piece.Shape!.Cells.Length; i++)
            {
                if (blocks[i] != null) blocks[i].color = tint;
                if (i < glows.Length && glows[i] != null) glows[i].color = glow;
            }
        }

        private static void PlaceCell(Image[] layer, int i, Vector2 centre, float side, Color color)
        {
            if (i >= layer.Length || layer[i] == null)
                return;
            var rect = (RectTransform)layer[i].transform;
            rect.anchoredPosition = centre;
            rect.sizeDelta = new Vector2(side, side);
            layer[i].color = color;
        }

        private void SetCellActive(int i, bool active)
        {
            Toggle(glows, i, active);
            Toggle(blocks, i, active);
            Toggle(overlays, i, active);
        }

        private static void Toggle(Image[] layer, int i, bool active)
        {
            if (i < layer.Length && layer[i] != null && layer[i].gameObject.activeSelf != active)
                layer[i].gameObject.SetActive(active);
        }

        /// <summary>Поки фігуру тягнуть, слот тьмяніє — фігура «пішла» на поле.</summary>
        public void SetDragging(bool dragging)
        {
            if (group != null && design != null)
                group.alpha = dragging ? design.TrayDraggingAlpha : 1f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_piece.IsEmpty)
                return;
            DragBegan?.Invoke(_index, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_piece.IsEmpty)
                return;
            Dragged?.Invoke(_index, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_piece.IsEmpty)
                return;
            DragEnded?.Invoke(_index, eventData);
        }
    }
}
