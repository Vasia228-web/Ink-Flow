using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class RunBotTests
    {
        [Test]
        public void Bot_PlaysWholeRunsToALegitimateLoss()
        {
            var catalog = PieceCatalogData.Default;
            var totalPlacements = 0;

            for (uint seed = 1; seed <= 25; seed++)
            {
                var session = TestBoard.NewSession(seed * 31u);
                var bot = new RunBot(BotWeights.Default, new XorShiftRandom(seed), noise: 3f);
                var guard = 0;
                MoveResult? last = null;

                while (!session.IsOver && guard++ < 5000)
                {
                    Assert.IsTrue(bot.TryChooseMove(session, out var index, out var anchor),
                        "поки партія жива, бот мусить бачити хід — інакше сесія і бот розійшлись у правилах");
                    last = session.TryPlace(index, anchor);
                    Assert.IsTrue(last.Accepted);
                }

                Assert.AreEqual(GameState.Lost, session.State, $"сід {seed}: партія має закінчитись програшем");
                Assert.IsTrue(last!.Has(GameEventType.GameLost));
                totalPlacements += session.PlacementCount;

                // Чесність смерті: якщо програш стався одразу після поповнення лотка,
                // на полі не має бути місця навіть для двоклітинкової фігури.
                if (last.Has(GameEventType.TrayRefilled))
                    for (var i = 0; i < catalog.Count; i++)
                        if (catalog[i].Size == 2)
                            Assert.IsFalse(PlacementRules.AnyFit(session.Board, catalog[i]),
                                $"сід {seed}: мішок видав неможливий набір, хоч {catalog[i].Id} влазить");
            }

            Assert.Greater(totalPlacements / 25, 12, "середня партія бота надто коротка — щось зламано в правилах");
        }

        [Test]
        public void Bot_CompletesPicturesInARealLibrary()
        {
            var completed = 0;
            for (uint seed = 1; seed <= 10; seed++)
            {
                var session = TestBoard.NewSession(seed * 17u);
                var bot = new RunBot(BotWeights.Default, new XorShiftRandom(seed), noise: 3f);
                var guard = 0;
                while (!session.IsOver && guard++ < 5000 && bot.TryChooseMove(session, out var index, out var anchor))
                    session.TryPlace(index, anchor);
                completed += session.PicturesCompleted;
            }
            Assert.Greater(completed, 0, "за десять забігів бот мусить домалювати хоч одну картинку");
        }

        [Test]
        public void Bot_PrefersClearingALineOverBurying()
        {
            var session = TestBoard.NewSession(7u, null, TestBoard.BlueSquare(8));
            TestBoard.FillRow(session.Board, 0, "bbbbbb..");
            TestBoard.SetTray(session, TestBoard.Piece("2h", TestBoard.Blue));

            var bot = new RunBot();
            Assert.IsTrue(bot.TryChooseMove(session, out var index, out var anchor));
            Assert.AreEqual(0, index);
            Assert.AreEqual(new GridPos(6, 0), anchor, "єдиний хід, що зриває лінію");
        }
    }
}
