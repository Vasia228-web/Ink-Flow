using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Tests
{
    /// <summary>Хелпери побудови LevelData/GridModel для тестів — без ScriptableObject і сцени.</summary>
    internal static class TestLevels
    {
        public static LevelData Make(
            int gridSize = 3,
            int burstThreshold = 10,
            int maxMoves = 20,
            WinConditionType winCondition = WinConditionType.ScoreAttack,
            int targetScore = 9999,
            params CellSeed[] seeds)
        {
            return new LevelData(
                gridSize, 3, burstThreshold, maxMoves,
                winCondition, targetScore, new List<CellSeed>(seeds));
        }

        public static GridModel Grid(int size, params CellSeed[] seeds)
        {
            var grid = new GridModel(size);
            foreach (var seed in seeds)
                grid[seed.Row, seed.Col] = new Cell(seed.Color, seed.Density);
            return grid;
        }

        public static CellSeed Seed(int row, int col, int color, int density) =>
            new CellSeed(row, col, color, density);
    }
}
