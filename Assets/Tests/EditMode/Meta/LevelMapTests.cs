using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Геометрія карти рахується формулою, тож зламати її можна одним числом
    /// у константі — звідси тести саме на межі полотна, а не на конкретні координати.
    /// </summary>
    public sealed class LevelMapTests
    {
        [Test]
        public void NodeY_FirstLevelIsAtTheBottom()
        {
            var map = LevelMap.CreateMock();

            // Y рахується від верху, тому «нижче» означає «більше».
            Assert.Greater(map.NodeY(1), map.NodeY(map.Total));
        }

        [Test]
        public void NodeY_EveryNodeFitsInsideCanvasHeight()
        {
            var map = LevelMap.CreateMock();

            foreach (var node in map.Nodes)
            {
                var y = map.NodeCenterY(node);
                Assert.GreaterOrEqual(y, 0f, $"вузол {node.Number} вилазить за верх");
                Assert.LessOrEqual(y, map.Height, $"вузол {node.Number} вилазить за низ");
            }
        }

        [Test]
        public void NodeCenterX_EveryNodeFitsOnScreen()
        {
            var map = LevelMap.CreateMock();

            // Саме тут ловиться бонус після 17-го: слід там стоїть майже впритул
            // до правої межі, і нескорочений зсув +130 виносив би вузол за екран.
            foreach (var node in map.Nodes)
            {
                var x = map.NodeCenterX(node);
                Assert.GreaterOrEqual(x, LevelMap.EdgePadding,
                    $"вузол {node.Number} ({node.Kind}) вилазить ліворуч");
                Assert.LessOrEqual(x, LevelMap.CanvasWidth - LevelMap.EdgePadding,
                    $"вузол {node.Number} ({node.Kind}) вилазить праворуч");
            }
        }

        [Test]
        public void NodeCenterX_BonusStaysOnItsOwnSide()
        {
            var map = LevelMap.CreateMock();

            foreach (var node in map.Nodes)
            {
                if (node.Kind != LevelNodeKind.Bonus)
                    continue;

                var anchor = map.NodeX(node.Number);
                var bonus = map.NodeCenterX(node);

                // Обрізання краєм не має перекинути гілку на протилежний бік:
                // інакше слід перетнув би сам себе.
                if (node.BonusOffsetX > 0f)
                    Assert.Greater(bonus, anchor, $"бонус після {node.Number} мав іти праворуч");
                else
                    Assert.Less(bonus, anchor, $"бонус після {node.Number} мав іти ліворуч");
            }
        }

        [Test]
        public void TrailWidth_ThickensTowardTheBoss()
        {
            var map = LevelMap.CreateMock();

            var far = map.TrailWidth(5);
            var near = map.TrailWidth(8);
            var atBoss = map.TrailWidth(10);

            Assert.Less(far, near);
            Assert.Less(near, atBoss);
        }

        [Test]
        public void TrailWidth_ResetsRightAfterTheBoss()
        {
            var map = LevelMap.CreateMock();

            // Одинадцятий — початок нового відрізка, слід має знову стати тонким.
            Assert.AreEqual(map.TrailWidth(1), map.TrailWidth(11), 0.001f);
        }

        [Test]
        public void NextBoss_IsTheBossItselfWhenStandingOnIt()
        {
            Assert.AreEqual(10, LevelMap.NextBoss(10));
            Assert.AreEqual(20, LevelMap.NextBoss(11));
            Assert.AreEqual(10, LevelMap.NextBoss(1));
        }

        [Test]
        public void Mock_CurrentIsUnlockedAndEverythingAfterItIsLocked()
        {
            var map = LevelMap.CreateMock(current: 12);
            var current = map.Current;

            Assert.NotNull(current);
            Assert.AreEqual(12, current!.Number);
            Assert.IsFalse(current.Locked);

            foreach (var node in map.Nodes)
            {
                if (node.Kind == LevelNodeKind.Bonus)
                    continue;
                Assert.AreEqual(node.Number > 12, node.Locked,
                    $"рівень {node.Number} має бути {(node.Number > 12 ? "замкнений" : "відкритий")}");
            }
        }

        [Test]
        public void Mock_OnlyOneNodeIsCurrent()
        {
            var map = LevelMap.CreateMock();

            var count = 0;
            foreach (var node in map.Nodes)
                if (node.IsCurrent)
                    count++;

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Mock_BonusUnlocksByTotalStarsNotByLevelNumber()
        {
            var map = LevelMap.CreateMock();
            var stars = map.TotalStars;

            foreach (var node in map.Nodes)
            {
                if (node.Kind != LevelNodeKind.Bonus)
                    continue;

                // Замок бонусу залежить від суми зірок — тому його можна взяти
                // й пізніше, повернувшись за зірками на пройдені рівні.
                Assert.AreEqual(stars < node.StarsRequired, node.Locked,
                    $"бонус на {node.StarsRequired}★ при {stars}★ у гравця");
            }
        }

        [Test]
        public void Mock_BonusesPointAtRealLevels()
        {
            var map = LevelMap.CreateMock();

            foreach (var node in map.Nodes)
            {
                Assert.Greater(node.PlayableLevel, 0, $"вузол {node.Number} нікуди не веде");
                if (node.Kind == LevelNodeKind.Bonus)
                    // Бонуси живуть в окремому діапазоні, щоб не збігтися з основними.
                    Assert.GreaterOrEqual(node.PlayableLevel, 101);
                else
                    Assert.AreEqual(node.Number, node.PlayableLevel);
            }
        }

        [Test]
        public void Mock_ClearedMeansUnlockedWithStars()
        {
            var map = LevelMap.CreateMock();

            foreach (var node in map.Nodes)
            {
                if (!node.Cleared)
                    continue;
                Assert.IsFalse(node.Locked);
                Assert.Greater(node.Stars, 0);
                Assert.LessOrEqual(node.Stars, 3);
            }
        }

        [Test]
        public void TotalStars_IsTheSumOfEarnedStars()
        {
            var map = LevelMap.CreateMock();

            var sum = 0;
            foreach (var node in map.Nodes)
                sum += node.Stars;

            Assert.AreEqual(sum, map.TotalStars);
        }

        [Test]
        public void DailyFraction_MatchesDoneOverCap()
        {
            var map = LevelMap.CreateMock();

            Assert.AreEqual((float)map.DailyDone / map.DailyCap, map.DailyFraction, 0.0001f);
            Assert.LessOrEqual(map.DailyFraction, 1f);
        }

        [Test]
        public void DailyFraction_IsZeroWhenThereIsNoCap()
        {
            // Ділення на нуль дало б NaN, а NaN у ширині заливки з'їдає весь Layout.
            var map = new LevelMap(System.Array.Empty<LevelNode>(), total: 0, dailyDone: 3, dailyCap: 0);

            Assert.AreEqual(0f, map.DailyFraction);
        }

        [Test]
        public void Height_CoversEveryNodePlusPadding()
        {
            var map = LevelMap.CreateMock();

            Assert.AreEqual(LevelMap.TopPadding, map.NodeY(map.Total), 0.001f);
            Assert.AreEqual(map.NodeY(1) + LevelMap.BottomPadding, map.Height, 0.001f);
        }

        [Test]
        public void BoardSize_GrowsWithProgressAndPeaksOnTheBoss()
        {
            Assert.AreEqual(4, LevelMap.BoardSize(1));
            Assert.AreEqual(5, LevelMap.BoardSize(7));
            Assert.AreEqual(6, LevelMap.BoardSize(15));
            Assert.AreEqual(7, LevelMap.BoardSize(20 + 3));

            // Бос завжди 6×6 — інакше 25 ходів на великому полі нічого не важать.
            Assert.AreEqual(6, LevelMap.BoardSize(10));
            Assert.AreEqual(6, LevelMap.BoardSize(20));
        }

        [Test]
        public void Moves_AreLargerOnBossLevels()
        {
            Assert.Greater(LevelMap.Moves(10), LevelMap.Moves(11));
        }

        [Test]
        public void Hue_CyclesThroughTheFiveInks()
        {
            Assert.AreEqual(InkColor.Magenta, LevelMap.Hue(1));
            Assert.AreEqual(LevelMap.Hue(1), LevelMap.Hue(6));
            Assert.AreEqual(LevelMap.Hue(3), LevelMap.Hue(13));

            for (var n = 1; n <= 40; n++)
            {
                var hue = LevelMap.Hue(n);
                // None — це «немає кольору», а не шостий відтінок: вузол із ним
                // намалювався б прозорим.
                Assert.AreNotEqual(InkColor.None, hue, $"рівень {n} лишився без кольору");
                Assert.LessOrEqual((int)hue, (int)InkColor.Violet,
                    $"рівень {n} дістав колір поза п'ятіркою ігрових чорнил");
            }
        }
    }
}
