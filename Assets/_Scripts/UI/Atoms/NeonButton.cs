using System.Collections;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Кнопка макета: ТЕМНА скляна основа з ледь помітним кольоровим підтоном,
    /// тонка кольорова рамка (1 px), делікатне кольорове гало назовні, світлий текст
    /// і пружне натискання (.28 s, back-out).
    ///
    /// Саме так, а не «яскрава заливка + світла рамка»: у макеті картки читаються як
    /// підсвічене скло на космічному фоні — фон крізь них просвічує, а колір тону
    /// живе в рамці й гало, не в заливці.
    ///
    /// Підтон робимо градієнтом по вершинах (135° як у макеті) — нуль зайвих
    /// draw call і жодного нового матеріалу.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class NeonButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Tone
        {
            /// <summary>Головна дія: #FF2D8A → #9D4DFF.</summary>
            Primary,

            /// <summary>Спокійна дія: #00D9C0 → #3B7BFF.</summary>
            Cool,

            /// <summary>Нагорода/валюта: #FFB300 → #FF2D8A.</summary>
            Warm,

            /// <summary>Другорядна: скло без градієнта.</summary>
            Ghost
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Button button;
        [SerializeField] private GradientImage background;
        [SerializeField] private Image glow;
        [SerializeField] private Image innerStroke;
        [SerializeField] private TMP_Text label;

        [SerializeField] private Tone tone = Tone.Primary;

        [Tooltip("На скільки стискається кнопка під пальцем.")]
        [SerializeField, Range(0.8f, 1f)] private float pressScale = 0.94f;

        private Coroutine? _animation;
        private Vector3 _restScale = Vector3.one;

        public Button Button => button;

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        private void Awake() => _restScale = transform.localScale;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public void Apply()
        {
            if (design == null)
                return;

            var accent = ToneAccent(tone);
            var ppu = GlassPanel.PixelsPerUnitFor(design.RadiusButton);

            if (background != null)
            {
                // Темне скло з кольоровим підтоном, що згасає по діагоналі 135°.
                // Альфу беремо зі скла — фон має просвічувати.
                var tinted = Color.Lerp(design.GlassFill, accent, design.ButtonTintStrength);
                tinted.a = design.GlassFill.a;
                background.SetGradient(tinted, design.GlassFill);
                background.color = Color.white;
                background.pixelsPerUnitMultiplier = ppu;
            }

            if (glow != null)
            {
                var visible = tone != Tone.Ghost;
                glow.color = DesignSystem.WithAlpha(accent, visible ? design.GlowButtonAlpha : 0f);
                glow.gameObject.SetActive(visible);
            }

            if (innerStroke != null)
            {
                // Рамка — кольорова й тонка; у Ghost лишається нейтральне скло.
                innerStroke.color = tone == Tone.Ghost
                    ? design.GlassStroke
                    : DesignSystem.WithAlpha(accent, design.ButtonStrokeAlpha);
                innerStroke.pixelsPerUnitMultiplier = ppu;
            }

            if (label != null)
            {
                // Світлий текст на темному — не навпаки.
                label.color = design.TextPrimary;
                label.fontSize = design.FontSizeBody;
                if (design.Font != null)
                    label.font = design.Font;
            }
        }

        /// <summary>Колір тону: він живе в рамці й гало, а в заливці — лише як слабкий підтон.</summary>
        private Color ToneAccent(Tone value) => value switch
        {
            Tone.Cool => design.AccentTeal,
            Tone.Warm => design.AccentGold,
            Tone.Ghost => design.TextMuted,
            _ => design.AccentPrimary
        };

        public void OnPointerDown(PointerEventData eventData) => PlayScale(pressScale, instant: true);

        public void OnPointerUp(PointerEventData eventData) => PlayScale(1f, instant: false);

        private void PlayScale(float target, bool instant)
        {
            if (!isActiveAndEnabled)
                return;
            if (_animation != null)
                StopCoroutine(_animation);
            _animation = StartCoroutine(ScaleRoutine(target, instant));
        }

        private IEnumerator ScaleRoutine(float target, bool instant)
        {
            var from = transform.localScale;
            var to = _restScale * target;

            // Натискання має відгукуватись миттєво, відпускання — пружно (back-out).
            var duration = instant ? design.MotionPressDuration * 0.35f : design.MotionPressDuration;
            var curve = instant ? design.CurveEaseInOut : design.CurveBackOut;

            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.LerpUnclamped(from, to, curve.Evaluate(t / duration));
                yield return null;
            }

            transform.localScale = to;
            _animation = null;
        }
    }
}
