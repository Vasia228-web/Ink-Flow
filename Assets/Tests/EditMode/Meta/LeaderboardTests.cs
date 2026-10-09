using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Сторожі таблиці лідерів. Головне — сортування: воно живе в моделі саме для
    /// того, щоб «Планети» й «Рекорд» не почали колись впорядковуватись по-різному.
    /// </summary>
    public sealed class LeaderboardTests
    {
        [Test]
        public void Mock_TopThreeMatchesReferenceScreenshot()
        {
            var board = Leaderboard.CreateMock();
            var ranked = new System.Collections.Generic.List<RankPlayer>();
            board.Ranked(RankScope.World, RankMetric.Planets, RankPeriod.Week, ranked);

            Assert.AreEqual("Вега", ranked[0].Nick);
            Assert.AreEqual(56, ranked[0].PlanetsWeek);
            Assert.AreEqual("Гелій", ranked[1].Nick);
            Assert.AreEqual(52, ranked[1].PlanetsWeek);
            Assert.AreEqual("Квазар", ranked[2].Nick);
            Assert.AreEqual(49, ranked[2].PlanetsWeek);
        }

        [Test]
        public void Ranked_SortsDescendingForEveryMetric()
        {
            var board = Leaderboard.CreateMock();

            foreach (RankMetric metric in System.Enum.GetValues(typeof(RankMetric)))
            foreach (RankPeriod period in System.Enum.GetValues(typeof(RankPeriod)))
            {
                var ranked = new System.Collections.Generic.List<RankPlayer>();
                board.Ranked(RankScope.World, metric, period, ranked);
                for (var i = 1; i < ranked.Count; i++)
                    Assert.GreaterOrEqual(
                        ranked[i - 1].Value(metric, period),
                        ranked[i].Value(metric, period),
                        $"{metric}/{period}: порядок порушено на позиції {i}.");
            }
        }

        [Test]
        public void Ranked_FriendsScopeKeepsOnlyFriends()
        {
            var board = Leaderboard.CreateMock();
            var friends = new System.Collections.Generic.List<RankPlayer>();
            var world = new System.Collections.Generic.List<RankPlayer>();
            board.Ranked(RankScope.Friends, RankMetric.Planets, RankPeriod.Week, friends);
            board.Ranked(RankScope.World, RankMetric.Planets, RankPeriod.Week, world);

            Assert.Less(friends.Count, world.Count);
            foreach (var player in friends)
                Assert.IsTrue(player.IsFriend, $"{player.Nick} потрапив у «Друзі», не будучи другом.");
        }

        [Test]
        public void Ranked_IsStableAcrossRepeatedCalls()
        {
            var board = Leaderboard.CreateMock();

            // Порядок не сміє стрибати між перемиканнями вкладок: рівні значення
            // розводяться ніком, а не порядком у вихідному списку.
            var buffer = new System.Collections.Generic.List<RankPlayer>();
            board.Ranked(RankScope.World, RankMetric.Galaxies, RankPeriod.Week, buffer);

            var first = new System.Collections.Generic.List<string>();
            foreach (var p in buffer)
                first.Add(p.Nick);

            board.Ranked(RankScope.Friends, RankMetric.Record, RankPeriod.AllTime, buffer);
            board.Ranked(RankScope.World, RankMetric.Galaxies, RankPeriod.Week, buffer);

            for (var i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i], buffer[i].Nick);
        }

        [Test]
        public void Value_PicksTheRightFieldPerMetricAndPeriod()
        {
            var board = Leaderboard.CreateMock();
            var you = board.You;

            Assert.AreEqual(12, you.Value(RankMetric.Planets, RankPeriod.Week));
            Assert.AreEqual(41, you.Value(RankMetric.Planets, RankPeriod.AllTime));
            Assert.AreEqual(1, you.Value(RankMetric.Galaxies, RankPeriod.Week));
            Assert.AreEqual(3, you.Value(RankMetric.Galaxies, RankPeriod.AllTime));
            // Рекорд — картинки в колекції (майстер-док §8, §10), не очки.
            Assert.AreEqual(3, you.Value(RankMetric.Record, RankPeriod.Week));
            Assert.AreEqual(11, you.Value(RankMetric.Record, RankPeriod.AllTime));
        }

        [Test]
        public void YourPosition_And_Gap_MatchTheReference()
        {
            var board = Leaderboard.CreateMock();

            // Картка «Ти» в макеті: «#214 … ще +2 до №213».
            Assert.AreEqual(214, board.YourPosition(RankMetric.Planets, RankPeriod.Week));
            Assert.AreEqual(2, board.GapToNext(RankMetric.Planets, RankPeriod.Week));
        }

        [Test]
        public void Units_DifferPerMetric()
        {
            Assert.AreEqual("планет", Leaderboard.Unit(RankMetric.Planets));
            Assert.AreEqual("галактик", Leaderboard.Unit(RankMetric.Galaxies));
            Assert.AreEqual("картинок", Leaderboard.Unit(RankMetric.Record));
        }

        [Test]
        public void Mock_HasEnoughPlayersForPodiumAndScroll()
        {
            var board = Leaderboard.CreateMock();

            // Подіум забирає трьох, решта має заповнити список із запасом на скрол.
            Assert.GreaterOrEqual(board.Players.Count, 15);
            Assert.IsTrue(board.You.IsYou);
        }

        [Test]
        public void WithRealPlayer_FollowsTheHideProfileSetting()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            Assert.IsFalse(Leaderboard.WithRealPlayer(state).You.Incognito, "за замовчуванням профіль видно");

            state.SetProfileHidden(true);
            Assert.IsTrue(Leaderboard.WithRealPlayer(state).You.Incognito, "§15: «Приховати профіль у рейтингах» — інкогніто");

            state.SetProfileHidden(false);
            Assert.IsFalse(Leaderboard.WithRealPlayer(state).You.Incognito, "перемикач назад — знову видно");
        }

        [Test]
        public void WithRealPlayer_CarriesTheChosenAvatar()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.SetAvatar(4);
            Assert.AreEqual(AvatarSet.InkOf(4), Leaderboard.WithRealPlayer(state).You.Avatar, "§14: аватар із профілю — у рядку рейтингу");
            foreach (var player in Leaderboard.CreateMock().Players)
                Assert.AreEqual(InkFlow.Core.InkColor.None, player.Avatar, "мокові гравці — за кольором краплі");
        }

        [Test]
        public void IncognitoPlayers_StayInTheTableButAreMarked()
        {
            var board = Leaderboard.CreateMock();

            var hidden = 0;
            foreach (var player in board.Players)
                if (player.Incognito)
                    hidden++;

            // Схований профіль не зникає з таблиці — інакше місця «поїхали» б.
            Assert.Greater(hidden, 0);
            Assert.Less(hidden, board.Players.Count);
        }
    }
}
