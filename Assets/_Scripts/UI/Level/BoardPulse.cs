using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Тривога поля «мало місця» (документ §11) у стилі A1Breathe (`docs/StyleRef/A1Breathe/`):
    /// навколо поля й трохи всередині нього повільно дихає тепле сяйво від коралового до
    /// бурштинового. Попереджає, але не дратує.
    ///
    /// Шари (знизу вгору): зовнішнє сяйво під панеллю → поле → внутрішнє сяйво в межах поля →
    /// тонка рамка над усім. Для кожного рівня свої спрайти: «мало місця» (<see cref="DangerLevel.Warn"/>,
    /// calm) і «останній хід» (<see cref="DangerLevel.Strong"/>, critical). Кольори, товщини й
    /// розмиття запечені в спрайтах; тут — лише альфа.
    ///
    /// Дихання: a = мін + (макс − мін)·(0.5 − 0.5·cos(2π·t / період)), сяйва одного рівня дихають
    /// разом. Рамка не дихає — вона постійна, поки тривога є. Перехід між рівнями й згасання — за
    /// <see cref="DesignSystem.PulseFadeDuration"/>, без стрибка. Рівень дає Core
    /// (<see cref="BoardDanger"/>). Щокадрово чіпаємо тільки CanvasRenderer.SetAlpha.
    /// </summary>
    public sealed class BoardPulse : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image outerCalm;
        [SerializeField] private Image outerCritical;
        [SerializeField] private Image innerCalm;
        [SerializeField] private Image innerCritical;
        [SerializeField] private Image frame;

        private DangerLevel _level;
        private float _calm;
        private float _critical;
        private float _calmPhase;
        private float _criticalPhase;

        public DangerLevel Level => _level;

        public void Apply()
        {
            foreach (var image in new[] { outerCalm, outerCritical, innerCalm, innerCritical, frame })
            {
                if (image == null)
                    continue;
                image.color = Color.white; // колір — у спрайті
                image.raycastTarget = false;
            }
            if (!Application.isPlaying)
                Write();
        }

        /// <summary>Новий рівень: сяйво плавно переходить або гасне.</summary>
        public void SetLevel(DangerLevel level) => _level = level;

        /// <summary>Миттєво й на піку дихання (рестарт, екран програшу, знімок екрана).</summary>
        public void SetLevelImmediate(DangerLevel level)
        {
            _level = level;
            _calm = level == DangerLevel.Warn ? 1f : 0f;
            _critical = level == DangerLevel.Strong ? 1f : 0f;
            _calmPhase = CalmPeriod * 0.5f;
            _criticalPhase = CriticalPeriod * 0.5f;
            Write();
        }

        private float CalmPeriod => design != null ? Mathf.Max(design.PulseCalmPeriod, 0.2f) : 1.8f;
        private float CriticalPeriod => design != null ? Mathf.Max(design.PulseCriticalPeriod, 0.2f) : 0.9f;

        private void LateUpdate()
        {
            var fade = design != null ? Mathf.Max(design.PulseFadeDuration, 0.05f) : 0.3f;
            var step = Time.deltaTime / fade;
            _calm = Mathf.MoveTowards(_calm, _level == DangerLevel.Warn ? 1f : 0f, step);
            _critical = Mathf.MoveTowards(_critical, _level == DangerLevel.Strong ? 1f : 0f, step);
            _calmPhase = (_calmPhase + Time.deltaTime) % CalmPeriod;
            _criticalPhase = (_criticalPhase + Time.deltaTime) % CriticalPeriod;
            // Пишемо щокадру навіть у спокої: CanvasRenderer.SetAlpha живе лише до наступної
            // перебудови графіки, і вимкнений компонент лишив би сяйво після перерозкладки.
            Write();
        }

        private void Write()
        {
            var calm = _calm * Breath(_calmPhase, CalmPeriod);
            var critical = _critical * Breath(_criticalPhase, CriticalPeriod);
            SetAlpha(outerCalm, calm);
            SetAlpha(innerCalm, calm);
            SetAlpha(outerCritical, critical);
            SetAlpha(innerCritical, critical);
            // Рамка постійна (0.85 запечено в спрайті): у переході між рівнями сума ваг = 1, тож вона не блимає.
            SetAlpha(frame, Mathf.Min(1f, _calm + _critical));
        }

        private float Breath(float phase, float period)
        {
            var min = design != null ? design.PulseAlphaMin : 0.25f;
            var max = design != null ? design.PulseAlphaMax : 1f;
            return min + (max - min) * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * phase / period));
        }

        private static void SetAlpha(Image? image, float alpha)
        {
            if (image != null)
                image.canvasRenderer.SetAlpha(alpha);
        }
    }
}
