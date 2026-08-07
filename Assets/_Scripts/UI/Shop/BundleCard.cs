using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>Комплект: фарби плюс нафта, зі старою ціною й позначкою вигоди.</summary>
    public sealed class BundleCard : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text oldPriceLabel;
        [SerializeField] private GradientImage savingFill;
        [SerializeField] private TMP_Text savingLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private GradientImage buyFill;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private GradientImage[] colorDots = System.Array.Empty<GradientImage>();

        private ShopBundle? _bundle;
        private System.Action<ShopBundle>? _onBuy;

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(() =>
                {
                    if (_bundle != null)
                        _onBuy?.Invoke(_bundle);
                });
        }

        public void Bind(System.Action<ShopBundle> onBuy) => _onBuy = onBuy;

        public void Show(ShopBundle bundle)
        {
            _bundle = bundle;
            if (design == null)
                return;

            gameObject.SetActive(true);

            Apply(nameLabel, bundle.Name, design.FontSizeShopPrice, design.TextPrimary);
            Apply(descriptionLabel, bundle.Description, design.FontSizeSmall, design.TextMuted);
            Apply(savingLabel, bundle.Saving, design.FontSizeCaption, design.ShopOnLightText);
            Apply(priceLabel, bundle.Price, design.FontSizeShopCard, design.TextPrimary);

            if (oldPriceLabel != null)
            {
                // Стару ціну закреслюємо тегом TMP — окремого шрифту для цього не треба.
                oldPriceLabel.text = $"<s>{bundle.OldPrice}</s>";
                oldPriceLabel.fontSize = design.FontSizeSmall;
                oldPriceLabel.color = design.TextFaint;
                if (design.Font != null) oldPriceLabel.font = design.Font;
            }

            if (savingFill != null)
                savingFill.SetGradient(design.AccentLime, design.AccentLime);

            if (buyFill != null)
                buyFill.SetGradient(design.AccentGold, design.AccentPrimary);

            for (var i = 0; i < colorDots.Length; i++)
            {
                var dot = colorDots[i];
                if (dot == null)
                    continue;
                var used = i < bundle.Colors.Count;
                dot.gameObject.SetActive(used);
                if (used)
                    dot.SetGradient(
                        DesignSystem.Lighten(bundle.Colors[i].ToColor(), 0.5f),
                        DesignSystem.Darken(bundle.Colors[i].ToColor(), 0.28f));
            }
        }

        public void Release()
        {
            _bundle = null;
            gameObject.SetActive(false);
        }

        private void Apply(TMP_Text? label, string text, float size, Color color)
        {
            if (label == null)
                return;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
