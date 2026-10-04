using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Мензурка-індикатор залишку фарби: вертикальна капсула, залита знизу вгору
    /// пропорційно до літрів.
    ///
    /// Один компонент на весь проєкт — його показує і картка магазину, і палітра
    /// профілю. Дві копії розійшлися б на першій же правці порогу «порожньо».
    ///
    /// Порожня має і тьмянішу заливку, і тоншу обводку: «нуль» мусить читатись
    /// боковим зором, ще до цифр.
    /// </summary>
    public sealed class BeakerGauge : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image background;
        [SerializeField] private Image stroke;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;

        [Tooltip("За скільки літрів мензурка повна. Порожнє поле — беремо з DesignSystem.")]
        [SerializeField, Min(0f)] private float capacityOverride;

        public void Show(float liters, Color color)
        {
            if (design == null)
                return;

            var capacity = capacityOverride > 0.01f ? capacityOverride : InkFlow.Meta.ShopCatalog.BeakerCapacity;
            var filled = liters > 0.001f;

            if (background != null)
                background.color = new Color(1f, 1f, 1f, filled ? 0.09f : 0.05f);
            if (stroke != null)
                stroke.color = new Color(1f, 1f, 1f, filled ? 0.3f : 0.12f);

            if (fill == null || fill.parent is not RectTransform tube)
                return;

            // Повну висоту міряємо щоразу з колби. Кеш тут неправильний двічі:
            // у Edit Mode Awake не виконується, а зняти висоту з самої заливки
            // не можна — вона вже стиснута попереднім показом.
            var k = Mathf.Clamp01(liters / capacity);
            var inset = design.ShopBeakerInset;
            fill.sizeDelta = new Vector2(
                fill.sizeDelta.x,
                Mathf.Max(0f, (tube.rect.height - inset * 2f) * k));

            if (fillImage != null)
                fillImage.color = color;
        }
    }
}
