using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картка фарби в магазині.
    ///
    /// Мензурка справа — не декор, а головний індикатор: із неї гравець дізнається,
    /// скільки фарби лишилось, ще не читаючи цифр. Порожня стає тьмяною й з тоншою
    /// обводкою, щоб «нуль» читався навіть боковим зором.
    /// </summary>
    public sealed class PaintCard : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;

        [Header("Картка")]
        [SerializeField] private Image cardStroke;
        [SerializeField] private Image specialGlow;

        [Header("Мензурка")]
        [Tooltip("Спільний компонент — той самий, що в палітрі профілю.")]
        [SerializeField] private BeakerGauge beaker;

        [Header("Вміст")]
        [SerializeField] private GradientImage drop;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text ownedLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text priceUnitLabel;

        [Header("Кнопка")]
        [SerializeField] private Button buyButton;
        [SerializeField] private GradientImage buyFill;
        [SerializeField] private Image buyStroke;
        [SerializeField] private TMP_Text buyLabel;

        private System.Action<PaintProduct>? _onBuy;
        private System.Action<PaintProduct>? _onTopUp;
        private PaintProduct? _product;

        /// <summary>Товар, який показує ця картка. null — картка вимкнена.</summary>
        public PaintProduct? Product => _product;

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(OnClicked);
        }

        public void Bind(System.Action<PaintProduct> onBuy, System.Action<PaintProduct> onTopUp)
        {
            _onBuy = onBuy;
            _onTopUp = onTopUp;
        }

        private void OnClicked()
        {
            if (_product == null)
                return;
            // Не вистачає нафти — кнопка веде в таб «Нафта», а не блокується:
            // глухий тап тут читався б як поламана кнопка.
            if (_affordable)
                _onBuy?.Invoke(_product);
            else
                _onTopUp?.Invoke(_product);
        }

        private bool _affordable = true;

        public void Release()
        {
            _product = null;
            gameObject.SetActive(false);
        }

        /// <summary>Показує товар. Не щокадрова операція — тут можна чіпати кольори.</summary>
        public void Show(PaintProduct product, long oil)
        {
            _product = product;
            _affordable = oil >= product.PricePerLiter;

            if (design == null)
                return;

            gameObject.SetActive(true);

            if (drop != null)
                drop.SetGradient(
                    DesignSystem.Lighten(product.Primary.ToColor(), 0.5f),
                    DesignSystem.Darken(product.Secondary.ToColor(), 0.28f));

            if (nameLabel != null)
            {
                nameLabel.text = product.Name;
                nameLabel.fontSize = design.FontSizeShopCard;
                nameLabel.color = design.TextPrimary;
                if (design.Font != null) nameLabel.font = design.Font;
            }

            if (ownedLabel != null)
            {
                ownedLabel.text = product.IsEmpty ? "порожньо" : $"{product.OwnedLiters:0.#} л";
                ownedLabel.fontSize = design.FontSizeSmall;
                ownedLabel.color = product.IsEmpty ? design.TextFaintest : design.TextMuted;
                if (design.Font != null) ownedLabel.font = design.Font;
            }

            if (priceLabel != null)
            {
                priceLabel.text = product.PricePerLiter.ToString();
                priceLabel.fontSize = design.FontSizeShopPrice;
                // Не вистачає — ціна м'яко-червона. Саме ціна, а не вся картка:
                // товар лишається читабельним.
                priceLabel.color = _affordable ? design.TextPrimary : design.ShopUnaffordablePrice;
                if (design.Font != null) priceLabel.font = design.Font;
            }

            if (priceUnitLabel != null)
            {
                priceUnitLabel.text = "/л";
                priceUnitLabel.fontSize = design.FontSizeCaption;
                priceUnitLabel.color = _affordable ? design.TextMuted : design.ShopUnaffordablePrice;
                if (design.Font != null) priceUnitLabel.font = design.Font;
            }

            beaker?.Show(product.OwnedLiters, product.Primary.ToColor());
            ApplyCard(product);
            ApplyBuyButton(product);
        }

        private void ApplyCard(PaintProduct product)
        {
            if (cardStroke != null)
                cardStroke.color = new Color(1f, 1f, 1f, product.IsSpecial ? 0.22f : 0.1f);

            // Спец-ефекти світяться в тон — це вся їхня різниця в сітці.
            if (specialGlow == null)
                return;
            specialGlow.gameObject.SetActive(product.IsSpecial);
            if (product.IsSpecial)
                specialGlow.color = DesignSystem.WithAlpha(product.Primary.ToColor(), design.ShopSpecialGlowAlpha);
        }

        private void ApplyBuyButton(PaintProduct product)
        {
            if (buyLabel != null)
            {
                buyLabel.text = _affordable ? "Купити" : "Поповнити";
                buyLabel.fontSize = design.FontSizeShopCard;
                buyLabel.color = _affordable ? OnColor(product.Primary.ToColor()) : design.PaintLowText;
                if (design.Font != null) buyLabel.font = design.Font;
            }

            if (buyFill != null)
            {
                if (_affordable)
                    buyFill.SetGradient(product.Primary.ToColor(), product.Secondary.ToColor());
                else
                    buyFill.SetGradient(design.PaintLowFill, design.PaintLowFill);
                buyFill.color = Color.white;
            }

            if (buyStroke != null)
            {
                buyStroke.gameObject.SetActive(!_affordable);
                buyStroke.color = design.PaintLowStroke;
            }
        }

        /// <summary>
        /// Темний напис на світлій заливці. На «Персику» чи «М'яті» білий текст
        /// просто зникає.
        /// </summary>
        private Color OnColor(Color background)
        {
            var luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return luma > 0.647f ? design.ShopOnLightText : design.TextPrimary;
        }
    }
}
