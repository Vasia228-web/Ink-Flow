namespace InkFlow.Core
{
    /// <summary>
    /// Значення однієї клітинки сітки. Чистий POCO-тип без UnityEngine.
    /// Порожня клітинка — це Density == 0 (Color тоді не має значення).
    /// </summary>
    public readonly struct Cell
    {
        /// <summary>Індекс кольору в палітрі рівня (0..colorsCount-1).</summary>
        public int Color { get; }

        /// <summary>Густота фарби. 0 — клітинка порожня.</summary>
        public int Density { get; }

        public bool IsEmpty => Density <= 0;

        public static readonly Cell Empty = default;

        public Cell(int color, int density)
        {
            Color = color;
            Density = density;
        }

        public Cell WithDensity(int density) => new Cell(Color, density);

        public override string ToString() => IsEmpty ? "·" : $"c{Color}:d{Density}";
    }
}
