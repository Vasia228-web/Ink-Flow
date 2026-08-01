using System;

namespace InkFlow.Core
{
    /// <summary>Стан партії (§4).</summary>
    public enum GameState : byte
    {
        Playing = 0,
        Won = 1,
        Lost = 2,

        /// <summary>Ходи ще є, але жодного дозволеного свайпу не лишилось — окремо від Lost,
        /// щоб гравцю чесно сказали «Тупік», а не змушували тикати в мертве поле.</summary>
        Deadlock = 3
    }

    /// <summary>
    /// Базова партія: сітка, ходи, скор, стан. Спільна для Puzzle/Endless/Boss.
    /// Core не анімує — ApplyMove лише мутує модель і повертає стрічку подій (§4).
    /// </summary>
    public abstract class GameSession
    {
        private readonly MoveResult _result = new MoveResult();
        protected readonly BurstResolver Resolver = new BurstResolver();

        protected GameSession(GridModel grid, BalanceData balance, IRandomSource random, int maxMoves)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            MaxMoves = maxMoves;
            MovesLeft = maxMoves;
        }

        public GridModel Grid { get; }
        public BalanceData Balance { get; }
        protected IRandomSource Random { get; }

        public int MaxMoves { get; }
        public int MovesLeft { get; protected set; }
        public int Score { get; protected set; }
        public GameState State { get; protected set; } = GameState.Playing;

        /// <summary>Скільки вибухів сталося за всю партію — від цього залежить приплив (§5.9).</summary>
        public int TotalBursts { get; private set; }

        /// <summary>Скільки ходів прийнято — бос діє кожен 3-й ПРИЙНЯТИЙ хід (§5.6).</summary>
        public int AcceptedMoves { get; private set; }

        public bool IsOver => State != GameState.Playing;

        /// <summary>Чи витрачає режим ходи (Endless — ні).</summary>
        protected virtual bool ConsumesMoves => true;

        /// <summary>
        /// Застосовує свайп. Повертає стрічку подій; екземпляр переиспользується
        /// між ходами — хто хоче зберегти, копіює (§12).
        /// </summary>
        public MoveResult ApplyMove(GridPos from, GridPos to)
        {
            _result.Reset();

            if (IsOver || !MoveValidator.IsValidMove(Grid, from, to))
                return _result; // Accepted = false, хід НЕ витрачається (§5.1)

            _result.MarkAccepted();
            AcceptedMoves++;

            var source = Grid[from];
            var target = Grid[to];
            var merged = MergeRules.MergedDensity(source, target);

            target.Density = merged;
            Grid[to] = target;
            Grid[from] = new Cell(InkColor.None, 0, source.Flags & ~MoveValidator.Blocking);

            _result.Add(GameEvent.Merge(from, to, target.Color, merged));
            _result.AddScore(ScoreCalculator.MergeScore(merged, Balance));

            if (MergeRules.ReachesThreshold(merged, Balance))
                Resolver.ResolveChain(Grid, to, Balance, Random, _result, BossAbove);

            TotalBursts += _result.ChainDepth;

            if (ConsumesMoves)
                MovesLeft--;

            OnMoveResolved(_result);

            // Комбо-множник застосовується до всього ходу — довгий ланцюг платить нелінійно.
            Score += ScoreCalculator.ApplyCombo(_result.ScoreGained, _result.ChainDepth);

            EvaluateState(_result);
            return _result;
        }

        /// <summary>Бос-рівень: усе, що вилилось за верхній край, влучає в Клякса (§5.6).</summary>
        protected virtual bool BossAbove => false;

        /// <summary>Хук режиму після резолву ланцюга, до перевірки стану (дозаправка, дії боса).</summary>
        protected virtual void OnMoveResolved(MoveResult result)
        {
        }

        /// <summary>Перевірка перемоги/поразки — ЗАВЖДИ після повного завершення ланцюга (§5.7).</summary>
        protected abstract void EvaluateState(MoveResult result);

        /// <summary>Миттєвий рестарт: повертає партію в стартовий стан без перезавантаження сцени.</summary>
        public virtual void Reset()
        {
            MovesLeft = MaxMoves;
            Score = 0;
            TotalBursts = 0;
            AcceptedMoves = 0;
            State = GameState.Playing;
        }

        /// <summary>Позначає тупик, якщо ходів фізично не лишилось (§18 інваріант 5).</summary>
        protected bool MarkDeadlockIfStuck(MoveResult result)
        {
            if (DeadlockDetector.HasAnyMove(Grid))
                return false;

            State = GameState.Deadlock;
            result.Add(GameEvent.Deadlock());
            return true;
        }
    }
}
