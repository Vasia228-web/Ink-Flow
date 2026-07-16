using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Статичні чисті методи правил гри. БЕЗ UnityEngine — покриваються
    /// Edit Mode тестами без завантаження сцени.
    ///
    /// ДЛЯ ЛЕВЕЛ-ДИЗАЙНЕРІВ (усе тюниться в LevelConfig, код читати не треба):
    ///  - Merge: свайп клітинки A в сусідню B того ж кольору складає густоти:
    ///    B.density = A.density + B.density, A стає порожньою. Різні кольори — відскок,
    ///    хід не витрачається.
    ///  - Burst: щойно density клітинки досягає burstThreshold (поле в LevelConfig,
    ///    базово 10) — клітинка лопається і зникає. Кожен її сусід по хресту:
    ///      порожній           → фарбується в колір вибуху з density = 1;
    ///      той самий колір    → +1 до своєї density (мікро-merge від вибуху);
    ///      інший колір        → без змін.
    ///  - Chain: якщо після цього хтось із сусідів сам досяг burstThreshold —
    ///    він теж лопається, і так далі. Скор = сума density усіх клітинок,
    ///    що лопнули (density у момент вибуху).
    ///  Тобто: менший burstThreshold = частіші вибухи і легші ланцюги;
    ///  більші стартові density наближають клітинку до вибуху.
    /// </summary>
    public static class GameRules
    {
        /// <summary>
        /// Запобіжник від патологічних конфігурацій рівнів: ланцюг довший за це
        /// значення обривається (кожен вибух додає сусідам сумарно до 4 density,
        /// тож теоретично можливі дуже довгі каскади на щільних монохромних сітках).
        /// </summary>
        public const int MaxChainBursts = 999;

        public static bool AreAdjacent(GridPos a, GridPos b)
        {
            var dr = a.Row - b.Row;
            var dc = a.Col - b.Col;
            if (dr < 0) dr = -dr;
            if (dc < 0) dc = -dc;
            return dr + dc == 1;
        }

        /// <summary>Merge дозволений: обидві клітинки в сітці, сусідні по хресту, непорожні, один колір.</summary>
        public static bool CanMerge(GridModel grid, GridPos from, GridPos to)
        {
            if (!grid.IsInside(from) || !grid.IsInside(to) || !AreAdjacent(from, to))
                return false;
            var a = grid[from];
            var b = grid[to];
            return !a.IsEmpty && !b.IsEmpty && a.Color == b.Color;
        }

        /// <summary>Чиста формула злиття: густоти складаються, колір лишається.</summary>
        public static Cell Merge(Cell from, Cell to) =>
            new Cell(to.Color, from.Density + to.Density);

        /// <summary>Клітинка лопається, коли її density досягла порогу рівня.</summary>
        public static bool CheckBurst(Cell cell, int burstThreshold) =>
            !cell.IsEmpty && cell.Density >= burstThreshold;

        /// <summary>
        /// Застосовує свайп from → напрямок direction. Мутує grid.
        /// Повертає MoveResult: відскок (хід не витрачено) або merge + можливий ланцюг вибухів.
        /// </summary>
        public static MoveResult ApplyMove(GridModel grid, GridPos from, Direction direction, int burstThreshold)
        {
            var to = grid.IsInside(from) ? from.Neighbor(direction) : from;
            if (!CanMerge(grid, from, to))
                return MoveResult.Rejected(from, to);

            grid[to] = Merge(grid[from], grid[to]);
            grid[from] = Cell.Empty;

            var bursts = ResolveChain(grid, to, burstThreshold);
            return MoveResult.Merged(from, to, bursts);
        }

        /// <summary>
        /// Ланцюгова реакція вибухів, починаючи з origin (якщо вона досягла порогу). Мутує grid.
        /// Обробка — черга (FIFO), сусіди кожного вибуху обходяться у фіксованому порядку
        /// Up, Right, Down, Left — для детермінованості. Клітинка, яку ланцюг знову
        /// «накачав» до порогу після її власного вибуху, лопається повторно — це нова крапля.
        /// </summary>
        public static List<BurstRecord> ResolveChain(GridModel grid, GridPos origin, int burstThreshold)
        {
            var bursts = new List<BurstRecord>();
            if (!CheckBurst(grid[origin], burstThreshold))
                return bursts;

            var queue = new Queue<GridPos>();
            var pending = new HashSet<GridPos>();
            queue.Enqueue(origin);
            pending.Add(origin);

            while (queue.Count > 0 && bursts.Count < MaxChainBursts)
            {
                var pos = queue.Dequeue();
                pending.Remove(pos);

                var cell = grid[pos];
                // Стан міг змінитись, поки клітинка чекала в черзі — перевіряємо ще раз.
                if (!CheckBurst(cell, burstThreshold))
                    continue;

                bursts.Add(new BurstRecord(pos, cell.Color, cell.Density));
                grid[pos] = Cell.Empty;

                foreach (var direction in GridPos.NeighborOrder)
                {
                    var neighborPos = pos.Neighbor(direction);
                    if (!grid.IsInside(neighborPos))
                        continue;

                    var neighbor = grid[neighborPos];
                    if (neighbor.IsEmpty)
                        grid[neighborPos] = new Cell(cell.Color, 1);
                    else if (neighbor.Color == cell.Color)
                        grid[neighborPos] = neighbor.WithDensity(neighbor.Density + 1);
                    else
                        continue; // інший колір — вибух його не чіпає

                    if (CheckBurst(grid[neighborPos], burstThreshold) && pending.Add(neighborPos))
                        queue.Enqueue(neighborPos);
                }
            }

            return bursts;
        }
    }
}
