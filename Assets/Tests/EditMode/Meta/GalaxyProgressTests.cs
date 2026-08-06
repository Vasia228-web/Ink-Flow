using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Сторожі мокової галактики. Дані взяті з макета один раз і легко тихо
    /// роз'їхались би з ним при наступній правці — а помітно це стало б лише
    /// очима на екрані.
    /// </summary>
    public sealed class GalaxyProgressTests
    {
        [Test]
        public void Mock_HasNinePlanets_OnePerType()
        {
            var galaxy = GalaxyProgress.CreateMock();

            Assert.AreEqual(PlanetTypes.Count, galaxy.Planets.Count,
                "Планет мусить бути стільки ж, скільки типів — по одній на тип.");

            var seen = new bool[PlanetTypes.Count];
            foreach (var planet in galaxy.Planets)
            {
                var i = (int)planet.Type;
                Assert.IsFalse(seen[i], $"Тип {planet.Type} трапився двічі.");
                seen[i] = true;
            }
        }

        [Test]
        public void Mock_MatchesMockupState()
        {
            var galaxy = GalaxyProgress.CreateMock();

            Assert.AreEqual(3, galaxy.DoneCount, "У макеті підпис «3 / 9 планет».");
            Assert.AreEqual(3, galaxy.CurrentIndex, "У макеті CURRENT_IDX = 3.");

            var terra = galaxy.Planets[3];
            Assert.AreEqual("Терра Прима", terra.Name);
            Assert.AreEqual(8, terra.TotalZones);
            Assert.AreEqual(5, terra.PaintedZones, "У макеті підпис «5 / 8 зон».");
            Assert.AreEqual(PlanetState.Current, terra.State);
        }

        [Test]
        public void Mock_HasExactlyOneCurrentPlanet()
        {
            var galaxy = GalaxyProgress.CreateMock();

            var current = 0;
            foreach (var planet in galaxy.Planets)
                if (planet.State == PlanetState.Current)
                    current++;

            // Двох «поточних» бути не може: карусель відкривається саме на ній.
            Assert.AreEqual(1, current);
        }

        [Test]
        public void DoneAndLockedPlanets_HaveConsistentZoneCounts()
        {
            var galaxy = GalaxyProgress.CreateMock();

            foreach (var planet in galaxy.Planets)
            {
                Assert.LessOrEqual(planet.PaintedZones, planet.TotalZones,
                    $"{planet.Name}: пофарбовано більше зон, ніж є.");

                if (planet.State == PlanetState.Done)
                    Assert.AreEqual(planet.TotalZones, planet.PaintedZones,
                        $"{planet.Name} завершена, але зони не всі.");

                if (planet.State == PlanetState.Locked)
                    Assert.AreEqual(0, planet.PaintedZones,
                        $"{planet.Name} замкнена, але має пофарбовані зони.");
            }
        }

        [Test]
        public void OtherPlayerGalaxy_HasNoPartialProgress()
        {
            // Чужу галактику показуємо без напівпройдених планет: скільки зон
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

            // Кільце прогресу ділить на TotalZones — нуль там дав би NaN і
            // зіпсовану дугу замість порожньої.
            Assert.AreEqual(0f, planet.Fraction);
        }
    }
}
