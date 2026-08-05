using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Скляна панель макета: напівпрозора заливка + тонкий внутрішній контур.
    /// У макеті це `rgba(255,255,255,.05)` + `inset 0 0 0 1px rgba(255,255,255,.09)`.
    ///
    /// Справжнього backdrop-blur не робимо: на мобільних це повноекранний grab-pass,
    /// а бюджет — 60 fps на iPhone SE 2 (§12). Ефект «скла» тримається на заливці,
    /// контурі й космічному фоні під ним — на темному тлі різниця непомітна.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GlassPanel : MonoBehaviour
    {
        public enum Corner
        {
            Small,
            Medium,
            Button,
            Card,
            Sheet
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Image fill;
        [SerializeField] private Image stroke;

        [Tooltip("Який радіус із дизайн-системи використати.")]
        [SerializeField] private Corner corner = Corner.Card;

        [Tooltip("Підвищена заливка для активних/виділених панелей.")]
        [SerializeField] private bool raised;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.Schedule(this, Apply);
#endif

        /// <summary>Перечитує значення з дизайн-системи. Викликається і в редакторі.</summary>
        public void Apply()
        {
            if (design == null)
                return;

            if (fill != null)
            {
                fill.color = raised ? design.GlassFillRaised : design.GlassFill;
                fill.pixelsPerUnitMultiplier = PixelsPerUnitFor(RadiusFor(corner));
            }

            if (stroke != null)
            {
                stroke.color = design.GlassStroke;
                stroke.pixelsPerUnitMultiplier = PixelsPerUnitFor(RadiusFor(corner));
            }
        }

        public float RadiusFor(Corner value) => value switch
        {
            Corner.Small => design.RadiusSmall,
            Corner.Medium => design.RadiusMedium,
            Corner.Button => design.RadiusButton,
            Corner.Sheet => design.RadiusSheet,
            _ => design.RadiusCard
        };

        /// <summary>
        /// 9-slice спрайт rounded-rect має кут 40 px при 128 px текстури.
        /// pixelsPerUnitMultiplier масштабує border так, щоб отримати потрібний радіус:
        /// саме через це поле радіус стає керованим, а не «зашитим у картинку».
        /// </summary>
        public static float PixelsPerUnitFor(float radiusInReferenceUnits)
        {
            const float spriteCornerPx = 40f;
            const float spritePixelsPerUnit = 128f;
            if (radiusInReferenceUnits <= 0f)
                return 1f;
            return spritePixelsPerUnit * (spriteCornerPx / spritePixelsPerUnit) / radiusInReferenceUnits;
        }
    }
}
