using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Режим «Рівні» (майстер-док §4). Ціль — довести поле до чистого стану за ліміт ходів.
    /// Вибух — інструмент, а не обов'язок: перемога самими злиттями цілком легальна (§18.5).
    /// </summary>
    public class PuzzleSession : GameSession
    {
        public PuzzleSession(LevelData level, BalanceData balance, IRandomSource? random = null)
            : base((level ?? throw new ArgumentNullException(nameof(level))).CreateGrid(),
                   balance,
                   random ?? XorShiftRandom.ForLevel(level.LevelId),
                   level.MaxMoves)
        {
            Level = level;
        }

        public LevelData Level { get; }

        /// <summary>Зірки за поточний результат (рахувати має сенс лише після перемоги).</summary>
        public virtual int Stars => StarCalculator.Stars(MovesLeft, MaxMoves, Balance);

        /// <summary>Поле «чисте»: один колір АБО одна крапля (§5.7).</summary>
        public static bool IsPuzzleWon(GridModel grid) =>
            grid.CountDistinctColors() <= 1 || grid.CountOccupied() <= 1;

        protected override void EvaluateState(MoveResult result)
        {
            // Порядок важливий: перемога останнім ходом — це перемога, а не поразка.
            if (IsWinConditionMet())
            {
                State = GameState.Won;
                return;
            }

            if (MovesLeft <= 0)
            {
                State = GameState.Lost;
                return;
            }

            // Тупик: ходи ще є, але жодного дозволеного свайпу немає.
            MarkDeadlockIfStuck(result);
        }

        protected virtual bool IsWinConditionMet() => IsPuzzleWon(Grid);

        public override void Reset()
        {
            base.Reset();
            RebuildGrid();
        }

        /// <summary>Відновлює стартову розкладку рівня (миттєвий ↺ < 300 мс, без завантаження сцени).</summary>
        protected void RebuildGrid()
        {
            for (var y = 0; y < Grid.Height; y++)
                for (var x = 0; x < Grid.Width; x++)
                    Grid[x, y] = Cell.Empty;

            foreach (var seed in Level.StartingCells)
                Grid[seed.Position] = new Cell(seed.Color, seed.Density, seed.Flags);
        }
    }
}
