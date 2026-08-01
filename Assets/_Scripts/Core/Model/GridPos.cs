using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Напрямок ходу/сусідства. Тільки 4 напрямки, без діагоналей.
    /// ПОРЯДОК ФІКСОВАНИЙ: Up, Right, Down, Left — від нього залежить детермінізм
    /// ланцюгів (архітектура §4, §5.5). Ніколи не міняти.
    /// </summary>
    public enum Direction : byte
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    /// <summary>Координата клітинки сітки. X — колонка (вправо), Y — ряд (вгору).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Кількість сусідів по хресту — зона вибуху завжди така (§5.3).</summary>
        public const int NeighborCount = 4;

        /// <summary>Сусід у заданому напрямку (без перевірки меж сітки).</summary>
        public GridPos Neighbor(Direction direction) => direction switch
        {
            Direction.Up => new GridPos(X, Y + 1),
            Direction.Right => new GridPos(X + 1, Y),
            Direction.Down => new GridPos(X, Y - 1),
            Direction.Left => new GridPos(X - 1, Y),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        /// <summary>
        /// Сусід за індексом 0..3 у канонічному порядку Up, Right, Down, Left.
        /// Не алокує — саме цей шлях використовують гарячі цикли (§12).
        /// </summary>
        public GridPos NeighborAt(int index) => Neighbor((Direction)index);

        /// <summary>Напрямок від a до b, якщо вони суміжні; інакше null.</summary>
        public static Direction? DirectionBetween(GridPos a, GridPos b)
        {
            if (a.X == b.X && b.Y == a.Y + 1) return Direction.Up;
            if (a.X == b.X && b.Y == a.Y - 1) return Direction.Down;
            if (a.Y == b.Y && b.X == a.X + 1) return Direction.Right;
            if (a.Y == b.Y && b.X == a.X - 1) return Direction.Left;
            return null;
        }

        /// <summary>Мангеттенська відстань — нею визначається гало бризок (§5.3).</summary>
        public static int ManhattanDistance(GridPos a, GridPos b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        public static bool AreAdjacent(GridPos a, GridPos b) => ManhattanDistance(a, b) == 1;

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({X},{Y})";
    }
}
