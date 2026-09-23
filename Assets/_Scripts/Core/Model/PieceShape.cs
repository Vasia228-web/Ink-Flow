using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Форма фігури — набір зайнятих клітинок у власних координатах, нормалізований так,
    /// що мінімальні X та Y дорівнюють нулю (документ §2). Фігури не обертаються:
    /// кожна орієнтація — окрема форма каталогу, і в лотку вона така сама, як на полі.
    ///
    /// Кольору форма не несе: одна й та сама форма видається лотку будь-яким пігментом.
    /// Незмінна й спільна: каталог створює по одному екземпляру, лоток тримає посилання —
    /// під час партії нуль алокацій.
    /// </summary>
    public sealed class PieceShape
    {
        public PieceShape(string id, GridPos[] cells, int weight = 1)
        {
            if (cells is null || cells.Length == 0)
                throw new ArgumentException("Форма без клітинок не існує.", nameof(cells));
            if (weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weight), "Вага має бути > 0.");

            Id = id ?? throw new ArgumentNullException(nameof(id));
            Weight = weight;

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
            }

            var normalized = new GridPos[cells.Length];
            int maxX = 0, maxY = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                var p = new GridPos(cells[i].X - minX, cells[i].Y - minY);
                normalized[i] = p;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            Cells = normalized;
            Width = maxX + 1;
            Height = maxY + 1;
        }

        public string Id { get; }

        /// <summary>Клітинки форми. Порядок фіксований — від нього залежить порядок подій у стрічці.</summary>
        public GridPos[] Cells { get; }

        /// <summary>Скільки клітинок займає — саме це число росте з прогресією (§8).</summary>
        public int Size => Cells.Length;

        public int Width { get; }
        public int Height { get; }

        /// <summary>Відносна частота видачі всередині свого розміру.</summary>
        public int Weight { get; }

        public override string ToString() => $"{Id}({Size})";
    }

    /// <summary>Фігура в лотку: форма плюс пігмент. Усі клітинки одного кольору (§2).</summary>
    public readonly struct PieceDef : IEquatable<PieceDef>
    {
        public PieceDef(PieceShape shape, Pigment pigment)
        {
            Shape = shape;
            Pigment = pigment;
        }

        public PieceShape? Shape { get; }
        public Pigment Pigment { get; }

        /// <summary>Порожня комірка лотка: фігуру вже поставили, лоток ще не поповнено.</summary>
        public bool IsEmpty => Shape is null;

        public int Size => Shape?.Size ?? 0;

        public static readonly PieceDef None = default;

        public bool Equals(PieceDef other) =>
            ReferenceEquals(Shape, other.Shape) && Pigment == other.Pigment;

        public override bool Equals(object? obj) => obj is PieceDef other && Equals(other);
        public override int GetHashCode() => ((Shape?.Id.GetHashCode() ?? 0) * 397) ^ (int)Pigment;

        public override string ToString() => IsEmpty ? "—" : $"{Pigment} {Shape!.Id}";
    }
}
