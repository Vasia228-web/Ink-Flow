using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рука гравця: три комірки з фігурами (документ §2). Показує рівно те, що лежить
    /// у <see cref="RunSession.Tray"/>, — окремого списку не тримає, інакше показане
    /// й поставлене розійшлися б.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private PieceView[] slots = System.Array.Empty<PieceView>();
        [SerializeField] private Image[] slotFills = System.Array.Empty<Image>();
        [SerializeField] private Image[] slotStrokes = System.Array.Empty<Image>();

        public System.Action<int, PointerEventData>? DragBegan;
        public System.Action<int, PointerEventData>? Dragged;
        public System.Action<int, PointerEventData>? DragEnded;

        private void Awake()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;
                slot.Bind(i);
                slot.DragBegan += (k, e) => DragBegan?.Invoke(k, e);
                slot.Dragged += (k, e) => Dragged?.Invoke(k, e);
                slot.DragEnded += (k, e) => DragEnded?.Invoke(k, e);
            }
        }

        public PieceView? Slot(int index) => index >= 0 && index < slots.Length ? slots[index] : null;

        public void Show(IReadOnlyList<PieceDef> tray)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var piece = i < tray.Count ? tray[i] : PieceDef.None;
                slots[i]?.Show(piece);
                ApplySlotStyle(i, !piece.IsEmpty);
            }
        }

        public void Apply()
        {
            if (design == null)
                return;
            for (var i = 0; i < slots.Length; i++)
                ApplySlotStyle(i, slots[i] != null && !slots[i].IsEmpty);
        }

        private void ApplySlotStyle(int i, bool occupied)
        {
            if (design == null)
                return;
            if (i < slotFills.Length && slotFills[i] != null)
                slotFills[i].color = occupied ? design.TraySlotFill : design.TraySlotEmptyFill;
            if (i < slotStrokes.Length && slotStrokes[i] != null)
                slotStrokes[i].color = occupied
                    ? design.TraySlotStroke
                    : DesignSystem.WithAlpha(design.TraySlotStroke, design.TraySlotStroke.a * 0.5f);
        }
    }
}
