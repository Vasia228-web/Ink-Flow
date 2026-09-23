using System;
using System.Collections.Generic;

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

    /// <summary>Що дала зірвана лінія: пігмент, кількість одиниць і чи була вона чистою (§3).</summary>
    public readonly struct PaintYield
    {
        public PaintYield(Pigment pigment, int amount, bool isPure)
        {
            Pigment = pigment;
            Amount = amount;
            IsPure = isPure;
        }

        public Pigment Pigment { get; }
        public int Amount { get; }
        public bool IsPure { get; }

        public override string ToString() => $"{Pigment}+{Amount}{(IsPure ? " pure" : "")}";
    }

    /// <summary>
    /// Серце ядра: скільки фарби дає зірвана лінія (документ §3, §12).
    ///
    /// Мішана лінія віддає лише домінантний пігмент, ⌊домінантних ÷ MixedDivisor⌋.
    /// Чиста — ⌊довжина ÷ MixedDivisor⌋ × PureLineBonus, тобто рівно «×3 до фарби» з §12
    /// відносно того, що дала б ця ж лінія, якби була мішаною, але вся одного кольору.
    /// Розрив навмисно великий: «зібрати рядок одного кольору» має бути рішенням,
    /// заради якого гравець терпить незручну фігуру, а не приємним бонусом.
    /// </summary>
    public static class LineResolver
    {
        /// <summary>Довжина лінії: рядок міряється шириною поля, стовпець — висотою.</summary>
        public static int LengthOf(Board board, LineKind kind) =>
            kind == LineKind.Row ? board.Width : board.Height;

        /// <summary>Клітинка лінії за порядковим номером. Порядок фіксований: зліва направо / знизу вгору.</summary>
        public static GridPos CellAt(Line line, int i) =>
            line.Kind == LineKind.Row ? new GridPos(i, line.Index) : new GridPos(line.Index, i);

        /// <summary>
        /// Рахує вихід фарби ЗА СТАНОМ ДО ОЧИЩЕННЯ. <paramref name="tankLevels"/> (індекс =
        /// <see cref="Pigments.IndexOf"/>) потрібен лише для тайбрейка при рівності: нічия
        /// віддає колір, якого в баках МЕНШЕ, — так фарба тече туди, де потрібніша, і
        /// нічия ніколи не залежить від випадковості. Без рівнів баків нічию розв'язує
        /// порядок enum.
        /// </summary>
        public static PaintYield Resolve(Board board, Line line, BalanceData balance,
            IReadOnlyList<int>? tankLevels = null)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (balance is null) throw new ArgumentNullException(nameof(balance));

            var length = LengthOf(board, line.Kind);

            var counts = new int[Pigments.Count];
            for (var i = 0; i < length; i++)
            {
                var pigment = board[CellAt(line, i)];
                if (pigment != Pigment.None)
                    counts[Pigments.IndexOf(pigment)]++;
            }

            var dominant = Pigment.None;
            var best = -1;
            for (var i = 0; i < Pigments.Count; i++)
            {
                var pigment = Pigments.FromIndex(i);
                var n = counts[i];
                if (n > best)
                {
                    best = n;
                    dominant = pigment;
                    continue;
                }

                if (n != best || tankLevels is null)
                    continue;

                if (tankLevels[i] < tankLevels[Pigments.IndexOf(dominant)])
                    dominant = pigment;
            }

            if (best <= 0)
                return new PaintYield(Pigment.None, 0, false);

            if (best == length)
                return new PaintYield(dominant, PureYield(length, balance), isPure: true);

            return new PaintYield(dominant, best / balance.MixedDivisor, isPure: false);
        }

        /// <summary>Скільки дає чиста лінія цієї довжини — стеля виходу фарби за одну лінію.</summary>
        public static int PureYield(int length, BalanceData balance) =>
            length / balance.MixedDivisor * balance.PureLineBonus;

        /// <summary>Множник ланцюга застосовується до КОЖНОЇ лінії окремо, округлення вниз (§2).</summary>
        public static int ApplyCombo(int amount, float multiplier) => (int)Math.Floor(amount * multiplier);
    }
}
