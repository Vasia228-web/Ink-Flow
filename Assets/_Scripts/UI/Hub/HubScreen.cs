using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        /// <summary>Увесь блок «аватар + нік» — кнопка в Профіль.</summary>
        [SerializeField] private Button profileButton;
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
        // Мокові числа лишаються ТІЛЬКИ для сцени-майстерні Hub.unity: там
        // стану гравця немає, а порожній екран нічого не показав би про
        // розкладку. У грі всі вони перекриваються реальними.
        [SerializeField] private string mockName = "Нова";
        [SerializeField] private long mockOil = 1250;
        [SerializeField] private int mockLevel = 12;
        [SerializeField] private int mockStars = 27;
        [SerializeField] private int mockRecord = 8420;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        /// <summary>Картка «Рівні».</summary>
        public System.Action? LevelsRequested;

        /// <summary>Картка «Нескінченний».</summary>
        public System.Action? EndlessRequested;

        /// <summary>Вкладка нижньої навігації: galaxy / shop / ranks / profile.</summary>
        public System.Action<string>? TabRequested;

        /// <summary>Тап на блок профілю в шапці.</summary>
        public System.Action? ProfileRequested;

        private void Awake()
        {
            // Хаб не знає, куди ведуть його картки — лише повідомляє, що їх натиснули.
            if (levelsCard != null)
                levelsCard.Clicked += () => LevelsRequested?.Invoke();
            if (endlessCard != null)
                endlessCard.Clicked += () => EndlessRequested?.Invoke();
            if (navBar != null)
                navBar.TabSelected += id => TabRequested?.Invoke(id);
            if (profileButton != null)
            {
                profileButton.onClick.AddListener(() => ProfileRequested?.Invoke());
                var press = profileButton.gameObject.AddComponent<PressScale>();
                press.Bind(design);
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Apply();
        }

        /// <summary>Підв'язати справжній гаманець замість мокових даних.</summary>
        public void Bind(Wallet wallet) => currency?.Bind(wallet);

        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            Bind(state.Wallet);
            StyleRefresh.Schedule(this, Apply);
        }

        /// <summary>
        /// Наступний рівень для картки «Рівні»: найдалі пройдений плюс один.
        /// Саме він відкритий, і саме його гравець побачить на карті поточним.
        /// </summary>
        private int LevelLine() =>
            State != null ? LevelProgress.HighestCleared(State.Progress) + 1 : mockLevel;

        private int StarsLine() =>
            State != null ? LevelProgress.TotalStars(State.Progress) : mockStars;

        private long RecordLine() =>
            State != null ? State.Progress.EndlessRecord : mockRecord;

        /// <summary>§8: головний рекорд — скільки різних картинок у колекції.</summary>
        private int PicturesLine() =>
            State != null ? State.Collection.Distinct : 0;


        /// <summary>Перечитати стан без повторного входу на екран.</summary>
        public void Refresh()
        {
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(playerName, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold);
            if (playerName != null)
                playerName.text = State?.Nick ?? mockName;


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
                $"Рівень {LevelLine()} · <sprite name=\"star\"> {StarsLine()}");
            endlessCard?.SetText("Нескінченний", "Малюй картинки",
                $"Картинок · {PicturesLine()} · рекорд {RecordLine():N0}");
            levelsCard?.Apply();
            endlessCard?.Apply();

            avatar?.Apply();
            // У грі валюту показує підписка на гаманець (Bind), у майстерні —
            // разове число. Друге не має затирати перше.
            if (State == null)
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
