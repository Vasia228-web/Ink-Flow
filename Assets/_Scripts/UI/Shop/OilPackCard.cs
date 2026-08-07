using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>Пакет нафти: крапля зростаючого розміру, кількість, ціна в грошах.</summary>
    public sealed class OilPackCard : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image cardStroke;
        [SerializeField] private Image hotGlow;
        [SerializeField] private RectTransform drop;
        [SerializeField] private GradientImage badgeFill;
        [SerializeField] private TMP_Text badgeLabel;
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private GradientImage buyFill;
        [SerializeField] private TMP_Text priceLabel;

        private OilPack? _pack;
        private System.Action<OilPack>? _onBuy;

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(() =>
                {
                    if (_pack != null)
                        _onBuy?.Invoke(_pack);
                });
        }

        public void Bind(System.Action<OilPack> onBuy) => _onBuy = onBuy;

        public void Show(OilPack pack, float mockupToReference)
        {
            _pack = pack;
            if (design == null)
                return;

            gameObject.SetActive(true);

            if (drop != null)
            {
                // Розмір краплі — головний сигнал «більший пакет»: 46 → 88 px макета.
                var size = pack.DropSize * mockupToReference;
                drop.sizeDelta = new Vector2(size, size * 1.08f);
            }

            if (amountLabel != null)
            {
                amountLabel.text = $"{pack.Amount:N0}".Replace(",", " ");
                amountLabel.fontSize = design.FontSizeSubtitle;
                amountLabel.color = design.TextPrimary;
                if (design.Font != null) amountLabel.font = design.Font;
            }

            if (priceLabel != null)
            {
                priceLabel.text = pack.Price;
                priceLabel.fontSize = design.FontSizeShopCard;
                priceLabel.color = design.TextPrimary;
                if (design.Font != null) priceLabel.font = design.Font;
            }

            if (buyFill != null)
                buyFill.SetGradient(design.AccentGold, design.AccentPrimary);

            var hasBadge = !string.IsNullOrEmpty(pack.Badge);
            if (badgeLabel != null)
            {
                badgeLabel.transform.parent.gameObject.SetActive(hasBadge);
                badgeLabel.text = pack.Badge ?? string.Empty;
                badgeLabel.fontSize = design.FontSizeCaption;
                // Теплий бейдж білим, холодний — темним: на лаймі білий не читається.
                badgeLabel.color = pack.Hot ? design.TextPrimary : design.ShopOnLightText;
                if (design.Font != null) badgeLabel.font = design.Font;
            }

            if (badgeFill != null)
                badgeFill.SetGradient(
                    pack.Hot ? design.AccentGold : design.AccentLime,
                    pack.Hot ? design.AccentPrimary : design.AccentTeal);

            if (cardStroke != null)
                cardStroke.color = new Color(1f, 1f, 1f, hasBadge ? 0.16f : 0.1f);

            if (hotGlow != null)
            {
                hotGlow.gameObject.SetActive(pack.Hot);
                if (pack.Hot)
                    hotGlow.color = DesignSystem.WithAlpha(design.AccentGold, design.ShopHotGlowAlpha);
            }
        }

        public void Release()
        {
            _pack = null;
            gameObject.SetActive(false);
        }
    }
}
