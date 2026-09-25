using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Ігрове поле 8×8 (документ §2). Клітинка або порожня (0), або несе індекс кольору
    /// майстер-палітри — і це завжди колір заливки поточної картинки (§5).
    ///
    /// Масив байтів, а не структур: бот клонує поле тисячі разів за прогін.
    /// </summary>
    public sealed class Board
    {
        public const byte Empty = MasterPalette.Empty;

        private readonly byte[] _cells;

        public int Width { get; }
        public int Height { get; }

        public Board(int width, int height)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "Поле має бути мінімум 2×2.");
            Width = width;
            Height = height;
            _cells = new byte[width * height];
        }

        public int CellCount => _cells.Length;

        public byte this[GridPos p]
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

        public byte this[int x, int y]
        {
            get => this[new GridPos(x, y)];
            set => this[new GridPos(x, y)] = value;
        }

        public bool Contains(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public bool IsEmpty(GridPos p) => this[p] == Empty;

        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        public int CountEmpty()
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i] == Empty)
                    count++;
            return count;
        }

        public int CountOf(byte color)
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i] == color)
                    count++;
            return count;
        }

        /// <summary>Скільки клітинок кожного кольору: індекс масиву — індекс палітри.</summary>
        public void CountColors(int[] into)
        {
            if (into is null) throw new ArgumentNullException(nameof(into));
            Array.Clear(into, 0, into.Length);
            for (var i = 0; i < _cells.Length; i++)
                if (_cells[i] != Empty && _cells[i] < into.Length)
                    into[_cells[i]]++;
        }

        /// <summary>
        /// Перефарбовує всі клітинки одного кольору в інший (§5: колір закінчився або
        /// прийшла нова картинка). Клітинки, що змінились, — у буфер викликача, щоб в'ю
        /// пустила по них хвилю. Повертає, скільки змінилось.
        /// </summary>
        public int Recolor(byte from, byte to, List<GridPos>? changed = null)
        {
            if (from == Empty || to == Empty)
                throw new ArgumentOutOfRangeException(nameof(from), "Порожнє не перефарбовується.");
            var n = 0;
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != from)
                    continue;
                _cells[i] = to;
                changed?.Add(new GridPos(i % Width, i / Width));
                n++;
            }
            return n;
        }

        /// <summary>Рядок повністю заповнений — його зриває хід (§2).</summary>
        public bool IsRowFull(int y)
        {
            for (var x = 0; x < Width; x++)
                if (_cells[y * Width + x] == Empty)
                    return false;
            return true;
        }

        public bool IsColumnFull(int x)
        {
            for (var y = 0; y < Height; y++)
                if (_cells[y * Width + x] == Empty)
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
                    hash = hash * 31 + _cells[i];
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
