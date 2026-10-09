using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.Platform;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Рейтинги (§16) на живому рендері: таблиця від сервісу з подіумом і рядками, картка «Ти» зі справжніми
    /// числами й аватаром; без мережі — «Немає з'єднання», без сервісів — «не підключені», і ніщо не падає;
    /// інкогніто не відкривається; тап по гравцю читає вітрину й відкриває галактику гостя.
    /// Потрібен зібраний префаб — Ink Flow → Setup → Build Rankings Screen.
    /// </summary>
    public sealed class RankingsScreenTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<RankingsScreen>.Available("RankingsScreen"))
                Assert.Ignore("Немає префаба рейтингів — спершу Ink Flow → Setup → Build Rankings Screen.");
        }

        private static (System.Collections.Generic.List<string> ids, System.Collections.Generic.List<int> slots) LayoutOf(PlayerState player)
        {
            var ids = new System.Collections.Generic.List<string>();
            var slots = new System.Collections.Generic.List<int>();
            foreach (var planet in player.Layout.Planets) { ids.Add(planet.Id); slots.Add(planet.Slots); }
            return (ids, slots);
        }

        [Test]
        public void Online_ShowsPodiumRowsAndARealYouCard()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            player.SetAvatar(4);
            rig.FillPlanet(player, 0);
            var leaderboards = new FakeLeaderboards();
            leaderboards.Submit(RankMetric.Planets, RankPeriod.Week, 1);
            leaderboards.Submit(RankMetric.Planets, RankPeriod.AllTime, 1);
            var (ids, slots) = LayoutOf(player);
            rig.Screen.BindServices(leaderboards, new FakeShowcase(ids, slots), new FakeIdentity("local-player"));

            rig.Enter(player, ScreenArgs.Empty);

            var board = rig.Screen.PreviewBoard;
            Assert.IsNotNull(board);
            Assert.IsTrue(board!.IsOk);
            Assert.IsFalse(rig.Screen.PreviewStatusShown, "таблиця є — блок стану схований");
            Assert.IsTrue(rig.Screen.PreviewPodiumShown, "вісімнадцять гравців — подіум є");
            Assert.AreEqual("Вега", board.Players[0].Nick, "мок збігається з еталонним скріншотом");
            Assert.AreEqual(1, board.You.Value, "картка «Ти» — з локального файлу: одна планета ожила");
            Assert.AreEqual(AvatarSet.InkOf(4), board.You.Avatar, "аватар із профілю");
            Assert.AreEqual($"#{board.YourPosition}", rig.Screen.PreviewYouPosition);
            Assert.AreEqual("1", rig.Screen.PreviewYouValue);

            var shownRows = 0;
            foreach (var row in rig.Screen.PreviewRows)
                if (row != null && row.gameObject.activeSelf)
                    shownRows++;
            Assert.Greater(shownRows, 0, "рядки після подіуму заповнені");
        }

        [Test]
        public void Offline_SaysNoConnection_KeepsYourNumbers_AndDoesNotThrow()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[1], "RankingsScreen");
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0);
            rig.Screen.BindServices(new FakeLeaderboards { Offline = true }, new FakeShowcase { Offline = true }, new FakeIdentity());

            rig.Enter(player, ScreenArgs.Empty);

            Assert.IsTrue(rig.Screen.PreviewStatusShown);
            Assert.AreEqual("Немає з'єднання", rig.Screen.PreviewStatusTitle);
            Assert.IsFalse(rig.Screen.PreviewPodiumShown);
            Assert.AreEqual("#—", rig.Screen.PreviewYouPosition, "місця без сервера не вигадуємо");
            Assert.AreEqual("1", rig.Screen.PreviewYouValue, "свої числа є й без мережі");

            // Перемикання метрики й періоду без мережі теж не падає.
            rig.Screen.PreviewSetMetric(RankMetric.Galaxies);
            rig.Screen.PreviewSetPeriod(RankPeriod.AllTime);
            Assert.IsTrue(rig.Screen.PreviewStatusShown);
            Assert.AreEqual("0", rig.Screen.PreviewYouValue, "галактик ще немає");
        }

        [Test]
        public void NoServices_SaysNotConfigured()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindServices(new NullLeaderboards(), new NullShowcase(), new NullIdentity());

            rig.Enter(player, ScreenArgs.Empty);

            Assert.IsTrue(rig.Screen.PreviewStatusShown);
            Assert.AreEqual("Рейтинги ще не підключені", rig.Screen.PreviewStatusTitle);
            Assert.AreEqual(LeaderboardStatus.NotConfigured, rig.Screen.PreviewBoard!.Status);
        }

        [Test]
        public void Tap_OpensAVisitorGalaxy_ButNeverAnIncognitoOrYourself()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            var (ids, slots) = LayoutOf(player);
            rig.Screen.BindServices(new FakeLeaderboards(), new FakeShowcase(ids, slots), new FakeIdentity("local-player"));
            GalaxyArgs? opened = null;
            rig.Screen.PlayerOpened += args => opened = args;
            rig.Enter(player, ScreenArgs.Empty);

            var board = rig.Screen.PreviewBoard!;
            RankPlayer? incognito = null;
            foreach (var p in board.Players)
                if (p.Incognito) { incognito = p; break; }
            Assert.IsNotNull(incognito, "у моку є інкогніто");

            rig.Screen.PreviewOpen(incognito!);
            Assert.IsNull(opened, "§16: прихований профіль не відкривається");
            rig.Screen.PreviewOpen(board.You);
            Assert.IsNull(opened, "себе відкривати нема куди");

            rig.Screen.PreviewOpen(board.Players[1]);
            Assert.IsNotNull(opened, "тап по гравцю відкриває його галактику");
            Assert.IsTrue(opened!.ReadOnly, "лише перегляд");
            Assert.IsNotNull(opened.Visitor);
            Assert.AreEqual(board.Players[1].Nick, opened.Visitor!.Nick, "вітрина того самого гравця");
            Assert.AreEqual(board.Players[1].Id, opened.Owner.Value);
        }

        [Test]
        public void LateAnswer_AfterLeavingTheScreen_IsIgnored()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            var slow = new DelayedLeaderboards();
            rig.Screen.BindServices(slow, new FakeShowcase(), new FakeIdentity());

            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual("Завантажую…", rig.Screen.PreviewStatusTitle);
            rig.Screen.OnExit();
            slow.AnswerOldest();
            Assert.AreEqual(0, rig.Screen.PreviewBoard!.Players.Count, "відповідь після виходу відкинута — таблиця не підмінена");
            Assert.IsFalse(rig.Screen.PreviewPodiumShown);

            // Повторний вхід — свіжий запит, а не стара відповідь.
            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual("Завантажую…", rig.Screen.PreviewStatusTitle);
            Assert.AreEqual(1, slow.Pending, "попередній запит забутий");
        }

        [Test]
        public void StaleAnswer_AfterSwitchingTheMetric_IsIgnored_AndTheNewOneLands()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            var slow = new DelayedLeaderboards();
            rig.Screen.BindServices(slow, new FakeShowcase(), new FakeIdentity());

            rig.Enter(player, ScreenArgs.Empty);                 // запит «Планети»
            rig.Screen.PreviewSetMetric(RankMetric.Galaxies);    // запит «Галактики», старий ще в дорозі
            slow.AnswerOldest();                                 // приходить відповідь «Планети»
            Assert.AreEqual(0, rig.Screen.PreviewBoard!.Players.Count, "стара відповідь не лягає на новий зріз");
            Assert.AreEqual(RankMetric.Galaxies, rig.Screen.PreviewBoard.Metric);

            slow.AnswerOldest();                                 // відповідь «Галактики»
            Assert.AreEqual(RankMetric.Galaxies, rig.Screen.PreviewBoard!.Metric);
            Assert.Greater(rig.Screen.PreviewBoard.Players.Count, 0, "нова відповідь застосована");
            Assert.IsTrue(rig.Screen.PreviewPodiumShown);
        }

        [Test]
        public void ReturningToTheScreen_KeepsTheTableWhileRefreshing()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            var slow = new DelayedLeaderboards();
            rig.Screen.BindServices(slow, new FakeShowcase(), new FakeIdentity());

            rig.Enter(player, ScreenArgs.Empty);
            slow.AnswerOldest();
            Assert.IsTrue(rig.Screen.PreviewPodiumShown);

            rig.Screen.OnExit();
            rig.Enter(player, ScreenArgs.Empty);                 // повернення з чужої галактики
            Assert.IsTrue(rig.Screen.PreviewPodiumShown, "попередня таблиця лишається, поки оновлення в дорозі");
            Assert.IsFalse(rig.Screen.PreviewStatusShown, "«Завантажую…» не блимає поверх таблиці");
        }

        [Test]
        public void YourId_IsReadAtRequestTime_NotAtBind()
        {
            using var rig = MetaScreenRig<RankingsScreen>.Create(ScreenRigBase.Devices[0], "RankingsScreen");
            var player = rig.NewPlayer();
            var identity = new DelayedIdentity();
            rig.Screen.BindServices(new FakeLeaderboards(), new FakeShowcase(), identity);

            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual("you", rig.Screen.PreviewBoard!.You.Id, "до входу — локальний id");

            identity.Finish("cloud-42");
            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual("cloud-42", rig.Screen.PreviewBoard!.You.Id, "після входу — хмарний id, без повторного BindServices");
        }

        /// <summary>Тотожність, що входить пізніше, ніж екран підв'язано, — як справжній асинхронний UGS.</summary>
        private sealed class DelayedIdentity : IIdentityService
        {
            private string _id = string.Empty;
            public bool IsSignedIn => _id.Length > 0;
            public string PlayerId => _id;
            public void SignIn(System.Action<bool> done) { }
            public void Finish(string id) => _id = id;
        }

        /// <summary>Сервіс, що відповідає лише коли його попросять, — щоб перевірити запізнілі відповіді.</summary>
        private sealed class DelayedLeaderboards : ILeaderboardService
        {
            private readonly System.Collections.Generic.Queue<(RankMetric metric, RankPeriod period, System.Action<LeaderboardPage> done)> _pending =
                new System.Collections.Generic.Queue<(RankMetric, RankPeriod, System.Action<LeaderboardPage>)>();

            public bool IsAvailable => true;

            public int Pending => _pending.Count;

            public void Fetch(RankMetric metric, RankPeriod period, int limit, System.Action<LeaderboardPage> done) =>
                _pending.Enqueue((metric, period, done));

            public void Submit(RankMetric metric, RankPeriod period, long value, System.Action<bool>? done = null) => done?.Invoke(true);

            /// <summary>Відповісти на найстаріший запит — так приходять запізнілі відповіді.</summary>
            public void AnswerOldest()
            {
                if (_pending.Count == 0)
                    return;
                var (metric, period, done) = _pending.Dequeue();
                done(MockRankings.Page(metric, period, 0));
            }
        }
    }
}
