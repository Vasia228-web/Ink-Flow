using System.Collections;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Platform;
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
    /// Екран забігу (документ §11): картинка, рахунок і живий рекорд, поле 8×8,
    /// лоток із трьома фігурами в кольорах картинки, попередження про застрягання й чесний фінал.
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

        [Header("Картинка (§11: головний фокус) і краплі (§5)")]
        [SerializeField] private PictureView picture;
        [SerializeField] private DropFlock drops;
        [SerializeField] private CompletionCard completion;

        [Header("Поле і лоток")]
        [SerializeField] private BoardView board;
        [SerializeField] private TrayView tray;
        [SerializeField] private Image boardPlate;
        [SerializeField] private Image boardPlateStroke;
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
        [SerializeField] private TMP_Text overCollectedLabel;
        [SerializeField] private PictureView[] overThumbs = System.Array.Empty<PictureView>();

        [Header("Реклама й донат (§9)")]
        [SerializeField] private Button overContinue;
        [SerializeField] private TMP_Text overContinueLabel;
        [SerializeField] private Button overDouble;
        [SerializeField] private TMP_Text overDoubleLabel;
        [SerializeField] private Button overRescue;
        [SerializeField] private TMP_Text overRescueLabel;
        [SerializeField] private Button overFinishPicture;
        [SerializeField] private TMP_Text overFinishPictureLabel;

        [Header("Картка перед забігом (§6: гра показує картинку заздалегідь)")]
        [SerializeField] private RectTransform introCard;
        [SerializeField] private CanvasGroup introGroup;
        [SerializeField] private Image introScrim;
        [SerializeField] private GradientImage introPanel;
        [SerializeField] private Image introPanelStroke;
        [SerializeField] private PictureView introPicture;
        [SerializeField] private TMP_Text introKicker;
        [SerializeField] private TMP_Text introName;
        [SerializeField] private TMP_Text introRarity;
        [SerializeField] private TMP_Text introHint;
        [SerializeField] private Button introButton;

        private RunSession? _session;
        private BalanceData _balance = BalanceData.Default;

        private Wallet? _wallet;
        private RewardCalculator? _rewards;
        private ProgressData? _progress;
        private PlayerState? _state;
        private Coroutine? _intro;
        private bool _annulled;
        private IAdsService? _ads;
        private IIapService? _iap;
        private bool _finalised;
        private long _rewardForScore;
        private bool _doubled;
        private int _annulledIndex = -1;
        private readonly System.Collections.Generic.List<int> _annulledFilled = new System.Collections.Generic.List<int>(256);
        private readonly System.Collections.Generic.List<int> _filledBuffer = new System.Collections.Generic.List<int>(256);

        /// <summary>Ідентифікатор товару «домалювати картинку одразу» (§9). Ціну показує стор.</summary>
        public const string FinishPictureProductId = "finish_picture";

        /// <summary>Платформні сервіси підставляє композиційний корінь: без них кнопок §9 просто немає.</summary>
        public void BindServices(IAdsService? ads, IIapService? iap)
        {
            _ads = ads;
            _iap = iap;
        }

        private LiveRecord _record;
        private int _dragging = -1;
        private int _pendingCompletion = -1;
        private bool _pendingCollected;
        private MoveResult? _pendingResult;
        private bool _ghostValid;
        private GridPos _ghostAnchor;

        private Coroutine? _playback;
        private Coroutine? _flash;
        private Coroutine? _combo;
        private Coroutine? _confetti;
        private Coroutine? _hint;
        private Coroutine? _completionFlow;
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
                overAgain.onClick.AddListener(OnAgainClicked);
            if (overMenu != null)
                overMenu.onClick.AddListener(() => LeaveRun(() => BackRequested?.Invoke()));
            if (introButton != null)
                introButton.onClick.AddListener(OnIntroTapped);
            if (overContinue != null)
                overContinue.onClick.AddListener(OnContinueClicked);
            if (overDouble != null)
                overDouble.onClick.AddListener(OnDoubleClicked);
            if (overRescue != null)
                overRescue.onClick.AddListener(OnRescueClicked);
            if (overFinishPicture != null)
                overFinishPicture.onClick.AddListener(OnFinishPictureClicked);
            if (board != null)
            {
                board.Router.PlaceRequested += OnPlaceRequested;
                board.ChainAdvanced += OnChainAdvanced;
                board.LineClearing += OnLineClearing;
            }

            if (tray != null)
            {
                tray.DragBegan += OnDragBegan;
                tray.Dragged += OnDragged;
                tray.DragEnded += OnDragEnded;
            }

            if (drops != null)
                drops.Arrived += OnDropArrived;
            if (completion != null)
                completion.Decided += OnCompletionDecided;
        }

        private void OnDestroy()
        {
            if (board != null)
            {
                board.Router.PlaceRequested -= OnPlaceRequested;
                board.ChainAdvanced -= OnChainAdvanced;
                board.LineClearing -= OnLineClearing;
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
            _state = state;
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
            CollectPendingIfAny();
            StopAllRoutines();
            base.OnExit();
        }

        private void StartSession()
        {
            // Сід — час старту: у Нескінченному рандом бажаний.
            var seed = unchecked((uint)System.DateTime.UtcNow.Ticks);
            // §9: незавершена картинка гарантовано перша — з тим самим прогресом і лічильником спроб.
            // §7: колода віддає невидані спершу — тому їй потрібна колекція.
            var collection = _state?.Collection;
            _session = new RunSession(_balance, PieceCatalogData.Default, new XorShiftRandom(seed),
                _state?.Library, _state?.RunStart, collection != null ? collection.Has : (System.Func<string, bool>?)null);
            GameEvents.RaiseSessionStarted(_session);

            _record = new LiveRecord(_progress?.EndlessRecord ?? _record.Stored);
            _finalised = false;
            _annulled = false;
            _rewardForScore = 0;
            _pendingCompletion = -1;
            _pendingCollected = false;
            _pendingResult = null;
            _hintPending = false;
            _dragging = -1;
            _idleSince = Time.time;

            if (board != null)
            {
                board.Router.Locked = false;
                board.Bind(_session);
            }

            tray?.Show(_session.Tray);
            picture?.Show(_session.Picture);
            picture?.ShowAttempts(_session.StartedWithCarried ? _session.AttemptsLeft : 0);
            drops?.Clear();
            completion?.Hide();
            HideOver();
            ShowIntro();
        }

        // ── Картка перед забігом ──

        /// <summary>
        /// §6: «гра показує заздалегідь, яка картинка в цьому забігу». Поле замкнене, поки
        /// картка висить; тап — або кілька секунд — і забіг починається.
        /// </summary>
        private void ShowIntro()
        {
            if (introCard == null || _session == null || design == null)
                return;

            var def = _session.Picture.Picture;
            var theme = ThemeNames.Of(def.ThemeId);
            introCard.gameObject.SetActive(true);
            if (introGroup != null) introGroup.alpha = 1f;
            if (introScrim != null) introScrim.color = design.OverScrim;
            if (introPanel != null) introPanel.SetGradient(design.OverCardFrom, design.OverCardTo);
            if (introPanelStroke != null) introPanelStroke.color = design.RarityColor(def.Rarity);

            ApplyFont(introKicker, design.FontSizeIntroKicker, design.TextDim, FontStyles.Bold, design.LetterSpacingWide);
            if (introKicker != null)
                introKicker.text = _session.StartedWithCarried
                    ? $"НЕЗАВЕРШЕНА · СПРОБ ЛИШИЛОСЬ: {_session.AttemptsLeft}"
                    : $"ЦЬОГО ЗАБІГУ · {theme.ToUpperInvariant()}";
            ApplyFont(introName, design.FontSizeIntroName, design.TextPrimary, FontStyles.Bold, 0f);
            if (introName != null) introName.text = def.Name;
            ApplyFont(introRarity, design.FontSizeIntroRarity, design.RarityColor(def.Rarity), FontStyles.Bold, design.LetterSpacingWide);
            if (introRarity != null) introRarity.text = $"{RarityNames.Of(def.Rarity)} · {RarityNames.Colors(def.FillColors.Count)}";
            ApplyFont(introHint, design.FontSizeIntroHint, design.TextMuted, FontStyles.Bold, 0f);
            if (introHint != null) introHint.text = "Тапни, щоб грати";

            // Силует без кольорів: інтрига лишається, форму видно (уточнення Сесії 1).
            if (_session.StartedWithCarried)
                introPicture?.Show(_session.Picture);
            else
                introPicture?.ShowOutline(def);
            introPicture?.ShowAttempts(_session.StartedWithCarried ? _session.AttemptsLeft : 0);

            if (board != null)
                board.Router.Locked = true;

            if (_intro != null)
                StopCoroutine(_intro);
            if (isActiveAndEnabled)
                _intro = StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            yield return new WaitForSeconds(design.RunIntroDuration);
            yield return FadeIntro();
        }

        private IEnumerator FadeIntro()
        {
            var duration = design.RunIntroFadeDuration;
            for (var t = 0f; t < duration && introGroup != null; t += Time.deltaTime)
            {
                introGroup.alpha = 1f - Mathf.Clamp01(t / duration);
                yield return null;
            }
            HideIntro();
        }

        private void OnIntroTapped()
        {
            if (introCard == null || !introCard.gameObject.activeSelf)
                return;
            if (_intro != null)
                StopCoroutine(_intro);
            _intro = isActiveAndEnabled ? StartCoroutine(FadeIntro()) : null;
            if (!isActiveAndEnabled)
                HideIntro();
        }

        private void HideIntro()
        {
            _intro = null;
            if (introCard != null && introCard.gameObject.activeSelf)
                introCard.gameObject.SetActive(false);
            if (introGroup != null) introGroup.alpha = 1f;
            if (board != null && _session != null && !_session.IsOver)
                board.Router.Locked = false;
            _idleSince = Time.time;
        }

        /// <summary>
        /// Лінія зривається — її краплі вилітають (§5): PixelFilled ідуть у стрічці одразу за
        /// своєю LineCleared, кожна летить із клітинки-джерела у свій піксель, одна за одною.
        /// Піксель кладеться, коли крапля долетіла (<see cref="OnDropArrived"/>). Без пулу
        /// в сцені (стара збірка) пікселі кладуться одразу.
        /// </summary>
        private void OnLineClearing(MoveResult result, int lineEventIndex)
        {
            if (_session == null || picture == null)
                return;
            var events = result.Events;
            var k = 0;
            for (var e = lineEventIndex + 1; e < events.Count; e++)
            {
                var ev = events[e];
                if (ev.Type != GameEventType.PixelFilled)
                    break;
                if (drops == null || board == null || ev.CellCount == 0)
                {
                    picture.PlayPixel(ev.Value);
                    continue;
                }
                drops.Launch(board.CellWorldPosition(result.Cell(ev, 0)), picture.PixelWorldPosition(ev.Value),
                    DesignSystem.PaletteColor(ev.Color), ev.Value, k * design.DropStagger);
                k++;
            }
        }

        private void OnDropArrived(int pixelIndex) => picture?.PlayPixel(pixelIndex);

        private static int CompletedIndex(MoveResult result)
        {
            for (var e = 0; e < result.Events.Count; e++)
                if (result.Events[e].Type == GameEventType.PictureCompleted)
                    return result.Events[e].Value;
            return -1;
        }

        // ── Завершення картинки (§8): пауза, знімок, картка, свайп ──

        private IEnumerator CompletionFlow(int completedIndex)
        {
            if (_session == null)
                yield break;
            _pendingCompletion = completedIndex;
            _pendingCollected = false;

            // Спалах на плитці — і лише тоді знімок: у фоні картки картинка вже сяє.
            picture?.PlayCompleted(null);
            yield return new WaitForSeconds(design != null ? design.PictureCompleteDuration * 0.5f : 0.3f);
            yield return new WaitForEndOfFrame();

            Texture2D? snapshot = null;
            if (design != null && completion != null)
                snapshot = SnapshotBlur.Capture(design.CompletionBlurDownscale, design.CompletionBlurPasses);

            if (completion != null)
                completion.Show(_session.Library[completedIndex], snapshot);
            else
                OnCompletionDecided(true);
            _completionFlow = null;
        }

        /// <summary>Свайп зроблено: у колекцію або ні — і забіг триває: хвиля перефарбування, наступна картинка.</summary>
        private void OnCompletionDecided(bool keep)
        {
            if (_session == null || _pendingCompletion < 0)
                return;
            var index = _pendingCompletion;
            _pendingCompletion = -1;
            if (keep && !_pendingCollected)
                _state?.CollectPicture(_session.Library[index].Id, System.DateTime.UtcNow);
            _pendingCollected = false;

            if (isActiveAndEnabled)
                StartCoroutine(ResumeAfterCompletion());
            else
                ResumeInstantly();
        }

        private IEnumerator ResumeAfterCompletion()
        {
            if (_session == null)
                yield break;
            picture?.Show(_session.Picture);
            picture?.ShowAttempts(0);
            if (board != null && _pendingResult != null)
                yield return board.PlayRecolor(_pendingResult);
            _pendingResult = null;
            tray?.Show(_session.Tray);
            FinishMove();
        }

        private void ResumeInstantly()
        {
            if (_session == null)
                return;
            picture?.Show(_session.Picture);
            picture?.ShowAttempts(0);
            board?.Repaint();
            _pendingResult = null;
            tray?.Show(_session.Tray);
            FinishMove();
        }

        /// <summary>Спільний хвіст ходу: розблокувати поле або зафіксувати програш.</summary>
        private void FinishMove()
        {
            if (board != null)
                board.Router.Locked = false;
            _playback = null;
            _idleSince = Time.time;
            _hintPending = false;

            if (_session != null && _session.IsOver)
            {
                GameEvents.RaiseSessionEnded(_session.State);
                OnLost();
            }
        }

        /// <summary>Поточна картинка з її пікселями — у стан гравця. Порожня не реєструється.</summary>
        private void TrackUnfinished()
        {
            if (_session == null || _state == null || _session.Picture.FilledCount == 0)
                return;
            _session.Picture.FilledIndices(_filledBuffer);
            _state.TrackUnfinished(_session.Picture.LibraryIndex, _filledBuffer);
        }

        /// <summary>
        /// §9: на згортання застосунку прогрес картинки пишеться одразу. Якщо висить картка
        /// завершення — картинка йде в колекцію зараз: убитий застосунок не має її відібрати.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused || _session == null)
                return;
            CollectPendingIfAny();
            if (!_session.IsOver && !_finalised)
                TrackUnfinished();
        }

        /// <summary>Картка завершення висить, а гравець іде геть: картинка — в колекцію, не в нікуди.</summary>
        private void CollectPendingIfAny()
        {
            if (_session == null || _pendingCompletion < 0 || _pendingCollected)
                return;
            _state?.CollectPicture(_session.Library[_pendingCompletion].Id, System.DateTime.UtcNow);
            _pendingCollected = true;
        }

        /// <summary>Миттєвий рестарт: нова сесія на місці, без перезавантаження сцени.</summary>
        public void Restart()
        {
            CollectPendingIfAny();
            StopAllRoutines();
            board?.StopAll();
            StartSession();
            Apply();
        }

        private void StopAllRoutines()
        {
            foreach (var routine in new[] { _playback, _flash, _combo, _confetti, _hint, _completionFlow })
                if (routine != null)
                    StopCoroutine(routine);
            _playback = _flash = _combo = _confetti = _hint = _completionFlow = null;
            _pendingCompletion = -1;
            _pendingResult = null;
            drops?.Clear();
            completion?.Hide();
            picture?.StopAll();
            introPicture?.StopAll();
            if (_intro != null)
                StopCoroutine(_intro);
            _intro = null;
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
            board.ShowGhost(shape, piece.Color, _ghostAnchor, _ghostValid);
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
            // Лише поставлена комірка спорожніє: решта лотка може вже бути перефарбована
            // під нову картинку, а це показується після картки завершення (§8).
            tray?.Slot(trayIndex)?.Show(PieceDef.None);
            _playback = StartCoroutine(PlayAndUnlock(result));
        }

        private IEnumerator PlayAndUnlock(MoveResult result)
        {
            var completed = CompletedIndex(result);
            yield return board!.PlayEvents(result, deferRecolor: completed >= 0);

            // Картинку закінчено: лоток і поле лишаються в старих кольорах до свайпу (§8).
            if (completed < 0)
                tray?.Show(_session!.Tray);
            ApplyStats();
            GameEvents.RaiseMovePlayed(result);

            // §9: прогрес незавершеної — у файл раз на лоток (і на паузу, див. OnApplicationPause).
            if (result.Has(GameEventType.TrayRefilled))
                TrackUnfinished();

            if (completed >= 0)
            {
                _pendingResult = result;
                var index = completed;
                if (drops != null)
                    drops.WhenAllLanded(() => _completionFlow = StartCoroutine(CompletionFlow(index)));
                else
                    _completionFlow = StartCoroutine(CompletionFlow(index));
                yield break;
            }

            FinishMove();
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
            board.ShowGhost(piece.Shape!, piece.Color, anchor, valid: true);
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

            if (comboPop != null) comboPop.gameObject.SetActive(false);

            tray?.Apply();
            picture?.Apply();
            completion?.Apply();
            introPicture?.Apply();
            foreach (var thumb in overThumbs)
                thumb?.Apply();
            if (introCard != null && !Application.isPlaying)
                introCard.gameObject.SetActive(false);
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

        // ── Фінал ──

        // ── Кінець партії: дві фази (§9) ──

        /// <summary>
        /// Програш. Якщо продовжити ще можна і ролик готовий — картка з «Продовжити за ролик»
        /// і «Завершити», без нагород: рахувати їх зараз означало б платити двічі. Інакше —
        /// одразу фінал.
        /// </summary>
        private void OnLost()
        {
            if (_session == null)
                return;
            var canContinue = _session.CanContinue && _ads != null && _ads.IsRewardedReady;
            if (canContinue)
                ShowOver(final: false);
            else
                FinishRun();
        }

        /// <summary>Фінал: §7 (незавершена), §10 (нафта, рекорди, партія дня), картка з нагородами.</summary>
        private void FinishRun()
        {
            if (_session == null || _finalised)
                return;
            CollectPendingIfAny();
            _finalised = true;
            _doubled = false;
            _annulledIndex = -1;
            _annulledFilled.Clear();

            var index = _session.Picture.LibraryIndex;
            _session.Picture.FilledIndices(_filledBuffer);
            // §9: незавершена реєструється або витрачає спробу; на третій — анулюється.
            _annulled = _state?.SettleUnfinished(index, _filledBuffer, _session.StartedWithCarried) ?? false;
            if (_annulled)
            {
                _annulledIndex = index;
                _annulledFilled.AddRange(_filledBuffer);
            }

            ShowOver(final: true);
        }

        private void OnContinueClicked()
        {
            if (_session == null || _ads == null || !_session.CanContinue)
                return;
            _ads.ShowRewarded(watched =>
            {
                if (!watched || _session == null || !_session.CanContinue)
                    return;
                var result = _session.ContinueAfterLoss();
                if (!result.Accepted)
                    return;
                HideOver();
                board?.Bind(_session);
                tray?.Show(_session.Tray);
                if (board != null)
                    board.Router.Locked = false;
                _idleSince = Time.time;
                GameEvents.RaiseSessionStarted(_session);
            });
        }

        private void OnDoubleClicked()
        {
            if (_state == null || _ads == null || _doubled || _rewardForScore <= 0)
                return;
            _ads.ShowRewarded(watched =>
            {
                if (!watched || _state == null || _doubled)
                    return;
                var bonus = _state.DoubleRunReward(_rewardForScore);
                _doubled = true;
                Toggle(overDouble, false);
                if (bonus > 0 && rewardNumber != null)
                    rewardNumber.text = $"+{Format(_rewardForScore * 2 + (_lastReward - _rewardForScore))}";
            });
        }

        private void OnRescueClicked()
        {
            if (_state == null || _ads == null || _annulledIndex < 0)
                return;
            _ads.ShowRewarded(watched =>
            {
                if (!watched || _state == null || _annulledIndex < 0)
                    return;
                _state.RestoreUnfinishedAttempt(_annulledIndex, _annulledFilled);
                _annulledIndex = -1;
                Toggle(overRescue, false);
                if (overBestLabel != null && _session != null)
                    overBestLabel.text = $"«{_session.Library[_state.Unfinished.PictureIndex].Name}» повернуто — одна спроба";
            });
        }

        private void OnFinishPictureClicked()
        {
            if (_state == null || _iap == null || _session == null || _session.Picture.IsComplete)
                return;
            _iap.Buy(FinishPictureProductId, purchase =>
            {
                if (!purchase.Success || _session == null || _state == null)
                    return;
                var def = _session.Picture.Picture;
                var result = _session.CompletePictureNow();
                if (!result.Accepted)
                    return;
                _state.CollectPicture(def.Id, System.DateTime.UtcNow);
                _state.RewardPicture(def.Rarity);
                picture?.Show(_session.Picture);
                Toggle(overFinishPicture, false);
                ShowCollected();
                if (overBestLabel != null)
                    overBestLabel.text = $"«{def.Name}» домальовано — у колекції";
            });
        }

        private void OnAgainClicked()
        {
            if (!_finalised)
            {
                FinishRun();
                return;
            }
            LeaveRun(Restart);
        }

        /// <summary>§9: інтерстиціал між забігами — раз на кілька, і лише коли партія вже завершена.</summary>
        private void LeaveRun(System.Action then)
        {
            if (!_finalised)
                FinishRun();
            if (_state != null && _ads != null && _state.ShouldShowInterstitial && _ads.IsInterstitialReady)
                _ads.ShowInterstitial(then);
            else
                then();
        }

        private long _lastReward;

        private void ShowOver(bool final)
        {
            if (overCard == null || design == null || _session == null)
                return;

            StopAllRoutines();
            overCard.gameObject.SetActive(true);

            var score = _session.Score;
            var newRecord = final && _record.Beaten;

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

            var reward = final ? Award(score) : 0;
            _lastReward = reward;
            Toggle(rewardRow, reward > 0);
            if (reward > 0 && isActiveAndEnabled)
                StartCoroutine(CountRewardRoutine(reward));

            ApplyFont(overBestLabel, design.FontSizeOverBest, design.TextMuted, FontStyles.Bold, 0f);
            Toggle(overBestLabel, final);
            if (overBestLabel != null)
                overBestLabel.text = _annulled
                    ? $"Картинку «{_session.Picture.Picture.Name}» анульовано — спроби вичерпано"
                    : $"Рекорд · {Format(_record.Shown)} · ланцюг ×{_session.BestChain}";

            if (overAgainFill != null)
                overAgainFill.SetGradient(design.AccentTeal, design.AccentBlue);
            ApplyFont(overAgainLabel, design.FontSizeOverPrimary, design.TextPrimary, FontStyles.Bold, 0f);
            if (overAgainLabel != null) overAgainLabel.text = final ? "Ще раз" : "Завершити";

            ApplyFont(overMenuLabel, design.FontSizeOverSecondary, design.TextMuted, FontStyles.Bold, 0f);
            if (overMenuLabel != null) overMenuLabel.text = "В меню";
            Toggle(overMenu, final);

            if (final)
                ShowCollected();
            else
            {
                Toggle(overCollectedLabel, false);
                foreach (var thumb in overThumbs)
                    Toggle(thumb, false);
            }

            ShowAdChips(final);

            if (final && _record.Commit() && isActiveAndEnabled)
                _confetti = StartCoroutine(ConfettiRoutine());
        }

        /// <summary>Чипи §9: продовжити (до фіналу), подвоїти, повернути картинку, домалювати за донат.</summary>
        private void ShowAdChips(bool final)
        {
            if (_session == null || design == null)
                return;
            var adsReady = _ads != null && _ads.IsRewardedReady;

            var showContinue = !final && adsReady && _session.CanContinue;
            Toggle(overContinue, showContinue);
            ApplyFont(overContinueLabel, design.FontSizeOverSecondary, design.TextPrimary, FontStyles.Bold, 0f);
            if (overContinueLabel != null) overContinueLabel.text = "Продовжити за ролик";

            var showDouble = final && adsReady && !_doubled && _rewardForScore > 0;
            Toggle(overDouble, showDouble);
            ApplyFont(overDoubleLabel, design.FontSizeOverSecondary, design.TextPrimary, FontStyles.Bold, 0f);
            if (overDoubleLabel != null) overDoubleLabel.text = "Подвоїти нафту · ролик";

            var showRescue = final && adsReady && _annulledIndex >= 0 &&
                             _session.Library[_annulledIndex].Rarity != Rarity.Common;
            Toggle(overRescue, showRescue);
            ApplyFont(overRescueLabel, design.FontSizeOverSecondary, design.TextPrimary, FontStyles.Bold, 0f);
            if (overRescueLabel != null) overRescueLabel.text = "Повернути картинку · ролик";

            var showFinish = final && _iap != null && _state != null && !_session.Picture.IsComplete &&
                             _session.Picture.FilledCount > 0;
            Toggle(overFinishPicture, showFinish);
            ApplyFont(overFinishPictureLabel, design.FontSizeOverSecondary, design.AccentGold, FontStyles.Bold, 0f);
            if (overFinishPictureLabel != null)
                overFinishPictureLabel.text = $"Домалювати «{_session.Picture.Picture.Name}» одразу";
        }

        /// <summary>Галерея партії (§11 крок 5): що домальовано цього забігу.</summary>
        private void ShowCollected()
        {
            if (_session == null || design == null)
                return;
            var collected = _session.PicturesCollected;
            var any = collected.Count > 0;
            Toggle(overCollectedLabel, any);
            ApplyFont(overCollectedLabel, design.FontSizeOverCollected, design.TextDim, FontStyles.Bold, design.LetterSpacingWide);
            if (overCollectedLabel != null)
                overCollectedLabel.text = collected.Count == 1 ? "ЗІБРАНО КАРТИНКУ" : $"ЗІБРАНО · {collected.Count}";

            for (var i = 0; i < overThumbs.Length; i++)
            {
                var thumb = overThumbs[i];
                if (thumb == null)
                    continue;
                // Останні зібрані — найцікавіші: показуємо хвіст списку.
                var index = collected.Count - overThumbs.Length + i;
                var show = index >= 0 && index < collected.Count;
                Toggle(thumb, show);
                if (show)
                    thumb.ShowCompleted(_session.Library[collected[index]], string.Empty);
            }
        }

        /// <summary>
        /// Підсумок забігу — у стан гравця (§10): очки → нафта з денним множником,
        /// картинки → нафта за рідкістю, рекорди, партія дня. Екран лише показує число.
        /// Якщо стан не підв'язали (екран відкрито окремою сценою), нагороди просто немає.
        /// </summary>
        private long Award(int score)
        {
            if (_state == null || _session == null)
                return 0;

            var reward = _state.CompleteRun(
                RunSummary.Of(score, _session.BestChain, _session.PicturesCollected, _session.Library), System.DateTime.UtcNow);
            _rewardForScore = reward.ForScore;
            return reward.Total;
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
            // Конфеті — у кольорах поточної картинки: дощ «її» фарби, а не випадкової.
            var palette = _session?.Picture.Picture.FillColors;
            for (var i = 0; i < confetti.Length; i++)
            {
                if (confetti[i] == null)
                    continue;
                confetti[i].gameObject.SetActive(true);
                confetti[i].color = palette != null && palette.Count > 0
                    ? DesignSystem.PaletteColor(palette[i % palette.Count])
                    : design.AccentGold;
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
