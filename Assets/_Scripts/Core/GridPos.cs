using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Напрямок свайпу. Тільки 4 напрямки, без діагоналей.
    /// Up означає +1 до Row: модель тримає row 0 внизу, в'ю мапить row на світову вісь Y.
    /// </summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>Координата клітинки в сітці (row 0 — нижній ряд).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public int Row { get; }
        public int Col { get; }

        public GridPos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public GridPos Neighbor(Direction direction) => direction switch
        {
            Direction.Up => new GridPos(Row + 1, Col),
            Direction.Down => new GridPos(Row - 1, Col),
            Direction.Left => new GridPos(Row, Col - 1),
            Direction.Right => new GridPos(Row, Col + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        /// <summary>
        /// Сусіди у фіксованому порядку Up, Right, Down, Left —
        /// порядок важливий для детермінованості ланцюгових вибухів (і тестів).
        /// </summary>
        public static readonly Direction[] NeighborOrder =
        {
            Direction.Up, Direction.Right, Direction.Down, Direction.Left
        };

        public bool Equals(GridPos other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (Row * 397) ^ Col;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({Row},{Col})";
    }
}
