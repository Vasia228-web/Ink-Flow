using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Platform;
using NUnit.Framework;

namespace InkFlow.Services.Tests
{
    /// <summary>
    /// Fake-сервіси рейтингів (§16) без Unity: таблиця тримає значення за метрикою Й періодом, офлайн
    /// віддає статус без винятків, мок-вітрина інкогніто прихована, а неповна розкладка не валить мок.
    /// </summary>
    public sealed class RankingsServicesTests
    {
        [Test]
        public void FakeLeaderboards_KeepValuesPerMetricAndPeriod()
        {
            var service = new FakeLeaderboards();
            service.Submit(RankMetric.Planets, RankPeriod.Week, 2);
            service.Submit(RankMetric.Planets, RankPeriod.AllTime, 500);

            Assert.AreEqual(2, service.Submitted(RankMetric.Planets, RankPeriod.Week));
            Assert.AreEqual(500, service.Submitted(RankMetric.Planets, RankPeriod.AllTime));
            Assert.IsNull(service.Submitted(RankMetric.Galaxies, RankPeriod.Week));

            LeaderboardPage? week = null, all = null;
            service.Fetch(RankMetric.Planets, RankPeriod.Week, 50, page => week = page);
            service.Fetch(RankMetric.Planets, RankPeriod.AllTime, 50, page => all = page);
            Assert.AreEqual(2, week!.YourValue, "тижнева сторінка — від тижневого значення");
            Assert.AreEqual(500, all!.YourValue, "«за весь час» — від лічильника");
            Assert.AreEqual(MockRankings.Count + 1, week.YourRank, "дві планети за тиждень — нижче за всіх вісімнадцятьох");
            Assert.AreEqual(1, all.YourRank, "п'ятсот за весь час — перше місце");
        }

        [Test]
        public void FakeLeaderboards_Offline_ReturnsNoConnectionWithoutThrowing()
        {
            var service = new FakeLeaderboards { Offline = true };
            LeaderboardPage? page = null;
            service.Fetch(RankMetric.Galaxies, RankPeriod.AllTime, 10, p => page = p);
            Assert.AreEqual(LeaderboardStatus.NoConnection, page!.Status);
            Assert.AreEqual(0, page.Entries.Count);
            var ok = true;
            service.Submit(RankMetric.Galaxies, RankPeriod.AllTime, 1, result => ok = result);
            Assert.IsFalse(ok, "офлайн — відправка не вдалась, але без винятку");
            Assert.AreEqual(0, service.Submissions);
        }

        [Test]
        public void FakeShowcase_ReturnsPublishedOrMock_AndHidesIncognito()
        {
            var layout = GalaxyLayout.Default;
            var ids = new List<string>();
            var slots = new List<int>();
            foreach (var planet in layout.Planets) { ids.Add(planet.Id); slots.Add(planet.Slots); }
            var service = new FakeShowcase(ids, slots);

            PublicShowcase? mock = null;
            service.Fetch(MockRankings.PlayerIdOf(1), s => mock = s);
            Assert.AreEqual("Гелій", mock!.Nick);
            Assert.Greater(mock.Slots.Count, 0);

            PublicShowcase? hidden = null;
            service.Fetch(MockRankings.PlayerIdOf(6), s => hidden = s);
            Assert.IsTrue(hidden!.Incognito, "шостий у моку — інкогніто");
            Assert.AreEqual(string.Empty, hidden.Nick, "прихований профіль не віддає ніка й у моку");
            Assert.AreEqual(0, hidden.Slots.Count);

            var mine = new PublicShowcase("me", "Нова", 2, false, "whale", 1, 0, 0, null, "");
            service.Publish(mine);
            PublicShowcase? back = null;
            service.Fetch("me", s => back = s);
            Assert.AreSame(mine, back, "опублікована вітрина повертається, а не мок");

            PublicShowcase? none = null;
            new FakeShowcase { Offline = true }.Fetch("me", s => none = s);
            Assert.IsNull(none, "офлайн — вітрини немає, без винятку");
        }

        [Test]
        public void MockShowcase_WithoutALayout_HasNoSlotsAndDoesNotThrow()
        {
            var bare = MockRankings.ShowcaseFor(MockRankings.PlayerIdOf(1), new string[0], new int[0]);
            Assert.AreEqual(0, bare.Slots.Count);
            var lopsided = MockRankings.ShowcaseFor(MockRankings.PlayerIdOf(1), new[] { "Earth", "Mars" }, new[] { 4 });
            Assert.AreEqual(4, lopsided.Slots.Count, "коротший зі списків обмежує");
        }

        [Test]
        public void NullServices_SayNotConfigured()
        {
            LeaderboardPage? page = null;
            new NullLeaderboards().Fetch(RankMetric.Planets, RankPeriod.Week, 10, p => page = p);
            Assert.AreEqual(LeaderboardStatus.NotConfigured, page!.Status);
            Assert.IsFalse(new NullIdentity().IsSignedIn);
            PublicShowcase? s = new PublicShowcase("x", "", 0, false, null, 0, 0, 0, null, "");
            new NullShowcase().Fetch("x", r => s = r);
            Assert.IsNull(s);
        }
    }
}
