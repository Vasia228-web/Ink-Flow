using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Tests
{
    /// <summary>
    /// Черга наступних крапель (майстер-док §5) і приплив.
    ///
    /// Головне, що тут доводиться: черга не ілюстрація. Те, що бачить гравець,
    /// і те, що падає в поле, — один і той самий об'єкт, тож розійтись вони не можуть.
    /// </summary>
    public sealed class EndlessQueueTests
    {
        private static BalanceData Balance() => BalanceData.Default;

        private static EndlessSession Session(uint seed = 11u, int w = 4, int h = 4, int colors = 3) =>
            new EndlessSession(new EndlessData(w, h, colors), Balance(), new XorShiftRandom(seed));

        [Test]
        public void Queue_IsAlwaysFull()
        {
            var session = Session();
            Assert.AreEqual(DropQueue.PreviewCount, session.Queue.Count, "після роздачі поля");

            for (var i = 0; i < 30 && !session.IsOver; i++)
            {
                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                session.ApplyMove(from, to);
                Assert.AreEqual(DropQueue.PreviewCount, session.Queue.Count, $"хід {i}");
            }
        }

        [Test]
        public void Queue_HeadIsExactlyWhatRefillPours()
        {
            var session = Session(seed: 5u);

            // Знімок черги ДО ходу — саме ці краплі мають лягти в поле.
            var expected = new List<QueuedDrop>();
            for (var i = 0; i < session.Queue.Count; i++)
                expected.Add(session.Queue.Peek(i));

            Assert.IsTrue(DeadlockDetector.TryFindMove(session.Grid, out var from, out var to));
            var result = session.ApplyMove(from, to);

            var poured = new List<GameEvent>();
            foreach (var e in result.Events)
                if (e.Type == GameEventType.Refill)
                    poured.Add(e);

            Assert.Greater(poured.Count, 0, "хід звільнив клітинку — щось мало долитись");

            // Долити могло більше, ніж видно в прев'ю (довгий ланцюг), тому
            // звіряємо рівно стільки, скільки гравець бачив.
            var checkable = poured.Count < expected.Count ? poured.Count : expected.Count;
            for (var i = 0; i < checkable; i++)
            {
                Assert.AreEqual(expected[i].Color, poured[i].Color,
                    $"крапля {i} з черги мала лягти саме такого кольору");
                Assert.AreEqual(expected[i].Density, poured[i].Value,
                    $"крапля {i} з черги мала лягти саме такої густоти");
            }
        }

        [Test]
        public void Refill_GoesColumnByColumnTopDown()
        {
            var session = Session(seed: 21u, w: 5, h: 5);

            Assert.IsTrue(DeadlockDetector.TryFindMove(session.Grid, out var from, out var to));
            var result = session.ApplyMove(from, to);

            GridPos? previous = null;
            foreach (var e in result.Events)
            {
                if (e.Type != GameEventType.Refill)
                    continue;

                if (previous is { } prev)
                {
                    // Порядок подій диктує анімацію падіння: спершу весь лівий
                    // стовпець згори вниз, потім наступний.
                    var ordered = e.Position.X > prev.X ||
                                  (e.Position.X == prev.X && e.Position.Y < prev.Y);
                    Assert.IsTrue(ordered,
                        $"{prev} → {e.Position}: долив має йти по стовпцях згори вниз");
                }

                previous = e.Position;
            }
        }

        [Test]
        public void Queue_SurvivesReset()
        {
            var session = Session();
            for (var i = 0; i < 5 && !session.IsOver; i++)
            {
                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                session.ApplyMove(from, to);
            }

            session.Reset();

            Assert.AreEqual(DropQueue.PreviewCount, session.Queue.Count);
            Assert.AreEqual(0, session.Grid.CountFree(), "після рестарту поле знову залите");
            Assert.IsTrue(DeadlockDetector.HasAnyMove(session.Grid));
        }

        [Test]
        public void Queue_NeverOffersAnAlreadyBurstingDrop()
        {
            var session = Session(seed: 77u);
            var balance = Balance();

            for (var move = 0; move < 60 && !session.IsOver; move++)
            {
                for (var i = 0; i < session.Queue.Count; i++)
                {
                    var drop = session.Queue.Peek(i);
                    Assert.Greater(drop.Density, 0, "крапля з черги не може бути порожньою");
                    Assert.IsFalse(MergeRules.ReachesThreshold(drop.Density, balance),
                        $"крапля {drop.Density} лопнула б одразу при доливі");
                    Assert.AreNotEqual(InkColor.None, drop.Color);
                }

                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                session.ApplyMove(from, to);
            }
        }

        [Test]
        public void Tide_ProgressTracksBurstsWithinTheLevel()
        {
            var session = Session();
            var step = session.Balance.TideStep;

            Assert.AreEqual(0, session.TideProgress);
            Assert.AreEqual(0f, session.TideFraction);

            for (var move = 0; move < 200 && !session.IsOver; move++)
            {
                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                session.ApplyMove(from, to);

                Assert.AreEqual(session.TotalBursts / step, session.TideLevel);
                Assert.AreEqual(session.TotalBursts % step, session.TideProgress);
                Assert.GreaterOrEqual(session.TideFraction, 0f);
                Assert.Less(session.TideFraction, 1f, "шкала повна означала б, що рівень уже виріс");
            }
        }

        [Test]
        public void Tide_RaisesMinimumDensityOfNewlyQueuedDrops()
        {
            var session = Session(seed: 99u, w: 6, h: 6, colors: 4);
            var seen = false;

            for (var move = 0; move < 300 && !session.IsOver; move++)
            {
                if (session.TideLevel > 0)
                {
                    seen = true;
                    // Приплив піднімає ПІДЛОГУ густоти, а не саму густоту:
                    // після кількох доливів у черзі не має лишитись одиниць.
                    Assert.GreaterOrEqual(session.MinDensity, 1 + session.TideLevel > session.Balance.MaxPaintPower
                        ? session.Balance.MaxPaintPower
                        : 1 + session.TideLevel);
                    break;
                }

                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                session.ApplyMove(from, to);
            }

            Assert.IsTrue(seen, "за 300 ходів приплив мав вирости хоч раз — інакше тест нічого не перевіряє");
        }

        // ---------- Сама черга як структура ----------

        [Test]
        public void DropQueue_IsFifoAcrossTheRingBoundary()
        {
            var queue = new DropQueue();
            for (var i = 0; i < DropQueue.PreviewCount; i++)
                queue.Enqueue(new QueuedDrop(InkColor.Cyan, i + 1));

            // Прокручуємо кільце півтора рази — саме на межі буфера ламаються
            // ручні реалізації FIFO.
            for (var round = 0; round < 8; round++)
            {
                var head = queue.Dequeue();
                Assert.AreEqual(round + 1, head.Density);
                queue.Enqueue(new QueuedDrop(InkColor.Cyan, round + 1 + DropQueue.PreviewCount));
                Assert.AreEqual(DropQueue.PreviewCount, queue.Count);
            }
        }

        [Test]
        public void DropQueue_RefusesToOverfillOrUnderflow()
        {
            var queue = new DropQueue();
            Assert.Throws<System.InvalidOperationException>(() => queue.Dequeue());

            for (var i = 0; i < DropQueue.PreviewCount; i++)
                queue.Enqueue(new QueuedDrop(InkColor.Lime, 1));

            Assert.Throws<System.InvalidOperationException>(
                () => queue.Enqueue(new QueuedDrop(InkColor.Lime, 1)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => queue.Peek(DropQueue.PreviewCount));
        }
    }
}
