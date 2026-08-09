using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>З яким рівнем відкривати екран партії.</summary>
    public sealed class LevelArgs : ScreenArgs
    {
        public LevelArgs(int level, LevelData? data = null)
        {
            Level = level;
            Data = data;
        }

        public int Level { get; }

        /// <summary>Готова розкладка. Якщо null — беремо стартову за номером.</summary>
        public LevelData? Data { get; }
    }

    /// <summary>
    /// Екран партії: шапка, дві капсули статистики й ігрове поле.
    ///
    /// Правил тут немає жодного — усе вирішує <see cref="GameSession"/> з Core.
    /// Екран лише показує стан, віддає намір гравця в сесію і програє те, що
    /// вона повернула. Саме тому «перемога/поразка/тупік» тут читаються з
    /// <see cref="GameState"/>, а не рахуються вдруге.
    /// </summary>
    public sealed class LevelScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button restartButton;

        [Header("Статистика")]
        [SerializeField] private Image movesCapsule;
        [SerializeField] private Image movesCapsuleStroke;
        [SerializeField] private Image movesCapsuleGlow;
        [SerializeField] private TMP_Text movesLabel;
        [SerializeField] private TMP_Text movesNumber;
        [SerializeField] private Image goalCapsule;
        [SerializeField] private Image goalCapsuleStroke;
        [SerializeField] private TMP_Text goalLabel;
        [SerializeField] private TMP_Text goalText;

        [Header("Поле")]
        [SerializeField] private BoardView board;

        [Header("Підсумок")]
        [SerializeField] private RectTransform outcomeCard;
        [SerializeField] private GradientImage outcomeFill;
        [SerializeField] private TMP_Text outcomeTitle;
        [SerializeField] private TMP_Text outcomeNote;
        [SerializeField] private Image[] outcomeStars = System.Array.Empty<Image>();
        [SerializeField] private Button outcomePrimary;
        [SerializeField] private TMP_Text outcomePrimaryLabel;
        [SerializeField] private Button outcomeSecondary;
        [SerializeField] private TMP_Text outcomeSecondaryLabel;

        private PuzzleSession? _session;
        private LevelData? _level;
        private Coroutine? _playback;
        private Coroutine? _warnPulse;
        private bool _warning;

        /// <summary>Назад на карту рівнів.</summary>
        public System.Action? BackRequested;

        /// <summary>Рівень пройдено: номер і зірки.</summary>
        public System.Action<int, int>? LevelCleared;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.Schedule(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
            if (outcomePrimary != null)
                outcomePrimary.onClick.AddListener(OnPrimary);
            if (outcomeSecondary != null)
                outcomeSecondary.onClick.AddListener(() => BackRequested?.Invoke());
            if (board != null)
                board.Router.MoveRequested += OnMoveRequested;
        }

        private void OnDestroy()
        {
            if (board != null)
                board.Router.MoveRequested -= OnMoveRequested;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);

            var request = args as LevelArgs;
            var number = request?.Level ?? 1;
            _level = request?.Data ?? LevelFor(number);

            StartSession();
            Apply();
        }

        /// <summary>
        /// Стартова розкладка за номером. Рівнів поки три — далі йдемо по колу,
        /// щоб карта рівнів була проходима цілком, а не впиралась у четвертий вузол.
        /// </summary>
        private static LevelData LevelFor(int number)
        {
            var all = StarterLevels.All();
            var index = (number - 1) % all.Length;
            if (index < 0)
                index += all.Length;
            return all[index];
        }

        private void StartSession()
        {
            if (_level == null)
                return;

            _session = new PuzzleSession(_level, BalanceData.Default);
            GameEvents.RaiseSessionStarted(_session);

            if (board != null)
            {
                board.Router.Locked = false;
                board.Bind(_session, _level.LevelId);
            }

            HideOutcome();
        }

        /// <summary>
        /// Миттєвий рестарт: сесія перебудовує стартову розкладку на місці.
        /// Ні перезавантаження сцени, ні перестворення крапель — саме тому вкладаємось
        /// у 300 мс навіть на слабкому пристрої.
        /// </summary>
        public void Restart()
        {
            if (_session == null)
                return;

            if (_playback != null)
            {
                StopCoroutine(_playback);
                _playback = null;
            }

            _session.Reset();
            board?.ClearSelection();
            if (board != null)
            {
                board.Router.Locked = false;
                board.Repaint();
            }

            HideOutcome();
            ApplyStats();
            GameEvents.RaiseSessionStarted(_session);
        }

        private void OnMoveRequested(GridPos from, GridPos to)
        {
            if (_session == null || _session.IsOver || board == null || board.Router.Locked)
                return;

            var result = _session.ApplyMove(from, to);

            // Замок тримається весь час програвання: інакше гравець устигав би
            // зробити хід поверх незавершеного ланцюга.
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

            if (_session != null && _session.IsOver)
            {
                GameEvents.RaiseSessionEnded(_session.State);
                ShowOutcome(_session.State);
            }
        }

        // ── Вигляд ──

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(title, design.FontSizeGameTitle, design.TextMuted,
                FontStyles.Bold, design.LetterSpacingWide);
            if (title != null)
                title.text = _level != null && _level.IsBoss
                    ? $"БОС · РІВЕНЬ {_level.LevelId}"
                    : $"РІВЕНЬ {_level?.LevelId ?? 1}";

            ApplyFont(movesLabel, design.FontSizeStatLabel, design.TextDim,
                FontStyles.Bold, design.LetterSpacingWide);
            if (movesLabel != null) movesLabel.text = "ХОДИ";

            ApplyFont(goalLabel, design.FontSizeStatLabel, design.TextDim,
                FontStyles.Bold, design.LetterSpacingWide);
            if (goalLabel != null) goalLabel.text = "ЦІЛЬ";

            ApplyFont(goalText, design.FontSizeGoal, design.TextPrimary, FontStyles.Bold, 0f);
            if (goalText != null)
                goalText.text = _level != null && _level.IsBoss ? "Зафарбуй Клякса" : "Розчисти сітку";

            if (goalCapsule != null) goalCapsule.color = design.StatCapsuleFill;
            if (goalCapsuleStroke != null) goalCapsuleStroke.color = design.StatCapsuleStroke;
            if (movesCapsule != null) movesCapsule.color = design.StatCapsuleFill;

            ApplyStats();
        }

        private void ApplyStats()
        {
            if (design == null)
                return;

            var left = _session?.MovesLeft ?? _level?.MaxMoves ?? 0;
            var over = _session?.IsOver ?? false;
            var warning = !over && left <= design.MovesWarnFrom;

            ApplyFont(movesNumber, design.FontSizeMovesNumber,
                warning ? design.MovesWarnText : design.TextPrimary, FontStyles.Bold, 0f);
            if (movesNumber != null)
                movesNumber.text = left.ToString();

            if (movesCapsuleStroke != null)
                movesCapsuleStroke.color = warning ? design.MovesWarnStroke : design.StatCapsuleStroke;
            if (movesCapsuleGlow != null)
                movesCapsuleGlow.color = warning ? design.MovesWarnGlow : design.MovesCalmGlow;

            if (warning == _warning)
                return;
            _warning = warning;

            // Пульс — окрема корутина, а не щокадрова перевірка: у спокійному стані
            // капсула не має коштувати нічого.
            if (_warnPulse != null)
            {
                StopCoroutine(_warnPulse);
                _warnPulse = null;
                if (movesCapsuleGlow != null)
                    movesCapsuleGlow.canvasRenderer.SetAlpha(1f);
            }

            if (warning && isActiveAndEnabled)
                _warnPulse = StartCoroutine(WarnPulseRoutine());
        }

        private IEnumerator WarnPulseRoutine()
        {
            while (true)
            {
                // Яскравість через CanvasRenderer: Image.color щокадру просив би
                // графіку на перебудову, а капсула — велика.
                var pulse = 0.5f + 0.5f * Mathf.Sin(
                    Time.time / design.MovesWarnPulseDuration * Mathf.PI * 2f);
                if (movesCapsuleGlow != null)
                    movesCapsuleGlow.canvasRenderer.SetAlpha(Mathf.Lerp(0.55f, 1f, pulse));
                if (movesCapsule != null)
                    movesCapsule.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.015f, pulse);
                yield return null;
            }
        }

        // ── Підсумок ──

        private void ShowOutcome(GameState state)
        {
            if (outcomeCard == null || design == null)
                return;

            outcomeCard.gameObject.SetActive(true);
            var won = state == GameState.Won;
            var stars = won ? _session?.Stars ?? 0 : 0;

            if (outcomeFill != null)
                outcomeFill.SetGradient(
                    won ? design.AccentTeal : design.GlassFillRaised,
                    won ? design.AccentPrimary : design.GlassFillRaised);

            ApplyFont(outcomeTitle, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (outcomeTitle != null)
                outcomeTitle.text = state switch
                {
                    GameState.Won => "Рівень пройдено",
                    GameState.Deadlock => "Ходів більше немає",
                    _ => "Ходи скінчились"
                };

            // Тупик — це не «поле померло», а чесно названа причина. Гравець мусить
            // розуміти, що програш стався саме тут і чому.
            ApplyFont(outcomeNote, design.FontSizeShopCard, design.TextMuted, FontStyles.Normal, 0f);
            if (outcomeNote != null)
                outcomeNote.text = state switch
                {
                    GameState.Won => stars >= 3 ? "Бездоганно" : "Спробуй лишити більше ходів на три зірки",
                    GameState.Deadlock => "Жодного дозволеного свайпу не лишилось",
                    _ => "Ліміт ходів вичерпано"
                };

            for (var i = 0; i < outcomeStars.Length; i++)
                if (outcomeStars[i] != null)
                    outcomeStars[i].color = i < stars ? design.AccentGold : design.StarPipEmpty;

            ApplyFont(outcomePrimaryLabel, design.FontSizeSubtitle, design.TextPrimary,
                FontStyles.Bold, 0f);
            if (outcomePrimaryLabel != null)
                outcomePrimaryLabel.text = won ? "Далі" : "Ще раз";

            ApplyFont(outcomeSecondaryLabel, design.FontSizeShopCard, design.TextMuted,
                FontStyles.Bold, 0f);
            if (outcomeSecondaryLabel != null)
                outcomeSecondaryLabel.text = "До карти";

            if (won && _level != null)
                LevelCleared?.Invoke(_level.LevelId, stars);
        }

        private void OnPrimary()
        {
            if (_session?.State == GameState.Won)
                BackRequested?.Invoke();
            else
                Restart();
        }

        private void HideOutcome()
        {
            if (outcomeCard != null && outcomeCard.gameObject.activeSelf)
                outcomeCard.gameObject.SetActive(false);
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
