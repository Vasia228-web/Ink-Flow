using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Рука гравця: три фігури в повітрі (документ §2, §11 — без плиток-підкладок).
    /// Показує рівно те, що лежить у <see cref="RunSession.Tray"/>, — окремого списку
    /// не тримає, інакше показане й поставлене розійшлися б.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private PieceView[] slots = System.Array.Empty<PieceView>();

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
                slots[i]?.Show(i < tray.Count ? tray[i] : PieceDef.None);
        }

        public void Apply()
        {
            _ = design;
        }
    }
}
