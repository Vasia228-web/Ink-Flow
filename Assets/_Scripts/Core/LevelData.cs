using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Тип умови перемоги рівня.</summary>
    public enum WinConditionType
    {
        /// <summary>Clear(SingleColor): усі непорожні клітинки — одного кольору (порожня сітка теж рахується).</summary>
        ClearSingleColor,

        /// <summary>Clear(SingleCell): на сітці лишилась максимум одна непорожня клітинка — фінальна крапля.</summary>
        ClearSingleCell,

        /// <summary>ScoreAttack: набрати TargetScore (сума density усіх burst) за MaxMoves ходів.</summary>
        ScoreAttack
    }

    /// <summary>Стартова клітинка рівня.</summary>
    public readonly struct CellSeed
    {
        public int Row { get; }
        public int Col { get; }
        public int Color { get; }
        public int Density { get; }

        public CellSeed(int row, int col, int color, int density)
        {
            Row = row;
            Col = col;
            Color = color;
            Density = density;
        }
    }

    /// <summary>
    /// Чистий POCO-знімок конфігурації рівня. GridModel/GameSession/тести працюють
    /// тільки з цим типом; ScriptableObject LevelConfig лише конвертується сюди.
    /// </summary>
    public sealed class LevelData
    {
        public int GridSize { get; }
        public int ColorsCount { get; }
        public int BurstThreshold { get; }
        public int MaxMoves { get; }
        public WinConditionType WinCondition { get; }
        public int TargetScore { get; }
        public IReadOnlyList<CellSeed> StartingCells { get; }

        public LevelData(
            int gridSize,
            int colorsCount,
            int burstThreshold,
            int maxMoves,
            WinConditionType winCondition,
            int targetScore,
            IReadOnlyList<CellSeed> startingCells)
        {
            if (gridSize < 2)
                throw new ArgumentOutOfRangeException(nameof(gridSize), "Сітка має бути мінімум 2x2.");
            // Поріг < 2 дозволив би нескінченні ланцюги: щойно пофарбована клітинка
            // з density = 1 одразу лопалась би і фарбувала сусідів далі по колу.
            if (burstThreshold < 2)
                throw new ArgumentOutOfRangeException(nameof(burstThreshold), "burstThreshold має бути >= 2.");
            if (maxMoves < 1)
                throw new ArgumentOutOfRangeException(nameof(maxMoves));
            if (colorsCount < 1)
                throw new ArgumentOutOfRangeException(nameof(colorsCount));

            GridSize = gridSize;
            ColorsCount = colorsCount;
            BurstThreshold = burstThreshold;
            MaxMoves = maxMoves;
            WinCondition = winCondition;
            TargetScore = targetScore;
            StartingCells = startingCells ?? Array.Empty<CellSeed>();
        }
    }
}
