using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Зразок фарби в нижній панелі: крапля кольору, назва, мензурка із залишком.
    ///
    /// Обрана піднімається й світиться; та, якої не вистачає на поточну зону,
    /// притлумлена — але клікабельна. Заблокувати її означало б сховати від гравця
    /// сам факт, що фарба існує.
    /// </summary>
    public sealed class PaintSwatch : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private PaintKind kind;

        [SerializeField] private Button button;
        [SerializeField] private RectTransform lift;
        [SerializeField] private Image selectionPanel;
        [SerializeField] private GradientImage drop;
        [SerializeField] private Image gloss;
        [SerializeField] private Image dropRing;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private RectTransform beakerFill;
        [SerializeField] private TMP_Text litersLabel;

        private float _beakerWidth;
        private System.Action<PaintKind>? _onPick;

        public PaintKind Kind => kind;

        private void Awake()
        {
            if (beakerFill != null)
                _beakerWidth = beakerFill.sizeDelta.x;
            if (button != null)
                button.onClick.AddListener(() => _onPick?.Invoke(kind));
        }

        public void Bind(System.Action<PaintKind> onPick) => _onPick = onPick;

        /// <summary>
        /// Перечитує вигляд. Викликається на зміну вибору чи запасу — не щокадру,
        /// тому тут можна вільно чіпати кольори й розміри.
        /// </summary>
        public void Refresh(float liters, bool selected, bool affordable)
        {
            if (design == null)
                return;

            var color = design.Paint(kind);

            if (nameLabel != null)
            {
                nameLabel.text = design.PaintName(kind);
                nameLabel.fontSize = design.FontSizeLabel;
                nameLabel.color = selected ? design.TextPrimary : design.TextDim;
                if (design.Font != null)
                    nameLabel.font = design.Font;
            }

            if (litersLabel != null)
            {
                // Ціле показуємо без «.0»: «6 л» читається, «6.0 л» — ні.
                litersLabel.text = $"{liters:0.#} л";
                litersLabel.fontSize = design.FontSizeCaption;
                litersLabel.color = design.TextDim;
                if (design.Font != null)
                    litersLabel.font = design.Font;
            }

            if (drop != null)
                drop.SetGradient(DesignSystem.Lighten(color, 0.5f), DesignSystem.Darken(color, 0.28f));

            if (gloss != null)
                gloss.color = new Color(1f, 1f, 1f, 0.6f);

            // Білий обідок — тільки на обраній: у макеті це єдина ознака вибору
            // на самій краплі.
            if (dropRing != null)
                dropRing.gameObject.SetActive(selected);

            if (selectionPanel != null)
            {
                selectionPanel.gameObject.SetActive(selected);
                selectionPanel.color = design.GlassFillRaised;
            }

            if (lift != null)
                lift.localPosition = new Vector3(0f, selected ? design.PaintSwatchLift : 0f, 0f);

            if (beakerFill != null)
            {
                // Мензурка повна на 10 л — далі шкала просто впирається.
                var k = Mathf.Clamp01(liters / design.PaintBeakerFullLiters);
                beakerFill.sizeDelta = new Vector2(_beakerWidth * k, beakerFill.sizeDelta.y);
                if (beakerFill.TryGetComponent<Image>(out var fill))
                    fill.color = color;
            }

            // Не вистачає — гасимо всю картку, але кнопку лишаємо живою.
            var alpha = affordable ? 1f : design.PaintUnaffordableAlpha;
            var group = GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = alpha;
        }
    }
}
