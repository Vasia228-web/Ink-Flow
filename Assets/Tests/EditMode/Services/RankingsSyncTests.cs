using System;
using InkFlow.App;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Platform;
using NUnit.Framework;

namespace InkFlow.Services.Tests
{
    /// <summary>
    /// §16: локальний файл — правда, у хмару йде лише вітрина й числа. Синхронізація публікує лише те,
    /// що змінилось, тижневій таблиці шле приріст, прихований профіль — без ніка, а вхід повторюється.
    /// </summary>
    public sealed class RankingsSyncTests
    {
        private static readonly DateTime Wednesday = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        private sealed class DelayedIdentity : IIdentityService
        {
            private Action<bool>? _pending;
            public bool IsSignedIn { get; private set; }
            public string PlayerId => IsSignedIn ? "cloud-7" : string.Empty;
            public int Attempts { get; private set; }

            public void SignIn(Action<bool> done)
            {
                Attempts++;
                _pending = done;
            }

            public void Finish(bool ok)
            {
                IsSignedIn = ok;
                var pending = _pending;
                _pending = null;
                pending?.Invoke(ok);
            }
        }

        private static void CompletePlanet(PlayerState state, int index, DateTime when)
        {
            var planet = state.Layout.Planets[index];
            for (var i = 0; i < planet.Slots; i++)
            {
                var id = $"pic-{index}-{i}";
                state.CollectPicture(id, when);
                Assert.IsTrue(state.TryPlaceInSlot(state.CurrentGalaxy, planet.Id, i, id, when));
            }
        }

        [Test]
        public void Start_PublishesShowcaseAndBothPeriods_OnceForUnchangedState()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var boards = new FakeLeaderboards();
            var showcases = new FakeShowcase();
            using var sync = new RankingsSync(state, new FakeIdentity("cloud-1"), boards, showcases, () => Wednesday);

            sync.Start();
            Assert.AreEqual(1, showcases.Publications);
            Assert.AreEqual("cloud-1", showcases.LastPublished!.PlayerId);
            Assert.AreEqual(4, boards.Submissions, "дві метрики × два періоди");
            Assert.AreEqual(0, boards.Submitted(RankMetric.Planets, RankPeriod.Week));

            sync.Start();
            sync.Flush();
            Assert.AreEqual(1, showcases.Publications, "нічого не змінилось — нічого не шлемо");
            Assert.AreEqual(4, boards.Submissions);
        }

        [Test]
        public void WeekTableGetsTheWeekIncrement_AllTimeGetsTheCounter()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            CompletePlanet(state, 0, Wednesday.AddDays(-10));
            CompletePlanet(state, 1, Wednesday.AddDays(-10));
            var boards = new FakeLeaderboards();
            using var sync = new RankingsSync(state, new FakeIdentity(), boards, new FakeShowcase(), () => Wednesday);

            sync.Start();
            Assert.AreEqual(2, boards.Submitted(RankMetric.Planets, RankPeriod.AllTime), "за весь час — лічильник");
            Assert.AreEqual(0, boards.Submitted(RankMetric.Planets, RankPeriod.Week), "минулого тижня — не цього");
            Assert.AreEqual(4, boards.Submissions);

            CompletePlanet(state, 2, Wednesday);
            Assert.IsTrue(sync.IsDirty, "постановка в слот позначила зміни");
            sync.Flush();
            Assert.AreEqual(3, boards.Submitted(RankMetric.Planets, RankPeriod.AllTime));
            Assert.AreEqual(1, boards.Submitted(RankMetric.Planets, RankPeriod.Week), "цього тижня — приріст");
            Assert.AreEqual(0, boards.Submitted(RankMetric.Galaxies, RankPeriod.AllTime));
            Assert.AreEqual(6, boards.Submissions, "галактики не змінились — їх не шлемо повторно, лише дві планетні таблиці");
        }

        [Test]
        public void ManySlotChanges_CoalesceIntoOnePublication()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var showcases = new FakeShowcase();
            var boards = new FakeLeaderboards();
            using var sync = new RankingsSync(state, new FakeIdentity(), boards, showcases, () => Wednesday);
            sync.Start();
            Assert.AreEqual(1, showcases.Publications);

            CompletePlanet(state, 0, Wednesday);   // чотири постановки без Flush між ними
            sync.Flush();
            Assert.AreEqual(2, showcases.Publications, "дванадцять картинок — одна публікація на кадр");
            Assert.AreEqual(state.Layout.Planets[0].Slots, showcases.LastPublished!.Slots.Count);
        }

        [Test]
        public void SoundToggles_PublishNothing_ButHidingTheProfileDoes()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var showcases = new FakeShowcase();
            using var sync = new RankingsSync(state, new FakeIdentity(), new FakeLeaderboards(), showcases, () => Wednesday);
            sync.Start();

            state.SetSound(false);
            state.SetMusic(false);
            state.SetVibration(false);
            sync.Flush();
            Assert.AreEqual(1, showcases.Publications, "перемикачі звуку вітрини не міняють");

            state.SetProfileHidden(true);
            sync.Flush();
            Assert.AreEqual(2, showcases.Publications);
            Assert.IsTrue(showcases.LastPublished!.Incognito);
            Assert.AreEqual(string.Empty, showcases.LastPublished.Nick, "прихований профіль — без ніка");

            state.SetNick("Нова Зоря", NickRules.Default, out _);
            sync.Flush();
            Assert.AreEqual(2, showcases.Publications, "нік прихованого профілю назовні не йде — публікувати нічого");

            state.SetProfileHidden(false);
            sync.Flush();
            Assert.AreEqual("Нова Зоря", showcases.LastPublished!.Nick);
        }

        [Test]
        public void SignIn_IsRetried_UntilItSucceeds_AndChangesWaitForIt()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var identity = new DelayedIdentity();
            var showcases = new FakeShowcase();
            using var sync = new RankingsSync(state, identity, new FakeLeaderboards(), showcases, () => Wednesday);

            sync.Start();
            Assert.AreEqual(1, identity.Attempts);
            Assert.AreEqual(0, showcases.Publications, "без входу нічого не йде");
            Assert.AreEqual(string.Empty, sync.PlayerId);

            identity.Finish(false);           // офлайн-старт
            sync.Flush();                     // наступний кадр — ще одна спроба
            Assert.AreEqual(2, identity.Attempts);
            sync.Flush();
            Assert.AreEqual(2, identity.Attempts, "поки триває вхід — без паралельних спроб");

            identity.Finish(true);
            Assert.AreEqual(1, showcases.Publications, "вхід удався — зміни, що чекали, пішли");
            Assert.AreEqual("cloud-7", showcases.LastPublished!.PlayerId);
            Assert.AreEqual("cloud-7", sync.PlayerId);
        }

        [Test]
        public void FailedPublication_StaysDirty_AndIsRetried()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var showcases = new FakeShowcase { Offline = true };
            var boards = new FakeLeaderboards { Offline = true };
            using var sync = new RankingsSync(state, new FakeIdentity(), boards, showcases, () => Wednesday);
            sync.Start();
            Assert.IsTrue(sync.IsDirty, "не вдалось — зміни лишаються");
            showcases.Offline = false;
            boards.Offline = false;
            sync.Flush();
            Assert.AreEqual(1, showcases.Publications);
            Assert.AreEqual(4, boards.Submissions);
            Assert.IsFalse(sync.IsDirty);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var showcases = new FakeShowcase();
            var sync = new RankingsSync(state, new FakeIdentity(), new FakeLeaderboards(), showcases, () => Wednesday);
            sync.Start();
            sync.Dispose();
            state.SetAvatar(3);
            sync.Flush();
            Assert.AreEqual(1, showcases.Publications, "після Dispose події не слухаються");
        }
    }
}
