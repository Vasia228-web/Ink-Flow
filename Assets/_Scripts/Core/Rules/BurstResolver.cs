using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Вибухи і ланцюги (§5.3-5.5) — найважливіший клас правил.
    ///
    /// ДЛЯ ЛЕВЕЛ-ДИЗАЙНЕРІВ (усі числа — в BalanceConfig.asset, код читати не треба):
    ///  • Зона вибуху ЗАВЖДИ хрест із 4 клітинок, для будь-якої сили. Форма не залежить від сили — ніколи.
    ///  • Сила фарбування = сила ÷ 10, стеля = поріг − 1 (базово 9). «Кожна десятка = +1».
    ///  • Бризки: 1 за кожні повні 15 сили; кожна — крапелька сили 1 у випадкову клітинку
    ///    гало (кільце на відстані 2). У Рівнях бризки детерміновані від сіда рівня.
    ///  • Ефект на клітинку: порожня → фарбується; той самий колір → +сила (ось так запалюються
    ///    ланцюги); чужий колір → РОЗМИВАЄТЬСЯ (густота −сила), і лише впавши до нуля,
    ///    перефарбовується з густотою 1.
    ///  Наслідок для балансу: більший поріг = рідші, але важчі вибухи; менший дільник
    ///  сили фарбування = агресивніші ланцюги.
    ///
    /// Екземпляр переиспользується сесією — черга ланцюга не алокується щоходу (§12).
    /// </summary>
    public sealed class BurstResolver
    {
        private readonly Queue<GridPos> _pending = new Queue<GridPos>(32);

        /// <summary>
        /// Сила фарбування: сила ÷ дільник, мінімум 1, стеля = поріг − 1.
        /// Стеля — інваріант §18.3: вибух не має права створити краплю, яка лопне сама,
        /// без рішення гравця. Інакше поле «проходило б себе» самостійно.
        /// </summary>
        public static int PaintPower(int force, BalanceData balance)
        {
            var power = force / balance.PaintPowerDivisor;
            if (power < 1)
                power = 1;
            if (power > balance.MaxPaintPower)
                power = balance.MaxPaintPower;
            return power;
        }

        /// <summary>Кількість бризок: 1 за кожні повні SplashDivisor сили.</summary>
        public static int SplashCount(int force, BalanceData balance) => force / balance.SplashDivisor;

        /// <summary>
        /// Проганяє ланцюг вибухів, починаючи з origin (якщо там справді досягнуто поріг).
        /// Мутує grid, пише події в result. Порядок строго детермінований:
        /// черга FIFO, хрест Up→Right→Down→Left, потім бризки в порядку генерації.
        /// </summary>
        public void ResolveChain(
            GridModel grid,
            GridPos origin,
            BalanceData balance,
            IRandomSource random,
            MoveResult result,
            bool bossAbove = false)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (result == null) throw new ArgumentNullException(nameof(result));

            _pending.Clear();
            _pending.Enqueue(origin);

            var chainIndex = 0;

            while (_pending.Count > 0)
            {
                var pos = _pending.Dequeue();
                var cell = grid[pos];

                // Стан міг змінитись, поки клітинка чекала в черзі (її вже здуло сусіднім вибухом
                // або розмило чужим кольором) — тому перевіряємо ще раз.
                if (cell.IsEmpty || !MergeRules.ReachesThreshold(cell.Density, balance))
                    continue;

                if (chainIndex >= balance.MaxChainBursts)
                {
                    // Щільне монохромне поле теоретично може зациклитись — обриваємо
                    // і чесно повідомляємо про це подією (§5.5).
                    result.Add(GameEvent.ChainTruncated(pos, chainIndex));
                    break;
                }

                var force = cell.Density;
                var color = cell.Color;

                result.Add(GameEvent.Burst(pos, color, force, chainIndex));
                result.AddScore(force * balance.ScorePerBurstForce);

                // Клітинка, що вибухнула, звільняється (стіна тут неможлива — на ній не буває краплі).
                grid[pos] = Cell.Empty;

                var power = PaintPower(force, balance);

                // Хрест — гарантована зона, порядок Up, Right, Down, Left.
                for (var i = 0; i < GridPos.NeighborCount; i++)
                {
                    var target = pos.NeighborAt(i);
                    if (ApplyInk(grid, pos, target, color, power, force, balance, result, chainIndex, bossAbove))
                        _pending.Enqueue(target);
                }

                // Бризки — «чесна ширина»: кілька непередбачуваних крапельок по гало.
                var splashes = SplashCount(force, balance);
                for (var s = 0; s < splashes; s++)
                {
                    var offset = GridModel.HaloOffsets[random.Next(GridModel.HaloOffsets.Length)];
                    var target = new GridPos(pos.X + offset.X, pos.Y + offset.Y);
                    result.Add(GameEvent.Splash(pos, target, color, chainIndex));
                    // Бризка завжди має силу фарбування 1, незалежно від сили вибуху.
                    if (ApplyInk(grid, pos, target, color, 1, 1, balance, result, chainIndex, bossAbove))
                        _pending.Enqueue(target);
                }

                chainIndex++;
            }
        }

        /// <summary>
        /// Ефект фарби на одну клітинку (§5.4). Повертає true, якщо клітинка досягла порогу
        /// і має лопнути в цьому ж ланцюгу.
        /// </summary>
        private static bool ApplyInk(
            GridModel grid,
            GridPos from,
            GridPos target,
            InkColor color,
            int power,
            int force,
            BalanceData balance,
            MoveResult result,
            int chainIndex,
            bool bossAbove)
        {
            if (!grid.Contains(target))
            {
                // Виняток бос-рівня: усе, що вилилось за ВЕРХНІЙ край, влучає в Клякса —
                // так вибухи «дострілюють» до боса без окремих правил стрільби (§5.6).
                if (bossAbove && target.Y >= grid.Height)
                    result.Add(GameEvent.BossHit(from, color, force, chainIndex));
                else
                    result.Add(GameEvent.OutOfBounds(from, target, color, chainIndex));
                return false;
            }

            var cell = grid[target];

            if (cell.Has(CellFlags.Wall))
                return false; // стіну не бере ніщо

            if (cell.Has(CellFlags.Blot))
            {
                // Клякса блокує клітинку, поки поруч щось не вибухне — оце воно і сталося.
                cell.Flags &= ~CellFlags.Blot;
                grid[target] = cell;
                result.Add(GameEvent.BlotCleared(target, chainIndex));
                return false;
            }

            if (cell.Has(CellFlags.Ice))
            {
                // Лід приймає удар на себе: цього разу тане, але фарба крізь нього не проходить.
                cell.Flags &= ~CellFlags.Ice;
                grid[target] = cell;
                result.Add(GameEvent.Thaw(target, chainIndex));
                return false;
            }

            if (cell.IsEmpty)
            {
                cell.Color = color;
                cell.Density = power;
                grid[target] = cell;
                result.Add(GameEvent.Paint(target, color, power, chainIndex));
            }
            else if (cell.Color == color)
            {
                cell.Density += power;
                grid[target] = cell;
                result.Add(GameEvent.Grow(target, color, cell.Density, chainIndex));
            }
            else
            {
                // РОЗМИВАННЯ: чужий колір стирається поступово. Миттєвої конвертації немає
                // ніколи (§18 інваріант 4) — «весь екран мого кольору» має бути серією рішень.
                cell.Density -= power;
                if (cell.Density <= 0)
                {
                    cell.Color = color;
                    cell.Density = 1;
                    grid[target] = cell;
                    result.Add(GameEvent.Repaint(target, color, chainIndex));
                }
                else
                {
                    grid[target] = cell;
                    result.Add(GameEvent.Blur(target, cell.Color, cell.Density, chainIndex));
                }
            }

            var updated = grid[target];
            return !updated.IsEmpty && MergeRules.ReachesThreshold(updated.Density, balance);
        }
    }
}
