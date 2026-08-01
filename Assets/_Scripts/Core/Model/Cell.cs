using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Модифікатори клітинки (архітектура §4).
    ///  Ice   — крапля скута льодом: не бере участі у свайпі, поки вибух не розморозить її.
    ///  Wall  — незмінна перешкода: вибух її не чіпає, свайп крізь неї неможливий.
    ///  Blot  — клякса боса: блокує клітинку, поки поруч щось не вибухне (майстер-док §6).
    ///  Heavy — «важка» крапля: не може бути ДЖЕРЕЛОМ свайпу, але приймає злиття.
    /// </summary>
    [Flags]
    public enum CellFlags : byte
    {
        None = 0,
        Ice = 1 << 0,
        Wall = 1 << 1,
        Blot = 1 << 2,
        Heavy = 1 << 3
    }

    /// <summary>Одна клітинка сітки. Struct — Core працює без алокацій (§12).</summary>
    public struct Cell
    {
        public InkColor Color;
        public int Density;
        public CellFlags Flags;

        public Cell(InkColor color, int density, CellFlags flags = CellFlags.None)
        {
            Color = color;
            Density = density;
            Flags = flags;
        }

        public bool IsEmpty => Color == InkColor.None;

        public bool Has(CellFlags flag) => (Flags & flag) != 0;

        /// <summary>Порожня клітинка може лишатись заблокованою (Wall/Blot) — це не «вільне місце».</summary>
        public bool IsFree => IsEmpty && !Has(CellFlags.Wall) && !Has(CellFlags.Blot);

        public static readonly Cell Empty = default;

        public static Cell Wall => new Cell(InkColor.None, 0, CellFlags.Wall);

        public override string ToString() =>
            IsEmpty ? (Flags == CellFlags.None ? "·" : $"·{Flags}") : $"{Color}:{Density}{(Flags == CellFlags.None ? "" : $"/{Flags}")}";
    }
}
