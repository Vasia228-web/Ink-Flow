using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Фігура в лотку: до п'яти блоків, викладених за формою в масштабі лотка
    /// (<see cref="BoardGeometry.TrayBox"/>), і жест перетягування.
    ///
    /// Сама фігура за пальцем не їде — на полі з'являється привид (як у прототипі v3):
    /// так фігуру видно з-під пальця, а комірка лотка лише тьмяніє, поки її тягнуть.
    /// Куди ставити, вирішує дошка; комірка знає лише свій індекс.
    /// </summary>
    public sealed class PieceView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private BlockView[] blocks = System.Array.Empty<BlockView>();
        [SerializeField] private CanvasGroup group;

        private int _index;
        private PieceDef _piece;

        public int Index => _index;
        public PieceDef Piece => _piece;
        public bool IsEmpty => _piece.IsEmpty;

        /// <summary>Палець ліг на фігуру / тягне / відпустив. Аргумент — індекс комірки.</summary>
        public System.Action<int, PointerEventData>? DragBegan;
        public System.Action<int, PointerEventData>? Dragged;
        public System.Action<int, PointerEventData>? DragEnded;

        public void Bind(int index) => _index = index;

        /// <summary>Розкладає блоки за формою. Центр фігури — центр комірки.</summary>
        public void Show(PieceDef piece)
        {
            _piece = piece;
            if (design == null)
                return;

            if (piece.IsEmpty)
            {
                for (var i = 0; i < blocks.Length; i++)
                    blocks[i]?.Hide();
                return;
            }

            var shape = piece.Shape!;
            // Крок — від фактичного розміру блока, який виставив збирач, а не від
            // константи: масштаб макета в збирача й у стилі різний (390 проти 402 px),
            // і другого джерела правди тут бути не може.
            var box = blocks.Length > 0 && blocks[0] != null
                ? ((RectTransform)blocks[0].transform).sizeDelta.x
                : BoardGeometry.TrayBox * DesignSystem.MockupToReference;
            var step = box * (BoardGeometry.TrayStep / BoardGeometry.TrayBox);
            var color = DesignSystem.PaletteColor(piece.Color);
            var offsetX = (shape.Width - 1) * 0.5f;
            var offsetY = (shape.Height - 1) * 0.5f;

            for (var i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                if (block == null)
                    continue;

                if (i >= shape.Cells.Length)
                {
                    block.Hide();
                    continue;
                }

                var cell = shape.Cells[i];
                var rect = (RectTransform)block.transform;
                // Позиція — на подію показу, не щокадру: anchoredPosition тут дозволена.
                rect.anchoredPosition = new Vector2((cell.X - offsetX) * step, (cell.Y - offsetY) * step);
                block.Show(color);
            }

            SetDragging(false);
        }

        /// <summary>Поки фігуру тягнуть, комірка тьмяніє — фігура «пішла» на поле.</summary>
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
