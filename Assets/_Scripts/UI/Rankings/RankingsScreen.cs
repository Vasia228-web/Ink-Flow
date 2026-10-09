using System;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Platform;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рейтинги (майстер-док §16): лише «Світ», метрики «Планети» й «Галактики», періоди «цей тиждень»
    /// і «за весь час»; подіум топ-3, віртуалізований список і закріплена картка «Ти» зі СПРАВЖНІМИ
    /// числами з локального файлу. Таблиця приходить від <see cref="ILeaderboardService"/>; без мережі
    /// чи без налаштувань екран каже «немає з'єднання» / «не підключено» і нічого не падає.
    ///
    /// Список рециклюється: рядків у пулі стільки, скільки вміщується в екран плюс запас, і під час
    /// скролу вони перепризначаються на інших гравців. Тап на чужого гравця читає його вітрину й
    /// відкриває ГОТОВИЙ екран Галактики з <see cref="GalaxyArgs.ForVisitor"/> — окремого екрана немає.
    /// </summary>
    public sealed class RankingsScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;

        [Header("Метрика")]
        [SerializeField] private Button[] metricButtons = Array.Empty<Button>();
        [SerializeField] private Image[] metricFills = Array.Empty<Image>();
        [SerializeField] private Image[] metricStrokes = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] metricLabels = Array.Empty<TMP_Text>();

        [Header("Період")]
        [SerializeField] private Button weekButton;
        [SerializeField] private Button allTimeButton;
        [SerializeField] private TMP_Text weekLabel;
        [SerializeField] private TMP_Text allTimeLabel;
        [SerializeField] private Image weekUnderline;
        [SerializeField] private Image allTimeUnderline;

        [Header("Подіум")]
        [SerializeField] private RectTransform podium;
        [SerializeField] private PodiumSlot[] podiumSlots = Array.Empty<PodiumSlot>();

        [Header("Список")]
        [SerializeField] private ScrollRect scroll;

        [Tooltip("Вміст скролу цілком: подіум і список разом.")]
        [SerializeField] private RectTransform scrollContent;

        [Tooltip("Вузол, у якому лежать рядки. Стоїть під подіумом, тому зсув " +
                 "рециклінгу рахується від нього, а не від верху вмісту.")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RankingRow[] rows = Array.Empty<RankingRow>();

        [Header("Стан без таблиці")]
        [SerializeField] private RectTransform statusBlock;
        [SerializeField] private TMP_Text statusTitle;
        [SerializeField] private TMP_Text statusHint;

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

        private ILeaderboardService? _leaderboards;
        private IShowcaseService? _showcases;
        private IIdentityService? _identity;

        /// <summary>Скільки місць просити у сервісу (AppConfig): подіум плюс список із запасом на скрол.</summary>
        private int _pageLimit = 50;

        private Leaderboard? _board;
        private RankMetric _metric = RankMetric.Planets;
        private RankPeriod _period = RankPeriod.Week;
        private bool _loading;
        private bool _requesting;
        private int _request;

        /// <summary>Скільки місць займає подіум — список починається з четвертого.</summary>
        private const int PodiumSize = 3;

        private int _firstBound = int.MinValue;
        private int _count;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Відкрити галактику гравця в режимі перегляду (з його вітриною).</summary>
        public System.Action<GalaxyArgs>? PlayerOpened;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        public Leaderboard? PreviewBoard => _board;
        public bool PreviewStatusShown => statusBlock != null && statusBlock.gameObject.activeSelf;
        public string PreviewStatusTitle => statusTitle != null ? statusTitle.text : string.Empty;
        public bool PreviewPodiumShown => podium != null && podium.gameObject.activeSelf;
        public string PreviewYouPosition => youPosition != null ? youPosition.text : string.Empty;
        public string PreviewYouValue => youValue != null ? youValue.text : string.Empty;
        public RankingRow[] PreviewRows => rows;
        public void PreviewSetMetric(RankMetric metric) => SetMetric(metric);
        public void PreviewSetPeriod(RankPeriod period) => SetPeriod(period);
        /// <summary>Тестам: тап по гравцю, як пальцем.</summary>
        public void PreviewOpen(RankPlayer player) => OpenPlayer(player);
#endif

        /// <summary>
        /// Платформні сервіси підставляє композиційний корінь; без них — «не підключено». Тотожність —
        /// сервісом, не рядком: вхід у хмару асинхронний, і id читається в момент запиту.
        /// </summary>
        public void BindServices(ILeaderboardService? leaderboards, IShowcaseService? showcases, IIdentityService? identity, int pageLimit = 50)
        {
            _leaderboards = leaderboards;
            _showcases = showcases;
            _identity = identity;
            _pageLimit = Mathf.Max(3, pageLimit);
        }

        /// <summary>Хмарний id гравця зараз; порожньо — не ввійшов (картка «Ти» тоді локальна).</summary>
        private string PlayerId => _identity != null && _identity.IsSignedIn ? _identity.PlayerId : string.Empty;

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
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
            Request();
            Apply();
        }

        public override void OnExit()
        {
            // Відповідь, що прийде після виходу, не має чіпати екран.
            _request++;
            _loading = false;
            base.OnExit();
        }

        /// <summary>Мережа могла з'явитись, поки застосунок був згорнутий: таблицю без даних перепитуємо.</summary>
        private void OnApplicationFocus(bool focus)
        {
            if (focus && isActiveAndEnabled && _board != null && !_board.IsOk && !_loading)
            {
                Request();
                ApplyBoard();
            }
        }

        /// <summary>Картка «Ти» — з локального файлу (§16): без стану (майстерня) — моковий гравець.</summary>
        private RankPlayer You() =>
            State != null
                ? RankPlayer.You(State, PlayerId, _metric, _period, DateTime.UtcNow)
                : new RankPlayer("you", "Нова", InkColor.Magenta, false, _metric == RankMetric.Planets ? 12 : 1, 0) { IsYou = true };

        /// <summary>
        /// Запит сторінки. Сервіс відповідає колбеком — можливо, одразу (Fake), після виходу з екрана або
        /// після зміни фільтра; відповіді на застарілий запит відкидаються за номером. Поки відповідь
        /// не прийшла, попередня вдала таблиця того ж зрізу лишається на екрані — повернення з чужої
        /// галактики не блимає «Завантажую…» і не скидає скрол.
        /// </summary>
        private void Request()
        {
            var you = You();
            var service = _leaderboards ?? (State == null ? WorkshopLeaderboards : null);
            if (service == null || !service.IsAvailable)
            {
                _loading = false;
                _board = Leaderboard.Unavailable(_metric, _period, LeaderboardStatus.NotConfigured, you);
                return;
            }

            var request = ++_request;
            _loading = true;
            var keep = _board != null && _board.IsOk && _board.Players.Count > 0 && _board.Metric == _metric && _board.Period == _period;
            if (!keep)
                _board = Leaderboard.Unavailable(_metric, _period, LeaderboardStatus.Ok, you);
            _requesting = true;
            service.Fetch(_metric, _period, _pageLimit, page =>
            {
                if (request != _request)
                    return;
                _loading = false;
                _board = new Leaderboard(page, you);
                // Синхронна відповідь (Fake) застосовується викликачем один раз; асинхронна — тут.
                if (!_requesting && isActiveAndEnabled)
                    ApplyBoard();
            });
            _requesting = false;
        }

        private static FakeLeaderboards? _workshop;

        /// <summary>Сцена-майстерня без стану й сервісів: таблиця «навмання», щоб розкладку було видно.</summary>
        private static FakeLeaderboards WorkshopLeaderboards => _workshop ??= new FakeLeaderboards();

        private void OpenPlayer(RankPlayer player)
        {
            // Інкогніто не відкриваємо (§16): гравець сам сховав профіль. Себе — теж нема куди
            // (і за прапорцем, і за хмарним id — рядок міг прийти раніше, ніж вхід).
            if (player == null || player.Incognito || player.IsYou || (PlayerId.Length > 0 && player.Id == PlayerId))
                return;
            var showcases = _showcases ?? (State == null ? WorkshopShowcases : null);
            if (showcases == null)
                return;
            var request = _request;
            showcases.Fetch(player.Id, showcase =>
            {
                if (request != _request || showcase == null)
                    return;
                PlayerOpened?.Invoke(GalaxyArgs.ForVisitor(showcase));
            });
        }

        private static FakeShowcase? _workshopShowcases;

        private static FakeShowcase WorkshopShowcases
        {
            get
            {
                if (_workshopShowcases != null)
                    return _workshopShowcases;
                var layout = GalaxyLayout.Default;
                var ids = new List<string>();
                var slots = new List<int>();
                foreach (var planet in layout.Planets) { ids.Add(planet.Id); slots.Add(planet.Slots); }
                return _workshopShowcases = new FakeShowcase(ids, slots);
            }
        }

        public void Apply()
        {
            if (design == null)
                return;

            // Майстерня без стану — таблиця «навмання» одразу; зі станом без сервісів — «не підключено».
            if (_board == null)
            {
                if (State == null) Request();
                else _board = Leaderboard.Unavailable(_metric, _period, LeaderboardStatus.NotConfigured, You());
            }

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "РЕЙТИНГИ";

            ApplyFont(weekLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(allTimeLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            if (weekLabel != null) weekLabel.text = "Цей тиждень";
            if (allTimeLabel != null) allTimeLabel.text = "За весь час";

            currency?.Apply();

            ApplyMetricSegments();
            ApplyPeriodTabs();
            ApplyBoard();
        }

        /// <summary>Усе, що залежить від таблиці: статус, подіум, список, картка «Ти».</summary>
        private void ApplyBoard()
        {
            ApplyStatus();
            ApplyList();
            ApplyPodium();
            ApplyYouCard();
        }

        private void SetMetric(RankMetric metric)
        {
            if (_metric == metric)
                return;
            _metric = metric;
            ApplyMetricSegments();
            Request();
            ApplyBoard();
        }

        private void SetPeriod(RankPeriod period)
        {
            if (_period == period)
                return;
            _period = period;
            ApplyPeriodTabs();
            Request();
            ApplyBoard();
        }

        private void ApplyMetricSegments()
        {
            for (var i = 0; i < metricLabels.Length; i++)
            {
                var exists = i <= (int)RankMetric.Galaxies;
                if (i < metricButtons.Length)
                    Toggle(metricButtons[i], exists);
                if (!exists)
                    continue;
                var active = (int)_metric == i;
                ApplyFont(metricLabels[i], design.FontSizeShopCard,
                    active ? design.TextPrimary : design.TextMuted, FontStyles.Bold, 0f);
                metricLabels[i].text = Leaderboard.Title((RankMetric)i);

                // Активний сегмент — трохи світліший фон плюс тонка обводка.
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

        /// <summary>Блок замість таблиці: завантаження, немає з'єднання, не підключено, помилка сервера.</summary>
        private void ApplyStatus()
        {
            if (_board == null)
                return;
            // Є що показати — показуємо, навіть поки оновлення в дорозі.
            var show = !_board.IsOk || _board.Players.Count == 0;
            Toggle(statusBlock, show);
            if (!show)
                return;

            var (caption, hint) = _loading && _board.IsOk
                ? ("Завантажую…", "Таблиця з'явиться за мить")
                : _board.Status switch
                {
                    LeaderboardStatus.NoConnection => ("Немає з'єднання", "Рейтинги з'являться, щойно буде мережа. Грати можна й так"),
                    LeaderboardStatus.NotConfigured => ("Рейтинги ще не підключені", "Твої числа справжні — таблиця з'явиться пізніше"),
                    LeaderboardStatus.Failed => ("Таблиця не відповіла", "Спробуй ще раз трохи згодом"),
                    _ => ("Тут поки порожньо", "Ожививши планету, ти станеш першим у таблиці")
                };
            ApplyFont(statusTitle, design.FontSizeSubtitle, design.TextMuted, FontStyles.Bold, 0f);
            if (statusTitle != null) statusTitle.text = caption;
            ApplyFont(statusHint, design.FontSizeCardSubtitle, design.TextFaint, FontStyles.Normal, 0f);
            if (statusHint != null) statusHint.text = hint;
        }

        private void ApplyPodium()
        {
            if (_board == null)
                return;

            // Подіум має сенс лише коли є щонайменше троє.
            var players = _board.Players;
            var show = _board.IsOk && players.Count >= PodiumSize;
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
                if (index < players.Count)
                    slot.Show(players[index], _metric);
                else
                    slot.Release();
            }
        }

        private void ApplyList()
        {
            if (_board == null || design == null)
                return;

            var players = _board.IsOk ? _board.Players : (IReadOnlyList<RankPlayer>)Array.Empty<RankPlayer>();
            var start = players.Count >= PodiumSize ? PodiumSize : 0;
            _count = Mathf.Max(0, players.Count - start);

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

            var players = _board.IsOk ? _board.Players : (IReadOnlyList<RankPlayer>)Array.Empty<RankPlayer>();
            var start = players.Count >= PodiumSize ? PodiumSize : 0;
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
                var player = players[start + index];
                row.Show(player, player.Rank > 0 ? player.Rank : start + index + 1, _metric);
            }
        }

        private void ApplyYouCard()
        {
            if (_board == null || design == null)
                return;

            var you = _board.You;

            // §16: щільний фон без кольорової обводки й світіння — картка стоїть над списком і не має
            // просвічувати рядок під собою; акцент дає лише колір тексту.
            if (youBackground != null)
                youBackground.SetGradient(design.YouCardFillFrom, design.YouCardFillTo);
            if (youStroke != null)
                youStroke.color = design.GlassStroke;
            Toggle(youGlow, false);

            // Місце — лише коли сервер його дав; без мережі — риска, а не вигадане число.
            var position = _board.IsOk ? _board.YourPosition : 0;
            ApplyFont(youPosition, design.FontSizeRankRow, design.YouCardText, FontStyles.Bold, 0f);
            if (youPosition != null)
                youPosition.text = position > 0 ? $"#{position}" : "#—";

            // Той самий колір, що в рядку таблиці: аватар із профілю (§14), а не моковий.
            if (youAvatar != null)
                RankingRow.PaintAvatar(youAvatar, design, you);

            ApplyFont(youNick, design.FontSizeRankRow, design.TextPrimary, FontStyles.Bold, 0f);
            if (youNick != null) youNick.text = $"Ти · {you.Nick}";

            var gap = position > 1 ? _board.GapToNext : 0;
            ApplyFont(youGap, design.FontSizeSmall, design.AccentTeal, FontStyles.Bold, 0f);
            if (youGap != null)
            {
                youGap.gameObject.SetActive(gap > 0);
                youGap.text = $"ще +{gap} до №{position - 1}";
            }

            ApplyFont(youValue, design.FontSizeRankValue, design.YouCardText, FontStyles.Bold, 0f);
            if (youValue != null)
                youValue.text = ScoreFormat.Full(you.Value);

            ApplyFont(youUnit, design.FontSizeCaption, design.TextFaint, FontStyles.Normal, 0f);
            if (youUnit != null) youUnit.text = Leaderboard.Unit(_metric);
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
