using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Рука гравця: три слоти K1Candy (кути 20, білий 3.5 % / обвідка 10 %) з фігурами
    /// (документ §2, §11). Показує рівно те, що лежить у <see cref="RunSession.Tray"/>, —
    /// окремого списку не тримає, інакше показане й поставлене розійшлися б.
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
            foreach (var slot in slots)
                slot?.Apply();
        }

        /// <summary>
        /// Три слоти порівну по фактичній ширині лотка (§11, будь-який екран). Виклик — на
        /// подію розкладки, не щокадру; після нього фігури треба показати знову.
        /// </summary>
        public void Layout()
        {
            var rect = ((RectTransform)transform).rect;
            if (rect.width < 1f || slots.Length == 0)
                return;
            var gap = rect.width * 0.025f;
            var slotWidth = (rect.width - gap * (slots.Length - 1)) / slots.Length;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    continue;
                var slot = (RectTransform)slots[i].transform;
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot = new Vector2(0f, 0.5f);
                slot.anchoredPosition = new Vector2(i * (slotWidth + gap), 0f);
                slot.sizeDelta = new Vector2(slotWidth, rect.height);
            }
        }
    }
}
