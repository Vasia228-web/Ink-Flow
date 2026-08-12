using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картка режиму в хаб-меню: скляна плитка з іконкою, три рядки тексту й шеврон.
    ///
    /// Числа з макета (390 px → ×2.769): радіус 28→78, padding 16/18→44/50, gap 15→42,
    /// плитка 52→144 з радіусом 18→50, назва 22→61, підзаголовок 12.5→35, стат 12→33,
    /// шеврон 25→69. Заливка — градієнт 135° від rgba(accent,.18) до rgba(secondary,.08),
    /// рамка rgba(accent,.38), гало 0 0 26px rgba(accent,.2).
    /// </summary>
    public sealed class ModeCard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Tone
        {
            /// <summary>«Рівні»: маджента → фіолет.</summary>
            Levels,

            /// <summary>«Нескінченний»: бірюза → лайм.</summary>
            Endless
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Tone tone = Tone.Levels;

        [Header("Частини")]
        [SerializeField] private GradientImage background;
        [SerializeField] private Image stroke;
        [SerializeField] private Image glow;
        [SerializeField] private Image iconTileFill;
        [SerializeField] private Image iconTileStroke;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private TMP_Text statLabel;
        [SerializeField] private TMP_Text chevronLabel;

        private Vector3 _restScale = Vector3.one;
        private Coroutine? _press;
        private float _glowBoost = 1f;

        private void Awake() => _restScale = transform.localScale;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public void SetText(string title, string subtitle, string stat)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;
            if (statLabel != null) statLabel.text = stat;
        }

        public void Apply()
        {
            if (design == null)
                return;

            var (accent, secondary) = ToneColors(tone);
            var ppu = GlassPanel.PixelsPerUnitFor(design.RadiusModeCard);

            if (background != null)
            {
                // Темна скляна основа з ледь помітним кольоровим підтоном по діагоналі.
                // Саме основа, а не заливка кольором: крізь картку мають бути видні зорі.
                var from = Blend(design.CardBase, accent, design.CardTintStrong);
                var to = Blend(design.CardBase, secondary, design.CardTintWeak);
                background.SetGradient(from, to);
                background.color = Color.white;
                background.pixelsPerUnitMultiplier = ppu;
            }

            if (stroke != null)
            {
                // Обведення — НЕЙТРАЛЬНЕ скляне, як у решти панелей, а не акцентне.
                // Акцентний контур замикався навколо картки суцільним яскравим
                // кільцем: на темному фоні тонка лінія повного насиченого тону
                // читається як обведення в редакторі, скільки їй не знижуй альфу.
                // Колір картці дають гало, плитка іконки й підпис — цього досить.
                stroke.color = DesignSystem.WithAlpha(design.GlassStroke, design.CardStrokeAlpha);
                stroke.pixelsPerUnitMultiplier = ppu;
            }

            if (glow != null)
            {
                // Базовий колір — тут, один раз. Спалах при натисканні йде через
                // CanvasRenderer.SetAlpha, бо Image.color щокадру бруднить графіку.
                glow.color = DesignSystem.WithAlpha(accent, design.CardGlowAlpha);
                glow.canvasRenderer.SetAlpha(_glowBoost);
            }

            if (iconTileFill != null)
            {
                iconTileFill.color = design.GlassFill;
                iconTileFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(design.RadiusMedium);
            }

            if (iconTileStroke != null)
            {
                iconTileStroke.color = design.GlassStroke;
                iconTileStroke.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(design.RadiusMedium);
            }

            ApplyFont(titleLabel, design.FontSizeTitle, design.TextPrimary, 0f);
            ApplyFont(subtitleLabel, design.FontSizeCardSubtitle, design.TextMuted, 0f);
            // Рядок статистики — освітлений акцент картки (#FF2D8A → #FF9ECB).
            ApplyFont(statLabel, design.FontSizeLabel, design.StatText(accent), design.LetterSpacingTight);
            ApplyFont(chevronLabel, design.FontSizeSubtitle * 1.47f, design.TextDim, 0f);
        }

        private void ApplyFont(TMP_Text? label, float size, Color color, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }

        /// <summary>Підмішує колір у скляну основу, НЕ чіпаючи її прозорість.</summary>
        private static Color Blend(Color glass, Color tone, float amount)
        {
            var mixed = Color.Lerp(glass, tone, amount);
            mixed.a = glass.a;
            return mixed;
        }

        private (Color accent, Color secondary) ToneColors(Tone value) => value switch
        {
            Tone.Endless => (design.AccentTeal, design.AccentLime),
            _ => (design.AccentPrimary, design.AccentSecondary)
        };

        /// <summary>Картку натиснули. Куди вона веде — вирішує не вона.</summary>
        public System.Action? Clicked;

        public void OnPointerDown(PointerEventData eventData) => Press(true);

        public void OnPointerUp(PointerEventData eventData)
        {
            Press(false);
            Clicked?.Invoke();
        }

        /// <summary>Стискання під пальцем і пружне повернення з overshoot.</summary>
        private void Press(bool down)
        {
            if (design == null || !isActiveAndEnabled)
                return;
            if (_press != null)
                StopCoroutine(_press);
            _press = StartCoroutine(PressRoutine(down));
        }

        private System.Collections.IEnumerator PressRoutine(bool down)
        {
            var from = transform.localScale;
            var to = down ? _restScale * design.PressScale : _restScale;
            var glowFrom = _glowBoost;
            var glowTo = down ? design.CardGlowPressBoost : 1f;

            // Стискання різке, повернення пружне — рука має відчути опір, а не желе.
            var duration = down ? design.PressDownDuration : design.PressReleaseDuration;
            var curve = down ? design.CurveEaseInOut : design.CurveBackOut;

            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var k = curve.Evaluate(t / duration);
                transform.localScale = Vector3.LerpUnclamped(from, to, k);
                _glowBoost = Mathf.LerpUnclamped(glowFrom, glowTo, k);
                UpdateGlow();
                yield return null;
            }

            transform.localScale = to;
            _glowBoost = glowTo;
            UpdateGlow();
            _press = null;
        }

        /// <summary>Спалах гало під пальцем. Лише альфа CanvasRenderer — жодного
        /// дотику до Image.color, інакше кожен кадр натискання бруднив би графіку.</summary>
        private void UpdateGlow()
        {
            if (glow == null || design == null)
                return;
            glow.canvasRenderer.SetAlpha(_glowBoost);
        }
    }
}
