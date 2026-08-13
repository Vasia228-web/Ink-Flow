using System.Collections;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Squash &amp; stretch при натисканні — той самий відгук, що в карток режимів
    /// і кнопок, але для довільного елемента.
    ///
    /// Масштаб анімується через <c>localScale</c>: він не бруднить графіку, тож
    /// щокадрова зміна тут безпечна. Розмір (<c>sizeDelta</c>) для цього
    /// використовувати не можна — він шле OnRectTransformDimensionsChange.
    /// </summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private DesignSystem? design;

        private Coroutine? _tween;
        private float _scale = 1f;

        /// <summary>Підставляє збирач сцени — компонент додається в рантаймі.</summary>
        public void Bind(DesignSystem source) => design = source;

        public void OnPointerDown(PointerEventData eventData) => Press(true);

        public void OnPointerUp(PointerEventData eventData) => Press(false);

        private void Press(bool down)
        {
            if (design == null || !isActiveAndEnabled)
                return;

            if (_tween != null)
                StopCoroutine(_tween);
            _tween = StartCoroutine(PressRoutine(down));
        }

        private IEnumerator PressRoutine(bool down)
        {
            // Корутину запускає лише Press(), який уже перевірив design —
            // але компілятор про це не знає, а поле [SerializeField] може
            // стати порожнім і після старту.
            var style = design;
            if (style == null)
                yield break;

            var from = _scale;
            var to = down ? style.PressScale : 1f;
            var duration = down ? style.PressDownDuration : style.PressReleaseDuration;

            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var k = style.CurveBackOut.Evaluate(Mathf.Clamp01(t / duration));
                _scale = Mathf.LerpUnclamped(from, to, k);
                transform.localScale = Vector3.one * _scale;
                yield return null;
            }

            _scale = to;
            transform.localScale = Vector3.one * _scale;
            _tween = null;
        }
    }
}
