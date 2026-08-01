using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Бос-рівень (майстер-док §6, архітектура §5.6). Це PuzzleSession з іншою умовою перемоги:
    /// вибухи у верхніх рядах «дострілюють» до Клякса, і треба зафарбувати всі його сегменти
    /// в один колір, поки він злизує фарбу й плюється.
    /// </summary>
    public sealed class BossSession : PuzzleSession
    {
        private readonly List<int> _spongeCandidates = new List<int>(8);
        private readonly List<GridPos> _spitCandidates = new List<GridPos>(64);

        public BossSession(LevelData level, BalanceData balance, IRandomSource? random = null)
            : base(level, balance, random)
        {
            Boss = new BossModel(level.BossSegments);
        }

        public BossModel Boss { get; }

        /// <summary>На бос-рівні верхній край сітки — це тіло Клякса.</summary>
        protected override bool BossAbove => true;

        /// <summary>3★ додатково вимагає чистого проходження — жодного власного перефарбування (§5.6).</summary>
        public override int Stars =>
            StarCalculator.Stars(MovesLeft, MaxMoves, Balance, bossCleanRun: Boss.PlayerRepaints == 0);

        protected override bool IsWinConditionMet() => Boss.IsDefeated();

        protected override void OnMoveResolved(MoveResult result)
        {
            ApplyBossHits(result);
            AdvanceBossTurn(result);
        }

        /// <summary>Влучання рахуються ТІЛЬКИ через OutOfBounds за верхній край — окремих правил стрільби немає.</summary>
        private void ApplyBossHits(MoveResult result)
        {
            // Копія індексів подій не потрібна: список подій за хід не змінюється під час обходу,
            // а нові події (фарбування сегментів) додаються в кінець.
            var count = result.Events.Count;
            for (var i = 0; i < count; i++)
            {
                var e = result.Events[i];
                if (e.Type != GameEventType.BossHit)
                    continue;

                var center = SegmentForColumn(e.Position.X);
                var segments = BossModel.SegmentsPainted(e.Value, Balance);

                // Фарбуємо від центру назовні: центр, потім лівіше, потім правіше.
                for (var s = 0; s < segments; s++)
                {
                    var offset = (s + 1) / 2 * (s % 2 == 1 ? -1 : 1);
                    var index = center + offset;
                    if (index < 0 || index >= Boss.SegmentCount)
                        continue;

                    var wasRepaint = Boss.PaintSegment(index, e.Color);
                    if (wasRepaint)
                    {
                        Boss.PlayerRepaints++;
                        result.Add(GameEvent.BossSegmentRepainted(index, e.Color, e.ChainIndex));
                    }
                    else
                    {
                        result.Add(GameEvent.BossSegmentPainted(index, e.Color, e.ChainIndex));
                    }
                }
            }
        }

        private int SegmentForColumn(int column)
        {
            var index = column * Boss.SegmentCount / Grid.Width;
            if (index < 0) index = 0;
            if (index >= Boss.SegmentCount) index = Boss.SegmentCount - 1;
            return index;
        }

        /// <summary>
        /// Бос діє кожен N-й прийнятий хід і завжди оголошує намір ходом раніше (§18.8):
        /// на ході N−1 обирається PendingAction, на ході N вона виконується.
        /// </summary>
        private void AdvanceBossTurn(MoveResult result)
        {
            var period = Balance.BossActsEveryMoves;
            var phase = AcceptedMoves % period;

            if (phase == 0)
            {
                ExecutePendingAction(result);
                Boss.PendingAction = BossActionType.None;
            }
            else if (phase == period - 1)
            {
                Boss.PendingAction = ChooseAction();
                result.Add(GameEvent.BossTelegraph(Boss.PendingAction));
            }
        }

        private BossActionType ChooseAction()
        {
            // Губка має сенс лише коли є що злизувати.
            var canSponge = Boss.CountPainted() > 0;
            if (!canSponge)
                return Random.Next(2) == 0 ? BossActionType.SpitRepaint : BossActionType.SpitBlot;

            return (BossActionType)(Random.Next(3) + 1);
        }

        private void ExecutePendingAction(MoveResult result)
        {
            switch (Boss.PendingAction)
            {
                case BossActionType.Sponge:
                    ExecuteSponge(result);
                    break;
                case BossActionType.SpitRepaint:
                    ExecuteSpitRepaint(result);
                    break;
                case BossActionType.SpitBlot:
                    ExecuteSpitBlot(result);
                    break;
            }
        }

        /// <summary>Злизує колір з одного пофарбованого сегмента — не з того самого двічі поспіль.</summary>
        private void ExecuteSponge(MoveResult result)
        {
            _spongeCandidates.Clear();
            for (var i = 0; i < Boss.SegmentCount; i++)
                if (Boss[i] != InkColor.None && i != Boss.LastSpongeSegment)
                    _spongeCandidates.Add(i);

            if (_spongeCandidates.Count == 0)
                return;

            var segment = _spongeCandidates[Random.Next(_spongeCandidates.Count)];
            Boss.ClearSegment(segment);
            result.Add(GameEvent.BossAction(BossActionType.Sponge, default, InkColor.None, segment));
        }

        /// <summary>Плює на поле: перефарбовує одну краплю гравця в інший колір.</summary>
        private void ExecuteSpitRepaint(MoveResult result)
        {
            _spitCandidates.Clear();
            foreach (var pos in Grid.AllPositions())
            {
                var cell = Grid[pos];
                if (!cell.IsEmpty && (cell.Flags & MoveValidator.Blocking) == 0)
                    _spitCandidates.Add(pos);
            }

            if (_spitCandidates.Count == 0)
                return;

            var target = _spitCandidates[Random.Next(_spitCandidates.Count)];
            var victim = Grid[target];
            var newColor = DifferentColor(victim.Color);
            victim.Color = newColor;
            Grid[target] = victim;
            result.Add(GameEvent.BossAction(BossActionType.SpitRepaint, target, newColor, -1));
        }

        /// <summary>Лишає кляксу-перешкоду: клітинка заблокована, поки поруч щось не вибухне.</summary>
        private void ExecuteSpitBlot(MoveResult result)
        {
            _spitCandidates.Clear();
            foreach (var pos in Grid.AllPositions())
                if ((Grid[pos].Flags & MoveValidator.Blocking) == 0)
                    _spitCandidates.Add(pos);

            if (_spitCandidates.Count == 0)
                return;

            var target = _spitCandidates[Random.Next(_spitCandidates.Count)];
            var cell = Grid[target];
            cell.Flags |= CellFlags.Blot;
            Grid[target] = cell;
            result.Add(GameEvent.BossAction(BossActionType.SpitBlot, target, cell.Color, -1));
        }

        private InkColor DifferentColor(InkColor current)
        {
            var palette = Level.ColorsCount;
            if (palette <= 1)
                return current;

            var index = Random.Next(palette - 1);
            var replacement = InkColors.FromIndex(index);
            // Пропускаємо поточний колір, щоб плювок завжди щось змінював.
            if (replacement == current)
                replacement = InkColors.FromIndex(palette - 1);
            return replacement;
        }

        public override void Reset()
        {
            base.Reset();
            Boss.Reset();
        }
    }
}
