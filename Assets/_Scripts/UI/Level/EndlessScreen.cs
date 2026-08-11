using System.Collections;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>З чим відкривати «Нескінченний».</summary>
    public sealed class EndlessArgs : ScreenArgs
    {
        public EndlessArgs(BalanceData? balance = null, EndlessData? config = null)
        {
            Balance = balance;
            Config = config;
        }

        /// <summary>
        /// Баланс із BalanceConfig.asset. Null — базовий: екран лишається запускним
        /// сам по собі, але в грі його підставляє композиційний корінь. Без цього
        /// правки балансу не доходили б до партії.
        /// </summary>
        public BalanceData? Balance { get; }

        public EndlessData? Config { get; }
    }

    /// <summary>
    /// Екран «Нескінченний»: рахунок, живий рекорд, черга наступних крапель,
    /// приплив і чесний фінал.
    ///
    /// Правил тут немає — усе рахує <see cref="EndlessSession"/>. Екран лише
    /// показує її стан і програє те, що вона повернула. Зокрема черга крапель
    /// НЕ малюється з окремого списку: вона читається з тієї самої
    /// <see cref="DropQueue"/>, з якої сесія бере краплі для доливу.
    /// </summary>
    public sealed class EndlessScreen : ScreenBase, IGameCommands
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button restartButton;

        [Header("Капсули")]
        [SerializeField] private Image scoreCapsule;
        [SerializeField] private Image scoreCapsuleStroke;
        [SerializeField] private Image scoreCapsuleGlow;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text scoreNumber;
        [SerializeField] private Image recordCapsule;
        [SerializeField] private Image recordCapsuleStroke;
        [SerializeField] private Image recordCapsuleGlow;
        [SerializeField] private TMP_Text recordLabel;
        [SerializeField] private TMP_Text recordNumber;

        [Header("Черга")]
        [SerializeField] private TMP_Text queueLabel;
        [SerializeField] private RectTransform queueRow;
        [SerializeField] private DropView[] queueDrops = System.Array.Empty<DropView>();
        [SerializeField] private Image queueHeadRing;

        [Header("Приплив")]
        [SerializeField] private GradientImage tideBadge;
        [SerializeField] private Image tideBadgeStroke;
        [SerializeField] private TMP_Text tideLabel;
        [SerializeField] private Image tideBarTrack;
        [SerializeField] private RectTransform tideBarFill;
        [SerializeField] private GradientImage tideBarGradient;

        [Header("Поле")]
        [SerializeField] private BoardView board;
        [SerializeField] private Image overflowRing;
        [SerializeField] private TMP_Text comboPop;

        [Header("Кінець партії")]
        [SerializeField] private RectTransform overCard;
        [SerializeField] private Image overScrim;
        [SerializeField] private GradientImage overPanel;
        [SerializeField] private Image overPanelStroke;
        [SerializeField] private TMP_Text overScoreLabel;
        [SerializeField] private TMP_Text overScoreNumber;
        [SerializeField] private GradientImage recordChip;
        [SerializeField] private TMP_Text recordChipLabel;
        [SerializeField] private RectTransform rewardRow;
        [SerializeField] private TMP_Text rewardNumber;
        [SerializeField] private TMP_Text rewardSuffix;
        [SerializeField] private TMP_Text overBestLabel;
        [SerializeField] private Button overAgain;
        [SerializeField] private GradientImage overAgainFill;
        [SerializeField] private TMP_Text overAgainLabel;
        [SerializeField] private Button overMenu;
        [SerializeField] private TMP_Text overMenuLabel;
        [SerializeField] private RectTransform confettiRoot;
        [SerializeField] private Image[] confetti = System.Array.Empty<Image>();

        private EndlessSession? _session;
        private BalanceData _balance = BalanceData.Default;
        private EndlessData _config = new EndlessData();

        private Wallet? _wallet;
        private RewardCalculator? _rewards;
        private ProgressData? _progress;

        private LiveRecord _record;
        private int _tideShown;
        private bool _overflow;

        private Coroutine? _playback;
        private Coroutine? _flash;
        private Coroutine? _tideFlash;
        private Coroutine? _combo;
        private Coroutine? _overflowPulse;
        private Coroutine? _confetti;
        private float _idleSince;
        private bool _hintPending;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
            if (overAgain != null)
                overAgain.onClick.AddListener(Restart);
            if (overMenu != null)
                overMenu.onClick.AddListener(() => BackRequested?.Invoke());
            if (board != null)
            {
                board.Router.MoveRequested += OnMoveRequested;
                board.ChainAdvanced += OnChainAdvanced;
            }
        }

        private void OnDestroy()
        {
            if (board == null)
                return;
            board.Router.MoveRequested -= OnMoveRequested;
            board.ChainAdvanced -= OnChainAdvanced;
        }

        /// <summary>
        /// Економіку підставляє композиційний корінь: рекорд читається зі
        /// збереження, нагорода — через той самий RewardCalculator, що й решта гри.
        /// Без цього екран рахував би нагороду за своїми числами.
        /// </summary>
        public void BindEconomy(Wallet wallet, RewardCalculator rewards, ProgressData progress)
        {
            _wallet = wallet;
            _rewards = rewards;
            _progress = progress;
            _record = new LiveRecord(progress.EndlessRecord);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);

            var request = args as EndlessArgs;
            _balance = request?.Balance ?? BalanceData.Default;
            _config = request?.Config ?? new EndlessData();

            StartSession();
            Apply();
        }

        public override void OnExit()
        {
            StopAllRoutines();
            base.OnExit();
        }

        private void StartSession()
        {
            // Сід — час старту: у Нескінченному рандом бажаний, на відміну від
            // рівнів, де розкладка мусить бути та сама при кожному заході.
            var seed = unchecked((uint)System.DateTime.UtcNow.Ticks);
            _session = new EndlessSession(_config, _balance, new XorShiftRandom(seed));
            GameEvents.RaiseSessionStarted(_session);

            _record = new LiveRecord(_progress?.EndlessRecord ?? _record.Stored);
            _tideShown = _session.TideLevel;
            _overflow = false;
            _hintPending = false;
            _idleSince = Time.time;

            if (board != null)
            {
                board.Router.Locked = false;
                board.Bind(_session, seed: 0);
            }

            HideOver();
        }

        /// <summary>Миттєвий рестарт: нова сесія на місці, без перезавантаження сцени.</summary>
        public void Restart()
        {
            StopAllRoutines();
            StartSession();
            Apply();
        }

        private void StopAllRoutines()
        {
            foreach (var routine in new[] { _playback, _flash, _tideFlash, _combo, _overflowPulse, _confetti })
                if (routine != null)
                    StopCoroutine(routine);
            _playback = _flash = _tideFlash = _combo = _overflowPulse = _confetti = null;
        }

        // ── Хід ──

        private void OnMoveRequested(GridPos from, GridPos to)
        {
            if (_session == null || _session.IsOver || board == null || board.Router.Locked)
                return;

            var result = _session.ApplyMove(from, to);
            board.Router.Locked = true;
            _playback = StartCoroutine(PlayAndUnlock(result));
        }

        private IEnumerator PlayAndUnlock(MoveResult result)
        {
            yield return board!.PlayEvents(result);

            ApplyStats();
            GameEvents.RaiseMovePlayed(result);

            board.Router.Locked = false;
            _playback = null;
            _idleSince = Time.time;
            _hintPending = false;

            if (_session != null && _session.IsOver)
            {
                GameEvents.RaiseSessionEnded(_session.State);
                ShowOver();
            }
        }

        private void OnChainAdvanced(int link)
        {
            if (link < 2)
                return;
            if (_combo != null)
                StopCoroutine(_combo);
            _combo = StartCoroutine(ComboRoutine(link));
        }

        /// <summary>
        /// Через 5 секунд без ходу підсвічуємо одну доступну пару. Таймер
        /// перевіряється в Update, але сама підказка йде подією — і саме тим
        /// самим RequestHint, що вже реалізований для Puzzle.
        /// </summary>
        private void Update()
        {
            if (_session == null || _session.IsOver || _hintPending || design == null)
                return;
            if (board == null || board.Router.Locked)
                return;
            if (Time.time - _idleSince < design.HintIdleDelay)
                return;

            _hintPending = true;
            RequestHint();
        }

        public void RequestHint()
        {
            if (_session == null || _session.IsOver)
                return;
            if (DeadlockDetector.TryFindMove(_session.Grid, out var from, out var to))
                GameEvents.RaiseHint(from, to);
        }

        // ── Вигляд ──

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(title, design.FontSizeGameTitle, design.TextMuted,
                FontStyles.Bold, design.LetterSpacingWide);
            if (title != null) title.text = "НЕСКІНЧЕННИЙ";

            ApplyFont(scoreLabel, design.FontSizeStatLabel, design.TextDim,
                FontStyles.Bold, design.LetterSpacingWide);
            if (scoreLabel != null) scoreLabel.text = "РАХУНОК";

            ApplyFont(recordLabel, design.FontSizeStatLabel, design.TextDim,
                FontStyles.Bold, design.LetterSpacingWide);
            if (recordLabel != null) recordLabel.text = "РЕКОРД";

            ApplyFont(queueLabel, design.FontSizeStatLabel, design.TextDim,
                FontStyles.Bold, design.LetterSpacingWide);
            if (queueLabel != null) queueLabel.text = "НАСТУПНІ КРАПЛІ";

            if (scoreCapsule != null) scoreCapsule.color = design.StatCapsuleFill;
            if (scoreCapsuleStroke != null) scoreCapsuleStroke.color = design.StatCapsuleStroke;
            if (scoreCapsuleGlow != null) scoreCapsuleGlow.color = design.ScoreCapsuleGlow;
            if (recordCapsule != null) recordCapsule.color = design.StatCapsuleFill;
            if (recordCapsuleStroke != null) recordCapsuleStroke.color = design.StatCapsuleStroke;
            if (recordCapsuleGlow != null) recordCapsuleGlow.color = design.RecordCapsuleGlow;

            if (tideBadgeStroke != null) tideBadgeStroke.color = design.GlassStroke;
            if (tideBarTrack != null) tideBarTrack.color = design.TideBarTrack;
            if (tideBarGradient != null) tideBarGradient.SetGradient(design.AccentTeal, design.AccentLime);

            if (overflowRing != null) overflowRing.color = Color.clear;
            if (comboPop != null) comboPop.gameObject.SetActive(false);

            ApplyStats();
        }

        /// <summary>
        /// Єдина точка, де рахунок і рекорд потрапляють на екран. Саме тому
        /// перевірка «рахунок перегнав рекорд» не може розійтись між шляхами:
        /// і злиття, і вибух приходять сюди одним і тим самим ходом.
        /// </summary>
        private void ApplyStats()
        {
            if (design == null)
                return;

            var score = _session?.Score ?? 0;

            ApplyFont(scoreNumber, design.FontSizeScoreNumber, design.TextPrimary, FontStyles.Bold, 0f);
            if (scoreNumber != null) scoreNumber.text = Format(score);

            // Момент перетину рахує LiveRecord — і показане число, і спалах
            // беруться з одного джерела, тож розійтись вони не можуть.
            var crossed = _record.Observe(score);

            ApplyFont(recordNumber, design.FontSizeScoreNumber, design.AccentGold, FontStyles.Bold, 0f);
            if (recordNumber != null) recordNumber.text = Format(_record.Shown);

            if (crossed && isActiveAndEnabled)
            {
                if (_flash != null) StopCoroutine(_flash);
                _flash = StartCoroutine(RecordFlashRoutine());
            }

            ApplyQueue();
            ApplyTide();
            ApplyOverflow();
        }

        private void ApplyQueue()
        {
            var queue = _session?.Queue;
            for (var i = 0; i < queueDrops.Length; i++)
            {
                var view = queueDrops[i];
                if (view == null)
                    continue;

                var visible = queue != null && i < queue.Count;
                if (view.gameObject.activeSelf != visible)
                    view.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                var head = i == 0;
                var drop = queue!.Peek(i);
                var size = head ? design.QueueHeadSize : design.QueueTailSize;

                var rect = (RectTransform)view.transform;
                rect.sizeDelta = new Vector2(size, size);
                view.ConfigureForBoard(head ? design.FontSizeQueueHead : design.FontSizeQueueTail);
                view.Show(drop.Color, drop.Density);
            }

            // Кільце навколо голови — окремий об'єкт, бо DropView про черги не знає.
            if (queueHeadRing != null)
                queueHeadRing.color = design.QueueHeadRing;
        }

        private void ApplyTide()
        {
            if (_session == null)
                return;

            var level = _session.TideLevel;
            var active = level > 0;

            ApplyFont(tideLabel, design.FontSizeTideBadge, design.TideText, FontStyles.Bold, 0f);
            if (tideLabel != null) tideLabel.text = $"Приплив ×{level + 1}";

            if (tideBadge != null)
                tideBadge.SetGradient(
                    active ? design.TideBadgeActiveFrom : design.TideBadgeIdle,
                    active ? design.TideBadgeActiveTo : design.TideBadgeIdle);

            if (tideBarFill != null && tideBarFill.parent is RectTransform track)
                tideBarFill.sizeDelta = new Vector2(
                    track.rect.width * Mathf.Clamp01(_session.TideFraction),
                    tideBarFill.sizeDelta.y);

            if (level == _tideShown)
                return;
            _tideShown = level;

            if (isActiveAndEnabled)
            {
                if (_tideFlash != null) StopCoroutine(_tideFlash);
                _tideFlash = StartCoroutine(TideFlashRoutine());
            }
        }

        private void ApplyOverflow()
        {
            if (_session == null || overflowRing == null)
                return;

            var free = _session.Grid.CountFree();
            var warn = !_session.IsOver && free <= design.OverflowWarnFrom;
            if (warn == _overflow)
                return;
            _overflow = warn;

            if (_overflowPulse != null)
            {
                StopCoroutine(_overflowPulse);
                _overflowPulse = null;
            }

            overflowRing.gameObject.SetActive(warn);
            if (warn && isActiveAndEnabled)
            {
                overflowRing.color = design.OverflowWarn;
                _overflowPulse = StartCoroutine(OverflowPulseRoutine());
            }
        }

        // ── Анімації ──

        private IEnumerator RecordFlashRoutine()
        {
            var duration = design.RecordFlashDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                // Через CanvasRenderer: Image.color щокадру просив би перебудову
                // великої капсули.
                var k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                if (recordCapsuleGlow != null)
                    recordCapsuleGlow.canvasRenderer.SetAlpha(Mathf.Lerp(1f, 3.5f, k));
                if (recordCapsule != null)
                    recordCapsule.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.05f, k);
                yield return null;
            }

            if (recordCapsuleGlow != null) recordCapsuleGlow.canvasRenderer.SetAlpha(1f);
            if (recordCapsule != null) recordCapsule.transform.localScale = Vector3.one;
            _flash = null;
        }

        private IEnumerator TideFlashRoutine()
        {
            var duration = design.TideFlashDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                if (tideBadge != null)
                    tideBadge.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, k);
                yield return null;
            }

            if (tideBadge != null) tideBadge.transform.localScale = Vector3.one;
            _tideFlash = null;
        }

        private IEnumerator ComboRoutine(int link)
        {
            if (comboPop == null)
                yield break;

            comboPop.text = $"×{link}";
            ApplyFont(comboPop, design.FontSizeComboPop, design.TextPrimary, FontStyles.Bold, 0f);
            comboPop.gameObject.SetActive(true);

            var rect = (RectTransform)comboPop.transform;
            var origin = rect.localPosition;
            var duration = design.ComboPopDuration;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                // Пружний вихід і плавне згасання вгору.
                var scale = k < 0.3f
                    ? Mathf.Lerp(0.4f, 1.15f, k / 0.3f)
                    : Mathf.Lerp(1.15f, 1f, (k - 0.3f) / 0.7f);
                rect.localScale = Vector3.one * scale;
                rect.localPosition = origin + new Vector3(0f, design.ComboPopRise * k, 0f);
                comboPop.canvasRenderer.SetAlpha(k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                yield return null;
            }

            rect.localPosition = origin;
            rect.localScale = Vector3.one;
            comboPop.canvasRenderer.SetAlpha(1f);
            comboPop.gameObject.SetActive(false);
            _combo = null;
        }

        private IEnumerator OverflowPulseRoutine()
        {
            var period = design.OverflowPulseDuration;
            while (true)
            {
                var k = 0.5f + 0.5f * Mathf.Sin(Time.time / period * Mathf.PI * 2f);
                overflowRing!.canvasRenderer.SetAlpha(Mathf.Lerp(0.35f, 1f, k));
                yield return null;
            }
        }

        // ── Фінал ──

        private void ShowOver()
        {
            if (overCard == null || design == null || _session == null)
                return;

            StopAllRoutines();
            overCard.gameObject.SetActive(true);

            var score = _session.Score;
            var newRecord = _record.Beaten;

            if (overScrim != null) overScrim.color = design.OverScrim;
            if (overPanel != null) overPanel.SetGradient(design.OverCardFrom, design.OverCardTo);
            if (overPanelStroke != null) overPanelStroke.color = design.GlassStroke;

            ApplyFont(overScoreLabel, design.FontSizeOverLabel, design.TextDim,
                FontStyles.Bold, design.LetterSpacingWide);
            if (overScoreLabel != null) overScoreLabel.text = "РАХУНОК";

            ApplyFont(overScoreNumber, design.FontSizeOverScore, design.TextPrimary, FontStyles.Bold, 0f);
            if (overScoreNumber != null) overScoreNumber.text = Format(score);

            Toggle(recordChip, newRecord);
            if (newRecord && recordChip != null)
            {
                recordChip.SetGradient(design.RecordChipFrom, design.RecordChipTo);
                ApplyFont(recordChipLabel, design.FontSizeRecordChip, design.RecordChipText,
                    FontStyles.Bold, design.LetterSpacingWide);
                if (recordChipLabel != null) recordChipLabel.text = "НОВИЙ РЕКОРД";
            }

            var reward = Award(score, newRecord);
            Toggle(rewardRow, reward > 0);
            if (reward > 0 && isActiveAndEnabled)
                StartCoroutine(CountRewardRoutine(reward));

            ApplyFont(overBestLabel, design.FontSizeOverBest, design.TextMuted, FontStyles.Bold, 0f);
            if (overBestLabel != null)
                overBestLabel.text = $"Рекорд · {Format(_record.Shown)}";

            if (overAgainFill != null)
                overAgainFill.SetGradient(design.AccentTeal, design.AccentBlue);
            ApplyFont(overAgainLabel, design.FontSizeOverPrimary, design.TextPrimary, FontStyles.Bold, 0f);
            if (overAgainLabel != null) overAgainLabel.text = "Ще раз";

            ApplyFont(overMenuLabel, design.FontSizeOverSecondary, design.TextMuted, FontStyles.Bold, 0f);
            if (overMenuLabel != null) overMenuLabel.text = "В меню";

            if (_record.Commit() && isActiveAndEnabled)
                _confetti = StartCoroutine(ConfettiRoutine());

        }

        /// <summary>
        /// Нарахування. Рахує <see cref="RewardCalculator"/> — той самий, що й для
        /// рівнів; екран лише показує число. Якщо економіку не підв'язали
        /// (екран відкрили окремою сценою), нагороди просто немає — вигадувати
        /// власну він не має права.
        /// </summary>
        private long Award(int score, bool newRecord)
        {
            if (_wallet == null || _rewards == null || _progress == null)
                return 0;

            var previous = _progress.EndlessRecord;
            var forRecord = _rewards.ForEndlessRecord(score, previous);
            var forMilestones = _rewards.ForMilestones(score, previous);

            if (newRecord)
                _progress.EndlessRecord = score;

            // Дві причини нарахування записуємо окремо: інакше в аналітиці
            // не відрізнити «побив свій рекорд» від «перетнув віху».
            if (forRecord > 0)
                _wallet.Add(forRecord, RewardSource.EndlessRecord);
            if (forMilestones > 0)
                _wallet.Add(forMilestones, RewardSource.EndlessMilestone);

            return forRecord + forMilestones;
        }

        private IEnumerator CountRewardRoutine(long reward)
        {
            ApplyFont(rewardNumber, design.FontSizeRewardNumber, design.RecordChipFrom, FontStyles.Bold, 0f);
            ApplyFont(rewardSuffix, design.FontSizeOverLabel, design.TextDim, FontStyles.Bold, 0f);
            if (rewardSuffix != null) rewardSuffix.text = "нагорода";

            var duration = design.RewardCountDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var shown = (long)Mathf.Round(Mathf.Lerp(0f, reward, Mathf.Clamp01(t / duration)));
                if (rewardNumber != null) rewardNumber.text = $"+{Format(shown)}";
                yield return null;
            }

            if (rewardNumber != null) rewardNumber.text = $"+{Format(reward)}";
        }

        /// <summary>
        /// Роздає конфеті кольори й стартові точки. Окремим методом, а не на
        /// початку корутини: колір графіки не можна чіпати всередині тіла, що
        /// повертає IEnumerator, — це те, що ловить check-ui-animation.
        /// </summary>
        private void PrepareConfetti(Vector3[] starts, float[] drift, float height)
        {
            var spread = design.PaintConfettiSpread;
            for (var i = 0; i < confetti.Length; i++)
            {
                if (confetti[i] == null)
                    continue;
                confetti[i].gameObject.SetActive(true);
                confetti[i].color = design.Ink(ConfettiHue(i));
                starts[i] = new Vector3(
                    Random.Range(-spread, spread),
                    height * 0.5f + Random.Range(0f, spread),
                    0f);
                drift[i] = Random.Range(-spread * 0.3f, spread * 0.3f);
                confetti[i].transform.localPosition = starts[i];
            }
        }

        private IEnumerator ConfettiRoutine()
        {
            if (confettiRoot == null || confetti.Length == 0)
                yield break;

            var duration = design.ConfettiFallDuration;
            var height = confettiRoot.rect.height;

            var starts = new Vector3[confetti.Length];
            var drift = new float[confetti.Length];
            PrepareConfetti(starts, drift, height);

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = t / duration;
                for (var i = 0; i < confetti.Length; i++)
                {
                    if (confetti[i] == null)
                        continue;
                    var rect = (RectTransform)confetti[i].transform;
                    rect.localPosition = starts[i] + new Vector3(
                        drift[i] * k,
                        -height * 1.2f * k * k,
                        0f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, k * 220f * (i % 2 == 0 ? 1f : -1f));
                    confetti[i].canvasRenderer.SetAlpha(k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                }
                yield return null;
            }

            foreach (var piece in confetti)
                if (piece != null)
                {
                    piece.canvasRenderer.SetAlpha(1f);
                    piece.gameObject.SetActive(false);
                }
            _confetti = null;
        }

        /// <summary>
        /// Конфеті беруть кольори чорнила по колу — окремої палітри не заводимо.
        /// Відлік із одиниці: нуль в InkColor — це None, тобто прозора крапля.
        /// </summary>
        private static InkColor ConfettiHue(int index) => (InkColor)(1 + index % 5);

        private void HideOver()
        {
            if (overCard != null && overCard.gameObject.activeSelf)
                overCard.gameObject.SetActive(false);
            foreach (var piece in confetti)
                if (piece != null && piece.gameObject.activeSelf)
                    piece.gameObject.SetActive(false);
        }

        // ── Дрібне ──

        /// <summary>Тисячі відділяємо вузьким пробілом, як у макеті: «8 420».</summary>
        private static string Format(long value) => value.ToString("N0")
            .Replace(",", " ").Replace(" ", " ");

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
