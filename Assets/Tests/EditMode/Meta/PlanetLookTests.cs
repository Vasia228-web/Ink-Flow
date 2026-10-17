using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>Вибір розкладки планети за порогом (вимога промту U1Glaze): до порогу включно — «Сфера», більше — «Горизонт».</summary>
    public sealed class PlanetLookTests
    {
        [Test]
        public void UpToTheThreshold_Sphere_Above_Horizon()
        {
            Assert.AreEqual(PlanetLook.Sphere, PlanetLooks.For(4, 8));
            Assert.AreEqual(PlanetLook.Sphere, PlanetLooks.For(8, 8), "поріг 8 — ще «Сфера» (SPEC)");
            Assert.AreEqual(PlanetLook.Horizon, PlanetLooks.For(9, 8));
            Assert.AreEqual(PlanetLook.Horizon, PlanetLooks.For(40, 8));
            // Поріг — повзунок: інший поріг — інша межа.
            Assert.AreEqual(PlanetLook.Sphere, PlanetLooks.For(10, 12));
            Assert.AreEqual(PlanetLook.Horizon, PlanetLooks.For(5, 4));
        }

        [Test]
        public void DefaultGalaxy_FiveSpheres_FourHorizons()
        {
            // Розкладка за замовчуванням: 4…8 комірок — «Сфера», 9…12 — «Горизонт».
            var spheres = 0;
            foreach (var planet in GalaxyLayout.Default.Planets)
                if (PlanetLooks.For(planet.Slots, 8) == PlanetLook.Sphere)
                    spheres++;
            Assert.AreEqual(5, spheres);
            Assert.AreEqual(4, GalaxyLayout.Default.Planets.Count - spheres);
        }

        [Test]
        public void Seed_IsStable_AndDiffersPerType()
        {
            Assert.AreEqual(1, PlanetLooks.SeedOf(PlanetType.Ocean));
            Assert.AreEqual(3, PlanetLooks.SeedOf(PlanetType.Ice), "Кріос — seed 3, як на макетах S04");
            Assert.AreNotEqual(PlanetLooks.SeedOf(PlanetType.Rocky), PlanetLooks.SeedOf(PlanetType.Gas));
        }
    }
}
