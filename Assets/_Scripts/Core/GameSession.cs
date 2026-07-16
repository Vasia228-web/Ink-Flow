using System;

namespace InkFlow.Core
{
    public enum SessionState
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// Стан однієї партії: сітка, ходи, скор, win/lose. Чистий POCO —
    /// GridController лише делегує сюди, а тести ганяють партію без сцени.
    /// Retry = Reset(): миттєва реініціалізація з LevelData без перезавантаження сцени.
    /// </summary>
    public sealed class GameSession
    {
        public LevelData Level { get; }
        public GridModel Grid { get; private set; }
        public int MovesLeft { get; private set; }
        public int Score { get; private set; }
        public SessionState State { get; private set; }

        /// <summary>Після кожного свайпу (і валідного, і відскоку).</summary>
        public event Action<MoveResult> MoveApplied;

        /// <summary>Після Reset (старт або Retry).</summary>
        public event Action SessionReset;

        /// <summary>Коли State перейшов у Won/Lost.</summary>
        public event Action<SessionState> StateChanged;

        public GameSession(LevelData level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Reset();
        }

        /// <summary>Миттєвий Retry: повертає стартовий стан рівня.</summary>
        public void Reset()
        {
            Grid = GridModel.FromLevel(Level);
            MovesLeft = Level.MaxMoves;
            Score = 0;
            State = SessionState.Playing;
            SessionReset?.Invoke();
        }

        /// <summary>
        /// Застосовує свайп. Хід витрачається ТІЛЬКИ при валідному merge;
        /// відскок (різні кольори / порожня ціль) — безкоштовний.
        /// Win перевіряється до Lose: перемога останнім ходом — це перемога.
        /// </summary>
        public MoveResult TryMove(GridPos from, Direction direction)
        {
            if (State != SessionState.Playing)
                return MoveResult.Rejected(from, from);

            var result = GameRules.ApplyMove(Grid, from, direction, Level.BurstThreshold);
            if (result.IsValidMerge)
            {
                MovesLeft--;
                Score += result.ScoreGained;
                EvaluateState();
            }

            MoveApplied?.Invoke(result);
            if (State != SessionState.Playing)
                StateChanged?.Invoke(State);
            return result;
        }

        private void EvaluateState()
        {
            var won = Level.WinCondition switch
            {
                WinConditionType.ClearSingleColor => Grid.IsMonochrome(),
                WinConditionType.ClearSingleCell => Grid.OccupiedCount() <= 1,
                WinConditionType.ScoreAttack => Score >= Level.TargetScore,
                _ => false
            };

            if (won)
                State = SessionState.Won;
            else if (MovesLeft <= 0)
                State = SessionState.Lost;
        }
    }
}
