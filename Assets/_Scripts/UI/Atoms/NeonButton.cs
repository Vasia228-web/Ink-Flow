using System.Collections;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Кнопка макета: градієнтна заливка, неонове гало, світлий внутрішній контур
    /// і пружне натискання (.28 s, back-out).
    ///
    /// Градієнт у макеті — `linear-gradient(135deg, …)`; в UGUI робимо його
    /// вершинними кольорами через VertexGradient на самому Image, без окремого шейдера.
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

        private void OnEnable() => Apply();

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif

        public void Apply()
        {
            if (design == null)
                return;

            var (from, to) = ToneColors(tone);

            if (background != null)
            {
                // 135° макета: зліва-вгору → вправо-вниз.
                background.SetGradient(from, to);
                background.color = Color.white;
                background.pixelsPerUnitMultiplier =
                    GlassPanel.PixelsPerUnitFor(design.RadiusButton);
            }

            if (glow != null)
            {
                glow.color = DesignSystem.WithAlpha(from, tone == Tone.Ghost ? 0f : design.GlowButtonAlpha);
                glow.gameObject.SetActive(tone != Tone.Ghost);
            }

            if (innerStroke != null)
            {
                // inset 0 0 0 2px rgba(255,255,255,.5) з макета.
                innerStroke.color = new Color(1f, 1f, 1f, tone == Tone.Ghost ? 0.09f : 0.5f);
                innerStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(design.RadiusButton);
            }

            if (label != null)
            {
                label.color = design.TextPrimary;
                label.fontSize = design.FontSizeBody;
                if (design.Font != null)
                    label.font = design.Font;
            }
        }

        private (Color from, Color to) ToneColors(Tone value) => value switch
        {
            Tone.Cool => (design.AccentTeal, design.AccentBlue),
            Tone.Warm => (design.AccentGold, design.AccentPrimary),
            Tone.Ghost => (design.GlassFill, design.GlassFill),
            _ => (design.AccentPrimary, design.AccentSecondary)
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
