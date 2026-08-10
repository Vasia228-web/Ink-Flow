using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Режим «Нескінченний» (майстер-док §5, архітектура §5.8-5.9).
    ///
    /// Ключовий інваріант (§18.6): СИСТЕМА НЕ ВБИВАЄ ГРАВЦЯ САМА. Якщо краплі долила гра
    /// і після цього ходу не лишилось — вона перегенеровує їхні кольори, доки хід не з'явиться,
    /// а в найгіршому разі детерміновано ставить пару однакових сусідів.
    /// Чесна смерть лишається можливою: поле повне, доливати нема куди, свайпів немає —
    /// до такого стану гравця доводять його власні ходи, а не рандом.
    /// </summary>
    public sealed class EndlessSession : GameSession
    {
        private readonly EndlessData _config;
        private readonly List<GridPos> _freshDrops = new List<GridPos>(64);
        private readonly DropQueue _queue = new DropQueue();

        public EndlessSession(EndlessData config, BalanceData balance, IRandomSource random)
            : base(new GridModel(
                       (config ?? throw new ArgumentNullException(nameof(config))).Width,
                       config.Height),
                   balance,
                   random,
                   maxMoves: int.MaxValue)
        {
            _config = config;
            StartFreshBoard();
        }

        /// <summary>Ходи в Endless не обмежені — партія живе, поки є хід.</summary>
        protected override bool ConsumesMoves => false;

        /// <summary>
        /// Черга наступних крапель. Долив бере краплі саме звідси, тож показане
        /// гравцеві прев'ю — це не ілюстрація, а те, що справді впаде.
        /// </summary>
        public DropQueue Queue => _queue;

        /// <summary>Скільки вибухів зроблено в межах поточного рівня припливу (0..TideStep−1).</summary>
        public int TideProgress => TotalBursts % Balance.TideStep;

        /// <summary>Прогрес до наступного підвищення припливу, 0..1 — для шкали під бейджем.</summary>
        public float TideFraction => (float)TideProgress / Balance.TideStep;

        /// <summary>Приплив: +1 до мінімальної густоти нових крапель кожні TideStep вибухів (§5.9).</summary>
        public int TideLevel => TotalBursts / Balance.TideStep;

        /// <summary>Мінімальна густота нових крапель. Стеля — поріг−1: краплі не падають уже лопнутими.</summary>
        public int MinDensity
        {
            get
            {
                var min = 1 + TideLevel;
                if (min < 1) min = 1;
                if (min > Balance.MaxPaintPower) min = Balance.MaxPaintPower;
                return min;
            }
        }

        protected override void OnMoveResolved(MoveResult result)
        {
            Refill(result);
            GuaranteePlayable(result);
        }

        protected override void EvaluateState(MoveResult result)
        {
            // Чесна смерть: поле повне І немає жодного дозволеного свайпу (§5.8).
            if (!DeadlockDetector.HasAnyMove(Grid) && Grid.CountFree() == 0)
            {
                State = GameState.Lost;
                result.Add(GameEvent.Deadlock());
            }
        }

        /// <summary>
        /// Нові краплі падають зверху у вільні клітинки. Заповнює список «свіжих».
        ///
        /// Обхід іде ПО СТОВПЦЯХ згори вниз — у тому ж порядку, в якому екран
        /// показує падіння. Порядок подій тут і порядок анімації мусять збігатись,
        /// інакше крапля «падала» б у клітинку, яку долили раніше.
        /// </summary>
        private void Refill(MoveResult? result)
        {
            _freshDrops.Clear();
            for (var x = 0; x < Grid.Width; x++)
            {
                for (var y = Grid.Height - 1; y >= 0; y--)
                {
                    var pos = new GridPos(x, y);
                    var existing = Grid[pos];
                    if (!existing.IsFree)
                        continue;

                    var next = TakeFromQueue();
                    var cell = new Cell(next.Color, next.Density, existing.Flags);
                    Grid[pos] = cell;
                    _freshDrops.Add(pos);
                    result?.Add(GameEvent.Refill(pos, cell.Color, cell.Density));
                }
            }
        }

        /// <summary>
        /// Знімає найближчу краплю з черги й одразу дозаповнює хвіст.
        /// Нова крапля генерується з ПОТОЧНИМ припливом: якщо він щойно виріс,
        /// жирніші краплі стануть видні в кінці черги, а не підміняться в її голові.
        /// </summary>
        private QueuedDrop TakeFromQueue()
        {
            var drop = _queue.Dequeue();
            _queue.Enqueue(NextDrop());
            return drop;
        }

        private QueuedDrop NextDrop() => new QueuedDrop(RandomColor(), RandomDensity());

        /// <summary>
        /// Гарантія: краплі, які долила ГРА, не мають права залишити гравця без ходу.
        /// Перегенеровуємо їхні кольори до MaxRefillAttempts разів, далі — детермінований fallback.
        /// Якщо доливати не було чого (поле й так повне) — не втручаємось: це чесний фінал.
        /// </summary>
        private void GuaranteePlayable(MoveResult? result)
        {
            if (_freshDrops.Count == 0 || DeadlockDetector.HasAnyMove(Grid))
                return;

            for (var attempt = 0; attempt < Balance.MaxRefillAttempts; attempt++)
            {
                RerollFreshDrops(result);
                if (DeadlockDetector.HasAnyMove(Grid))
                    return;
            }

            ForceMatchingPair(result);
        }

        private void RerollFreshDrops(MoveResult? result)
        {
            foreach (var pos in _freshDrops)
            {
                var cell = Grid[pos];
                if (cell.IsEmpty)
                    continue;
                cell.Color = RandomColor();
                Grid[pos] = cell;
                result?.Add(GameEvent.Refill(pos, cell.Color, cell.Density));
            }
        }

        /// <summary>
        /// Останній рубіж: детерміновано робить хід можливим, перефарбовуючи дві свіжі
        /// сусідні краплі в один колір. Викликається, лише коли рандом не впорався.
        /// </summary>
        private void ForceMatchingPair(MoveResult? result)
        {
            foreach (var pos in _freshDrops)
            {
                for (var i = 0; i < GridPos.NeighborCount; i++)
                {
                    var neighbor = pos.NeighborAt(i);
                    if (!Grid.Contains(neighbor))
                        continue;

                    var a = Grid[pos];
                    var b = Grid[neighbor];
                    if (a.IsEmpty || b.IsEmpty)
                        continue;
                    if ((a.Flags & MoveValidator.Blocking) != 0 || (b.Flags & MoveValidator.Blocking) != 0)
                        continue;
                    if (a.Has(CellFlags.Heavy) && b.Has(CellFlags.Heavy))
                        continue;

                    b.Color = a.Color;
                    Grid[neighbor] = b;
                    result?.Add(GameEvent.Refill(neighbor, b.Color, b.Density));
                    return;
                }
            }
        }

        private InkColor RandomColor() => InkColors.FromIndex(Random.Next(_config.ColorsCount));

        private int RandomDensity()
        {
            var min = MinDensity;
            var max = Balance.MaxPaintPower; // нові краплі ніколи не падають уже лопнутими
            return max <= min ? min : min + Random.Next(max - min + 1);
        }

        private void StartFreshBoard()
        {
            _queue.Clear();
            while (_queue.Count < DropQueue.PreviewCount)
                _queue.Enqueue(NextDrop());

            foreach (var pos in Grid.AllPositions())
                Grid[pos] = Cell.Empty;
            Refill(null);
            GuaranteePlayable(null);
        }

        public override void Reset()
        {
            base.Reset();
            StartFreshBoard();
        }
    }
}
