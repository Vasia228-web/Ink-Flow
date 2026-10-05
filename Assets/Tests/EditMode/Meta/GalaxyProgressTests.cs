using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Розкладка галактики (§12) і похідний стан планет. Планети й слоти — з конфігу,
    /// галактик нескінченно, назви циклів не повторюються — усе це тримається тут.
    /// </summary>
    public sealed class GalaxyProgressTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        private static readonly GalaxyLayout Layout = GalaxyLayout.Default;

        private static void FillPlanet(GalaxyData data, int galaxy, int planetIndex, int count = -1)
        {
            var planet = Layout.Planets[planetIndex];
            var n = count < 0 ? planet.Slots : count;
            for (var i = 0; i < n; i++)
                GalaxyState.Set(data, galaxy, planet.Id, i, $"pic-{galaxy}-{planetIndex}-{i}", Now);
        }

        // ── Розкладка ──

        [Test]
        public void Layout_HasNinePlanets_OnePerType()
        {
            Assert.AreEqual(PlanetTypes.Count, Layout.Planets.Count,
                "Планет мусить бути стільки ж, скільки типів — по одній на тип.");

            var seen = new bool[PlanetTypes.Count];
            foreach (var planet in Layout.Planets)
            {
                var i = (int)planet.Type;
                Assert.IsFalse(seen[i], $"Тип {planet.Type} трапився двічі.");
                seen[i] = true;
            }
        }

        [Test]
        public void Layout_SlotsGrowFromFourToTwelve()
        {
            for (var i = 0; i < Layout.Planets.Count; i++)
                Assert.AreEqual(4 + i, Layout.Planets[i].Slots, $"{Layout.Planets[i].Name}: §12 — 4, 5, 6 … 12");
            Assert.AreEqual(72, Layout.SlotsPerGalaxy);
            Assert.IsTrue(Layout.Planets[Layout.Planets.Count - 1].IsFinale, "остання планета — фінальна");
        }

        [Test]
        public void Layout_NamesGalaxiesWithRomanNumerals_WithoutRepeats()
        {
            Assert.IsTrue(Layout.NameOf(0).StartsWith("ГАЛАКТИКА I ·", StringComparison.Ordinal), Layout.NameOf(0));
            Assert.IsTrue(Layout.NameOf(1).StartsWith("ГАЛАКТИКА II ·", StringComparison.Ordinal), Layout.NameOf(1));
            Assert.IsTrue(Layout.NameOf(3).StartsWith("ГАЛАКТИКА IV ·", StringComparison.Ordinal), Layout.NameOf(3));

            // Імен по колу, але номер завжди новий — двох однакових назв не буває.
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (var g = 0; g < 50; g++)
                Assert.IsTrue(names.Add(Layout.NameOf(g)), $"назва галактики {g} повторилась: {Layout.NameOf(g)}");

            Assert.AreEqual("IV", GalaxyLayout.Roman(4));
            Assert.AreEqual("XII", GalaxyLayout.Roman(12));
            Assert.AreEqual("MMXXVI", GalaxyLayout.Roman(2026));
            Assert.AreEqual("I", GalaxyLayout.Roman(0), "нуля в римських немає — перша");
        }

        [Test]
        public void Layout_FindsPlanetsById()
        {
            Assert.AreEqual(3, Layout.IndexOf("Earth"));
            Assert.AreEqual(3, Layout.IndexOf(PlanetType.Earth));
            Assert.AreEqual("Терра Прима", Layout.Find("Earth")!.Name);
            Assert.IsNull(Layout.Find("Mars"));
            Assert.AreEqual(-1, Layout.IndexOf("Mars"));
        }

        [Test]
        public void Layout_RejectsDuplicatesAndEmptiness()
        {
            Assert.Throws<ArgumentException>(() => new GalaxyLayout(
                new[] { new PlanetLayout(PlanetType.Ocean, "А", 4), new PlanetLayout(PlanetType.Ocean, "Б", 5) },
                new[] { "X" }), "два записи одного типу мали б один ідентифікатор у файлі");
            Assert.Throws<ArgumentException>(() => new GalaxyLayout(new PlanetLayout[0], new[] { "X" }));
            Assert.Throws<ArgumentException>(() => new GalaxyLayout(
                new[] { new PlanetLayout(PlanetType.Ocean, "А", 4) }, new string[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlanetLayout(PlanetType.Ocean, "А", 0));
        }

        // ── Мок майстерні ──

        [Test]
        public void Mock_HasExactlyOneCurrentPlanet_AndConsistentCounts()
        {
            var galaxy = GalaxyProgress.CreateMock();

            var current = 0;
            foreach (var planet in galaxy.Planets)
            {
                if (planet.State == PlanetState.Current)
                    current++;
                Assert.LessOrEqual(planet.FilledSlots, planet.TotalSlots, planet.Name);
                if (planet.State == PlanetState.Done)
                    Assert.AreEqual(planet.TotalSlots, planet.FilledSlots, $"{planet.Name} ожила, але слоти не всі");
                if (planet.State == PlanetState.Locked)
                    Assert.AreEqual(0, planet.FilledSlots, $"{planet.Name} замкнена, але має картинки");
            }

            Assert.AreEqual(1, current, "карусель відкривається саме на поточній");
            Assert.AreEqual(3, galaxy.DoneCount);
            Assert.AreEqual(3, galaxy.CurrentIndex);
        }

        // ── Зі збереження ──

        [Test]
        public void FromSave_NewGalaxyOpensExactlyOnePlanet()
        {
            var galaxy = GalaxyProgress.FromSave(new GalaxyData(), Layout);

            Assert.AreEqual(0, galaxy.Index);
            Assert.AreEqual(0, galaxy.DoneCount);
            Assert.AreEqual(0, galaxy.CurrentIndex, "перша планета — поточна");
            Assert.AreEqual(Layout.NameOf(0), galaxy.Name);
            Assert.AreEqual(Layout.NameOf(1), galaxy.NextName);

            var locked = 0;
            foreach (var planet in galaxy.Planets)
                if (planet.State == PlanetState.Locked)
                    locked++;
            Assert.AreEqual(galaxy.Planets.Count - 1, locked, "решта замкнена");
        }

        [Test]
        public void FromSave_CompletingAPlanetOpensTheNext()
        {
            var data = new GalaxyData();
            FillPlanet(data, 0, 0);

            var galaxy = GalaxyProgress.FromSave(data, Layout);

            Assert.AreEqual(PlanetState.Done, galaxy.Planets[0].State);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[1].State);
            Assert.AreEqual(PlanetState.Locked, galaxy.Planets[2].State);
            Assert.AreEqual(1f, galaxy.Planets[0].Fraction, 1e-5f);
        }

        [Test]
        public void FromSave_PlanetWithPicturesNeverLocksAgain()
        {
            // Гравець заповнив першу, поставив щось на другу, а потім забрав картинку з першої:
            // друга планета не має замкнутись разом із тим, що на ній стоїть.
            var data = new GalaxyData();
            FillPlanet(data, 0, 0);
            FillPlanet(data, 0, 1, count: 1);
            GalaxyState.Clear(data, 0, Layout.Planets[0].Id, 0);

            var galaxy = GalaxyProgress.FromSave(data, Layout);

            Assert.AreEqual(PlanetState.Current, galaxy.Planets[0].State);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[1].State, "картинки стоять — планета відкрита");
            Assert.AreEqual(0, galaxy.CurrentIndex, "карусель відкривається на першій відкритій");
            Assert.AreEqual(PlanetState.Locked, galaxy.Planets[2].State);
        }

        [Test]
        public void FromSave_EmptyOpenedPlanetStaysOpen_WhenAnEarlierPlanetLosesAPicture()
        {
            // Аквіла, Рудокам, Кріос ожили; Терра відкрита, але ще порожня. Гравець забирає кита з Аквіли,
            // щоб перенести його на Терру: Терра не має замкнутись у цю мить — її відкрив ожилий Кріос.
            var data = new GalaxyData();
            FillPlanet(data, 0, 0);
            FillPlanet(data, 0, 1);
            FillPlanet(data, 0, 2);
            GalaxyState.Clear(data, 0, Layout.Planets[0].Id, 0);

            var galaxy = GalaxyProgress.FromSave(data, Layout);

            Assert.AreEqual(PlanetState.Current, galaxy.Planets[0].State, "Аквіла без одного кита — знову в роботі");
            Assert.AreEqual(PlanetState.Done, galaxy.Planets[1].State);
            Assert.AreEqual(PlanetState.Done, galaxy.Planets[2].State);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[3].State, "відкрита порожня планета не замикається");
            Assert.AreEqual(PlanetState.Locked, galaxy.Planets[4].State);
            Assert.IsTrue(GalaxyState.IsPlanetOpen(data, 0, Layout, 3));
            Assert.IsFalse(GalaxyState.IsPlanetOpen(data, 0, Layout, 4));
        }

        [Test]
        public void FromSave_IgnoresSlotRecordsBeyondTheLayout()
        {
            // Автор зменшив слоти першої планети з 4 до 3 після того, як гравець заповнив усі 4.
            var data = new GalaxyData();
            FillPlanet(data, 0, 0);
            var shrunk = new GalaxyLayout(
                new[]
                {
                    new PlanetLayout(PlanetType.Ocean, "Аквіла", 3),
                    new PlanetLayout(PlanetType.Rocky, "Рудокам", 2)
                },
                new[] { "X" });

            var galaxy = GalaxyProgress.FromSave(data, shrunk);

            Assert.AreEqual(PlanetState.Done, galaxy.Planets[0].State);
            Assert.AreEqual(3, galaxy.Planets[0].FilledSlots, "показуємо стільки, скільки слотів є");
            Assert.AreEqual(3, GalaxyState.FilledCount(data, 0, Layout.Planets[0].Id, 3));
            Assert.AreEqual(4, GalaxyState.FilledCount(data, 0, Layout.Planets[0].Id), "сирі факти файлу — усі чотири записи");
            Assert.AreEqual(3, GalaxyState.TotalFilled(data, shrunk));
            Assert.AreEqual(0, GalaxyState.PlacedCopies(data, "pic-0-0-3", shrunk), "запис у слоті, якого немає, копію не тримає");
            Assert.AreEqual(1, GalaxyState.PlacedCopies(data, "pic-0-0-3"), "…але у файлі він лишається");
        }

        [Test]
        public void FromSave_ShowsTheCurrentGalaxy_WhenTheFirstIsComplete()
        {
            var data = new GalaxyData();
            for (var p = 0; p < Layout.Planets.Count; p++)
                FillPlanet(data, 0, p);
            FillPlanet(data, 1, 0, count: 2);

            var galaxy = GalaxyProgress.FromSave(data, Layout);

            Assert.AreEqual(1, galaxy.Index, "Галактика I завершена — показуємо другу");
            Assert.IsTrue(galaxy.Name.StartsWith("ГАЛАКТИКА II ·", StringComparison.Ordinal), galaxy.Name);
            Assert.AreEqual(2, galaxy.Planets[0].FilledSlots);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[0].State);
            Assert.AreEqual(0, galaxy.DoneCount);

            var first = GalaxyProgress.FromSave(data, Layout, 0);
            Assert.IsTrue(first.IsComplete);
            Assert.AreEqual(Layout.Planets.Count, first.DoneCount);
        }

        [Test]
        public void OtherPlayerGalaxy_HasNoPartialProgress()
        {
            // Чужу галактику показуємо без напівпройдених планет: скільки слотів
            // лишилось іншому гравцеві — не наша справа.
            var galaxy = GalaxyProgress.CreateMockForOther(5);

            Assert.AreEqual(5, galaxy.DoneCount);
            Assert.AreEqual(-1, galaxy.CurrentIndex, "У чужій галактиці «поточної» планети немає.");

            foreach (var planet in galaxy.Planets)
                Assert.AreNotEqual(PlanetState.Current, planet.State);
        }

        [Test]
        public void Fraction_IsSafeForZeroTotal()
        {
            var planet = new PlanetProgress(PlanetType.Ocean, "Порожня", 0);

            // Кільце прогресу ділить на TotalSlots — нуль там дав би NaN і
            // зіпсовану дугу замість порожньої.
            Assert.AreEqual(0f, planet.Fraction);
        }
    }
}
