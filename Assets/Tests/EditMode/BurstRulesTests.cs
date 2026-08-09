using NUnit.Framework;
using static InkFlow.Core.Tests.TestBoard;

namespace InkFlow.Core.Tests
{
    /// <summary>Формули §5.1-5.4: хід, злиття, сила фарбування, бризки, ефект на клітинку.</summary>
    [TestFixture]
    public class BurstRulesTests
    {
        // ---------- §5.1 Хід ----------

        [Test]
        public void ValidMove_RequiresAdjacencyAndSameColor()
        {
            var grid = Grid(4, 4, At(0, 0, A, 2), At(1, 0, A, 3), At(0, 1, B, 3), At(3, 3, A, 1));

            Assert.IsTrue(MoveValidator.IsValidMove(grid, P(0, 0), P(1, 0)));  // сусід, той самий колір
            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(0, 0), P(0, 1))); // інший колір
            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(0, 0), P(3, 3))); // не сусід
            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(1, 0), P(1, 1))); // ціль порожня
            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(0, 0), P(-1, 0))); // поза сіткою
        }

        [Test]
        public void ValidMove_BlockedByIceWallBlot()
        {
            var grid = Grid(4, 4,
                At(0, 0, A, 2), At(1, 0, A, 2, CellFlags.Ice),
                At(2, 0, A, 2), At(3, 0, A, 2, CellFlags.Wall),
                At(0, 1, A, 2), At(1, 1, A, 2, CellFlags.Blot));

            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(0, 0), P(1, 0)), "лід не свайпається");
            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(2, 0), P(3, 0)), "стіна не свайпається");
            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(0, 1), P(1, 1)), "клякса блокує");
        }

        [Test]
        public void ValidMove_HeavyCannotBeSourceButAcceptsMerge()
        {
            var grid = Grid(4, 4, At(0, 0, A, 2, CellFlags.Heavy), At(1, 0, A, 3));

            Assert.IsFalse(MoveValidator.IsValidMove(grid, P(0, 0), P(1, 0)), "важку не зрушити");
            Assert.IsTrue(MoveValidator.IsValidMove(grid, P(1, 0), P(0, 0)), "але вона приймає злиття");
        }

        // ---------- §5.2 Злиття і правило «перельоту» ----------

        [Test]
        public void Merge_AddsDensities_OverflowKeepsFullForce()
        {
            var balance = Balance();
            var session = new PuzzleSession(
                Level(seeds: new[] { At(0, 0, A, 6), At(1, 0, A, 7) }), balance);

            var result = session.ApplyMove(P(0, 0), P(1, 0));

            // Вибух рахує ФІНАЛЬНУ густоту 13, а не факт «досяг 10».
            var burst = FirstEvent(result, GameEventType.Burst);
            Assert.AreEqual(13, burst.Value);
        }

        [Test]
        public void RejectedMove_DoesNotConsumeMoveOrChangeGrid()
        {
            var session = new PuzzleSession(
                Level(maxMoves: 5, seeds: new[] { At(0, 0, A, 2), At(1, 0, B, 3) }), Balance());

            var result = session.ApplyMove(P(0, 0), P(1, 0));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(5, session.MovesLeft);
            Assert.AreEqual(2, session.Grid[0, 0].Density);
            Assert.AreEqual(3, session.Grid[1, 0].Density);
        }

        // ---------- §5.3 Сила фарбування і бризки ----------

        [Test]
        public void PaintPower_IsForceOverTen_ClampedToOne()
        {
            var balance = Balance();
            Assert.AreEqual(1, BurstResolver.PaintPower(10, balance));
            Assert.AreEqual(2, BurstResolver.PaintPower(23, balance));
            Assert.AreEqual(4, BurstResolver.PaintPower(47, balance));
            Assert.AreEqual(6, BurstResolver.PaintPower(68, balance));
            Assert.AreEqual(1, BurstResolver.PaintPower(5, balance), "мінімум 1");
        }

        /// <summary>ЗАПОБІЖНИК §14.1: PaintPower ніколи не >= threshold — інакше самопідтримний ланцюг.</summary>
        [Test]
        public void PaintPower_NeverReachesThreshold_ForAnyForce()
        {
            foreach (var threshold in new[] { 2, 3, 5, 10, 15 })
            {
                var balance = Balance(burstThreshold: threshold);
                for (var force = 1; force <= 5000; force++)
                {
                    var power = BurstResolver.PaintPower(force, balance);
                    Assert.IsTrue(power >= 1, $"сила {force}: power {power} < 1");
                    Assert.IsTrue(power < threshold,
                        $"поріг {threshold}, сила {force}: power {power} >= порогу — вибух створив би краплю, що лопне сама");
                }
            }
        }

        [Test]
        public void SplashCount_IsOnePerFullFifteen()
        {
            var balance = Balance();
            Assert.AreEqual(0, BurstResolver.SplashCount(14, balance));
            Assert.AreEqual(1, BurstResolver.SplashCount(15, balance));
            Assert.AreEqual(3, BurstResolver.SplashCount(47, balance));
        }

        [Test]
        public void Halo_IsManhattanRingOfExactlyTwo()
        {
            Assert.AreEqual(8, GridModel.HaloOffsets.Length);
            foreach (var offset in GridModel.HaloOffsets)
                Assert.AreEqual(2, GridPos.ManhattanDistance(default, offset));
        }

        [Test]
        public void NearMiss_IsTheBandJustBelowTheThreshold()
        {
            var balance = Balance();

            // Порог 10, частка 0.8 — смуга «ось-ось лопне» це рівно 8 і 9.
            Assert.IsFalse(MergeRules.IsNearMiss(7, 0.8f, balance));
            Assert.IsTrue(MergeRules.IsNearMiss(8, 0.8f, balance));
            Assert.IsTrue(MergeRules.IsNearMiss(9, 0.8f, balance));

            // Десятка вже не «майже»: вона лопнула. Підсвітка на ній була б брехнею —
            // такої краплі на полі не існує.
            Assert.IsFalse(MergeRules.IsNearMiss(10, 0.8f, balance));
            Assert.IsFalse(MergeRules.IsNearMiss(13, 0.8f, balance));

            Assert.IsFalse(MergeRules.IsNearMiss(0, 0.8f, balance));
        }

        [Test]
        public void NearMiss_NeverOverlapsWithBursting_ForAnyThreshold()
        {
            foreach (var threshold in new[] { 4, 6, 10, 16 })
            {
                var balance = new BalanceData(burstThreshold: threshold);
                for (var density = 0; density <= threshold * 2; density++)
                {
                    var near = MergeRules.IsNearMiss(density, 0.8f, balance);
                    var bursts = MergeRules.ReachesThreshold(density, balance);
                    Assert.IsFalse(near && bursts,
                        $"поріг {threshold}, густота {density}: одночасно «майже» і «вибух»");
                }
            }
        }

        // ---------- §5.4 Ефект на клітинку ----------

        [Test]
        public void Burst_PaintsEmpty_GrowsSame_BlursOther()
        {
            var balance = Balance();
            // Сила 30 → power 3. Хрест: (2,3) порожня, (3,2) той самий колір d2, (1,2) чужий d5, (2,1) чужий d2.
            var grid = Grid(5, 5,
                At(2, 2, A, 30),
                At(3, 2, A, 2),
                At(1, 2, B, 5),
                At(2, 1, B, 2));

            ResolveAt(grid, P(2, 2), balance);

            Assert.IsTrue(grid[2, 2].IsEmpty, "власна клітинка звільняється");
            Assert.AreEqual(A, grid[2, 3].Color);
            Assert.AreEqual(3, grid[2, 3].Density, "порожня → густота = силі фарбування");
            Assert.AreEqual(5, grid[3, 2].Density, "той самий колір: 2 + 3");
            Assert.AreEqual(B, grid[1, 2].Color, "чужий колір лише розмито");
            Assert.AreEqual(2, grid[1, 2].Density, "5 − 3");
            Assert.AreEqual(A, grid[2, 1].Color, "розмито до нуля → перефарбовано");
            Assert.AreEqual(1, grid[2, 1].Density, "перефарбована крапля завжди густоти 1");
        }

        [Test]
        public void Burst_ZoneIsAlwaysCrossOfFour_RegardlessOfForce()
        {
            var balance = Balance();
            foreach (var force in new[] { 10, 47, 250, 9999 })
            {
                // Центр поля 7×7, навколо порожньо: рахуємо, скільки клітинок отримало фарбу
                // безпосередньо (хрест). Бризки летять у гало — вони не рахуються як зона.
                var grid = Grid(7, 7, At(3, 3, A, force));
                var result = ResolveAt(grid, P(3, 3), balance, new FixedRandom(0));

                var crossPaints = 0;
                foreach (var e in result.Events)
                {
                    if (e.ChainIndex != 0) continue;
                    if (e.Type != GameEventType.Paint && e.Type != GameEventType.Grow &&
                        e.Type != GameEventType.Blur && e.Type != GameEventType.Repaint)
                        continue;
                    if (GridPos.ManhattanDistance(P(3, 3), e.Position) == 1)
                        crossPaints++;
                }

                Assert.AreEqual(4, crossPaints, $"сила {force}: зона має бути рівно 4 клітинки");
            }
        }

        /// <summary>ЗАПОБІЖНИК §14.7: вибух у кутку не кидає виняток (вихід за межі сітки).</summary>
        [Test]
        public void Burst_InCorner_DoesNotThrow_AndReportsOutOfBounds()
        {
            var balance = Balance();
            var grid = Grid(4, 4, At(0, 0, A, 45));

            MoveResult result = null!;
            Assert.DoesNotThrow(() => result = ResolveAt(grid, P(0, 0), balance, new FixedRandom(4)));

            Assert.IsTrue(result.CountEvents(GameEventType.OutOfBounds) >= 2, "два промені хреста вилились за край");
            Assert.IsTrue(grid[0, 0].IsEmpty);
        }

        [Test]
        public void Burst_WallIsImmune_IceThaws_BlotCleared()
        {
            var balance = Balance();
            var grid = Grid(5, 5,
                At(2, 2, A, 20),
                At(2, 3, B, 4, CellFlags.Wall),
                At(3, 2, B, 4, CellFlags.Ice),
                At(1, 2, InkColor.None, 0, CellFlags.Blot));

            var result = ResolveAt(grid, P(2, 2), balance);

            Assert.AreEqual(4, grid[2, 3].Density, "стіну не бере ніщо");
            Assert.IsTrue(grid[2, 3].Has(CellFlags.Wall));
            Assert.AreEqual(4, grid[3, 2].Density, "лід прийняв удар, фарба крізь нього не пройшла");
            Assert.IsFalse(grid[3, 2].Has(CellFlags.Ice), "але розтанув");
            Assert.IsFalse(grid[1, 2].Has(CellFlags.Blot), "кляксу змито сусіднім вибухом");
            Assert.AreEqual(1, result.CountEvents(GameEventType.Thaw));
            Assert.AreEqual(1, result.CountEvents(GameEventType.BlotCleared));
        }

        [Test]
        public void Splashes_LandOnHalo_WithPowerOne()
        {
            var balance = Balance();
            // Сила 30 → 2 бризки; FixedRandom(0) шле обидві в HaloOffsets[0] = (0,+2).
            var grid = Grid(7, 7, At(3, 3, A, 30));

            var result = ResolveAt(grid, P(3, 3), balance, new FixedRandom(0));

            Assert.AreEqual(2, result.CountEvents(GameEventType.Splash));
            var splash = FirstEvent(result, GameEventType.Splash);
            Assert.AreEqual(P(3, 5), splash.Position);
            // Перша бризка малює густоту 1, друга додає ще 1 (той самий колір).
            Assert.AreEqual(2, grid[3, 5].Density);
        }
    }
}
