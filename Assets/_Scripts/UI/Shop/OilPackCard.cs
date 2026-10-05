using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Пакет нафти (§13): крапля зростаючого розміру, кількість, кнопка з ціною зі стору.
    /// Поки ціни немає (стор ще відповідає або його немає взагалі) — кнопка неактивна з рискою:
    /// зашита ціна тут не з'являється ніколи.
    /// </summary>
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

        private System.Action<OilPack>? _onBuy;

        /// <summary>Пакет на картці; null — картка вільна.</summary>
        public OilPack? Pack { get; private set; }

        /// <summary>Напис на кнопці (тестам): ціна зі стору або риска.</summary>
        public string PriceText => priceLabel != null ? priceLabel.text : string.Empty;

        /// <summary>Чи можна натиснути «купити» (тестам).</summary>
        public bool CanBuy => buyButton != null && buyButton.interactable;

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(() =>
                {
                    if (Pack != null && CanBuy)
                        _onBuy?.Invoke(Pack);
                });
        }

        public void Bind(System.Action<OilPack> onBuy) => _onBuy = onBuy;

        /// <param name="dropSize">Діаметр краплі в одиницях канваса — з токенів за номером пакета.</param>
        public void Show(OilPack pack, float dropSize)
        {
            Pack = pack;
            if (design == null)
                return;

            gameObject.SetActive(true);

            if (drop != null)
                drop.sizeDelta = new Vector2(dropSize, dropSize * 1.08f);

            if (amountLabel != null)
            {
                amountLabel.text = InkFlow.Core.ScoreFormat.Full(pack.Amount);
                amountLabel.fontSize = design.FontSizeSubtitle;
                amountLabel.color = design.TextPrimary;
                if (design.Font != null) amountLabel.font = design.Font;
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

            SetPrice(null, false);
        }

        /// <summary>Ціна зі стору на кнопку; null — ціни немає, купити не можна.</summary>
        public void SetPrice(string? price, bool canBuy)
        {
            var known = price is { Length: > 0 };
            if (priceLabel != null)
            {
                priceLabel.text = known ? price! : "—";
                if (design != null)
                {
                    priceLabel.fontSize = design.FontSizeShopCard;
                    priceLabel.color = known && canBuy ? design.TextPrimary : design.TextDim;
                    if (design.Font != null) priceLabel.font = design.Font;
                }
            }
            if (buyButton != null)
                buyButton.interactable = known && canBuy;
            if (buyFill != null && design != null)
            {
                if (known && canBuy)
                    buyFill.SetGradient(design.AccentGold, design.AccentPrimary);
                else
                    buyFill.SetGradient(design.ButtonDisabledFill, design.ButtonDisabledFill);
            }
        }

        public void Release()
        {
            Pack = null;
            gameObject.SetActive(false);
        }
    }
}
