using System;
using System.Collections.Generic;
using System.Linq;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Ріст галактик (2026-10-23): формула комірок і планет, ідентифікатори планет довгих галактик, міграція v11 → v12
    /// без втрат, нагорода за галактику й обіцянка «наступна — Епічна+», що не згорає з програшем.
    /// </summary>
    public sealed class GalaxyGrowthTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FirstGalaxy_IsSixPlanetsFromThreeToEightCells()
        {
            Assert.AreEqual("3,4,5,6,7,8", string.Join(",", GalaxyGrowth.Default.SlotsFor(0)));
            Assert.AreEqual(33, GalaxyLayout.Growing.For(0).SlotsPerGalaxy);
            Assert.AreEqual(39, GalaxyLayout.Growing.For(1).SlotsPerGalaxy, "кожна наступна — на 6 комірок більша");
        }

        [Test]
        public void EveryGalaxy_SumsToTheFormula_GrowsUp_AndRespectsTheCap()
        {
            var growth = GalaxyGrowth.Default;
            var previousPlanets = 0;
            for (var g = 0; g < 80; g++)
            {
                var slots = growth.SlotsFor(g);
                Assert.AreEqual(growth.CellsFor(g), slots.Sum(), $"галактика {g}");
                Assert.GreaterOrEqual(slots.Length, growth.MinPlanets);
                Assert.GreaterOrEqual(slots.Length, previousPlanets, "планет не меншає");
                for (var i = 0; i < slots.Length; i++)
                {
                    Assert.LessOrEqual(slots[i], growth.MaxCellsPerPlanet, $"галактика {g}, планета {i}: стеля");
                    Assert.GreaterOrEqual(slots[i], 1);
                    if (i > 0)
                        Assert.GreaterOrEqual(slots[i], slots[i - 1], "драбинка: від меншої до більшої");
                }
                previousPlanets = slots.Length;
            }
            Assert.AreEqual(6, growth.SlotsFor(18).Length, "поки планети не вперлись у 24 — росте кількість комірок");
            Assert.Greater(growth.SlotsFor(30).Length, 6, "після стелі — росте кількість планет");
        }

        [Test]
        public void LongGalaxy_RepeatsTemplates_WithUniqueIds_AndTheFinaleLast()
        {
            var layout = new GalaxyLayout(GalaxyLayout.Default.Planets, GalaxyLayout.Default.GalaxyNames,
                new GalaxyGrowth(baseCells: 40, cellsPerGalaxy: 0, maxCellsPerPlanet: 2, minPlanets: 6));
            var galaxy = layout.For(0);
            Assert.AreEqual(20, galaxy.Planets.Count);
            Assert.AreEqual(galaxy.Planets.Count, galaxy.Planets.Select(p => p.Id).Distinct().Count(), "ідентифікатори в одній галактиці різні");
            Assert.IsTrue(galaxy.Planets[^1].IsFinale);
            Assert.AreEqual(PlanetType.Pearl, galaxy.Planets[^1].Type);
            Assert.AreEqual("Ocean", galaxy.Planets[0].Id);
            Assert.AreEqual("Ocean-2", galaxy.Planets[8].Id, "повтор шаблону — з номером");
            Assert.AreEqual("Аквіла II", galaxy.Planets[8].Name);
            Assert.AreSame(galaxy, layout.For(0), "розкладка галактики кешується");
        }

        [Test]
        public void Migration_V11_KeepsPlacedPictures_MovesOnlyThoseWhoseCellIsGone()
        {
            var save = new SaveFile { Version = 11 };
            void Place(string planet, int slot, string picture)
            {
                save.Collection.Pictures.Add(new CollectedPicture { PictureId = picture, Count = 1, FirstUtc = Today.ToString("o") });
                save.Galaxy.Slots.Add(new PlanetSlotRecord { Galaxy = 0, PlanetId = planet, Slot = slot, PictureId = picture, FilledUtc = "x" });
            }
            // Стара перша галактика: Аквіла 4, …, Терра Прима 7, …, Вихор 9 — повна Аквіла, сьома комірка Терри, Вихор.
            for (var s = 0; s < 4; s++)
                Place("Ocean", s, $"ocean{s}");
            Place("Earth", 6, "earth6");
            Place("Gas", 0, "gas0");

            var migrated = SaveMigrations.Migrate(save, new MigrationContext(GalaxyLayout.Growing));

            Assert.AreEqual(12, migrated.Version);
            Assert.AreEqual(6, migrated.Galaxy.Slots.Count, "жодна картинка не загубилась");
            for (var s = 0; s < 3; s++)
                Assert.AreEqual($"ocean{s}", GalaxyState.PictureAt(migrated.Galaxy, 0, "Ocean", s), "комірка є й у новій розкладці — на місці");
            // В Аквіли тепер 3 комірки: четверта, Терра 6 і Вихор переїжджають у перші вільні — Рудокам 0, 1, 2.
            Assert.AreEqual("ocean3", GalaxyState.PictureAt(migrated.Galaxy, 0, "Rocky", 0));
            Assert.AreEqual("earth6", GalaxyState.PictureAt(migrated.Galaxy, 0, "Rocky", 1));
            Assert.AreEqual("gas0", GalaxyState.PictureAt(migrated.Galaxy, 0, "Rocky", 2));
            foreach (var record in migrated.Galaxy.Slots)
                Assert.IsTrue(GalaxyState.IsAddressed(record, GalaxyLayout.Growing), $"{record.PlanetId}/{record.Slot} видно в новій розкладці");
            Assert.AreEqual(33 - 6, 33 - GalaxyState.TotalFilled(migrated.Galaxy, GalaxyLayout.Growing));
        }

        [Test]
        public void CompletingAGalaxy_PaysOil_AndPromisesARarePicture_UntilOneIsFinished()
        {
            var library = TestLibrary.Real;
            var state = PlayerState.NewPlayer(EconomyData.Default, null, library, null, GalaxyLayout.Growing);
            var galaxy = GalaxyLayout.Growing.For(0);
            var k = 0;
            var rewarded = -1;
            state.GalaxyRewarded += g => rewarded = g;
            foreach (var planet in galaxy.Planets)
                for (var s = 0; s < planet.Slots; s++)
                {
                    var id = library[k++ % library.Count].Id;
                    state.CollectPicture(id, Today);
                    Assert.IsTrue(state.TryPlaceInSlot(0, planet.Id, s, id, Today));
                }
            var oilBefore = state.Wallet.OilDrops;

            Assert.AreEqual(1, state.CurrentGalaxy);
            Assert.AreEqual(0, rewarded, "нагорода за першу галактику");
            Assert.AreEqual(GalaxyReward.Default.Oil, oilBefore, "300 нафти");
            Assert.AreEqual(Rarity.Epic, state.GuaranteedRarity);

            var common = library.Pictures.First(p => p.Rarity == Rarity.Common).Id;
            var epic = library.Pictures.First(p => p.Rarity >= Rarity.Epic).Id;
            state.CollectPicture(common, Today);
            Assert.AreEqual(Rarity.Epic, state.GuaranteedRarity, "звичайна обіцянку не знімає");
            state.CollectPicture(epic, Today);
            Assert.IsNull(state.GuaranteedRarity, "домальована Епічна+ — обіцянку виконано");
            Assert.AreEqual(oilBefore, state.Wallet.OilDrops, "нагорода — один раз");
        }

        [Test]
        public void GuaranteedRun_StartsWithAPictureOfThatRarityOrRarer()
        {
            var library = TestLibrary.Real;
            for (var seed = 1u; seed <= 40u; seed++)
            {
                var session = new RunSession(BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(seed), library,
                    null, Rarity.Epic);
                Assert.GreaterOrEqual(session.Picture.Picture.Rarity, Rarity.Epic, $"сід {seed}");
                Assert.IsNull(session.NextPictureAtLeast, "обіцянка — на одну картинку");
            }
        }
    }
}
