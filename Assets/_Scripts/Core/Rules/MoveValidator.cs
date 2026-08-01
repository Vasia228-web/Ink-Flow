namespace InkFlow.Core
{
    /// <summary>
    /// Перевірка ходу (§5.1). Єдине місце, де вирішується «чи можна свайпнути» —
    /// і валідатор ходу, і детектор тупика мусять питати саме тут, інакше поле може
    /// вважатися живим, коли жоден хід насправді не проходить.
    /// </summary>
    public static class MoveValidator
    {
        /// <summary>Клітинки, скуті льодом/стіною/кляксою, у свайпі не беруть участі.</summary>
        public const CellFlags Blocking = CellFlags.Wall | CellFlags.Blot | CellFlags.Ice;

        public static bool IsValidMove(GridModel grid, GridPos from, GridPos to)
        {
            if (!grid.Contains(from) || !grid.Contains(to))
                return false;
            if (!GridPos.AreAdjacent(from, to))
                return false;

            var source = grid[from];
            var target = grid[to];

            if (source.IsEmpty || target.IsEmpty)
                return false;
            if ((source.Flags & Blocking) != 0 || (target.Flags & Blocking) != 0)
                return false;
            // «Важку» краплю не зрушити з місця, але вона може прийняти чужу.
            if (source.Has(CellFlags.Heavy))
                return false;

            return source.Color == target.Color;
        }
    }

    /// <summary>Формула злиття (§5.2).</summary>
    public static class MergeRules
    {
        /// <summary>
        /// Густоти просто додаються: 4+3=7. Правило «перельоту» — вибух рахує ФІНАЛЬНУ
        /// густоту, а не факт досягнення порогу: злиття 6+7 лопає з силою 13, не 10.
        /// </summary>
        public static int MergedDensity(in Cell from, in Cell to) => from.Density + to.Density;

        public static bool ReachesThreshold(int density, BalanceData balance) =>
            density >= balance.BurstThreshold;
    }

    /// <summary>
    /// Детектор живості поля (§5.7). ЄДИНА перевірка «чи гра ще жива» —
    /// вибух не є умовою перемоги (§18 інваріант 5).
    /// </summary>
    public static class DeadlockDetector
    {
        /// <summary>Чи існує хоч один дозволений свайп.</summary>
        public static bool HasAnyMove(GridModel grid)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var from = new GridPos(x, y);
                    // Досить перевірити сусідів праворуч і вгору — але в обидва боки,
                    // бо Heavy робить хід несиметричним (важку краплю не зрушити).
                    var right = new GridPos(x + 1, y);
                    if (MoveValidator.IsValidMove(grid, from, right) ||
                        MoveValidator.IsValidMove(grid, right, from))
                        return true;

                    var up = new GridPos(x, y + 1);
                    if (MoveValidator.IsValidMove(grid, from, up) ||
                        MoveValidator.IsValidMove(grid, up, from))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Перший знайдений дозволений хід — для підказки від застою (майстер-док §4).</summary>
        public static bool TryFindMove(GridModel grid, out GridPos from, out GridPos to)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var origin = new GridPos(x, y);
                    for (var i = 0; i < GridPos.NeighborCount; i++)
                    {
                        var neighbor = origin.NeighborAt(i);
                        if (!MoveValidator.IsValidMove(grid, origin, neighbor))
                            continue;
                        from = origin;
                        to = neighbor;
                        return true;
                    }
                }
            }

            from = default;
            to = default;
            return false;
        }
    }
}
