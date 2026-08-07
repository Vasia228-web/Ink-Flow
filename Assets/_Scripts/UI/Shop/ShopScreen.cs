using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>Аргументи магазину: з якої вкладки відкрити.</summary>
    public sealed class ShopArgs : ScreenArgs
    {
        public ShopArgs() { }

        public ShopArgs(bool oilTab) => OilTab = oilTab;

        /// <summary>Відкрити одразу «Нафту» — сюди веде «Мало фарби» з фарбування.</summary>
        public bool OilTab { get; }
    }

    /// <summary>
    /// Магазин: дві вкладки — фарби й нафта.
    ///
    /// Картки створює бутстрап один раз, під час скролу не інстанціюється нічого.
    /// Асортимент невеликий (19 фарб), тож віртуалізація тут була б складністю без
    /// виграшу — важливо лише те, що скрол не створює об'єктів.
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;
        [SerializeField] private Button plusButton;

        [Header("Вкладки")]
        [SerializeField] private Button paintTabButton;
        [SerializeField] private Button oilTabButton;
        [SerializeField] private GradientImage paintTabFill;
        [SerializeField] private GradientImage oilTabFill;
        [SerializeField] private TMP_Text paintTabLabel;
        [SerializeField] private TMP_Text oilTabLabel;
        [SerializeField] private RectTransform paintTabRoot;
        [SerializeField] private RectTransform oilTabRoot;

        [Header("Фарба тижня")]
        [SerializeField] private GradientImage weeklyBackground;
        [SerializeField] private Image weeklyGlow;
        [SerializeField] private GradientImage weeklyDrop;
        [SerializeField] private TMP_Text weeklyKicker;
        [SerializeField] private TMP_Text weeklyName;
        [SerializeField] private TMP_Text weeklyOldPrice;
        [SerializeField] private TMP_Text weeklyPrice;
        [SerializeField] private TMP_Text weeklyTimer;
        [SerializeField] private Button weeklyBuyButton;
        [SerializeField] private GradientImage weeklyBuyFill;
        [SerializeField] private TMP_Text weeklyBuyLabel;

        [Header("Секції фарб")]
        [SerializeField] private TMP_Text[] sectionTitles = System.Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] sectionSubtitles = System.Array.Empty<TMP_Text>();
        [SerializeField] private PaintCard[] paintCards = System.Array.Empty<PaintCard>();

        [Header("Нафта")]
        [SerializeField] private TMP_Text oilPromise;
        [SerializeField] private OilPackCard[] oilPacks = System.Array.Empty<OilPackCard>();
        [SerializeField] private TMP_Text bundlesTitle;
        [SerializeField] private BundleCard[] bundles = System.Array.Empty<BundleCard>();

        [Header("Вибір кількості")]
        [SerializeField] private RectTransform quantitySheet;
        [SerializeField] private TMP_Text quantityTitle;
        [SerializeField] private Button[] quantityButtons = System.Array.Empty<Button>();
        [SerializeField] private GradientImage[] quantityFills = System.Array.Empty<GradientImage>();
        [SerializeField] private TMP_Text[] quantityLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] quantityTotals = System.Array.Empty<TMP_Text>();
        [SerializeField] private Button confirmButton;
        [SerializeField] private GradientImage confirmFill;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;

        [Header("Мокові дані")]
        [SerializeField] private long mockOil = 1250;

        private ShopCatalog? _catalog;
        private Wallet? _wallet;
        private bool _oilTab;
        private PaintProduct? _buying;
        private int _quantityIndex;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.Schedule(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (plusButton != null)
                plusButton.onClick.AddListener(() => SetTab(true));
            if (paintTabButton != null)
                paintTabButton.onClick.AddListener(() => SetTab(false));
            if (oilTabButton != null)
                oilTabButton.onClick.AddListener(() => SetTab(true));
            if (cancelButton != null)
                cancelButton.onClick.AddListener(CloseSheet);
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
            if (weeklyBuyButton != null)
                weeklyBuyButton.onClick.AddListener(OpenWeekly);

            for (var i = 0; i < quantityButtons.Length; i++)
            {
                var index = i;
                quantityButtons[i]?.onClick.AddListener(() => SelectQuantity(index));
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _oilTab = (args as ShopArgs)?.OilTab ?? false;
            _catalog = ShopCatalog.CreateMock();
            _wallet = new Wallet(mockOil);
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            _catalog ??= ShopCatalog.CreateMock();
            _wallet ??= new Wallet(mockOil);

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "МАГАЗИН";

            ApplyFont(paintTabLabel, design.FontSizeBody, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(oilTabLabel, design.FontSizeBody, design.TextPrimary, FontStyles.Bold, 0f);
            if (paintTabLabel != null) paintTabLabel.text = "Фарби";
            if (oilTabLabel != null) oilTabLabel.text = "Нафта";

            ApplyFont(oilPromise, design.FontSizeCardSubtitle, design.TextMuted, FontStyles.Normal, 0f);
            if (oilPromise != null)
                oilPromise.text = "Нафта прискорює красу, а не силу.\nНа проходження рівнів покупки не впливають.";

            ApplyFont(bundlesTitle, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (bundlesTitle != null) bundlesTitle.text = "Набори";

            ApplyFont(cancelLabel, design.FontSizeShopCard, design.TextMuted, FontStyles.Bold, 0f);
            if (cancelLabel != null) cancelLabel.text = "Скасувати";

            currency?.Bind(_wallet);
            currency?.Apply();

            ApplyTabs();
            ApplyWeekly();
            ApplySections();
            ApplyOilTab();
            CloseSheet();
        }

        private void SetTab(bool oil)
        {
            _oilTab = oil;
            ApplyTabs();
        }

        private void ApplyTabs()
        {
            if (design == null)
                return;

            Toggle(paintTabRoot, !_oilTab);
            Toggle(oilTabRoot, _oilTab);

            // Активна капсула — градієнт маджента → фіолет, неактивна прозора.
            if (paintTabFill != null)
                paintTabFill.SetGradient(
                    !_oilTab ? design.ShopTabActiveFrom : Color.clear,
                    !_oilTab ? design.ShopTabActiveTo : Color.clear);
            if (oilTabFill != null)
                oilTabFill.SetGradient(
                    _oilTab ? design.ShopTabActiveFrom : Color.clear,
                    _oilTab ? design.ShopTabActiveTo : Color.clear);

            if (paintTabLabel != null)
                paintTabLabel.color = !_oilTab ? design.TextPrimary : design.TextMuted;
            if (oilTabLabel != null)
                oilTabLabel.color = _oilTab ? design.TextPrimary : design.TextMuted;
        }

        private void ApplyWeekly()
        {
            if (_catalog == null || design == null)
                return;

            var offer = _catalog.Weekly;
            var paint = offer.Paint;

            if (weeklyBackground != null)
                weeklyBackground.SetGradient(
                    DesignSystem.WithAlpha(paint.Primary.ToColor(), design.ShopWeeklyTintFrom),
                    DesignSystem.WithAlpha(paint.Secondary.ToColor(), design.ShopWeeklyTintTo));

            if (weeklyGlow != null)
                weeklyGlow.color = DesignSystem.WithAlpha(paint.Primary.ToColor(), design.ShopWeeklyGlowAlpha);

            if (weeklyDrop != null)
                weeklyDrop.SetGradient(
                    DesignSystem.Lighten(paint.Primary.ToColor(), 0.5f),
                    DesignSystem.Darken(paint.Secondary.ToColor(), 0.28f));

            ApplyFont(weeklyKicker, design.FontSizeCaption, design.AccentTeal,
                FontStyles.Bold, design.LetterSpacingWide);
            if (weeklyKicker != null) weeklyKicker.text = "ФАРБА ТИЖНЯ";

            ApplyFont(weeklyName, design.FontSizeTitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (weeklyName != null) weeklyName.text = paint.Name;

            ApplyFont(weeklyOldPrice, design.FontSizeLabel, design.TextFaint, FontStyles.Normal, 0f);
            if (weeklyOldPrice != null) weeklyOldPrice.text = $"<s>{offer.OldPrice}</s>";

            ApplyFont(weeklyPrice, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (weeklyPrice != null) weeklyPrice.text = offer.Price.ToString();

            ApplyFont(weeklyTimer, design.FontSizeCaption, design.TextMuted, FontStyles.Normal, 0f);
            if (weeklyTimer != null) weeklyTimer.text = $"ще {offer.DaysLeft} дні";

            ApplyFont(weeklyBuyLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            if (weeklyBuyLabel != null) weeklyBuyLabel.text = "Купити";

            if (weeklyBuyFill != null)
                weeklyBuyFill.SetGradient(paint.Primary.ToColor(), paint.Secondary.ToColor());
        }

        /// <summary>
        /// Розкладає товари по вже створених картках. Порядок обходу той самий,
        /// що й у каталозі, тож картка №N завжди показує товар №N.
        /// </summary>
        private void ApplySections()
        {
            if (_catalog == null || _wallet == null)
                return;

            for (var i = 0; i < sectionTitles.Length; i++)
            {
                var used = i < _catalog.Sections.Count;
                Toggle(sectionTitles[i], used);
                if (i < sectionSubtitles.Length)
                    Toggle(sectionSubtitles[i], used);
                if (!used)
                    continue;

                ApplyFont(sectionTitles[i], design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
                sectionTitles[i].text = _catalog.Sections[i].Title;

                if (i >= sectionSubtitles.Length)
                    continue;
                ApplyFont(sectionSubtitles[i], design.FontSizeSmall, design.TextFaint, FontStyles.Normal, 0f);
                sectionSubtitles[i].text = _catalog.Sections[i].Subtitle;
            }

            var card = 0;
            for (var s = 0; s < _catalog.Sections.Count; s++)
            {
                var items = _catalog.Sections[s].Items;
                for (var i = 0; i < items.Count && card < paintCards.Length; i++, card++)
                    paintCards[card]?.Show(items[i], _wallet.OilDrops);
            }

            for (; card < paintCards.Length; card++)
                paintCards[card]?.Release();
        }

        private void ApplyOilTab()
        {
            if (_catalog == null)
                return;

            for (var i = 0; i < oilPacks.Length; i++)
            {
                if (oilPacks[i] == null)
                    continue;
                if (i < _catalog.OilPacks.Count)
                    oilPacks[i].Show(_catalog.OilPacks[i], DesignSystem.MockupToReference);
                else
                    oilPacks[i].Release();
            }

            for (var i = 0; i < bundles.Length; i++)
            {
                if (bundles[i] == null)
                    continue;
                if (i < _catalog.Bundles.Count)
                    bundles[i].Show(_catalog.Bundles[i]);
                else
                    bundles[i].Release();
            }
        }

        /// <summary>Прив'язує колбеки карток. Викликається бутстрапом і при вході.</summary>
        private void Start()
        {
            for (var i = 0; i < paintCards.Length; i++)
                paintCards[i]?.Bind(OpenSheet, _ => SetTab(true));
        }

        private void OpenWeekly()
        {
            if (_catalog != null)
                OpenSheet(_catalog.Weekly.Paint);
        }

        private void OpenSheet(PaintProduct product)
        {
            _buying = product;
            _quantityIndex = 0;
            Toggle(quantitySheet, true);
            RefreshSheet();
        }

        private void CloseSheet()
        {
            _buying = null;
            Toggle(quantitySheet, false);
        }

        private void SelectQuantity(int index)
        {
            _quantityIndex = index;
            RefreshSheet();
        }

        private void RefreshSheet()
        {
            if (_buying == null || _wallet == null || design == null)
                return;

            ApplyFont(quantityTitle, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (quantityTitle != null)
                quantityTitle.text = $"{_buying.Name} · {_buying.PricePerLiter} /л";

            var options = ShopCatalog.Quantities;
            for (var i = 0; i < quantityLabels.Length && i < options.Length; i++)
            {
                var selected = i == _quantityIndex;
                var total = ShopCatalog.PriceFor(_buying, options[i].Liters);

                ApplyFont(quantityLabels[i], design.FontSizeShopPrice, design.TextPrimary, FontStyles.Bold, 0f);
                quantityLabels[i].text = options[i].Badge == null
                    ? $"{options[i].Liters} л"
                    : $"{options[i].Liters} л  {options[i].Badge}";

                if (i < quantityTotals.Length)
                {
                    ApplyFont(quantityTotals[i], design.FontSizeSmall, design.TextMuted, FontStyles.Normal, 0f);
                    quantityTotals[i].text = total.ToString();
                }

                if (i < quantityFills.Length && quantityFills[i] != null)
                    quantityFills[i].SetGradient(
                        selected ? design.ShopTabActiveFrom : design.GlassFill,
                        selected ? design.ShopTabActiveTo : design.GlassFill);
            }

            var cost = ShopCatalog.PriceFor(_buying, options[_quantityIndex].Liters);
            var affordable = _wallet.OilDrops >= cost;

            ApplyFont(confirmLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (confirmLabel != null)
            {
                confirmLabel.text = affordable ? $"Купити · {cost}" : "Поповнити нафту";
                confirmLabel.color = affordable ? design.TextPrimary : design.PaintLowText;
            }

            if (confirmFill != null)
                confirmFill.SetGradient(
                    affordable ? design.AccentPrimary : design.PaintLowFill,
                    affordable ? design.AccentGold : design.PaintLowFill);
        }

        private void Confirm()
        {
            if (_buying == null || _catalog == null || _wallet == null)
                return;

            var liters = ShopCatalog.Quantities[_quantityIndex].Liters;
            if (!_catalog.Buy(_buying, liters, _wallet))
            {
                // Не вистачило — ведемо в «Нафту», а не мовчимо.
                CloseSheet();
                SetTab(true);
                return;
            }

            CloseSheet();
            ApplySections();
            ApplyWeekly();
            currency?.Apply();
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
