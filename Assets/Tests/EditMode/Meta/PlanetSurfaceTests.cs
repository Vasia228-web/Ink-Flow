using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Поверхня планети зі слотами (§12). Головне: слоти стоять детерміновано, не збиваються
    /// в купу й не ховаються за полюси — інакше картинки «переїжджали» б між запусками, а на
    /// планеті з дванадцятьма слотами два стояли б один на одному.
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
        public void Slots_StayInsideTheLatitudeBand_AndOnTheGlobe()
        {
            for (var count = 1; count <= 16; count++)
                for (var i = 0; i < count; i++)
                {
                    SlotLayout.Position(i, count, out var lon, out var lat);
                    Assert.GreaterOrEqual(lon, 0f, $"{i}/{count}: довгота");
                    Assert.Less(lon, 360f, $"{i}/{count}: довгота");
                    Assert.LessOrEqual(Math.Abs(lat), SlotLayout.MaxLatitude + 1e-3f,
                        $"{i}/{count}: слот біля полюса не видно й не тапнеш");
                }
        }

        [Test]
        public void Slots_AreDeterministic()
        {
            SlotLayout.Position(5, 12, out var lon1, out var lat1);
            SlotLayout.Position(5, 12, out var lon2, out var lat2);

            Assert.AreEqual(lon1, lon2);
            Assert.AreEqual(lat1, lat2);
        }

        [Test]
        public void Slots_KeepTheirDistance_EvenOnThePearl()
        {
            // Дванадцять слотів фінальної планети: найближча пара — не ближче, ніж
            // розмір самого слота на кулі, інакше дві картинки накладуться.
            foreach (var planet in Layout.Planets)
            {
                var surface = PlanetSurface.For(planet);
                var minDistance = float.MaxValue;
                for (var a = 0; a < surface.Slots.Count; a++)
                    for (var b = a + 1; b < surface.Slots.Count; b++)
                    {
                        var d = SlotLayout.AngularDistance(
                            surface.Slots[a].Longitude, surface.Slots[a].Latitude,
                            surface.Slots[b].Longitude, surface.Slots[b].Latitude);
                        if (d < minDistance)
                            minDistance = d;
                    }

                if (surface.Slots.Count > 1)
                    Assert.GreaterOrEqual(minDistance, 24f,
                        $"{planet.Name} ({planet.Slots} слотів): найближча пара на {minDistance:0.#}°");
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
