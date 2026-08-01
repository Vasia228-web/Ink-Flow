using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Ціль звичайного рівня (майстер-док §4).</summary>
    public enum PuzzleGoal : byte
    {
        /// <summary>Поле «чисте»: лишився один колір АБО одна крапля.</summary>
        Clear = 0,

        /// <summary>Бос-рівень: зафарбувати всі сегменти Клякса в один колір.</summary>
        DefeatBoss = 1
    }

    /// <summary>Стартова клітинка рівня.</summary>
    public readonly struct CellSeed
    {
        public GridPos Position { get; }
        public InkColor Color { get; }
        public int Density { get; }
        public CellFlags Flags { get; }

        public CellSeed(GridPos position, InkColor color, int density, CellFlags flags = CellFlags.None)
        {
            Position = position;
            Color = color;
            Density = density;
            Flags = flags;
        }

        public CellSeed(int x, int y, InkColor color, int density, CellFlags flags = CellFlags.None)
            : this(new GridPos(x, y), color, density, flags)
        {
        }
    }

    /// <summary>
    /// POCO-дзеркало LevelDefinition.asset (§7). Логіка і симулятори працюють тільки з ним —
    /// ScriptableObject лишається в шарі Unity.
    /// </summary>
    public sealed class LevelData
    {
        public int LevelId { get; }
        public int Width { get; }
        public int Height { get; }
        public int MaxMoves { get; }
        public PuzzleGoal Goal { get; }
        public IReadOnlyList<CellSeed> StartingCells { get; }

        /// <summary>Скільки кольорів палітри використовує рівень (для дозаправки/боса).</summary>
        public int ColorsCount { get; }

        /// <summary>Кількість сегментів Клякса (для бос-рівня).</summary>
        public int BossSegments { get; }

        public bool IsBoss => Goal == PuzzleGoal.DefeatBoss;

        public LevelData(
            int levelId,
            int width,
            int height,
            int maxMoves,
            PuzzleGoal goal,
            IReadOnlyList<CellSeed> startingCells,
            int colorsCount = 3,
            int bossSegments = 5)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "Сітка має бути мінімум 2×2.");
            if (maxMoves < 1)
                throw new ArgumentOutOfRangeException(nameof(maxMoves));
            if (colorsCount < 1 || colorsCount > InkColors.MaxCount)
                throw new ArgumentOutOfRangeException(nameof(colorsCount));
            if (goal == PuzzleGoal.DefeatBoss && bossSegments < 1)
                throw new ArgumentOutOfRangeException(nameof(bossSegments));

            LevelId = levelId;
            Width = width;
            Height = height;
            MaxMoves = maxMoves;
            Goal = goal;
            StartingCells = startingCells ?? Array.Empty<CellSeed>();
            ColorsCount = colorsCount;
            BossSegments = bossSegments;
        }

        /// <summary>Будує стартову сітку рівня.</summary>
        public GridModel CreateGrid()
        {
            var grid = new GridModel(Width, Height);
            foreach (var seed in StartingCells)
            {
                if (!grid.Contains(seed.Position))
                    throw new InvalidOperationException(
                        $"Рівень {LevelId}: стартова клітинка {seed.Position} поза сіткою {Width}×{Height}.");
                grid[seed.Position] = new Cell(seed.Color, seed.Density, seed.Flags);
            }

            return grid;
        }
    }

    /// <summary>Налаштування партії «Нескінченний» (§5.8-5.9).</summary>
    public sealed class EndlessData
    {
        public int Width { get; }
        public int Height { get; }
        public int ColorsCount { get; }

        public EndlessData(int width = 6, int height = 6, int colorsCount = 4)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (colorsCount < 2 || colorsCount > InkColors.MaxCount)
                throw new ArgumentOutOfRangeException(nameof(colorsCount), "Треба щонайменше 2 кольори.");
            Width = width;
            Height = height;
            ColorsCount = colorsCount;
        }
    }
}
