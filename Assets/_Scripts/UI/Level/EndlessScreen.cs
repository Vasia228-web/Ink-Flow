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
    /// Екран забігу (документ §11, U1Glaze S02Run): шапка «‹ · НЕСКІНЧЕННИЙ · шестерня», ліворуч картинка з
    /// гало рідкості (рішення Сесії 5: без назви й відсотка), праворуч панелі РАХУНОК і РЕКОРД, поле 8×8,
    /// лоток із трьома фігурами в кольорах картинки, тривога «мало місця» й чесний фінал. Накладки — діалоги
    /// U1Glaze на затемненні 78 %: картка перед забігом, картинку домальовано (<see cref="CompletionCard"/>)
    /// і програш у дві фази (поки можна продовжити — один чип продовження; далі — фінал із нагородами).
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
        [SerializeField] private Button settingsButton;

        [Header("Рахунок і рекорд (S02Run_01: дві панелі праворуч від картинки)")]
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text scoreNumber;
        [SerializeField] private TMP_Text recordLabel;
        [SerializeField] private TMP_Text recordNumber;

        [Header("Розкладка (§11: під будь-який екран)")]
        [SerializeField] private RectTransform pictureRoot;
        [SerializeField] private RectTransform statsRoot;
        [SerializeField] private RectTransform boardRoot;
        [SerializeField] private RectTransform trayRoot;

        [Header("Картинка (§11: головний фокус) і краплі (§5)")]
        [SerializeField] private PictureView picture;
        [SerializeField] private DropFlock drops;
        [SerializeField] private CompletionCard completion;

        [Header("Поле і лоток (K1Candy)")]
        [SerializeField] private BoardView board;
        [SerializeField] private TrayView tray;
        [SerializeField] private Image boardPlate;
        [SerializeField] private Image boardPlateStroke;
        [SerializeField] private BoardPulse pulse;
        [SerializeField] private TMP_Text comboPop;

        [Header("Програш (S02Run_05): діалог у дві фази")]
        [SerializeField] private DialogView overDialog;
        [Tooltip("Обгортка панелі: її масштабує FitOverCard, коли діалог вищий за екран (саму панель масштабує поява).")]
        [SerializeField] private RectTransform overFit;
        [SerializeField] private RectTransform overPanel;
        [SerializeField] private TMP_Text overKicker;
        [SerializeField] private TMP_Text overScore;
        [SerializeField] private RectTransform overRecordRow;
        [SerializeField] private TMP_Text overRecordValue;
        [SerializeField] private RectTransform overRecordBadge;
        [SerializeField] private RectTransform overReward;
        [SerializeField] private TMP_Text overRewardNumber;
        [SerializeField] private CandyButton overDouble;
        [SerializeField] private RectTransform overPicture;
        [SerializeField] private RarityFrameView overPictureFrame;
        [SerializeField] private PictureView overPictureThumb;
        [SerializeField] private TMP_Text overPictureTitle;
        [SerializeField] private TMP_Text overPictureNote;
        [SerializeField] private CandyButton overFinish;
        [SerializeField] private TMP_Text overFinishPrice;
        [SerializeField] private TMP_Text overContinueLabel;
        [SerializeField] private CandyButton overContinueAd;
        [SerializeField] private CandyButton overContinueOil;
        [SerializeField] private TMP_Text overContinuePrice;
        [SerializeField] private RectTransform overCollected;
        [SerializeField] private TMP_Text overCollectedLabel;
        [SerializeField] private RarityFrameView[] overThumbFrames = System.Array.Empty<RarityFrameView>();
        [SerializeField] private PictureView[] overThumbs = System.Array.Empty<PictureView>();
        [SerializeField] private CandyButton overAgain;
        [SerializeField] private Button overEnd;
        [SerializeField] private Button overMenu;
        [SerializeField] private RectTransform confettiRoot;
        [SerializeField] private Image[] confetti = System.Array.Empty<Image>();

        [Header("Картка перед забігом (§6, S02Run_02: гра показує картинку заздалегідь)")]
        [SerializeField] private DialogView introDialog;
        [SerializeField] private Button introButton;
        [SerializeField] private TMP_Text introKicker;
        [SerializeField] private RarityFrameView introFrame;
        [SerializeField] private PictureView introPicture;
        [SerializeField] private TMP_Text introName;
        [SerializeField] private RarityBadgeView introBadge;
        [SerializeField] private Image[] introSwatches = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text introColors;
        [SerializeField] private CandyButton introPlay;

        private RunSession? _session;
        private BalanceData _balance = BalanceData.Default;

        private Wallet? _wallet;
        private RewardCalculator? _rewards;
        private ProgressData? _progress;
        private PlayerState? _state;
        private Coroutine? _intro;
        private IAdsService? _ads;
        private bool _finalised;
        private long _rewardForScore;
        private bool _doubled;
        private bool _resumed;
        private bool _layoutDirty;

        /// <summary>
        /// Забіг живий, а поверх екрана — інший (налаштування, §15): OnEnter повертає його без перезапуску
        /// й без картки «ПРОДОВЖЕННЯ». Знімається, коли екран іде зі стеку (SetRoot з «Додому») або
        /// роутер каже «Заново» (<see cref="DiscardSuspendedRun"/>).
        /// </summary>
        private bool _suspended;

        /// <summary>Екран у стеку навігації: перший OnExit входу — призупинення, другий (SetRoot) — вихід зі стеку.</summary>
        private bool _onStack;

        /// <summary>Коефіцієнт макета (402 px) → reference-одиниці (1080), як у збирачі екрана.</summary>
        private const float K = DesignSystem.MockupToReference;

        private static float M(float mockupPx) => Mathf.Round(mockupPx * K);

        /// <summary>Платформні сервіси підставляє композиційний корінь: тут лише реклама — нафта витрачається зі стану (§13).</summary>
        public void BindServices(IAdsService? ads) => _ads = ads;

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
        private Coroutine? _rewardCount;
        private float _idleSince;
        private bool _hintPending;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        private void OnEnable()
        {
            StyleRefresh.Schedule(this, Apply);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugPulseChanged += OnDebugPulseChanged;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnDisable() => DebugPulseChanged -= OnDebugPulseChanged;
#endif

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            if (overAgain != null)
                overAgain.Button.onClick.AddListener(OnAgainClicked);
            if (overEnd != null)
                overEnd.onClick.AddListener(FinishRun);
            if (overMenu != null)
                overMenu.onClick.AddListener(() => LeaveRun(() => BackRequested?.Invoke()));
            if (introButton != null)
                introButton.onClick.AddListener(OnIntroTapped);
            if (introPlay != null)
                introPlay.Button.onClick.AddListener(OnIntroTapped);
            if (overContinueAd != null)
                overContinueAd.Button.onClick.AddListener(OnContinueClicked);
            if (overContinueOil != null)
                overContinueOil.Button.onClick.AddListener(OnContinueClicked);
            if (overDouble != null)
                overDouble.Button.onClick.AddListener(OnDoubleClicked);
            if (overFinish != null)
                overFinish.Button.onClick.AddListener(OnFinishPictureClicked);
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
            _onStack = true;
            // Призупинений забіг — та сама сесія. Програна, але ще не завершена (вийшли посеред програшного ходу)
            // теж: інакше старий зліпок з останнього лотка воскресив би забіг і програш зник би.
            if (_suspended && _session != null && (_session.IsOver || _state == null || _state.SavedRun != null))
                ResumeSuspended();
            else
                StartSession();
            Apply();
        }

        public override void OnExit()
        {
            CollectPendingIfAny();
            SaveRun();
            StopAllRoutines();
            // Екран лишається в стеку (поверх — налаштування): забіг живий, наступний OnEnter його відновить —
            // і програний посеред ходу теж (тоді вхід покаже програш). Другий OnExit того самого входу —
            // SetRoot(хаб): екран іде зі стеку; програний, але не завершений забіг тоді завершується тихо (§10:
            // рекорд і нафта не мають пропасти), а наступний вхід починає зі зліпка.
            if (!_onStack)
                CommitLostRun();
            _suspended = _onStack && _session != null && !_finalised;
            _onStack = false;
            base.OnExit();
        }

        /// <summary>Програний забіг, якому не показали фінал (екран пішов зі стеку): нагорода й рекорд — без картки.</summary>
        private void CommitLostRun()
        {
            if (_session == null || !_session.IsOver || _finalised)
                return;
            CollectPendingIfAny();
            _state?.ClearRun();
            _finalised = true;
            Award(_session.Score);
            _record.Commit();
        }

        /// <summary>Роутер, «Заново» з налаштувань: призупинений забіг забути — наступний вхід стартує нову сесію.</summary>
        public void DiscardSuspendedRun() => _suspended = false;

        /// <summary>
        /// Повернення з налаштувань: та сама сесія, без картки перед забігом — гравець нікуди не йшов.
        /// Вигляд перечитується з моделі, бо OnExit зупинив корутини (краплі, картку завершення) посеред ходу.
        /// </summary>
        private void ResumeSuspended()
        {
            _suspended = false;
            if (_session == null)
                return;
            _pendingCompletion = -1;
            _pendingCollected = false;
            _pendingResult = null;
            _hintPending = false;
            _dragging = -1;

            if (board != null)
                board.Bind(_session);
            tray?.Show(_session.Tray);
            picture?.Show(_session.Picture);
            drops?.Clear();
            completion?.Hide();
            HideOver();
            HideIntro();
            _loggedDanger = DangerLevel.None;
            ShowDanger(immediate: true);
            ApplyStats();
            // Пішли посеред програшного ходу (анімацію обірвав OnExit) — показуємо те, що мало статись: програш.
            if (_session.IsOver)
                OnLost();
        }

        private void StartSession()
        {
            // §7: колода віддає невидані спершу — тому їй потрібна колекція.
            var collection = _state?.Collection;
            var isCollected = collection != null ? collection.Has : (System.Func<string, bool>?)null;
            // §9: перерваний забіг продовжується з того самого місця; інакше — новий, сід — час старту.
            var saved = _state?.SavedRun;
            _resumed = saved != null && _state != null;
            _session = _resumed
                ? new RunSession(_balance, PieceCatalogData.Default, _state!.Library, saved!, isCollected)
                : new RunSession(_balance, PieceCatalogData.Default, new XorShiftRandom(unchecked((uint)System.DateTime.UtcNow.Ticks)),
                    _state?.Library, isCollected);
            GameEvents.RaiseSessionStarted(_session);

            _record = new LiveRecord(_progress?.EndlessRecord ?? _record.Stored);
            _finalised = false;
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
            drops?.Clear();
            completion?.Hide();
            HideOver();
            _loggedDanger = DangerLevel.None;
            ShowDanger(immediate: true);
            ShowIntro();
            if (_session.IsOver)
                OnLost();
        }

        // ── Картка перед забігом (S02Run_02) ──

        /// <summary>
        /// §6: «гра показує заздалегідь, яка картинка в цьому забігу». Поле замкнене, поки
        /// картка висить; тап — або кілька секунд — і забіг починається.
        /// </summary>
        private void ShowIntro()
        {
            if (introDialog == null || _session == null || design == null)
                return;

            var def = _session.Picture.Picture;
            var theme = ThemeNames.Of(def.ThemeId).ToUpperInvariant();
            introDialog.Show();
            if (introKicker != null)
                introKicker.text = _resumed ? $"ПРОДОВЖЕННЯ · {theme}" : $"ЦЕЙ ЗАБІГ · {theme}";
            if (introName != null) introName.text = def.Name;
            introBadge?.SetRarity(def.Rarity);
            introFrame?.SetRarity(def.Rarity);

            // Зразки кольорів картинки — родини в її кольорах, стільки, скільки родин.
            var families = def.FillColors;
            for (var i = 0; i < introSwatches.Length; i++)
            {
                var swatch = introSwatches[i];
                if (swatch == null)
                    continue;
                var on = i < families.Count;
                Toggle(swatch, on);
                if (on)
                    swatch.color = DesignSystem.PaletteColor(families[i]);
            }
            if (introColors != null)
                introColors.text = Plural.Count(families.Count, "колір", "кольори", "кольорів");

            // Силует без кольорів: інтрига лишається, форму видно (уточнення Сесії 1).
            // Продовження — з уже намальованими пікселями: гравець упізнає, де зупинився.
            if (_resumed)
                introPicture?.Show(_session.Picture);
            else
                introPicture?.ShowOutline(def);

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

        /// <summary>Картка гасне сама (прозорість усього діалогу), а не зникає стрибком.</summary>
        private IEnumerator FadeIntro()
        {
            var group = introDialog != null ? introDialog.Group : null;
            introDialog?.Settle();
            var duration = design.RunIntroFadeDuration;
            for (var t = 0f; t < duration && group != null; t += Time.deltaTime)
            {
                group.alpha = 1f - Mathf.Clamp01(t / duration);
                yield return null;
            }
            HideIntro();
        }

        private void OnIntroTapped()
        {
            if (introDialog == null || !introDialog.gameObject.activeSelf)
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
            if (introDialog != null && introDialog.gameObject.activeSelf)
                introDialog.Hide();
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

        // ── Завершення картинки (§8, S02Run_03): пауза, спалах, картка ──

        private IEnumerator CompletionFlow(int completedIndex)
        {
            if (_session == null)
                yield break;
            // Позначено ще в PlayAndUnlock; прапорець «уже зібрано» (пауза посеред польоту) тут не скидаємо.
            _pendingCompletion = completedIndex;

            // Спалах на картинці над полем — і лише тоді картка поверх затемнення.
            picture?.PlayCompleted(null);
            yield return new WaitForSeconds(design != null ? design.PictureCompleteDuration * 0.5f : 0.3f);

            if (completion != null)
                completion.Show(_session.Library[completedIndex]);
            else
                OnCompletionDecided(true);
            _completionFlow = null;
        }

        /// <summary>Рішення ухвалено: у колекцію або ні — і забіг триває: хвиля перефарбування, наступна картинка.</summary>
        private void OnCompletionDecided(bool keep)
        {
            if (_session == null || _pendingCompletion < 0)
                return;
            var index = _pendingCompletion;
            _pendingCompletion = -1;
            if (keep && !_pendingCollected)
                _state?.CollectPicture(_session.Library[index].Id, System.DateTime.UtcNow);
            _pendingCollected = false;

            if (isActiveAndEnabled && Application.isPlaying)
                StartCoroutine(ResumeAfterCompletion());
            else
                ResumeInstantly();
        }

        private IEnumerator ResumeAfterCompletion()
        {
            if (_session == null)
                yield break;
            picture?.Show(_session.Picture, recolor: true); // гало рідкості міняє колір разом із хвилею
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
                ShowDanger(immediate: false);
                GameEvents.RaiseSessionEnded(_session.State);
                OnLost();
                return;
            }
            // §11: пульсація «мало місця» — рівень рахує Core за полем і лотком після ходу.
            ShowDanger(immediate: false);
        }

        // ── Пульсація «мало місця» (§11) ──

        private DangerLevel _loggedDanger;

        /// <summary>
        /// Рівень небезпеки з сесії (після програшу — None) на рамку поля. У дев-збірці: лог у
        /// консоль на кожну зміну рівня і ручне перемикання з дев-панелі (<see cref="SetDebugPulse"/>) —
        /// щоб відрізнити «не спрацьовує» від «не малюється».
        /// </summary>
        private void ShowDanger(bool immediate)
        {
            var level = _session == null || _session.IsOver ? DangerLevel.None : _session.Danger.Level;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_session != null && level != _loggedDanger)
            {
                LastDanger = $"{level} на ході {_session.PlacementCount}: {_session.Danger}";
                Debug.Log($"[InkFlow] Пульсація: {_loggedDanger} → {LastDanger}");
            }
            if (DebugPulse.HasValue)
                level = DebugPulse.Value;
#endif
            _loggedDanger = _session == null || _session.IsOver ? DangerLevel.None : _session.Danger.Level;
            if (immediate)
                pulse?.SetLevelImmediate(level);
            else
                pulse?.SetLevel(level);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Дев-панель: примусовий рівень пульсації (null — рахує гра).</summary>
        public static DangerLevel? DebugPulse { get; private set; }

        /// <summary>Останній рівень, який порахувала гра, — для дев-панелі.</summary>
        public static string LastDanger { get; private set; } = "—";

        private static event System.Action? DebugPulseChanged;

        public static void SetDebugPulse(DangerLevel? level)
        {
            DebugPulse = level;
            DebugPulseChanged?.Invoke();
        }

        private void OnDebugPulseChanged() => ShowDanger(immediate: false);
#endif

        /// <summary>§9: зліпок забігу — у стан гравця. Програний чи завершений забіг не пишеться.</summary>
        private void SaveRun()
        {
            if (_session == null || _state == null || _session.IsOver || _finalised)
                return;
            _state.SaveRun(_session);
        }

        /// <summary>
        /// §9: на згортання застосунку забіг зберігається одразу. Якщо висить картка
        /// завершення — картинка йде в колекцію зараз: убитий застосунок не має її відібрати.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused || _session == null)
                return;
            CollectPendingIfAny();
            SaveRun();
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
            _state?.ClearRun();
            StopAllRoutines();
            board?.StopAll();
            StartSession();
            Apply();
        }

        private void StopAllRoutines()
        {
            foreach (var routine in new[] { _playback, _flash, _combo, _confetti, _hint, _completionFlow, _rewardCount })
                if (routine != null)
                    StopCoroutine(routine);
            _playback = _flash = _combo = _confetti = _hint = _completionFlow = _rewardCount = null;
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
            // §9: програш — зліпку більше немає одразу, а не після анімації: якщо гравець піде посеред неї,
            // старий зліпок з останнього лотка не воскресить забіг.
            if (_session.IsOver)
                _state?.ClearRun();

            board.Router.Locked = true;
            // Лише поставлена комірка спорожніє: решта лотка може вже бути перефарбована
            // під нову картинку, а це показується після картки завершення (§8).
            tray?.Slot(trayIndex)?.Show(PieceDef.None);
            _playback = StartCoroutine(PlayAndUnlock(result));
        }

        private IEnumerator PlayAndUnlock(MoveResult result)
        {
            var completed = CompletedIndex(result);
            // Картинку домальовано вже в моделі: якщо гравець піде посеред анімації чи польоту крапель, OnExit і пауза
            // мусять покласти її в колекцію (CollectPendingIfAny), а не загубити разом із картою, що так і не з'явилась.
            if (completed >= 0)
            {
                _pendingCompletion = completed;
                _pendingCollected = false;
            }
            yield return board!.PlayEvents(result, deferRecolor: completed >= 0);

            // Картинку закінчено: лоток і поле лишаються в старих кольорах до рішення (§8).
            if (completed < 0)
                tray?.Show(_session!.Tray);
            ApplyStats();
            GameEvents.RaiseMovePlayed(result);

            // §9: зліпок забігу — у файл раз на лоток (і на паузу, див. OnApplicationPause).
            if (result.Has(GameEventType.TrayRefilled))
                SaveRun();

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
            if (_layoutDirty)
                Layout();
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

        /// <summary>
        /// Вигляд написів, кеглі й кольори ставить збирач фабриками U1 (ролі типографіки); тут — лише те, що
        /// залежить від стану: тексти, поле, числа, розкладка.
        /// </summary>
        public void Apply()
        {
            if (design == null)
                return;

            if (title != null) title.text = "НЕСКІНЧЕННИЙ";
            if (scoreLabel != null) scoreLabel.text = "РАХУНОК";
            if (recordLabel != null) recordLabel.text = "РЕКОРД";

            // K1Candy: панель поля — спрайт із запеченим градієнтом і обвідкою; під нею — тінь.
            if (boardPlate != null) boardPlate.color = Color.white;
            if (boardPlateStroke != null) boardPlateStroke.color = design.PanelShadow;
            pulse?.Apply();

            if (comboPop != null) comboPop.gameObject.SetActive(false);

            tray?.Apply();
            picture?.Apply();
            completion?.Apply();
            introPicture?.Apply();
            overPictureThumb?.Apply();
            foreach (var thumb in overThumbs)
                thumb?.Apply();
            if (!Application.isPlaying)
            {
                // Майстерня й знімки: накладки лежать вимкненими, поки стан їх не покличе.
                if (introDialog != null) introDialog.gameObject.SetActive(false);
                if (overDialog != null) overDialog.gameObject.SetActive(false);
            }
            Layout();
            ApplyStats();
        }

        // ── Розкладка (§11): лоток при низу, поле — що лишилось, картинка стискається ──

        private void OnRectTransformDimensionsChange() => _layoutDirty = true;

        /// <summary>
        /// Позиції й розміри блоків — з фактичного прямокутника екрана (safe area), а не з
        /// макета: на iPad поле інакше виштовхувало лоток за нижній край. Правило — у
        /// <see cref="RunLayout"/> (Core), яке тримає тест на розмірах реальних пристроїв.
        /// </summary>
        public void Layout()
        {
            _layoutDirty = false;
            var rect = ((RectTransform)transform).rect;
            if (rect.width < 1f || rect.height < 1f)
                return;
            var boardMargin = design != null ? design.BoardSideMargin : BoardGeometry.DefaultSideMargin;
            var canvasMax = design != null ? design.RunCanvasMaxSide : RunLayout.CanvasMaxSide;
            var haloReserve = design != null ? design.RarityHaloReserve : RunLayout.DefaultHaloReserve;
            var layout = RunLayout.For(rect.width / K, rect.height / K, boardMargin, canvasMax, haloReserve);

            if (pictureRoot != null)
            {
                // Блок картинки — полотно плюс запас під гало з кожного боку; полотно — врівень із шапкою зліва.
                pictureRoot.anchorMin = pictureRoot.anchorMax = new Vector2(0f, 1f);
                pictureRoot.pivot = new Vector2(0f, 1f);
                pictureRoot.anchoredPosition = new Vector2(M(layout.PictureLeft), -M(layout.PictureTop));
                pictureRoot.sizeDelta = new Vector2(M(layout.PictureBlock), M(layout.PictureBlock));
                pictureRoot.localScale = Vector3.one;
                picture?.SetCanvasSide(M(layout.CanvasSide));
            }

            if (statsRoot != null)
            {
                statsRoot.anchorMin = statsRoot.anchorMax = new Vector2(0f, 1f);
                statsRoot.pivot = new Vector2(0f, 1f);
                statsRoot.anchoredPosition = new Vector2(M(layout.StatsLeft), -M(layout.StatsTop));
                statsRoot.sizeDelta = new Vector2(M(layout.StatsWidth), M(layout.StatsHeight));
                var group = statsRoot.GetComponent<VerticalLayoutGroup>();
                if (group != null)
                    group.spacing = M(RunLayout.StatsRowGap);
                LayoutRebuilder.ForceRebuildLayoutImmediate(statsRoot);
            }

            if (boardRoot != null)
            {
                boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 1f);
                boardRoot.pivot = new Vector2(0.5f, 1f);
                boardRoot.anchoredPosition = new Vector2(0f, -M(layout.BoardTop));
                boardRoot.sizeDelta = new Vector2(M(layout.BoardSide), M(layout.BoardSide));
            }

            if (trayRoot != null)
            {
                trayRoot.anchorMin = new Vector2(0f, 0f);
                trayRoot.anchorMax = new Vector2(1f, 0f);
                trayRoot.pivot = new Vector2(0.5f, 0f);
                trayRoot.offsetMin = new Vector2(M(layout.BoardMargin), 0f);
                trayRoot.offsetMax = new Vector2(-M(layout.BoardMargin), M(RunLayout.TrayHeight));
            }

            tray?.Layout();
            if (_session != null)
                tray?.Show(_session.Tray);
            board?.ApplyGeometry();
            ShowStats();
            if (overDialog != null && overDialog.gameObject.activeSelf)
                FitOverCard();
        }

        /// <summary>Єдина точка, де рахунок і рекорд потрапляють на екран після ходу.</summary>
        private void ApplyStats()
        {
            if (design == null)
                return;

            var score = _session?.Score ?? 0;

            // Момент перетину рахує LiveRecord — і показане число, і спалах
            // беруться з одного джерела, тож розійтись вони не можуть.
            var crossed = _record.Observe(score);
            ShowStats();

            if (crossed && isActiveAndEnabled)
            {
                if (_flash != null) StopCoroutine(_flash);
                _flash = StartCoroutine(RecordFlashRoutine());
            }
        }

        /// <summary>Числа в панелях — без спостереження рекорду (перерозкладка, зміна пристрою).</summary>
        private void ShowStats()
        {
            if (design == null)
                return;
            ShowStat(scoreNumber, scoreLabel, _session?.Score ?? 0, design.InkText);
            ShowStat(recordNumber, recordLabel, _record.Shown, design.Record);
        }

        /// <summary>
        /// §11: число вміщається в панель для будь-якого значення до 999 999 999 — правило
        /// <see cref="ScoreFormat"/>: повний запис, менший шрифт, далі компактний; цифри табличні.
        /// Найбільший кегль — h1, але не вищий за те, що вміщається під підписом у висоту панелі (SE).
        /// </summary>
        private void ShowStat(TMP_Text? label, TMP_Text? caption, long value, Color color)
        {
            if (label == null || design == null)
                return;
            var panel = label.transform.parent as RectTransform;
            var group = panel != null ? panel.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;
            var column = panel != null && panel.rect.width > 1f
                ? panel.rect.width - (group != null ? group.padding.horizontal : 0f)
                : M(RunLayout.StatsMinWidth);
            var max = Mathf.Min(design.UiH1, NumberFontByHeight(label, caption, panel, group));
            var text = ScoreFormat.Fit(value, column, max, max * design.ScoreMinFontScale, out var size);
            label.enableAutoSizing = false;
            label.fontSize = size;
            label.color = color;
            label.text = Tabular(text);
        }

        /// <summary>Кегль числа, при якому підпис і один рядок числа вміщаються у висоту панелі.</summary>
        private static float NumberFontByHeight(TMP_Text number, TMP_Text? caption, RectTransform? panel,
            HorizontalOrVerticalLayoutGroup? group)
        {
            if (panel == null || panel.rect.height < 1f)
                return float.MaxValue;
            var room = panel.rect.height - (group != null ? group.padding.vertical + group.spacing : 0f)
                       - (caption != null ? caption.preferredHeight : 0f);
            var face = number.font != null ? number.font.faceInfo : default;
            var lineEm = face.pointSize > 0f ? (face.ascentLine - face.descentLine) / face.pointSize : 1.364f;
            return Mathf.Max(1f, room / Mathf.Max(lineEm, 0.5f));
        }

        /// <summary>
        /// Цифри однакової ширини (табличні), щоб ширина не стрибала під час нарахування —
        /// тег <mspace>; розряди розділяє нерозривний пробіл U+00A0 із самого рядка (ScoreFormat),
        /// він проходить у TMP як є.
        /// </summary>
        private string Tabular(string text)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var em = (design != null ? design.ScoreDigitEm : ScoreFormat.DigitEm).ToString("0.00", culture);
            var sb = new System.Text.StringBuilder(text.Length + 32);
            var inDigits = false;
            foreach (var c in text)
            {
                var digit = c >= '0' && c <= '9';
                if (digit && !inDigits) sb.Append("<mspace=").Append(em).Append("em>");
                if (!digit && inDigits) sb.Append("</mspace>");
                sb.Append(c);
                inDigits = digit;
            }
            if (inDigits) sb.Append("</mspace>");
            return sb.ToString();
        }

        // ── Анімації ──

        private IEnumerator RecordFlashRoutine()
        {
            var duration = design.RecordFlashDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                if (recordNumber != null)
                    recordNumber.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, k);
                yield return null;
            }

            if (recordNumber != null) recordNumber.transform.localScale = Vector3.one;
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
            comboPop.gameObject.SetActive(true);
        }

        // ── Кінець партії: дві фази (§9, S02Run_05) ──

        /// <summary>
        /// Програш. Якщо продовжити ще можна — діалог із одним чипом продовження (ролик, а коли ролика вже
        /// не можна — нафта) і «Завершити», без нагород: рахувати їх зараз означало б платити двічі. Інакше —
        /// одразу фінал.
        /// </summary>
        private void OnLost()
        {
            if (_session == null)
                return;
            // §9: програш — забігу більше немає; продовження за ролик чи нафту живе лише в цій сесії.
            _state?.ClearRun();
            if (CanOfferContinue)
                ShowOver(final: false);
            else
                FinishRun();
        }

        /// <summary>
        /// §10, §17: перше продовження за забіг — за ролик (коли він готовий), далі — за нафту (§13).
        /// Саме правило — у <see cref="ContinueOffer"/> без Unity; тут лише факти: скільки було, чи готовий
        /// ролик, чи є гаманець. Чи вистачає нафти — скаже сам чип.
        /// </summary>
        private ContinueKind ContinueKindNow =>
            _session == null || !_session.CanContinue
                ? ContinueKind.None
                : ContinueOffer.Decide(_session.ContinuesUsed, _session.Balance.ContinuesPerRun,
                    _ads != null && _ads.IsRewardedReady, _state != null);

        private bool CanOfferContinue => ContinueKindNow != ContinueKind.None;

        /// <summary>Фінал: §10 (нафта, рекорди, партія дня), картка з нагородами. Недомальована картинка втрачена (§9).</summary>
        private void FinishRun()
        {
            if (_session == null || _finalised)
                return;
            CollectPendingIfAny();
            _state?.ClearRun();
            _finalised = true;
            _doubled = false;
            ShowOver(final: true);
        }

        private void OnContinueClicked()
        {
            var kind = ContinueKindNow;
            if (kind == ContinueKind.None)
                return;
            if (kind == ContinueKind.Ad)
            {
                _ads!.ShowRewarded(watched =>
                {
                    if (!watched || _session == null || !_session.CanContinue)
                        return;
                    ResumeAfterContinue();
                });
                return;
            }

            // Ролик уже був (або реклами немає) — за нафту. Не вистачає — кнопка вже вимкнена, тап нічого не робить.
            if (_state == null || !_state.TryContinueRun())
            {
                ShowContinue();
                return;
            }
            ResumeAfterContinue();
        }

        private void ResumeAfterContinue()
        {
            if (_session == null)
                return;
            var result = _session.ContinueAfterLoss();
            if (!result.Accepted)
                return;
            HideOver();
            board?.Bind(_session);
            tray?.Show(_session.Tray);
            ShowDanger(immediate: true);
            if (board != null)
                board.Router.Locked = false;
            _idleSince = Time.time;
            GameEvents.RaiseSessionStarted(_session);
        }

        /// <summary>
        /// Один чип продовження (рішення автора, ContinueOffer): за ролик — «▶ Ролик»; за нафту — «Продовжити» з
        /// ціною, а коли нафти не вистачає — вимкнена плоска кнопка «Не вистачає нафти» з тією ж ціною.
        /// </summary>
        private void ShowContinue()
        {
            if (_session == null || design == null)
                return;
            var kind = ContinueKindNow;
            Toggle(overContinueAd, kind == ContinueKind.Ad);
            Toggle(overContinueOil, kind == ContinueKind.Oil);
            if (kind != ContinueKind.Oil || overContinueOil == null)
                return;
            var cost = _state != null ? _state.ContinueCost : 0;
            var affordable = _state != null && _state.Wallet.OilDrops >= cost;
            overContinueOil.Text = affordable ? "Продовжити" : "Не вистачає нафти";
            overContinueOil.Interactable = affordable;
            ShowPrice(overContinuePrice, cost, affordable);
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
                if (bonus > 0 && overRewardNumber != null)
                {
                    if (_rewardCount != null)
                        StopCoroutine(_rewardCount);
                    _rewardCount = null;
                    overRewardNumber.text = $"+{Format(_lastReward + bonus)}";
                }
            });
        }

        /// <summary>§13: «домалювати одразу» — за нафту. Ціна за рідкістю, пропорційна решті пікселів.</summary>
        private void OnFinishPictureClicked()
        {
            if (_state == null || _session == null || _session.Picture.IsComplete)
                return;
            var done = _session.Picture;
            var def = done.Picture;
            if (!_state.TryFinishPicture(def.Rarity, 1f - done.FilledFraction))
            {
                ShowFinish();
                return;
            }
            // Модель одразу переходить до наступної картинки — рядок показує ту, що домалювали (її прогрес тепер повний).
            var result = _session.CompletePictureNow();
            if (!result.Accepted)
                return;
            _state.CollectPicture(def.Id, System.DateTime.UtcNow);
            _state.RewardPicture(def.Rarity);
            picture?.Show(_session.Picture);
            Toggle(overFinish, false);
            ShowPictureRow(done);
            ShowCollected();
            FitOverCard();
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

        /// <summary>
        /// Діалог програшу. До фіналу: «МІСЦЯ БІЛЬШЕ НЕМАЄ», рахунок, рекорд, картинка, що згорить, «ПРОДОВЖИТИ ЗАБІГ»
        /// з одним чипом і «Завершити». Фінал: той самий діалог — новий рекорд або рекорд, нагорода з «Подвоїти»,
        /// картинка з «Домалювати» за нафту, зібране за забіг, коралова «Ще раз» і «В меню».
        /// </summary>
        private void ShowOver(bool final)
        {
            if (overDialog == null || design == null || _session == null)
                return;

            StopAllRoutines();
            pulse?.SetLevelImmediate(DangerLevel.None);
            // Фінал після «Завершити» — той самий відкритий діалог: повторна поява блимнула б затемненням.
            if (overDialog.IsShown)
                overDialog.Settle();
            else
                overDialog.Show();

            var score = _session.Score;
            var newRecord = final && _record.Beaten;

            if (overKicker != null) overKicker.text = "МІСЦЯ БІЛЬШЕ НЕМАЄ";
            if (overScore != null) overScore.text = Format(score);

            Toggle(overRecordBadge, newRecord);
            Toggle(overRecordRow, !newRecord);
            if (overRecordValue != null) overRecordValue.text = Format(_record.Shown);

            var reward = final ? Award(score) : 0;
            _lastReward = reward;
            Toggle(overReward, reward > 0);
            if (overRewardNumber != null) overRewardNumber.text = "+0";
            if (reward > 0 && isActiveAndEnabled && Application.isPlaying)
                _rewardCount = StartCoroutine(CountRewardRoutine(reward));
            else if (overRewardNumber != null)
                overRewardNumber.text = $"+{Format(reward)}";
            Toggle(overDouble, final && _ads != null && _ads.IsRewardedReady && !_doubled && _rewardForScore > 0);

            ShowPictureRow();

            var showFinish = final && _state != null && !_session.Picture.IsComplete && _session.Picture.FilledCount > 0;
            Toggle(overFinish, showFinish);
            if (showFinish)
                ShowFinish();

            Toggle(overContinueLabel, !final);
            if (final)
            {
                Toggle(overContinueAd, false);
                Toggle(overContinueOil, false);
            }
            else
                ShowContinue();

            if (final)
                ShowCollected();
            else
                Toggle(overCollected, false);

            Toggle(overAgain, final);
            Toggle(overMenu, final);
            Toggle(overEnd, !final);

            FitOverCard();

            if (final && _record.Commit() && isActiveAndEnabled && Application.isPlaying)
                _confetti = StartCoroutine(ConfettiRoutine());
        }

        /// <summary>
        /// Картинка забігу у вкладеній панелі: «Назва · 55 %» і «Не домалюєш — картинка згорить»; після «Домалювати» —
        /// «Домальовано — у колекції». Немає намальованого (0 пікселів) — рядка немає: втрачати нічого.
        /// </summary>
        private void ShowPictureRow(PictureProgress? shown = null)
        {
            if (_session == null || design == null)
                return;
            var progress = shown ?? _session.Picture;
            var def = progress.Picture;
            var show = progress.FilledCount > 0;
            Toggle(overPicture, show);
            if (!show)
                return;
            overPictureFrame?.SetRarity(def.Rarity);
            if (progress.IsComplete)
            {
                overPictureThumb?.ShowCompleted(def, string.Empty);
                if (overPictureTitle != null) overPictureTitle.text = def.Name;
                if (overPictureNote != null)
                {
                    overPictureNote.text = "Домальовано — у колекції";
                    overPictureNote.color = design.Teal;
                }
                return;
            }
            overPictureThumb?.Show(progress);
            var percent = Mathf.Clamp(Mathf.FloorToInt(progress.FilledFraction * 100f), 0, 99);
            if (overPictureTitle != null) overPictureTitle.text = $"{def.Name} · {percent}%";
            if (overPictureNote != null)
            {
                overPictureNote.text = "Не домалюєш — картинка згорить";
                overPictureNote.color = design.Danger;
            }
        }

        /// <summary>Діалог мусить уміститись по висоті на будь-якому екрані: коли не влазить — меншає цілком.</summary>
        private void FitOverCard()
        {
            if (overPanel == null || overFit == null)
                return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(overPanel);
            var available = ((RectTransform)transform).rect.height - M(16f) * 2f;
            var height = overPanel.rect.height;
            var scale = height > 1f ? Mathf.Min(1f, available / height) : 1f;
            overFit.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>«Домалювати» (§13): ціна в нафті; коли не вистачає — вимкнена плоска «Не вистачає нафти».</summary>
        private void ShowFinish()
        {
            if (_session == null || _state == null || design == null || overFinish == null)
                return;
            var cost = _state.FinishPictureCost(_session.Picture.Picture.Rarity, 1f - _session.Picture.FilledFraction);
            var affordable = _state.Wallet.OilDrops >= cost;
            overFinish.Text = affordable ? "Домалювати" : "Не вистачає нафти";
            overFinish.Interactable = affordable;
            ShowPrice(overFinishPrice, cost, affordable);
        }

        /// <summary>Число в чипі ціни: на бірюзовій — teal-ink, на вимкненій — приглушене.</summary>
        private void ShowPrice(TMP_Text? price, long cost, bool affordable)
        {
            if (price == null || design == null)
                return;
            price.text = ScoreFormat.Full(cost);
            price.color = affordable ? design.TealInk : design.ButtonOffText;
        }

        /// <summary>Галерея партії (§11 крок 5): що домальовано цього забігу — підпис і до трьох мініатюр у рамках.</summary>
        private void ShowCollected()
        {
            if (_session == null || design == null)
                return;
            var collected = _session.PicturesCollected;
            var any = collected.Count > 0;
            Toggle(overCollected, any);
            if (!any)
                return;
            if (overCollectedLabel != null)
                overCollectedLabel.text = collected.Count == 1 ? "ЗІБРАНО КАРТИНКУ" : $"ЗІБРАНО · {collected.Count}";

            for (var i = 0; i < overThumbs.Length; i++)
            {
                var thumb = overThumbs[i];
                var frame = i < overThumbFrames.Length ? overThumbFrames[i] : null;
                // Останні зібрані — найцікавіші: показуємо хвіст списку.
                var index = collected.Count - overThumbs.Length + i;
                var show = index >= 0 && index < collected.Count;
                var holder = frame != null ? frame.transform.parent : thumb != null ? thumb.transform : null;
                if (holder != null && holder.gameObject.activeSelf != show)
                    holder.gameObject.SetActive(show);
                if (!show)
                    continue;
                var def = _session.Library[collected[index]];
                frame?.SetRarity(def.Rarity);
                thumb?.ShowCompleted(def, string.Empty);
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
            var duration = design.RewardCountDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var shown = (long)Mathf.Round(Mathf.Lerp(0f, reward, Mathf.Clamp01(t / duration)));
                if (overRewardNumber != null) overRewardNumber.text = $"+{Format(shown)}";
                yield return null;
            }

            if (overRewardNumber != null) overRewardNumber.text = $"+{Format(reward)}";
            _rewardCount = null;
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
                    : design.Record;
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
            if (overDialog != null && overDialog.gameObject.activeSelf)
                overDialog.Hide();
            foreach (var piece in confetti)
                if (piece != null && piece.gameObject.activeSelf)
                    piece.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        // ── Знімки з редактора (Ink Flow → Debug → Capture Run Screenshots) і UI-тести: стани без корутин ──

        /// <summary>Підставляє готову сесію (поле, лоток, картинка) і показує її як є — без картки перед забігом.</summary>
        public void PreviewSession(RunSession session, PlayerState? state)
        {
            if (state != null)
                BindState(state);
            _session = session;
            _balance = session.Balance;
            _record = new LiveRecord(state?.Progress.EndlessRecord ?? 0);
            _finalised = false;
            _resumed = false;
            Apply();
            if (board != null)
            {
                board.Router.Locked = true;
                board.Bind(session);
            }
            tray?.Show(session.Tray);
            picture?.Show(session.Picture);
            drops?.Clear();
            completion?.Hide();
            HideOver();
            if (introDialog != null) introDialog.gameObject.SetActive(false);
            _loggedDanger = DangerLevel.None;
            ShowDanger(immediate: true);
            ApplyStats();
        }

        /// <summary>Рахунок і рекорд довільної довжини — перевірка, що число не налазить на сусідів.</summary>
        public void PreviewStats(long score, long record)
        {
            if (design == null)
                return;
            ShowStat(scoreNumber, scoreLabel, score, design.InkText);
            ShowStat(recordNumber, recordLabel, record, design.Record);
        }

        public void PreviewPulse(DangerLevel level) => pulse?.SetLevelImmediate(level);

        /// <summary>
        /// Тестам: чи висить картка перед забігом і що на ній написано. Дивимось на корутину, а не на об'єкт:
        /// поза Play Mode <see cref="Apply"/> ховає сам об'єкт картки (майстерня, знімки), але корутина
        /// живе від <see cref="ShowIntro"/> до <see cref="HideIntro"/> — і в Edit Mode теж.
        /// </summary>
        public bool PreviewIntroShown => _intro != null;
        public string PreviewIntroKicker => introKicker != null ? introKicker.text : string.Empty;

        /// <summary>Картка перед забігом поверх поточної сесії (знімок S02Run_02).</summary>
        public void PreviewIntro(bool resumed)
        {
            _resumed = resumed;
            ShowIntro();
        }

        /// <summary>Картка завершеної картинки (S02Run_03): картинка поточної сесії.</summary>
        public void PreviewCompletion()
        {
            if (_session == null || completion == null)
                return;
            _pendingCompletion = -1;
            completion.Show(_session.Picture.Picture);
        }

        /// <summary>Картка завершення й «Викинути», як пальцем (Епічна+ — підтвердження S02Run_04).</summary>
        public void PreviewCompletionDrop()
        {
            PreviewCompletion();
            completion?.PreviewDrop();
        }

        /// <summary>Програш до фіналу (S02Run_05): один чип продовження за правилом ContinueOffer.</summary>
        public void PreviewOverContinue()
        {
            if (_session == null)
                return;
            _finalised = false;
            ShowOver(final: false);
        }

        /// <summary>Програш як фінал: нагороди, «Домалювати», галерея, «Ще раз».</summary>
        public void PreviewOver()
        {
            if (_session == null)
                return;
            _finalised = false;
            FinishRun();
        }

        /// <summary>Тестам: діалог програшу й те, що в ньому видно.</summary>
        public bool PreviewOverShown => overDialog != null && overDialog.gameObject.activeSelf;
        public RectTransform? PreviewOverPanel => overPanel;
        public float PreviewOverScale => overFit != null ? overFit.localScale.x : 1f;
        public CandyButton? PreviewContinueAd => overContinueAd;
        public CandyButton? PreviewContinueOil => overContinueOil;
        public CandyButton? PreviewFinish => overFinish;
        public bool PreviewPictureRowShown => overPicture != null && overPicture.gameObject.activeSelf;
        public string PreviewPictureNote => overPictureNote != null ? overPictureNote.text : string.Empty;
        public CandyButton? PreviewAgain => overAgain;
        public Button? PreviewEnd => overEnd;
        public CompletionCard? PreviewCompletionCard => completion;
        public RectTransform? PreviewStatsRoot => statsRoot;
        public RectTransform? PreviewPictureRoot => pictureRoot;
        public TMP_Text? PreviewScoreNumber => scoreNumber;
        public TMP_Text? PreviewRecordNumber => recordNumber;

        /// <summary>Тестам: тап «Завершити» в першій фазі.</summary>
        public void PreviewEndRun() => overEnd?.onClick.Invoke();
#endif

        // ── Дрібне ──

        /// <summary>Тисячі відділяємо нерозривним пробілом (§11), цифри табличні: «8 420».</summary>
        private string Format(long value) => Tabular(ScoreFormat.Full(value));

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }
    }
}
