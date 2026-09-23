using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Ігрове поле 8×8 (документ §2). Клітинка або порожня, або несе один із трьох
    /// пігментів — жодних густот, флагів і модифікаторів: єдине дієслово за хід —
    /// «постав фігуру».
    ///
    /// Масив байтів, а не структур: бот клонує поле тисячі разів за прогін,
    /// і кожен зайвий байт у клітинці — це секунди.
    /// </summary>
    public sealed class Board
    {
        private readonly Pigment[] _cells;

        public int Width { get; }
        public int Height { get; }

        public Board(int width, int height)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "Поле має бути мінімум 2×2.");
            Width = width;
            Height = height;
            _cells = new Pigment[width * height];
        }

        public int CellCount => _cells.Length;

        public Pigment this[GridPos p]
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

        public Pigment this[int x, int y]
        {
            get => this[new GridPos(x, y)];
            set => this[new GridPos(x, y)] = value;
        }

        public bool Contains(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public bool IsEmpty(GridPos p) => this[p] == Pigment.None;

        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        public int CountEmpty()
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i] == Pigment.None)
                    count++;
            return count;
        }

        public int CountOf(Pigment pigment)
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i] == pigment)
                    count++;
            return count;
        }

        /// <summary>Рядок повністю заповнений — його зриває хід (§2).</summary>
        public bool IsRowFull(int y)
        {
            for (var x = 0; x < Width; x++)
                if (_cells[y * Width + x] == Pigment.None)
                    return false;
            return true;
        }

        public bool IsColumnFull(int x)
        {
            for (var y = 0; y < Height; y++)
                if (_cells[y * Width + x] == Pigment.None)
                    return false;
            return true;
        }

        public Board Clone()
        {
            var clone = new Board(Width, Height);
            Array.Copy(_cells, clone._cells, _cells.Length);
            return clone;
        }

        /// <summary>Копіює поле того ж розміру без алокацій — гарячий шлях бота.</summary>
        public void CopyFrom(Board other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            if (other.Width != Width || other.Height != Height)
                throw new ArgumentException("Розміри полів не збігаються.", nameof(other));
            Array.Copy(other._cells, _cells, _cells.Length);
        }

        /// <summary>Стабільний хеш стану — перевірка детермінізму реплею.</summary>
        public long StateHash()
        {
            unchecked
            {
                long hash = 17;
                for (var i = 0; i < _cells.Length; i++)
                    hash = hash * 31 + (int)_cells[i];
                return hash;
            }
        }

        private int Index(GridPos p) => p.Y * Width + p.X;

        private void EnsureInside(GridPos p)
        {
            if (!Contains(p))
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} поза межами поля {Width}×{Height}.");
        }
    }
}
