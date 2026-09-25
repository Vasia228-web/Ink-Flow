using System;

namespace InkFlow.Core
{
    /// <summary>Рядок чи стовпець — від цього залежить напрямок анімації зриву.</summary>
    public enum LineKind : byte
    {
        Row = 0,
        Column = 1
    }

    /// <summary>Одна зірвана лінія: рядок чи стовпець і його індекс.</summary>
    public readonly struct Line : IEquatable<Line>
    {
        public Line(LineKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public LineKind Kind { get; }
        public int Index { get; }

        public bool Equals(Line other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object? obj) => obj is Line other && Equals(other);
        public override int GetHashCode() => ((int)Kind << 16) ^ Index;
        public override string ToString() => $"{Kind}[{Index}]";
    }

    /// <summary>Що дала зірвана лінія: чи чиста, її головний колір і скільки пікселів на клітинку.</summary>
    public readonly struct LineYield
    {
        public LineYield(bool isPure, byte dominant, int pixelsPerCell)
        {
            IsPure = isPure;
            Dominant = dominant;
            PixelsPerCell = pixelsPerCell;
        }

        /// <summary>Усі клітинки одного кольору (§5: «чиста одноколірна лінія»).</summary>
        public bool IsPure { get; }

        /// <summary>Колір, якого в лінії найбільше — для напису «+N» і його кольору.</summary>
        public byte Dominant { get; }

        /// <summary>Скільки пікселів дає кожна клітинка лінії: 1, або PureLineBonus для чистої.</summary>
        public int PixelsPerCell { get; }
    }

    /// <summary>
    /// Серце ядра (документ §5): кожна клітинка зірваної лінії кольору X заповнює один
    /// піксель кольору X у картинці; чиста одноколірна лінія дає ×PureLineBonus пікселів
    /// на клітинку. Розрив навмисно великий: «зібрати рядок одного кольору» має бути
    /// рішенням, заради якого гравець терпить незручну фігуру, а не приємним бонусом.
    /// </summary>
    public static class LineResolver
    {
        /// <summary>Довжина лінії: рядок міряється шириною поля, стовпець — висотою.</summary>
        public static int LengthOf(Board board, LineKind kind) =>
            kind == LineKind.Row ? board.Width : board.Height;

        /// <summary>Клітинка лінії за порядковим номером. Порядок фіксований: зліва направо / знизу вгору.</summary>
        public static GridPos CellAt(Line line, int i) =>
            line.Kind == LineKind.Row ? new GridPos(i, line.Index) : new GridPos(line.Index, i);

        /// <summary>Рахує вихід лінії ЗА СТАНОМ ДО ОЧИЩЕННЯ.</summary>
        public static LineYield Resolve(Board board, Line line, BalanceData balance)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (balance is null) throw new ArgumentNullException(nameof(balance));

            var length = LengthOf(board, line.Kind);
            var first = Board.Empty;
            var pure = true;
            var dominant = Board.Empty;
            var best = 0;

            // Домінантний — за кількістю; при рівності — менший індекс палітри, щоб нічия
            // не залежала від порядку клітинок.
            for (var i = 0; i < length; i++)
            {
                var color = board[CellAt(line, i)];
                if (color == Board.Empty)
                    continue;
                if (first == Board.Empty)
                    first = color;
                else if (color != first)
                    pure = false;

                var n = 0;
                for (var j = 0; j < length; j++)
                    if (board[CellAt(line, j)] == color)
                        n++;
                if (n > best || (n == best && color < dominant))
                {
                    best = n;
                    dominant = color;
                }
            }

            if (best == 0)
                return new LineYield(false, Board.Empty, 0);

            return new LineYield(pure, dominant, pure ? balance.PureLineBonus : 1);
        }

        /// <summary>Множник ланцюга — до очок за лінію, округлення вниз (§2).</summary>
        public static int ApplyCombo(int amount, float multiplier) => (int)Math.Floor(amount * multiplier);
    }
}
