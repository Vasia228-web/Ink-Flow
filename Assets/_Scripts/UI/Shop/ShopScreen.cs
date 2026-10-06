using System.Collections.Generic;
using InkFlow.Meta;
using InkFlow.Platform;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Магазин — тільки нафта (майстер-док §13): один екран, чотири пакети з конфігу. Ціна на кнопці —
    /// локалізований рядок зі стору через <see cref="IIapService"/>, не з гри: стор знає валюту й податки
    /// гравця. Поки стор не відповів або його немає — кнопки неактивні з рискою, і екран каже чому.
    /// Покупка вдалась → нафта в гаманець і одразу у файл (<see cref="PlayerState.GrantPurchasedOil"/>).
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;

        [Header("Пакети")]
        [SerializeField] private TMP_Text promise;
        [SerializeField] private OilPackCard[] packs = System.Array.Empty<OilPackCard>();
        [SerializeField] private TMP_Text statusLabel;

        [Header("Мокові дані")]
        [SerializeField] private long mockOil = 1250;

        private readonly Dictionary<string, string> _prices = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private readonly List<string> _ids = new List<string>(4);

        private IIapService? _iap;
        private Wallet? _wallet;
        private bool _pending;
        private string _message = string.Empty;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        /// <summary>Тести: картки пакетів.</summary>
        public IReadOnlyList<OilPackCard> PreviewPacks => packs;

        /// <summary>Тести: рядок стану під пакетами.</summary>
        public string PreviewStatus => statusLabel != null && statusLabel.gameObject.activeSelf ? statusLabel.text : string.Empty;

        /// <summary>Тести: натиснути «купити» на картці, як пальцем.</summary>
        public void PreviewBuy(int index)
        {
            if (index >= 0 && index < packs.Length && packs[index] != null && packs[index].Pack != null)
                OnBuy(packs[index].Pack!);
        }
#endif

        /// <summary>Стор підставляє композиційний корінь; null — магазин без стору.</summary>
        public void BindServices(IIapService? iap)
        {
            _iap = iap;
            _prices.Clear();
        }

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            for (var i = 0; i < packs.Length; i++)
                packs[i]?.Bind(OnBuy);
        }

        private IReadOnlyList<OilPack> Packs => State?.OilPacks ?? EconomyData.Default.OilPacks;

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _wallet = State?.Wallet ?? new Wallet(mockOil);
            _message = string.Empty;
            Apply();
            RequestPrices();
        }

        public void Apply()
        {
            if (design == null)
                return;

            _wallet ??= State?.Wallet ?? new Wallet(mockOil);

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary, FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "МАГАЗИН";

            ApplyFont(promise, design.FontSizeCardSubtitle, design.TextMuted, FontStyles.Normal, 0f);
            if (promise != null)
                promise.text = "Нафта прискорює красу, а не силу:\nдомалювати картинку або продовжити забіг.";

            ApplyFont(statusLabel, design.FontSizeCaption, design.TextMuted, FontStyles.Bold, 0f);

            currency?.Bind(_wallet);
            currency?.Apply();

            var list = Packs;
            for (var i = 0; i < packs.Length; i++)
            {
                if (packs[i] == null)
                    continue;
                if (i < list.Count)
                    packs[i].Show(list[i], design.OilDropSize(i));
                else
                    packs[i].Release();
            }
            ApplyPrices();
        }

        /// <summary>Ціни — зі стору, асинхронно: кнопки оживають, коли стор відповів.</summary>
        private void RequestPrices()
        {
            if (_iap == null || !_iap.IsAvailable)
            {
                ApplyPrices();
                return;
            }

            _ids.Clear();
            var list = Packs;
            for (var i = 0; i < list.Count; i++)
                _ids.Add(list[i].Id);
            _iap.Query(_ids, products =>
            {
                if (this == null)
                    return;
                _prices.Clear();
                for (var i = 0; i < products.Count; i++)
                    _prices[products[i].Id] = products[i].LocalizedPrice;
                ApplyPrices();
            });
        }

        private void ApplyPrices()
        {
            var storeReady = _iap != null && _iap.IsAvailable;
            for (var i = 0; i < packs.Length; i++)
            {
                var card = packs[i];
                if (card == null || card.Pack == null)
                    continue;
                _prices.TryGetValue(card.Pack.Id, out var price);
                card.SetPrice(price, storeReady && !_pending);
            }

            if (statusLabel == null || design == null)
                return;
            string status;
            if (_message.Length > 0)
                status = _message;
            else if (_pending)
                status = "Купівля…";
            else if (!storeReady)
                status = "Магазин недоступний — спробуй пізніше";
            else if (_prices.Count == 0)
                status = "Дізнаємось ціни…";
            else
                status = string.Empty;
            statusLabel.text = status;
            Toggle(statusLabel, status.Length > 0);
        }

        private void OnBuy(OilPack pack)
        {
            if (_pending || _iap == null || !_iap.IsAvailable)
                return;
            _pending = true;
            _message = string.Empty;
            ApplyPrices();

            _iap.Buy(pack.Id, result =>
            {
                if (this == null)
                    return;
                _pending = false;
                if (!result.Success)
                {
                    // Відмова стору — словами, не мовчанням: гравець має знати, що гроші не списано.
                    _message = $"Купівля не вдалася: {result.Error ?? "стор відмовив"}";
                    ApplyPrices();
                    return;
                }

                // Стор підтвердив — нафта в гаманець і у файл. Без стану (майстерня) — лише в моковий гаманець.
                if (State != null)
                    State.GrantPurchasedOil(pack);
                else
                    _wallet?.Add(pack.Amount, RewardSource.Purchase);
                _message = $"+{InkFlow.Core.ScoreFormat.Full(pack.Amount)} нафти";
                currency?.Apply();
                ApplyPrices();
            });
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private void ApplyFont(TMP_Text? label, float size, Color color, FontStyles style, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
