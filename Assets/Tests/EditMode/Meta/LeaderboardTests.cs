using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Таблиця лідерів (§16): сторінка від сервісу → гравці на екрані, місце й розрив; мок детермінований
    /// і збігається з еталонним скріншотом; без мережі таблиця порожня зі статусом, картка «Ти» лишається.
    /// </summary>
    public sealed class LeaderboardTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        private static RankPlayer You(long value) => new RankPlayer("you", "Нова", InkColor.Magenta, false, value, 0) { IsYou = true };

        [Test]
        public void Mock_TopThreeMatchesReferenceScreenshot()
        {
            var page = MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 0);
            Assert.AreEqual("Вега", page.Entries[0].Nick);
            Assert.AreEqual(56, page.Entries[0].Value);
            Assert.AreEqual("Гелій", page.Entries[1].Nick);
            Assert.AreEqual(52, page.Entries[1].Value);
            Assert.AreEqual("Квазар", page.Entries[2].Nick);
            Assert.AreEqual(49, page.Entries[2].Value);
            Assert.AreEqual(MockRankings.Count, page.Entries.Count, "вісімнадцять гравців — вистачає на подіум і скрол");
        }

        [Test]
        public void Mock_RanksAreSequentialAndSortedForEveryMetricAndPeriod()
        {
            foreach (var metric in new[] { RankMetric.Planets, RankMetric.Galaxies })
                foreach (var period in new[] { RankPeriod.Week, RankPeriod.AllTime })
                {
                    var page = MockRankings.Page(metric, period, 0);
                    Assert.AreEqual(LeaderboardStatus.Ok, page.Status);
                    for (var i = 0; i < page.Entries.Count; i++)
                    {
                        Assert.AreEqual(i + 1, page.Entries[i].Rank, $"{metric}/{period}: місце {i}");
                        if (i > 0)
                            Assert.GreaterOrEqual(page.Entries[i - 1].Value, page.Entries[i].Value, $"{metric}/{period}: порядок");
                    }
                }
        }

        [Test]
        public void Mock_YourRankIsCountedFromYourValue_AndLimitCutsTheTail()
        {
            Assert.AreEqual(1, MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 100).YourRank, "вище за всіх — перше місце");
            Assert.AreEqual(3, MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 50).YourRank, "між Гелієм 52 і Квазаром 49");
            Assert.AreEqual(MockRankings.Count + 1, MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 0).YourRank, "нижче за всіх");

            var top = MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 50, limit: 5);
            Assert.AreEqual(5, top.Entries.Count);
            Assert.AreEqual(3, top.YourRank, "місце рахується по всій таблиці, не по сторінці");
        }

        [Test]
        public void FromPage_BuildsPlayersWithAvatarIncognitoPositionAndGap()
        {
            var page = MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 50);
            var board = new Leaderboard(page, You(50));

            Assert.IsTrue(board.IsOk);
            Assert.AreEqual(RankMetric.Planets, board.Metric);
            Assert.AreEqual(MockRankings.Count, board.Players.Count);
            Assert.AreEqual(1, board.Players[0].Rank);
            Assert.AreEqual(AvatarSet.InkOf(0), board.Players[0].Avatar, "аватар — із набору за номером");
            Assert.AreEqual(3, board.YourPosition);
            Assert.AreEqual(2, board.GapToNext, "до Гелія (52) бракує двох");

            var incognito = 0;
            foreach (var player in board.Players)
                if (player.Incognito)
                {
                    incognito++;
                    Assert.AreEqual("Плутон", player.Nick == "Плутон" ? "Плутон" : player.Nick == "Метеор" ? "Плутон" : player.Nick, "інкогніто лишаються в таблиці");
                }
            Assert.AreEqual(2, incognito, "двоє сховали профіль — вони в таблиці, але позначені");
            Assert.IsTrue(board.You.IsYou);
            Assert.AreEqual(PlanetType.Earth, board.You.Planet);
        }

        [Test]
        public void FromPage_FirstPlaceHasNoGap_AndUnknownRankStaysZero()
        {
            var first = new Leaderboard(MockRankings.Page(RankMetric.Galaxies, RankPeriod.AllTime, 1000), You(1000));
            Assert.AreEqual(1, first.YourPosition);
            Assert.AreEqual(0, first.GapToNext);

            var page = new LeaderboardPage(RankMetric.Planets, RankPeriod.Week, LeaderboardStatus.Ok,
                new[] { new LeaderboardEntry("a", "Альфа", 1, false, 9, 2), new LeaderboardEntry("b", "Бета", 2, false, 12, 1) }, 0, 0);
            var board = new Leaderboard(page, You(3));
            Assert.AreEqual("Бета", board.Players[0].Nick, "порядок — за місцем, не за порядком у відповіді");
            Assert.AreEqual(0, board.YourPosition, "сервер місця не дав — невідоме, а не вигадане");
            Assert.AreEqual(0, board.GapToNext);

            var mine = new LeaderboardPage(RankMetric.Planets, RankPeriod.Week, LeaderboardStatus.Ok,
                new[] { new LeaderboardEntry("b", "Бета", 2, false, 12, 1), new LeaderboardEntry("you", "Нова", 0, false, 3, 2) }, 0, 3);
            var withMe = new Leaderboard(mine, You(3));
            Assert.AreEqual(2, withMe.YourPosition, "мій рядок у сторінці — місце з нього");
            Assert.IsTrue(withMe.Players[1].IsYou);
            Assert.AreEqual(9, withMe.GapToNext);
        }

        [Test]
        public void Unavailable_KeepsStatusAndYou()
        {
            var board = Leaderboard.Unavailable(RankMetric.Planets, RankPeriod.Week, LeaderboardStatus.NoConnection, You(4));
            Assert.IsFalse(board.IsOk);
            Assert.AreEqual(LeaderboardStatus.NoConnection, board.Status);
            Assert.AreEqual(0, board.Players.Count);
            Assert.AreEqual(0, board.YourPosition);
            Assert.AreEqual(4, board.You.Value, "локальний файл — правда: свої числа є й без мережі");
        }

        [Test]
        public void Units_AndTitles_DifferPerMetric()
        {
            Assert.AreEqual("планет", Leaderboard.Unit(RankMetric.Planets));
            Assert.AreEqual("галактик", Leaderboard.Unit(RankMetric.Galaxies));
            Assert.AreEqual("Планети", Leaderboard.Title(RankMetric.Planets));
            Assert.AreEqual("Галактики", Leaderboard.Title(RankMetric.Galaxies));
        }

        [Test]
        public void You_FollowsTheProfile_AvatarNickAndHiddenFlag()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.SetAvatar(4);
            var you = RankPlayer.You(state, "p-1", RankMetric.Planets, RankPeriod.AllTime, Today);
            Assert.AreEqual("p-1", you.Id);
            Assert.AreEqual(state.Nick, you.Nick);
            Assert.AreEqual(AvatarSet.InkOf(4), you.Avatar, "§14: аватар із профілю — у картці «Ти»");
            Assert.IsFalse(you.Incognito);
            Assert.IsTrue(you.IsYou);

            state.SetProfileHidden(true);
            Assert.IsTrue(RankPlayer.You(state, "p-1", RankMetric.Planets, RankPeriod.AllTime, Today).Incognito, "§15: «Приховати профіль» — інкогніто");
            Assert.AreEqual("you", RankPlayer.You(state, "", RankMetric.Planets, RankPeriod.AllTime, Today).Id, "без хмарного id — локальний");
        }

        [Test]
        public void Planet_CyclesByRank_SoNeighboursNeverMatch()
        {
            var board = new Leaderboard(MockRankings.Page(RankMetric.Planets, RankPeriod.Week, 0), You(0));
            for (var i = 1; i < board.Players.Count; i++)
                Assert.AreNotEqual(board.Players[i - 1].Planet, board.Players[i].Planet, $"сусіди {i - 1} і {i}");
        }
    }
}
