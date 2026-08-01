using System.Collections.Generic;

namespace InkFlow.Core.Tests
{
    /// <summary>Хелпери побудови сітки й рівнів для тестів — без Unity API і без сцени.</summary>
    internal static class TestBoard
    {
        public const InkColor A = InkColor.Magenta;
        public const InkColor B = InkColor.Cyan;
        public const InkColor C = InkColor.Amber;

        public static GridModel Grid(int width, int height, params CellSeed[] seeds)
        {
            var grid = new GridModel(width, height);
            foreach (var seed in seeds)
                grid[seed.Position] = new Cell(seed.Color, seed.Density, seed.Flags);
            return grid;
        }

        public static CellSeed At(int x, int y, InkColor color, int density, CellFlags flags = CellFlags.None) =>
            new CellSeed(x, y, color, density, flags);

        public static GridPos P(int x, int y) => new GridPos(x, y);

        public static LevelData Level(
            int width = 4,
            int height = 4,
            int maxMoves = 10,
            PuzzleGoal goal = PuzzleGoal.Clear,
            int levelId = 1,
            int colorsCount = 3,
            int bossSegments = 5,
            params CellSeed[] seeds) =>
            new LevelData(levelId, width, height, maxMoves, goal, new List<CellSeed>(seeds), colorsCount, bossSegments);

        /// <summary>Баланс за замовчуванням із можливістю точково змінити поріг/дільники.</summary>
        public static BalanceData Balance(
            int burstThreshold = 10,
            int paintPowerDivisor = 10,
            int splashDivisor = 15,
            int maxChainBursts = 64,
            int bossActsEveryMoves = 3) =>
            new BalanceData(
                burstThreshold: burstThreshold,
                paintPowerDivisor: paintPowerDivisor,
                splashDivisor: splashDivisor,
                maxChainBursts: maxChainBursts,
                bossActsEveryMoves: bossActsEveryMoves);

        /// <summary>RNG, що завжди повертає 0 — робить бризки передбачуваними в тестах.</summary>
        public sealed class FixedRandom : IRandomSource
        {
            private readonly int _value;
            public FixedRandom(int value = 0) => _value = value;
            public int Next(int maxExclusive) => _value % maxExclusive;
            public uint State => (uint)_value;
        }

        /// <summary>Проганяє ланцюг з позиції та повертає стрічку подій (без сесії).</summary>
        public static MoveResult ResolveAt(GridModel grid, GridPos origin, BalanceData balance,
            IRandomSource? random = null, bool bossAbove = false)
        {
            var result = new MoveResult();
            new BurstResolver().ResolveChain(grid, origin, balance, random ?? new FixedRandom(), result, bossAbove);
            return result;
        }

        /// <summary>Перша подія заданого типу (або кидає, якщо її немає).</summary>
        public static GameEvent FirstEvent(MoveResult result, GameEventType type)
        {
            foreach (var e in result.Events)
                if (e.Type == type)
                    return e;
            throw new KeyNotFoundException($"У стрічці немає події {type}.");
        }
    }
}
