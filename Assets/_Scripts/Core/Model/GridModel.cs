using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Сітка клітинок. Чиста модель без Unity (§18 інваріант 1).
    /// Координати: X — колонка зліва направо, Y — ряд знизу вгору (Y=Height-1 — верхній ряд,
    /// саме він «дострілює» до боса, §5.4).
    /// </summary>
    public sealed class GridModel
    {
        private readonly Cell[] _cells;

        public int Width { get; }
        public int Height { get; }

        public GridModel(int width, int height)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "Сітка має бути мінімум 2×2.");
            Width = width;
            Height = height;
            _cells = new Cell[width * height];
        }

        public Cell this[GridPos p]
        {
            get
            {
                EnsureInside(p);
                return _cells[Index(p)];
            }
            set
            {
                EnsureInside(p);
                _cells[Index(p)] = value;
            }
        }

        public Cell this[int x, int y]
        {
            get => this[new GridPos(x, y)];
            set => this[new GridPos(x, y)] = value;
        }

        public bool Contains(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        /// <summary>
        /// Сусіди по хресту у канонічному порядку Up, Right, Down, Left (§4).
        /// Алокує ітератор — для гарячих циклів використовуй GridPos.NeighborAt (§12).
        /// </summary>
        public IEnumerable<GridPos> Neighbors(GridPos p)
        {
            for (var i = 0; i < GridPos.NeighborCount; i++)
            {
                var neighbor = p.NeighborAt(i);
                if (Contains(neighbor))
                    yield return neighbor;
            }
        }

        /// <summary>
        /// Гало бризок: кільце Мангеттенської відстані рівно 2 від центру (§5.3).
        /// Порядок фіксований (за годинниковою від верхньої точки) — детермінізм бризок.
        /// </summary>
        public static readonly GridPos[] HaloOffsets =
        {
            new GridPos(0, 2), new GridPos(1, 1), new GridPos(2, 0), new GridPos(1, -1),
            new GridPos(0, -2), new GridPos(-1, -1), new GridPos(-2, 0), new GridPos(-1, 1)
        };

        /// <summary>Клітинки гало навколо центру, що лежать у межах сітки.</summary>
        public IEnumerable<GridPos> Halo(GridPos center)
        {
            foreach (var offset in HaloOffsets)
            {
                var p = new GridPos(center.X + offset.X, center.Y + offset.Y);
                if (Contains(p))
                    yield return p;
            }
        }

        public IEnumerable<GridPos> AllPositions()
        {
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    yield return new GridPos(x, y);
        }

        public int CountOccupied()
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
                if (!_cells[i].IsEmpty)
                    count++;
            return count;
        }

        /// <summary>Скільки вільних клітинок (порожні й не заблоковані Wall/Blot).</summary>
        public int CountFree()
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i].IsFree)
                    count++;
            return count;
        }

        /// <summary>Скільки різних кольорів на полі — умова перемоги Puzzle (§5.7).</summary>
        public int CountDistinctColors()
        {
            var seen = 0;
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
            {
                var color = _cells[i].Color;
                if (color == InkColor.None)
                    continue;
                var bit = 1 << (int)color;
                if ((seen & bit) != 0)
                    continue;
                seen |= bit;
                count++;
            }

            return count;
        }

        public GridModel Clone()
        {
            var clone = new GridModel(Width, Height);
            Array.Copy(_cells, clone._cells, _cells.Length);
            return clone;
        }

        /// <summary>Копіює вміст іншої сітки того ж розміру (без алокацій — для симулятора).</summary>
        public void CopyFrom(GridModel other)
        {
            if (other.Width != Width || other.Height != Height)
                throw new ArgumentException("Розміри сіток не збігаються.", nameof(other));
            Array.Copy(other._cells, _cells, _cells.Length);
        }

        /// <summary>Стабільний хеш стану — для дедуплікації станів у LevelSolver (§15).</summary>
        public long StateHash()
        {
            unchecked
            {
                long hash = 17;
                for (var i = 0; i < _cells.Length; i++)
                {
                    var cell = _cells[i];
                    hash = hash * 31 + (int)cell.Color;
                    hash = hash * 31 + cell.Density;
                    hash = hash * 31 + (int)cell.Flags;
                }

                return hash;
            }
        }

        private int Index(GridPos p) => p.Y * Width + p.X;

        private void EnsureInside(GridPos p)
        {
            if (!Contains(p))
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} поза межами сітки {Width}×{Height}.");
        }
    }
}
