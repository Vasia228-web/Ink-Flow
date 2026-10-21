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
    /// Рейтинги «Ліга» (майстер-док §16, U1Glaze S08): угорі картка «Ти» — місце, аватар, своє число за період, смужка
    /// й «ще N до №M»; нижче — сегменти «Планети / Галактики», таби «Цей тиждень / За весь час» і компактна таблиця
    /// з медалями для топ-3. Лише «Світ». Картка «Ти» — СПРАВЖНІ числа з локального файлу; таблиця — від
    /// <see cref="ILeaderboardService"/>; без мережі чи без налаштувань — блок стану з «Спробувати ще», і нічого не падає.
    ///
    /// Список рециклюється: рядків у пулі стільки, скільки вміщується в картку з запасом, і під час скролу вони
    /// перепризначаються. Тап на чужого гравця читає його вітрину й відкриває ГОТОВИЙ екран Галактики з
    /// <see cref="GalaxyArgs.ForVisitor"/>.
    /// </summary>
    public sealed class RankingsScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;

        [Header("Картка «Ти»")]
        [SerializeField] private CanvasGroup youGroup;
        [SerializeField] private Image youRing;
        [SerializeField] private TMP_Text youPosition;
        [SerializeField] private Image youAvatar;
        [SerializeField] private TMP_Text youNick;
        [SerializeField] private TMP_Text youSubtitle;
        [SerializeField] private GameObject youProgressRow;
        [SerializeField] private RectTransform youProgressFill;
        [SerializeField] private TMP_Text youGap;

        [Header("Метрика й період")]
        [SerializeField] private Button[] metricButtons = Array.Empty<Button>();
        [SerializeField] private Image[] metricOn = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] metricLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Button weekButton;
        [SerializeField] private Button allTimeButton;
        [SerializeField] private TMP_Text weekLabel;
        [SerializeField] private TMP_Text allTimeLabel;
        [SerializeField] private Image weekUnderline;
        [SerializeField] private Image allTimeUnderline;

        [Header("Таблиця")]
        [SerializeField] private GameObject tableCard;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RankingRow[] rows = Array.Empty<RankingRow>();

        [Header("Стан без таблиці")]
        [SerializeField] private GameObject statusCard;
        [SerializeField] private Image statusIcon;
        [SerializeField] private TMP_Text statusTitle;
        [SerializeField] private TMP_Text statusHint;
        [SerializeField] private CandyButton retryButton;
        [SerializeField] private Sprite offlineIcon;
        [SerializeField] private Sprite emptyIcon;

        [Header("Навігація й аватари")]
        [SerializeField] private NavBar navBar;
        [SerializeField] private Sprite[] avatarSprites = Array.Empty<Sprite>();
        [SerializeField] private Sprite? incognitoAvatar;

        private ILeaderboardService? _leaderboards;
        private IShowcaseService? _showcases;
        private IIdentityService? _identity;

        /// <summary>Скільки місць просити у сервісу (AppConfig): таблиця із запасом на скрол.</summary>
        private int _pageLimit = 50;

        private Leaderboard? _board;
        private RankMetric _metric = RankMetric.Planets;
        private RankPeriod _period = RankPeriod.Week;
        private bool _loading;
        private bool _requesting;
        private int _request;

        private int _firstBound = int.MinValue;
        private int _count;

        /// <summary>Назад у хаб (вкладка «назад» не показує, але подія лишається для підекрана).</summary>
        public System.Action? BackRequested;

        /// <summary>Відкрити галактику гравця в режимі перегляду (з його вітриною).</summary>
        public System.Action<GalaxyArgs>? PlayerOpened;

        /// <summary>Вкладка нижньої навігації.</summary>
        public System.Action<string>? TabRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        public Leaderboard? PreviewBoard => _board;
        public bool PreviewStatusShown => statusCard != null && statusCard.activeSelf;
        public string PreviewStatusTitle => statusTitle != null ? statusTitle.text : string.Empty;
        public bool PreviewTableShown => tableCard != null && tableCard.activeSelf;
        public bool PreviewRetryShown => retryButton != null && retryButton.gameObject.activeSelf;
        public string PreviewYouPosition => youPosition != null ? youPosition.text : string.Empty;
        public string PreviewYouSubtitle => youSubtitle != null ? youSubtitle.text : string.Empty;
        public string PreviewYouGap => youGap != null && youGap.gameObject.activeInHierarchy ? youGap.text : string.Empty;
        public float PreviewYouProgress => youProgressFill != null ? youProgressFill.localScale.x : 0f;
        public RankingRow[] PreviewRows => rows;
        public void PreviewSetMetric(RankMetric metric) => SetMetric(metric);
        public void PreviewSetPeriod(RankPeriod period) => SetPeriod(period);
        public void PreviewRetry() => Retry();
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
            if (retryButton != null)
                retryButton.Button.onClick.AddListener(Retry);
            if (scroll != null)
                scroll.onValueChanged.AddListener(_ => Recycle());
            if (navBar != null)
                navBar.TabSelected += id => TabRequested?.Invoke(id);
        }

        private void Start()
        {
            for (var i = 0; i < rows.Length; i++)
                rows[i]?.Bind(OpenPlayer);
        }

        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            currency?.Bind(state.Wallet);
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
                Retry();
        }

        private void Retry()
        {
            Request();
            ApplyBoard();
        }

        /// <summary>Картка «Ти» — з локального файлу (§16): без стану (майстерня) — моковий гравець.</summary>
        private RankPlayer You() =>
            State != null
                ? RankPlayer.You(State, PlayerId, _metric, _period, DateTime.UtcNow)
                : new RankPlayer("you", "Гравець", false, _metric == RankMetric.Planets ? 2 : 1, 0, 3) { IsYou = true };

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

            if (title != null) title.text = "Рейтинги";
            // Рейтинги — вкладка навігації (U1Glaze): «‹» немає, навігація є.
            Toggle(backButton, false);
            navBar?.SetActive(NavBar.Ranks);
            currency?.Apply();

            ApplyMetricSegments();
            ApplyPeriodTabs();
            ApplyBoard();
        }

        /// <summary>Усе, що залежить від таблиці: стан, список, картка «Ти».</summary>
        private void ApplyBoard()
        {
            ApplyStatus();
            ApplyList();
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

        /// <summary>Сегменти «Планети / Галактики»: активний — бірюзова цукерка з написом teal-ink, другий — ink-muted.</summary>
        private void ApplyMetricSegments()
        {
            for (var i = 0; i < metricLabels.Length; i++)
            {
                var active = (int)_metric == i;
                if (metricLabels[i] != null)
                {
                    metricLabels[i].text = Leaderboard.Title((RankMetric)i);
                    metricLabels[i].color = active ? design.TealInk : design.InkMuted;
                }
                if (i < metricOn.Length && metricOn[i] != null)
                    metricOn[i].enabled = active;
            }
        }

        /// <summary>Таби періоду: активний — ink з бурштиновим підкресленням, другий — ink-muted.</summary>
        private void ApplyPeriodTabs()
        {
            var week = _period == RankPeriod.Week;
            if (weekLabel != null)
            {
                weekLabel.text = "Цей тиждень";
                weekLabel.color = week ? design.InkText : design.InkMuted;
            }
            if (allTimeLabel != null)
            {
                allTimeLabel.text = "За весь час";
                allTimeLabel.color = week ? design.InkMuted : design.InkText;
            }
            if (weekUnderline != null)
                weekUnderline.enabled = week;
            if (allTimeUnderline != null)
                allTimeUnderline.enabled = !week;
        }

        /// <summary>Картка стану замість таблиці (S08_04): завантаження, немає з'єднання, не підключено, помилка, порожньо.</summary>
        private void ApplyStatus()
        {
            if (_board == null)
                return;
            // Є що показати — показуємо, навіть поки оновлення в дорозі.
            var show = !_board.IsOk || _board.Players.Count == 0;
            Toggle(statusCard, show);
            Toggle(tableCard, !show);
            if (!show)
                return;

            var loading = _loading && _board.IsOk;
            var (caption, hint) = loading
                ? ("Завантажую…", "Таблиця з'явиться за мить.")
                : _board.Status switch
                {
                    LeaderboardStatus.NoConnection => ("Немає з'єднання", "Рейтинги оновляться, щойно з'явиться інтернет."),
                    LeaderboardStatus.NotConfigured => ("Рейтинги ще не підключені", "Твої числа справжні — таблиця з'явиться пізніше."),
                    LeaderboardStatus.Failed => ("Таблиця не відповіла", "Спробуй ще раз трохи згодом."),
                    _ => ("Тут поки порожньо", "Ожививши планету, ти станеш першим у таблиці.")
                };
            if (statusTitle != null) statusTitle.text = caption;
            if (statusHint != null) statusHint.text = hint;
            if (statusIcon != null)
                statusIcon.sprite = _board.Status == LeaderboardStatus.NoConnection || _board.Status == LeaderboardStatus.Failed
                    ? offlineIcon
                    : emptyIcon;
            // «Спробувати ще» має сенс лише там, де мережа чи сервер можуть ожити.
            Toggle(retryButton, !loading && (_board.Status == LeaderboardStatus.NoConnection || _board.Status == LeaderboardStatus.Failed));
        }

        private void ApplyList()
        {
            if (_board == null || design == null)
                return;

            var players = _board.IsOk ? _board.Players : (IReadOnlyList<RankPlayer>)Array.Empty<RankPlayer>();
            _count = players.Count;

            if (listContent != null)
            {
                var step = design.RankRowHeight + design.RankRowGap;
                listContent.sizeDelta = new Vector2(listContent.sizeDelta.x, _count * step + design.RankListBottomPadding);
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
            var step = design.RankRowHeight + design.RankRowGap;
            if (step <= 0.01f)
                return;

            var first = Mathf.Max(0, Mathf.FloorToInt(listContent.anchoredPosition.y / step));
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
                var player = players[index];
                row.Show(player, player.Rank > 0 ? player.Rank : index + 1, index == _count - 1);
            }
        }

        /// <summary>
        /// Картка «Ти» (S08_01/04): місце бірюзовим display (без місця — «#—»), аватар, «Ти · нік», своє число за
        /// період; смужка своє / (своє + N) і «ще N планет до №M». Без мережі — приглушена, без обводки й смужки,
        /// «місце оновиться з інтернетом».
        /// </summary>
        private void ApplyYouCard()
        {
            if (_board == null || design == null)
                return;

            var you = _board.You;
            var position = _board.IsOk ? _board.YourPosition : 0;
            var online = _board.IsOk && !(_loading && _board.Players.Count == 0);

            if (youGroup != null)
                youGroup.alpha = online ? 1f : design.YouCardOfflineAlpha;
            if (youRing != null)
            {
                youRing.enabled = online;
                youRing.color = DesignSystem.WithAlpha(design.Teal, design.YouCardRingAlpha);
            }
            if (youPosition != null)
            {
                youPosition.text = position > 0 ? $"#{position}" : "#—";
                youPosition.color = position > 0 ? design.Teal300 : design.InkDim;
            }
            if (youAvatar != null)
                youAvatar.sprite = RankingRow.AvatarOf(you, avatarSprites, incognitoAvatar) ?? youAvatar.sprite;
            if (youNick != null)
                youNick.text = $"Ти · {you.Nick}";

            var unit = _metric == RankMetric.Planets
                ? Plural.Of(Clip(you.Value), "планета", "планети", "планет")
                : Plural.Of(Clip(you.Value), "галактика", "галактики", "галактик");
            var period = _period == RankPeriod.Week ? "за тиждень" : "за весь час";
            if (youSubtitle != null)
                youSubtitle.text = online || position > 0
                    ? $"{ScoreFormat.Full(you.Value)} {unit} {period}"
                    : "місце оновиться з інтернетом";

            var showProgress = online && position > 0;
            Toggle(youProgressRow, showProgress);
            if (!showProgress)
                return;
            // Ширина заливки — масштабом з півотом зліва: розмір похідний від доріжки, на подію (не щокадру).
            if (youProgressFill != null)
                youProgressFill.localScale = new Vector3(Mathf.Clamp01(_board.ProgressToNext), 1f, 1f);
            if (youGap != null)
            {
                var gap = _board.GapToNext;
                var gapUnit = _metric == RankMetric.Planets
                    ? Plural.Of(Clip(gap), "планета", "планети", "планет")
                    : Plural.Of(Clip(gap), "галактика", "галактики", "галактик");
                youGap.text = position == 1 ? "Ти перший — тримай місце!"
                    : gap > 0 ? $"ще {ScoreFormat.Full(gap)} {gapUnit} до №{position - 1}"
                    : $"нарівні з №{position - 1}";
            }
        }

        /// <summary>Відмінок рахується за останніми цифрами — для чисел понад int досить межі.</summary>
        private static int Clip(long value) => (int)Math.Min(value, int.MaxValue);

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private static void Toggle(GameObject? target, bool on)
        {
            if (target != null && target.activeSelf != on)
                target.SetActive(on);
        }
    }
}
