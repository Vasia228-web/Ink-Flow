using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Похідні від стану: галактика, карта рівнів, рейтинги.
    /// Усе це РАХУЄТЬСЯ зі збереження, а не зберігається окремо — тому
    /// розійтись із фактами не може, і саме це тут перевіряється.
    /// </summary>
    public sealed class DerivedProfileTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        private static PlayerState Fresh() => PlayerState.NewPlayer(EconomyData.Default);

        /// <summary>Збирає стільки картинок, скільки слотів, і ставить їх на планету — вона оживає.</summary>
        private static void CompletePlanet(PlayerState state, int index)
        {
            var planet = state.Layout.Planets[index];
            for (var i = 0; i < planet.Slots; i++)
            {
                var id = $"pic-{index}-{i}";
                state.CollectPicture(id, Today);
                Assert.IsTrue(state.TryPlaceInSlot(state.CurrentGalaxy, planet.Id, i, id, Today),
                    $"{planet.Name}, слот {i}: планета мала б бути відкритою");
            }
        }

        // ── Галактика ──

        [Test]
        public void NewGalaxy_OpensExactlyOnePlanet()
        {
            var galaxy = GalaxyProgress.FromSave(new GalaxyData(), GalaxyLayout.Default);

            Assert.AreEqual(0, galaxy.DoneCount);
            Assert.AreEqual(0, galaxy.CurrentIndex, "перша планета — поточна");

            var locked = 0;
            foreach (var planet in galaxy.Planets)
                if (planet.State == PlanetState.Locked)
                    locked++;
            Assert.AreEqual(galaxy.Planets.Count - 1, locked, "решта замкнена");
        }

        [Test]
        public void Galaxy_CompletingAPlanetOpensTheNext()
        {
            var state = Fresh();
            CompletePlanet(state, 0);

            var galaxy = GalaxyProgress.FromSave(state.Galaxy, state.Layout);

            Assert.AreEqual(PlanetState.Done, galaxy.Planets[0].State);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[1].State);
        }

        [Test]
        public void Galaxy_PartialProgressKeepsThePlanetCurrent()
        {
            var state = Fresh();
            state.CollectPicture("whale", Today);
            state.TryPlaceInSlot(0, state.Layout.Planets[0].Id, 0, "whale", Today);

            var galaxy = GalaxyProgress.FromSave(state.Galaxy, state.Layout);

            Assert.AreEqual(PlanetState.Current, galaxy.Planets[0].State);
            Assert.AreEqual(1, galaxy.Planets[0].FilledSlots);
        }

        [Test]
        public void Galaxy_CompletingEveryPlanetOpensTheNextGalaxy()
        {
            var state = Fresh();
            for (var i = 0; i < state.Layout.Planets.Count; i++)
                CompletePlanet(state, i);

            Assert.AreEqual(1, state.CurrentGalaxy, "усі планети ожили — Галактика II");
            var galaxy = GalaxyProgress.FromSave(state.Galaxy, state.Layout);
            Assert.AreEqual(1, galaxy.Index);
            Assert.AreEqual(0, galaxy.CurrentIndex, "у новій галактиці відкрита перша планета");
            Assert.IsTrue(state.CanEditPlanet(1, state.Layout.Planets[0].Id));
            Assert.IsFalse(state.CanEditPlanet(0, state.Layout.Planets[0].Id), "завершену галактику не редагуємо");

            // Нові слоти потребують нових копій — старі всі зайняті.
            Assert.IsFalse(state.TryPlaceInSlot(1, state.Layout.Planets[0].Id, 0, "pic-0-0", Today));
            state.CollectPicture("pic-0-0", Today);
            Assert.IsTrue(state.TryPlaceInSlot(1, state.Layout.Planets[0].Id, 0, "pic-0-0", Today), "друга копія — слот у другій галактиці");
        }

        // ── Карта рівнів ──

        [Test]
        public void LevelMap_OfANewPlayerOpensOnlyTheFirst()
        {
            var map = LevelMap.FromProgress(new ProgressData(), 0, 10);

            Assert.AreEqual(1, map.Current!.Number);
            Assert.AreEqual(0, map.TotalStars);

            foreach (var node in map.Nodes)
            {
                if (node.Kind == LevelNodeKind.Bonus || node.Number == 1)
                    continue;
                Assert.IsTrue(node.Locked, $"рівень {node.Number} має бути замкнений");
            }
        }

        [Test]
        public void LevelMap_ZeroStarRecordDoesNotOpenTheNextLevel()
        {
            var progress = new ProgressData();
            // «Заходив і програв» — не «пройшов».
            LevelProgress.Record(progress, 1, 0);

            var map = LevelMap.FromProgress(progress, 0, 10);

            Assert.AreEqual(1, map.Current!.Number);
        }

        [Test]
        public void LevelMap_FollowsClearedLevels()
        {
            var progress = new ProgressData();
            LevelProgress.Record(progress, 1, 3);
            LevelProgress.Record(progress, 2, 2);

            var map = LevelMap.FromProgress(progress, 3, 10);

            Assert.AreEqual(3, map.Current!.Number);
            Assert.AreEqual(5, map.TotalStars);
            Assert.AreEqual(0.3f, map.DailyFraction, 0.001f);
        }

        [Test]
        public void LevelMap_BonusUnlocksByTotalStars()
        {
            var progress = new ProgressData();
            for (var id = 1; id <= 9; id++)
                LevelProgress.Record(progress, id, 3);

            var map = LevelMap.FromProgress(progress, 0, 10);

            foreach (var node in map.Nodes)
            {
                if (node.Kind != LevelNodeKind.Bonus)
                    continue;
                Assert.AreEqual(map.TotalStars < node.StarsRequired, node.Locked,
                    $"бонус на {node.StarsRequired}★ при {map.TotalStars}★");
            }
        }

        // ── Рейтинги ──

        [Test]
        public void Rankings_YouCardShowsRealNumbers()
        {
            var state = Fresh();
            state.Progress.EndlessRecord = 3400;
            state.CollectPicture("whale", Today);
            state.CollectPicture("comet", Today);
            CompletePlanet(state, 0);

            var board = Leaderboard.WithRealPlayer(state);

            Assert.AreEqual(state.Nick, board.You.Nick);
            Assert.AreEqual(2 + state.Layout.Planets[0].Slots, board.You.RecordAll, "§10: рекорд колекції — різні картинки");
            Assert.AreEqual(1, board.You.PlanetsAll);
            Assert.AreEqual(0, board.You.GalaxiesAll);
            Assert.IsTrue(board.You.IsYou);
        }
    }
}
