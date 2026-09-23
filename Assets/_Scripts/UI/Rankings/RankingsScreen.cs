using System.Collections.Generic;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рейтинги: подіум топ-3, віртуалізований список і закріплена картка «Ти».
    ///
    /// Список рециклюється: рядків у пулі стільки, скільки вміщується в екран плюс
    /// запас, і під час скролу вони перепризначаються на інших гравців. Тап на
    /// чужого гравця відкриває ГОТОВИЙ екран Галактики з
    /// <see cref="GalaxyArgs.ReadOnly"/> — окремого екрана для цього немає.
    /// </summary>
    public sealed class RankingsScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;

        [Header("Друзі / Світ")]
        [SerializeField] private Button friendsTabButton;
        [SerializeField] private Button worldTabButton;
        [SerializeField] private GradientImage friendsTabFill;
        [SerializeField] private GradientImage worldTabFill;
        [SerializeField] private TMP_Text friendsTabLabel;
        [SerializeField] private TMP_Text worldTabLabel;

        [Header("Метрика")]
        [SerializeField] private Button[] metricButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] metricFills = System.Array.Empty<Image>();
        [SerializeField] private Image[] metricStrokes = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] metricLabels = System.Array.Empty<TMP_Text>();

        [Header("Період")]
        [SerializeField] private Button weekButton;
        [SerializeField] private Button allTimeButton;
        [SerializeField] private TMP_Text weekLabel;
        [SerializeField] private TMP_Text allTimeLabel;
        [SerializeField] private Image weekUnderline;
        [SerializeField] private Image allTimeUnderline;

        [Header("Подіум")]
        [SerializeField] private RectTransform podium;
        [SerializeField] private PodiumSlot[] podiumSlots = System.Array.Empty<PodiumSlot>();

        [Header("Список")]
        [SerializeField] private ScrollRect scroll;

        [Tooltip("Вміст скролу цілком: подіум і список разом.")]
        [SerializeField] private RectTransform scrollContent;

        [Tooltip("Вузол, у якому лежать рядки. Стоїть під подіумом, тому зсув " +
                 "рециклінгу рахується від нього, а не від верху вмісту.")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RankingRow[] rows = System.Array.Empty<RankingRow>();

        [Header("Порожній стан друзів")]
        [SerializeField] private RectTransform emptyState;
        [SerializeField] private TMP_Text emptyTitle;
        [SerializeField] private TMP_Text emptyHint;
        [SerializeField] private TMP_Text myCodeLabel;
        [SerializeField] private TMP_Text enterCodeLabel;

        [Header("Картка «Ти»")]
        [SerializeField] private GradientImage youBackground;
        [SerializeField] private Image youStroke;
        [SerializeField] private Image youGlow;
        [SerializeField] private TMP_Text youPosition;
        [SerializeField] private GradientImage youAvatar;
        [SerializeField] private TMP_Text youNick;
        [SerializeField] private TMP_Text youGap;
        [SerializeField] private TMP_Text youValue;
        [SerializeField] private TMP_Text youUnit;

        private Leaderboard? _board;
        private RankScope _scope = RankScope.World;
        private RankMetric _metric = RankMetric.Planets;
        private RankPeriod _period = RankPeriod.Week;

        /// <summary>Скільки місць займає подіум — список починається з четвертого.</summary>
        private const int PodiumSize = 3;

        private int _firstBound = int.MinValue;
        private int _count;

        /// <summary>Поточний зріз таблиці. Перечитується лише на зміну фільтра,
        /// тож скрол не сортує нічого.</summary>
        private readonly List<RankPlayer> _ranked = new List<RankPlayer>();

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Відкрити галактику гравця в режимі перегляду.</summary>
        public System.Action<GalaxyArgs>? PlayerOpened;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (friendsTabButton != null)
                friendsTabButton.onClick.AddListener(() => SetScope(RankScope.Friends));
            if (worldTabButton != null)
                worldTabButton.onClick.AddListener(() => SetScope(RankScope.World));
            if (weekButton != null)
                weekButton.onClick.AddListener(() => SetPeriod(RankPeriod.Week));
            if (allTimeButton != null)
                allTimeButton.onClick.AddListener(() => SetPeriod(RankPeriod.AllTime));

            for (var i = 0; i < metricButtons.Length; i++)
            {
                var metric = (RankMetric)i;
                metricButtons[i]?.onClick.AddListener(() => SetMetric(metric));
            }

            if (scroll != null)
                scroll.onValueChanged.AddListener(_ => Recycle());
        }

        private void Start()
        {
            for (var i = 0; i < rows.Length; i++)
                rows[i]?.Bind(OpenPlayer);
            for (var i = 0; i < podiumSlots.Length; i++)
                podiumSlots[i]?.Bind(OpenPlayer);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _board = BuildBoard();
            Apply();
        }

        private void OpenPlayer(RankPlayer player) =>
            PlayerOpened?.Invoke(new GalaxyArgs(new PlayerId(player.Id), true));

        /// <summary>
        /// Світовий список — мок (бекенду немає), картка «Ти» — реальна.
        /// Позиція в таблиці рахується від справжніх чисел гравця.
        /// </summary>
        private Leaderboard BuildBoard() =>
            State != null
                ? Leaderboard.WithRealPlayer(State, GalaxyProgress.FromSave(State.Galaxy))
                : Leaderboard.CreateMock();

        public void Apply()
        {
            if (design == null)
                return;

            _board ??= BuildBoard();

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "РЕЙТИНГИ";

            ApplyFont(friendsTabLabel, design.FontSizeBody, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(worldTabLabel, design.FontSizeBody, design.TextPrimary, FontStyles.Bold, 0f);
            if (friendsTabLabel != null) friendsTabLabel.text = "Друзі";
            if (worldTabLabel != null) worldTabLabel.text = "Світ";

            ApplyFont(weekLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(allTimeLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            if (weekLabel != null) weekLabel.text = "Цей тиждень";
            if (allTimeLabel != null) allTimeLabel.text = "За весь час";

            ApplyFont(emptyTitle, design.FontSizeSubtitle, design.TextMuted, FontStyles.Bold, 0f);
            if (emptyTitle != null) emptyTitle.text = "Тут поки порожньо";

            ApplyFont(emptyHint, design.FontSizeCardSubtitle, design.TextFaint, FontStyles.Normal, 0f);
            if (emptyHint != null)
                emptyHint.text = "Додай друзів, щоб змагатися у своєму затишному колі";

            ApplyFont(myCodeLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            if (myCodeLabel != null) myCodeLabel.text = "Мій код";

            ApplyFont(enterCodeLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            if (enterCodeLabel != null) enterCodeLabel.text = "Ввести код";

            currency?.Apply();

            ApplyScopeTabs();
            ApplyMetricSegments();
            ApplyPeriodTabs();
            ApplyList();
            ApplyPodium();
            ApplyYouCard();
        }

        private void SetScope(RankScope scope)
        {
            _scope = scope;
            ApplyScopeTabs();
            ApplyList();
            ApplyPodium();
            ApplyYouCard();
        }

        private void SetMetric(RankMetric metric)
        {
            _metric = metric;
            ApplyMetricSegments();
            ApplyList();
            ApplyPodium();
            ApplyYouCard();
        }

        private void SetPeriod(RankPeriod period)
        {
            _period = period;
            ApplyPeriodTabs();
            ApplyList();
            ApplyPodium();
            ApplyYouCard();
        }

        private void ApplyScopeTabs()
        {
            var world = _scope == RankScope.World;
            SetTabFill(friendsTabFill, !world);
            SetTabFill(worldTabFill, world);
            if (friendsTabLabel != null)
                friendsTabLabel.color = !world ? design.TextPrimary : design.TextMuted;
            if (worldTabLabel != null)
                worldTabLabel.color = world ? design.TextPrimary : design.TextMuted;
        }

        private void SetTabFill(GradientImage? fill, bool active)
        {
            if (fill == null)
                return;
            fill.SetGradient(
                active ? design.ShopTabActiveFrom : Color.clear,
                active ? design.ShopTabActiveTo : Color.clear);
        }

        private void ApplyMetricSegments()
        {
            var names = new[] { "Планети", "Галактики", "Колекція" };
            for (var i = 0; i < metricLabels.Length && i < names.Length; i++)
            {
                var active = (int)_metric == i;
                ApplyFont(metricLabels[i], design.FontSizeShopCard,
                    active ? design.TextPrimary : design.TextMuted, FontStyles.Bold, 0f);
                metricLabels[i].text = names[i];

                // Активний сегмент — трохи світліший фон плюс тонка обводка.
                // Градієнта тут немає навмисно: він сперечався б із капсулою вище.
                if (i < metricFills.Length && metricFills[i] != null)
                    metricFills[i].color = active ? design.GlassFillRaised : Color.clear;
                if (i < metricStrokes.Length && metricStrokes[i] != null)
                    metricStrokes[i].gameObject.SetActive(active);
            }
        }

        private void ApplyPeriodTabs()
        {
            var week = _period == RankPeriod.Week;
            if (weekLabel != null)
                weekLabel.color = week ? design.TextPrimary : design.TextMuted;
            if (allTimeLabel != null)
                allTimeLabel.color = week ? design.TextMuted : design.TextPrimary;
            if (weekUnderline != null)
            {
                weekUnderline.gameObject.SetActive(week);
                weekUnderline.color = design.AccentPrimary;
            }

            if (allTimeUnderline != null)
            {
                allTimeUnderline.gameObject.SetActive(!week);
                allTimeUnderline.color = design.AccentPrimary;
            }
        }

        private void ApplyPodium()
        {
            if (_board == null)
                return;

            // Подіум має сенс лише коли є щонайменше троє.
            var ranked = _ranked;
            var show = ranked.Count >= PodiumSize;
            if (podium != null)
                podium.gameObject.SetActive(show);
            if (!show)
            {
                for (var i = 0; i < podiumSlots.Length; i++)
                    podiumSlots[i]?.Release();
                return;
            }

            for (var i = 0; i < podiumSlots.Length; i++)
            {
                var slot = podiumSlots[i];
                if (slot == null)
                    continue;
                var index = slot.Place - 1;
                if (index < ranked.Count)
                    slot.Show(ranked[index], _metric, _period);
                else
                    slot.Release();
            }
        }

        private void ApplyList()
        {
            if (_board == null || design == null)
                return;

            _board.Ranked(_scope, _metric, _period, _ranked);
            var start = _ranked.Count >= PodiumSize ? PodiumSize : 0;
            _count = Mathf.Max(0, _ranked.Count - start);

            var empty = _scope == RankScope.Friends && _ranked.Count == 0;
            if (emptyState != null)
                emptyState.gameObject.SetActive(empty);

            if (listContent != null && scrollContent != null)
            {
                var step = design.RankRowHeight + design.RankRowGap;
                var rowsHeight = _count * step;
                listContent.sizeDelta = new Vector2(0f, rowsHeight);
                // Висота вмісту = подіум (він над рядками) + рядки + запас під
                // закріплену картку, щоб останній рядок не ховався під нею.
                scrollContent.sizeDelta = new Vector2(0f,
                    -listContent.anchoredPosition.y + rowsHeight + design.RankListBottomPadding);
            }

            _firstBound = int.MinValue;
            Recycle();
        }

        /// <summary>
        /// Перепризначає рядки під поточне положення скролу. Викликається з
        /// onValueChanged, тобто з LateUpdate ScrollRect — поза проходом канваса.
        /// </summary>
        private void Recycle()
        {
            if (_board == null || design == null || listContent == null || rows.Length == 0)
                return;

            var start = _ranked.Count >= PodiumSize ? PodiumSize : 0;
            var step = design.RankRowHeight + design.RankRowGap;
            if (step <= 0.01f)
                return;

            // Скільки рядків «з'їхало» вгору за верхній край в'юпорта. Подіум теж
            // скролиться, тому від зсуву вмісту віднімаємо його висоту — вона й є
            // від'ємним anchoredPosition хоста рядків.
            var scrolled = scrollContent != null ? scrollContent.anchoredPosition.y : 0f;
            var offset = scrolled + listContent.anchoredPosition.y;
            var first = Mathf.Max(0, Mathf.FloorToInt(offset / step));
            first = Mathf.Clamp(first, 0, Mathf.Max(0, _count - rows.Length));

            if (first == _firstBound)
                return;
            _firstBound = first;

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                    continue;

                var index = first + i;
                if (index >= _count)
                {
                    row.Release();
                    continue;
                }

                var rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(0f, -index * step);
                row.Show(_ranked[start + index], start + index + 1, _metric, _period);
            }
        }

        private void ApplyYouCard()
        {
            if (_board == null || design == null)
                return;

            var you = _board.You;

            if (youBackground != null)
                youBackground.SetGradient(
                    DesignSystem.WithAlpha(design.AccentPrimary, design.YouCardTintFrom),
                    DesignSystem.WithAlpha(design.AccentSecondary, design.YouCardTintTo));

            if (youStroke != null)
                youStroke.color = design.YouCardStroke;
            if (youGlow != null)
                youGlow.color = DesignSystem.WithAlpha(design.AccentPrimary, design.YouCardGlowAlpha);

            ApplyFont(youPosition, design.FontSizeRankRow, design.YouCardText, FontStyles.Bold, 0f);
            if (youPosition != null)
                youPosition.text = $"#{_board.YourPosition(_metric, _period)}";

            if (youAvatar != null)
            {
                var color = you.DropColor.ToColor();
                youAvatar.SetGradient(DesignSystem.Lighten(color, 0.5f), DesignSystem.Darken(color, 0.28f));
            }

            ApplyFont(youNick, design.FontSizeRankRow, design.TextPrimary, FontStyles.Bold, 0f);
            if (youNick != null) youNick.text = $"Ти · {you.Nick}";

            var gap = _board.GapToNext(_metric, _period);
            var position = _board.YourPosition(_metric, _period);
            ApplyFont(youGap, design.FontSizeSmall, design.AccentTeal, FontStyles.Bold, 0f);
            if (youGap != null)
            {
                youGap.gameObject.SetActive(gap > 0);
                youGap.text = $"ще +{gap} до №{position - 1}";
            }

            ApplyFont(youValue, design.FontSizeRankValue, design.YouCardText, FontStyles.Bold, 0f);
            if (youValue != null)
                youValue.text = you.Value(_metric, _period).ToString("N0").Replace(",", " ");

            ApplyFont(youUnit, design.FontSizeCaption, design.TextFaint, FontStyles.Normal, 0f);
            if (youUnit != null) youUnit.text = Leaderboard.Unit(_metric);
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
