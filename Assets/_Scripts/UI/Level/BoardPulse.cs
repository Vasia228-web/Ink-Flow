using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Пульсація «мало місця» (документ §11): рамка поля м'яко пульсує теплим червоним —
    /// світіння по контуру панелі, цикл ~1–1.4 с. Нічого поза полем: без віньєтки й затемнення.
    /// Рівень дає Core (<see cref="BoardDanger"/>), тут — лише плавна поява, згасання й ритм.
    /// Щокадрово чіпаємо тільки CanvasRenderer.SetAlpha.
    /// </summary>
    public sealed class BoardPulse : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image glow;

        private DangerLevel _level;
        private float _intensity;
        private float _phase;

        public DangerLevel Level => _level;

        public void Apply()
        {
            if (design == null || glow == null)
                return;
            glow.color = design.PulseColor;
            glow.raycastTarget = false;
            if (!Application.isPlaying)
                glow.canvasRenderer.SetAlpha(0f);
        }

        /// <summary>Новий рівень: пульсація плавно з'являється або зникає.</summary>
        public void SetLevel(DangerLevel level) => _level = level;

        /// <summary>Миттєво (рестарт, екран програшу, знімок екрана).</summary>
        public void SetLevelImmediate(DangerLevel level)
        {
            _level = level;
            _intensity = level == DangerLevel.None ? 0f : 1f;
            _phase = 0f;
            Write(true);
        }

        private float TargetAlpha => _level switch
        {
            DangerLevel.Strong => design != null ? design.PulseStrongAlpha : 0.85f,
            DangerLevel.Warn => design != null ? design.PulseWarnAlpha : 0.45f,
            _ => 0f
        };

        private float Period => _level == DangerLevel.Strong
            ? (design != null ? design.PulseStrongPeriod : 1f)
            : (design != null ? design.PulseWarnPeriod : 1.4f);

        private void LateUpdate()
        {
            var target = _level == DangerLevel.None ? 0f : 1f;
            var fade = design != null ? Mathf.Max(design.PulseFadeDuration, 0.05f) : 0.35f;
            _intensity = Mathf.MoveTowards(_intensity, target, Time.deltaTime / fade);
            _phase += Time.deltaTime;
            // Пишемо щокадру навіть у спокої: CanvasRenderer.SetAlpha живе лише до наступної
            // перебудови графіки, і вимкнений компонент лишив би червону рамку після перерозкладки.
            Write(false);
        }

        private void Write(bool peak)
        {
            if (glow == null)
                return;
            var wave = peak ? 1f : 0.5f + 0.5f * Mathf.Sin(_phase / Mathf.Max(Period, 0.1f) * Mathf.PI * 2f - Mathf.PI * 0.5f);
            var floor = 0.35f; // навіть у «низу» хвилі рамка ледь світиться — це дихання, а не блимання
            glow.canvasRenderer.SetAlpha(TargetAlpha * _intensity * (floor + (1f - floor) * wave));
        }
    }
}
