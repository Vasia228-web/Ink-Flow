using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Крапля чорнила як UI-атом: тінт із палітри, глянцевий відблиск, число густоти,
    /// «дихання» в спокої та squash &amp; stretch при тапі.
    ///
    /// Таймінги й амплітуди — з макета через DesignSystem:
    ///  • погойдування: 5.5 s ease-in-out, scale ±3%, нахил ±2°;
    ///  • приземлення: .47 s, .7 → 1.25/.82 → .92/1.12 → 1;
    ///  • near-miss: 1.15 s, гало 3 px → 14 px.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DropView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private DesignSystem design;

        [Tooltip("Тіло краплі — circle-soft, фарбується кольором чорнила.")]
        [SerializeField] private Image body;

        [Tooltip("Відблиск — circle-gloss, завжди білий, змінюється лише прозорість.")]
        [SerializeField] private Image gloss;

        [Tooltip("Гало під краплею — circle-soft, колір чорнила з низькою альфою.")]
        [SerializeField] private Image glow;

        [SerializeField] private TMP_Text densityLabel;

        [SerializeField] private InkColor ink = InkColor.Magenta;
        [SerializeField, Min(0)] private int density = 1;

        [Tooltip("Погойдування в спокої. Вимикати для щільних списків — це Update на кожну краплю.")]
        [SerializeField] private bool idleWobble = true;

        private Coroutine? _squash;
        private float _phase;
        private bool _nearMiss;

        public InkColor Ink => ink;

        private void Awake() =>
            // Розводимо фази, щоб краплі не «дихали» синхронно, як метроном.
            _phase = Random.value * Mathf.PI * 2f;

        private void OnEnable() => Apply();

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif

        /// <summary>Задає колір і густоту краплі.</summary>
        public void Show(InkColor color, int value)
        {
            ink = color;
            density = value;
            Apply();
        }

        /// <summary>Крапля на порозі вибуху: пульсуюче гало (near-miss із макета).</summary>
        public void SetNearMiss(bool active) => _nearMiss = active;

        public void Apply()
        {
            if (design == null)
                return;

            var baseColor = design.Ink(ink);

            if (body != null)
                body.color = baseColor;

            if (gloss != null)
                gloss.color = new Color(1f, 1f, 1f, design.DropGlossAlpha);

            if (glow != null)
                glow.color = design.InkGlow(ink);

            if (densityLabel != null)
            {
                densityLabel.text = density > 0 ? density.ToString() : string.Empty;
                densityLabel.color = design.TextPrimary;
                if (design.Font != null)
                    densityLabel.font = design.Font;
            }
        }

        private void Update()
        {
            if (design == null || _squash != null)
                return;

            var scale = Vector3.one;
            var tilt = 0f;

            if (idleWobble)
            {
                // Одна синусоїда керує і масштабом, і нахилом — рух виходить злитим,
                // як у макеті, де це один keyframe-цикл.
                var t = Mathf.Sin((Time.time / design.MotionWobbleDuration + _phase) * Mathf.PI * 2f);
                var amp = design.MotionWobbleScale;
                scale = new Vector3(1f + amp * t, 1f - amp * t, 1f);
                tilt = design.MotionWobbleTilt * t;
            }

            if (_nearMiss && glow != null)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time / design.MotionNearMissDuration * Mathf.PI * 2f);
                var min = design.GlowNearMissMin / design.GlowNearMissMax;
                var size = Mathf.Lerp(min, 1f, pulse);
                glow.transform.localScale = Vector3.one * (1.15f + 0.45f * size);
                glow.color = DesignSystem.WithAlpha(design.Ink(ink), design.DropShadowAlpha * (0.5f + 0.5f * pulse));
                scale *= Mathf.Lerp(1f, 1.08f, pulse);
            }

            transform.localScale = scale;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        public void OnPointerClick(PointerEventData eventData) => PlayLand();

        /// <summary>Squash &amp; stretch: крапля пружно «сідає» на місце.</summary>
        public void PlayLand()
        {
            if (!isActiveAndEnabled || design == null)
                return;
            if (_squash != null)
                StopCoroutine(_squash);
            _squash = StartCoroutine(LandRoutine());
        }

        private IEnumerator LandRoutine()
        {
            var duration = design.MotionLandDuration;
            var curve = design.CurveLand;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = curve.Evaluate(t / duration);
                // Об'єм зберігається: розтягнення по X = стиснення по Y.
                var squash = 1f + (k - 1f) * 0.6f;
                transform.localScale = new Vector3(k, squash <= 0f ? 0.01f : 1f / Mathf.Max(0.35f, k) * squash, 1f);
                yield return null;
            }

            transform.localScale = Vector3.one;
            _squash = null;
        }
    }
}
