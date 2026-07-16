using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Чиста C#-модель сітки NxN. Без залежності від Unity API — юніт-тестується
    /// в Edit Mode без завантаження сцени.
    /// </summary>
    public sealed class GridModel
    {
        private readonly Cell[,] _cells;

        public int Size { get; }

        public GridModel(int size)
        {
            if (size < 2)
                throw new ArgumentOutOfRangeException(nameof(size), "Сітка має бути мінімум 2x2.");
            Size = size;
            _cells = new Cell[size, size];
        }

        public Cell this[GridPos pos]
        {
            get => this[pos.Row, pos.Col];
            set => this[pos.Row, pos.Col] = value;
        }

        public Cell this[int row, int col]
        {
            get
            {
                EnsureInside(row, col);
                return _cells[row, col];
            }
            set
            {
                EnsureInside(row, col);
                _cells[row, col] = value;
            }
        }

        public bool IsInside(GridPos pos) => IsInside(pos.Row, pos.Col);

        public bool IsInside(int row, int col) =>
            row >= 0 && row < Size && col >= 0 && col < Size;

        public IEnumerable<GridPos> AllPositions()
        {
            for (var row = 0; row < Size; row++)
                for (var col = 0; col < Size; col++)
                    yield return new GridPos(row, col);
        }

        public int OccupiedCount()
        {
            var count = 0;
            foreach (var pos in AllPositions())
                if (!this[pos].IsEmpty)
                    count++;
            return count;
        }

        /// <summary>
        /// true, якщо всі непорожні клітинки одного кольору.
        /// Повністю порожня сітка теж рахується монохромною (рівень «вичищено»).
        /// </summary>
        public bool IsMonochrome()
        {
            var color = -1;
            foreach (var pos in AllPositions())
            {
                var cell = this[pos];
                if (cell.IsEmpty)
                    continue;
                if (color < 0)
                    color = cell.Color;
                else if (cell.Color != color)
                    return false;
            }

            return true;
        }

        /// <summary>Створює сітку зі стартовими клітинками рівня.</summary>
        public static GridModel FromLevel(LevelData level)
        {
            var grid = new GridModel(level.GridSize);
            foreach (var seed in level.StartingCells)
            {
                if (!grid.IsInside(seed.Row, seed.Col))
                    throw new ArgumentOutOfRangeException(
                        nameof(level),
                        $"Стартова клітинка ({seed.Row},{seed.Col}) поза сіткою {level.GridSize}x{level.GridSize}.");
                grid[seed.Row, seed.Col] = new Cell(seed.Color, seed.Density);
            }

            return grid;
        }

        private void EnsureInside(int row, int col)
        {
            if (!IsInside(row, col))
                throw new ArgumentOutOfRangeException(
                    $"({row},{col})", $"Поза межами сітки {Size}x{Size}.");
        }
    }
}
