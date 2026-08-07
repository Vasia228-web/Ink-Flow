using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Пропускає вертикальні жести крізь горизонтальний скрол до зовнішнього.
    ///
    /// Навіщо: `ScrollRect` забирає перетягування собі незалежно від напрямку і
    /// НЕ передає його батьківському. Без цього ряд карток з'їдав би вертикальні
    /// свайпи, і сторінка магазину не гортались би пальцем по картках — а це майже
    /// вся її площа.
    ///
    /// Працює так: на початку жесту дивимось, чого більше — руху по X чи по Y.
    /// Якщо по Y, вимикаємо внутрішній скрол (його `IsActive()` стає false, і він
    /// сам пропускає подію) і ведемо жест у зовнішній. Тому цей компонент мусить
    /// стояти в списку компонентів ПЕРЕД `ScrollRect` — бутстрап додає його першим.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class NestedScrollForwarder : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private ScrollRect inner;
        [SerializeField] private ScrollRect outer;

        private bool _forwarding;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _forwarding = false;
            if (inner == null || outer == null)
                return;

            if (Mathf.Abs(eventData.delta.y) <= Mathf.Abs(eventData.delta.x))
                return;

            _forwarding = true;
            inner.enabled = false;
            outer.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_forwarding && outer != null)
                outer.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_forwarding)
                return;

            _forwarding = false;
            if (outer != null)
                outer.OnEndDrag(eventData);
            if (inner != null)
                inner.enabled = true;
        }
    }
}
