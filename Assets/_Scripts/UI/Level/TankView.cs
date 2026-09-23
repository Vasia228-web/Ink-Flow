using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Бак фарби (документ §4): вертикальна капсула, залита знизу вгору кольором
    /// свого пігменту, з числом рівня. Гравець ставить синю фігуру, зриває рядок —
    /// і бачить, як синє ллється в синій бак: зв'язок читається без пояснень.
    ///
    /// Повний бак = один виплеск змішувача: після ходу в трьох баках разом завжди менше,
    /// тож заливка не впирається в стелю. Анімується масштабом (localScale.y при півоті
    /// знизу), не розміром: щокадрова зміна sizeDelta просила б графіку на перебудову.
    /// </summary>
    public sealed class TankView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Pigment pigment = Pigment.Blue;
        [SerializeField] private Image track;
        [SerializeField] private Image stroke;
        [SerializeField] private Image fill;
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private TMP_Text number;

        private float _shown;
        private Coroutine? _tween;

        public Pigment Pigment => pigment;

        /// <summary>Звідки летить струмінь у змішувач.</summary>
        public RectTransform Rect => (RectTransform)transform;

        public void Apply()
        {
            if (design == null)
                return;

            if (track != null) track.color = design.TankTrackFill;
            if (stroke != null) stroke.color = design.TankTrackStroke;
            if (fill != null) fill.color = design.PigmentColor(pigment);

            if (number != null)
            {
                number.fontSize = design.FontSizeTankNumber;
                number.color = design.TextPrimary;
                number.fontStyle = FontStyles.Bold;
                if (design.Font != null)
                    number.font = design.Font;
            }

            if (fillRect != null)
                fillRect.localScale = new Vector3(1f, _shown, 1f);
        }

        /// <summary>Показує рівень. З анімацією — після ходу; без — при старті партії.</summary>
        public void Show(int level, int capacity, bool animate)
        {
            if (design == null)
                return;

            if (number != null)
                number.text = level.ToString();

            var target = capacity > 0 ? Mathf.Clamp01((float)level / capacity) : 0f;

            if (_tween != null)
            {
                StopCoroutine(_tween);
                _tween = null;
            }

            if (!animate || !isActiveAndEnabled || design.TankFillDuration <= 0f)
            {
                _shown = target;
                if (fillRect != null)
                    fillRect.localScale = new Vector3(1f, target, 1f);
                transform.localScale = Vector3.one;
                return;
            }

            _tween = StartCoroutine(FillRoutine(target, target > _shown + 0.001f));
        }

        private IEnumerator FillRoutine(float target, bool poured)
        {
            var from = _shown;
            var duration = design.TankFillDuration;
            var curve = design.CurveLand;
            var pulse = poured ? design.TankPourPulse : 0f;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                var eased = curve.Evaluate(k);
                _shown = Mathf.LerpUnclamped(from, target, eased);
                if (fillRect != null)
                    fillRect.localScale = new Vector3(1f, Mathf.Max(0f, _shown), 1f);
                // Бак ледь «здригається», коли в нього ллється, — і повертається.
                var bump = 1f + pulse * Mathf.Sin(k * Mathf.PI);
                transform.localScale = new Vector3(bump, 1f / bump, 1f);
                yield return null;
            }

            _shown = target;
            if (fillRect != null)
                fillRect.localScale = new Vector3(1f, target, 1f);
            transform.localScale = Vector3.one;
            _tween = null;
        }
    }
}
