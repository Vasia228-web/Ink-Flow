using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Поверхня планети зі слотами (§12): скільки слотів, що в них стоїть, коли планета ожила. Де слоти стоять на
    /// екрані, з U1Glaze кроку 4 рахує розкладка («Сфера» — <see cref="GlobeSlots"/>, «Горизонт» —
    /// <see cref="HorizonLayout"/>, свої тести), а не поверхня: у файлі — лише номер слота.
    /// </summary>
    public sealed class PlanetSurfaceTests
    {
        private static readonly GalaxyLayout Layout = GalaxyLayout.Default;

        [Test]
        public void Surface_HasAsManySlotsAsTheLayoutSays()
        {
            foreach (var planet in Layout.Planets)
            {
                var surface = PlanetSurface.For(planet);
                Assert.AreEqual(planet.Slots, surface.Slots.Count, planet.Name);
                Assert.AreEqual(planet.Type, surface.Type);
                Assert.AreEqual(0, surface.FilledCount, "розкладка приходить порожньою");
                Assert.IsFalse(surface.IsComplete);
                for (var i = 0; i < surface.Slots.Count; i++)
                    Assert.AreEqual(i, surface.Slots[i].Index, "номер слота — його місце у списку");
            }
        }

        [Test]
        public void Surface_IsCompleteWhenEverySlotIsFilled()
        {
            var surface = PlanetSurface.For(Layout, PlanetType.Ocean);

            for (var i = 0; i < surface.Slots.Count; i++)
            {
                Assert.IsFalse(surface.IsComplete);
                surface.Slots[i].PictureId = $"pic{i}";
            }

            Assert.IsTrue(surface.IsComplete);
            Assert.AreEqual(surface.Slots.Count, surface.FilledCount);
        }

        [Test]
        public void Find_ReturnsNullOutsideRange()
        {
            var surface = PlanetSurface.For(Layout, PlanetType.Ocean);

            Assert.IsNotNull(surface.Find(0));
            Assert.IsNotNull(surface.Find(surface.Slots.Count - 1));
            Assert.IsNull(surface.Find(surface.Slots.Count));
            Assert.IsNull(surface.Find(-1));
        }

        [Test]
        public void Surface_ForAPlanetMissingFromTheLayout_Throws()
        {
            var small = new GalaxyLayout(
                new[] { new PlanetLayout(PlanetType.Ocean, "Аквіла", 4) }, new[] { "ТЕСТ" });

            Assert.Throws<ArgumentException>(() => PlanetSurface.For(small, PlanetType.Pearl));
        }
    }
}
