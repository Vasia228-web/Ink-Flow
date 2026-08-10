using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Головний екран гри (хаб-меню). Розкладка зібрана за розміткою макета
    /// «Ink Flow v2» (полотно 390×844) з коефіцієнтом ×2.769 у reference 1080×1920.
    ///
    /// Прив'язки: шапка до верху SafeArea, навігація до низу SafeArea, а лого з картками
    /// центруються в проміжку між ними. Тому на вищому екрані розтягується порожній
    /// простір посередині, а не «їде» верстка.
    ///
    /// Дані поки мокові — екран нічого не знає про Meta, окрім гаманця.
    /// </summary>
    public sealed class HubScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private DropView avatar;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text playerTitle;
        [SerializeField] private CurrencyWidget currency;

        [Header("Лого")]
        [SerializeField] private TMP_Text logo;
        [SerializeField] private TMP_Text tagline;

        [Header("Режими")]
        [SerializeField] private ModeCard levelsCard;
        [SerializeField] private ModeCard endlessCard;

        [Header("Навігація")]
        [SerializeField] private NavBar navBar;

        [Header("Мокові дані")]
        [SerializeField] private string mockName = "Нова";
        [SerializeField] private string mockTitle = "Художниця галактик";
        [SerializeField] private long mockOil = 1250;
        [SerializeField] private int mockLevel = 12;
        [SerializeField] private int mockStars = 27;
        [SerializeField] private int mockRecord = 8420;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.Schedule(this, Apply);
#endif

        /// <summary>Картка «Рівні».</summary>
        public System.Action? LevelsRequested;

        /// <summary>Картка «Нескінченний».</summary>
        public System.Action? EndlessRequested;

        /// <summary>Вкладка нижньої навігації: galaxy / shop / ranks / profile.</summary>
        public System.Action<string>? TabRequested;

        private void Awake()
        {
            // Хаб не знає, куди ведуть його картки — лише повідомляє, що їх натиснули.
            if (levelsCard != null)
                levelsCard.Clicked += () => LevelsRequested?.Invoke();
            if (endlessCard != null)
                endlessCard.Clicked += () => EndlessRequested?.Invoke();
            if (navBar != null)
                navBar.TabSelected += id => TabRequested?.Invoke(id);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Apply();
        }

        /// <summary>Підв'язати справжній гаманець замість мокових даних.</summary>
        public void Bind(Wallet wallet) => currency?.Bind(wallet);

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(playerName, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold);
            ApplyFont(playerTitle, design.FontSizeSmall, design.TextFaint, FontStyles.Normal);
            if (playerName != null) playerName.text = mockName;
            if (playerTitle != null) playerTitle.text = mockTitle;

            if (logo != null)
            {
                logo.fontSize = design.FontSizeLogo;
                logo.lineSpacing = -10f; // line-height .9 макета
                logo.characterSpacing = 1f; // letter-spacing .01em
                // Два кольори одним написом — rich text дешевший за два об'єкти.
                logo.text =
                    $"<color=#{ColorUtility.ToHtmlStringRGB(design.AccentPrimary)}>Ink</color> " +
                    $"<color=#{ColorUtility.ToHtmlStringRGB(design.AccentTeal)}>Flow</color>";
                if (design.Font != null)
                    logo.font = design.Font;
            }

            if (tagline != null)
            {
                tagline.text = "ФАРБУЙ ГАЛАКТИКУ";
                tagline.fontSize = design.FontSizeLabel;
                tagline.color = new Color(1f, 1f, 1f, 0.4f);
                tagline.characterSpacing = design.LetterSpacingTagline;
                tagline.fontStyle = FontStyles.Normal;
                if (design.Font != null)
                    tagline.font = design.Font;
            }

            // ★ немає в Nunito, тож підставляємо іконку тегом. Голий символ не годиться:
            // TMP шукає відсутні гліфи лише у fallback-ШРИФТАХ, а не у спрайт-асеті,
            // і замінює їх на порожній квадрат.
            levelsCard?.SetText("Рівні", "Розчисти сітку",
                $"Рівень {mockLevel} · <sprite name=\"star\"> {mockStars}");
            endlessCard?.SetText("Нескінченний", "Набирай рекорд", $"Рекорд · {mockRecord:N0}");
            levelsCard?.Apply();
            endlessCard?.Apply();

            avatar?.Apply();
            currency?.SetPreviewAmount(mockOil);
            currency?.Apply();
            navBar?.Apply();
        }

        private void ApplyFont(TMP_Text? label, float size, Color color, FontStyles style)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
