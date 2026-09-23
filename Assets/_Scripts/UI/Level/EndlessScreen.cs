using System.Collections;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>З чим відкривати «Нескінченний».</summary>
    public sealed class EndlessArgs : ScreenArgs
    {
        public EndlessArgs(BalanceData? balance = null)
        {
            Balance = balance;
        }

        /// <summary>
        /// Баланс із BalanceConfig.asset. Null — базовий: екран лишається запускним
        /// сам по собі, але в грі його підставляє композиційний корінь. Без цього
        /// правки балансу не доходили б до партії.
        /// </summary>
        public BalanceData? Balance { get; }
    }

    /// <summary>
    /// Екран «Нескінченний» (документ §11, крок 1): рахунок, живий рекорд, поле 8×8,
    /// лоток із трьома фігурами, попередження про переповнення й чесний фінал.
    ///
    /// Правил тут немає — усе рахує <see cref="RunSession"/>. Екран лише показує її
    /// стан, віддає намір гравця (фігура № в клітинку) і програє те, що вона повернула.
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

        [Header("Баки і змішувач")]
        [SerializeField] private TankView[] tanks = System.Array.Empty<TankView>();
        [SerializeField] private MixerView mixer;

        [Header("Поле і лоток")]
        [SerializeField] private BoardView board;
        [SerializeField] private TrayView tray;
        [SerializeField] private Image boardPlate;
        [SerializeField] private Image boardPlateStroke;
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

        private RunSession? _session;
        private BalanceData _balance = BalanceData.Default;

        private Wallet? _wallet;
        private RewardCalculator? _rewards;
        private ProgressData? _progress;

        private LiveRecord _record;
        private bool _overflow;
        private int _dragging = -1;
        private bool _ghostValid;
        private GridPos _ghostAnchor;

        private Coroutine? _playback;
        private Coroutine? _flash;
        private Coroutine? _combo;
        private Coroutine? _overflowPulse;
        private Coroutine? _confetti;
        private Coroutine? _hint;
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
                board.Router.PlaceRequested += OnPlaceRequested;
                board.ChainAdvanced += OnChainAdvanced;
            }

            if (tray != null)
            {
                tray.DragBegan += OnDragBegan;
                tray.Dragged += OnDragged;
                tray.DragEnded += OnDragEnded;
            }
        }

        private void OnDestroy()
        {
            if (board != null)
            {
                board.Router.PlaceRequested -= OnPlaceRequested;
                board.ChainAdvanced -= OnChainAdvanced;
            }

            if (tray != null)
            {
                tray.DragBegan -= OnDragBegan;
                tray.Dragged -= OnDragged;
                tray.DragEnded -= OnDragEnded;
            }
        }

        /// <summary>
        /// Економіку підставляє композиційний корінь: рекорд читається зі
        /// збереження, нагорода — через той самий RewardCalculator, що й решта гри.
        /// </summary>
        public override void BindState(PlayerState state)
        {
            base.BindState(state);
            _wallet = state.Wallet;
            _rewards = state.Rewards;
            _progress = state.Progress;
            _record = new LiveRecord(state.Progress.EndlessRecord);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            var request = args as EndlessArgs;
            _balance = request?.Balance ?? BalanceData.Default;
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
            // Сід — час старту: у Нескінченному рандом бажаний.
            var seed = unchecked((uint)System.DateTime.UtcNow.Ticks);
            _session = new RunSession(_balance, PieceCatalogData.Default, new XorShiftRandom(seed));
            GameEvents.RaiseSessionStarted(_session);

            _record = new LiveRecord(_progress?.EndlessRecord ?? _record.Stored);
            _overflow = false;
            _hintPending = false;
            _dragging = -1;
            _idleSince = Time.time;

            if (board != null)
            {
                board.Router.Locked = false;
                board.Bind(_session);
            }

            tray?.Show(_session.Tray);
            ApplyTanks(animate: false);
            HideOver();
        }

        /// <summary>Рівні баків і змішувача — із сесії, як є. Для старту партії й миттєвих станів.</summary>
        private void ApplyTanks(bool animate)
        {
            if (_session == null)
                return;
            var capacity = _session.Mixer.SplashSize;
            for (var i = 0; i < tanks.Length; i++)
            {
                var tank = tanks[i];
                if (tank == null)
                    continue;
                tank.Show(_session.Tanks[tank.Pigment], capacity, animate);
            }
            mixer?.Show(_session.Tanks.Levels, capacity, _session.Balance, animate);
        }

        private readonly int[] _levelsAfter = new int[Pigments.Count];
        private readonly int[] _taken = new int[Pigments.Count];

        /// <summary>
        /// Програє фарбу ходу: наливання в баки — з подій (рівень після кожного), спрацювання
        /// змішувача — чергою у <see cref="MixerView"/>, яка НЕ блокує поле (§4: «не відволікає»).
        /// </summary>
        private void PlayPaint(MoveResult result)
        {
            if (_session == null)
                return;
            var capacity = _session.Mixer.SplashSize;
            for (var i = 0; i < Pigments.Count; i++)
            {
                _levelsAfter[i] = _session.Tanks.Levels[i];
                _taken[i] = 0;
            }

            var fired = false;
            var events = result.Events;
            for (var e = 0; e < events.Count; e++)
            {
                var ev = events[e];
                switch (ev.Type)
                {
                    case GameEventType.PaintPoured:
                        _levelsAfter[Pigments.IndexOf(ev.Pigment)] = ev.Extra;
                        TankFor(ev.Pigment)?.Show(ev.Extra, capacity, animate: true);
                        break;
                    case GameEventType.TankDrained:
                        _taken[Pigments.IndexOf(ev.Pigment)] = ev.Value;
                        _levelsAfter[Pigments.IndexOf(ev.Pigment)] = ev.Extra;
                        break;
                    case GameEventType.MixerFired:
                        fired = true;
                        mixer?.Fire(new MixerView.Shot
                        {
                            Hue = (Hue)ev.Extra,
                            Taken0 = _taken[0], Taken1 = _taken[1], Taken2 = _taken[2],
                            After0 = _levelsAfter[0], After1 = _levelsAfter[1], After2 = _levelsAfter[2],
                            Capacity = capacity
                        });
                        for (var i = 0; i < Pigments.Count; i++)
                            _taken[i] = 0;
                        break;
                }
            }

            if (!fired && result.PaintYielded > 0)
                mixer?.Show(_session.Tanks.Levels, capacity, _session.Balance, animate: true);
        }

        private TankView? TankFor(Pigment pigment)
        {
            for (var i = 0; i < tanks.Length; i++)
                if (tanks[i] != null && tanks[i].Pigment == pigment)
                    return tanks[i];
            return null;
        }

        /// <summary>Миттєвий рестарт: нова сесія на місці, без перезавантаження сцени.</summary>
        public void Restart()
        {
            StopAllRoutines();
            board?.StopAll();
            StartSession();
            Apply();
        }

        private void StopAllRoutines()
        {
            foreach (var routine in new[] { _playback, _flash, _combo, _overflowPulse, _confetti, _hint })
                if (routine != null)
                    StopCoroutine(routine);
            _playback = _flash = _combo = _overflowPulse = _confetti = _hint = null;
            mixer?.StopAll();
        }

        // ── Перетягування ──

        private void OnDragBegan(int index, PointerEventData eventData)
        {
            if (_session == null || _session.IsOver || board == null || board.Router.Locked)
                return;
            _dragging = index;
            _ghostValid = false;
            tray?.Slot(index)?.SetDragging(true);
            _idleSince = Time.time;
            _hintPending = false;
            UpdateGhost(eventData);
        }

        private void OnDragged(int index, PointerEventData eventData)
        {
            if (_dragging != index)
                return;
            UpdateGhost(eventData);
        }

        private void OnDragEnded(int index, PointerEventData eventData)
        {
            if (_dragging != index)
                return;
            _dragging = -1;
            tray?.Slot(index)?.SetDragging(false);

            var valid = _ghostValid;
            var anchor = _ghostAnchor;
            board?.HideGhost();

            if (valid)
                board?.Router.RequestPlace(index, anchor);
        }

        private void UpdateGhost(PointerEventData eventData)
        {
            if (_session == null || board == null || _dragging < 0)
                return;

            var piece = _session.TrayPieces[_dragging];
            if (piece.IsEmpty)
                return;

            if (!board.TryCellAt(eventData, out var column, out var row))
            {
                _ghostValid = false;
                board.HideGhost();
                return;
            }

            var shape = piece.Shape!;
            _ghostAnchor = BoardGeometry.AnchorFor(shape, column, row);
            _ghostValid = PlacementRules.CanPlace(_session.Board, shape, _ghostAnchor);
            board.ShowGhost(shape, piece.Pigment, _ghostAnchor, _ghostValid);
        }

        // ── Хід ──

        private void OnPlaceRequested(int trayIndex, GridPos anchor)
        {
            if (_session == null || _session.IsOver || board == null || board.Router.Locked)
                return;

            var result = _session.TryPlace(trayIndex, anchor);
            if (!result.Accepted)
                return;

            board.Router.Locked = true;
            tray?.Show(_session.Tray);
            _playback = StartCoroutine(PlayAndUnlock(result));
        }

        private IEnumerator PlayAndUnlock(MoveResult result)
        {
            yield return board!.PlayEvents(result);

            tray?.Show(_session!.Tray);
            PlayPaint(result);
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

        private void OnChainAdvanced(int lines)
        {
            if (lines < 2)
                return;
            if (_combo != null)
                StopCoroutine(_combo);
            if (isActiveAndEnabled)
                _combo = StartCoroutine(ComboRoutine(lines));
        }

        /// <summary>Через кілька секунд без ходу підсвічуємо одну валідну позицію.</summary>
        private void Update()
        {
            if (_session == null || _session.IsOver || _hintPending || design == null)
                return;
            if (board == null || board.Router.Locked || _dragging >= 0)
                return;
            if (Time.time - _idleSince < design.HintIdleDelay)
                return;

            _hintPending = true;
            RequestHint();
        }

        public void RequestHint()
        {
            if (_session == null || _session.IsOver || board == null)
                return;
            if (!_session.TryFindHint(out var index, out var anchor))
                return;

            GameEvents.RaiseHint(index, anchor);
            var piece = _session.TrayPieces[index];
            board.ShowGhost(piece.Shape!, piece.Pigment, anchor, valid: true);
            if (_hint != null)
                StopCoroutine(_hint);
            if (isActiveAndEnabled)
                _hint = StartCoroutine(HintRoutine());
        }

        private IEnumerator HintRoutine()
        {
            yield return new WaitForSeconds(design.HintShowDuration);
            if (_dragging < 0)
                board?.HideGhost();
            _hint = null;
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

            if (scoreCapsule != null) scoreCapsule.color = design.StatCapsuleFill;
            if (scoreCapsuleStroke != null) scoreCapsuleStroke.color = design.StatCapsuleStroke;
            if (scoreCapsuleGlow != null) scoreCapsuleGlow.color = design.ScoreCapsuleGlow;
            if (recordCapsule != null) recordCapsule.color = design.StatCapsuleFill;
            if (recordCapsuleStroke != null) recordCapsuleStroke.color = design.StatCapsuleStroke;
            if (recordCapsuleGlow != null) recordCapsuleGlow.color = design.RecordCapsuleGlow;

            if (boardPlate != null) boardPlate.color = design.BoardPlateFill;
            if (boardPlateStroke != null) boardPlateStroke.color = design.BoardPlateStroke;

            if (overflowRing != null) overflowRing.color = Color.clear;
            if (comboPop != null) comboPop.gameObject.SetActive(false);

            tray?.Apply();
            foreach (var tank in tanks)
                tank?.Apply();
            mixer?.Apply();
            ApplyStats();
        }

        /// <summary>Єдина точка, де рахунок і рекорд потрапляють на екран.</summary>
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

            ApplyOverflow();
        }

        private void ApplyOverflow()
        {
            if (_session == null || overflowRing == null)
                return;

            var warn = !_session.IsOver && _session.HaloWarning;
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

        private IEnumerator ComboRoutine(int lines)
        {
            if (comboPop == null)
                yield break;

            PrepareComboPop(lines);

            var rect = (RectTransform)comboPop.transform;
            var origin = rect.localPosition;
            var duration = design.ComboPopDuration;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
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

        private void PrepareComboPop(int lines)
        {
            comboPop.text = $"×{lines}";
            ApplyFont(comboPop, design.FontSizeComboPop, design.TextPrimary, FontStyles.Bold, 0f);
            comboPop.gameObject.SetActive(true);
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
                overBestLabel.text = $"Рекорд · {Format(_record.Shown)} · ланцюг ×{_session.BestChain}";

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
        /// Нарахування. Рахує <see cref="RewardCalculator"/> — екран лише показує число.
        /// Якщо економіку не підв'язали (екран відкрили окремою сценою), нагороди
        /// просто немає — вигадувати власну він не має права.
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

            if (forRecord > 0)
                _wallet.Add(forRecord, RewardSource.EndlessRecord);
            if (forMilestones > 0)
                _wallet.Add(forMilestones, RewardSource.EndlessMilestone);

            // Рекорд і нафта мусять пережити закриття гри одразу, а не чекати
            // згортання застосунку: партія може бути останньою за сесію.
            State?.Persist();

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

        /// <summary>Роздає конфеті кольори й стартові точки — до старту корутини.</summary>
        private void PrepareConfetti(Vector3[] starts, float[] drift, float height)
        {
            var spread = design.PaintConfettiSpread;
            for (var i = 0; i < confetti.Length; i++)
            {
                if (confetti[i] == null)
                    continue;
                confetti[i].gameObject.SetActive(true);
                confetti[i].color = design.PigmentColor(Pigments.FromIndex(i % Pigments.Count));
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
                    rect.localPosition = starts[i] + new Vector3(drift[i] * k, -height * 1.2f * k * k, 0f);
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
            .Replace(",", " ").Replace(" ", " ");

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
