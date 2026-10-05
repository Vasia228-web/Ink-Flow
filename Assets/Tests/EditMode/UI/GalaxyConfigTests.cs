using InkFlow.Gameplay;
using InkFlow.Meta;
using NUnit.Framework;
using UnityEngine;

namespace InkFlow.UI.Tests
{
    /// <summary>Розкладка галактики з асета: порожній конфіг дає ту саму гру, що й код.</summary>
    public sealed class GalaxyConfigTests
    {
        [Test]
        public void FreshConfig_MatchesTheDefaultLayout()
        {
            var config = ScriptableObject.CreateInstance<GalaxyConfig>();
            try
            {
                var layout = config.ToLayout();
                var expected = GalaxyLayout.Default;

                Assert.AreEqual(expected.Planets.Count, layout.Planets.Count);
                for (var i = 0; i < expected.Planets.Count; i++)
                {
                    Assert.AreEqual(expected.Planets[i].Id, layout.Planets[i].Id, $"планета {i}");
                    Assert.AreEqual(expected.Planets[i].Name, layout.Planets[i].Name);
                    Assert.AreEqual(expected.Planets[i].Slots, layout.Planets[i].Slots);
                    Assert.AreEqual(expected.Planets[i].HasMoons, layout.Planets[i].HasMoons);
                    Assert.AreEqual(expected.Planets[i].HasRing, layout.Planets[i].HasRing);
                    Assert.AreEqual(expected.Planets[i].IsFinale, layout.Planets[i].IsFinale);
                }
                Assert.AreEqual(expected.SlotsPerGalaxy, layout.SlotsPerGalaxy);
                Assert.AreEqual(expected.NameOf(0), layout.NameOf(0));
                Assert.AreEqual(expected.NameOf(7), layout.NameOf(7), "імена циклів по колу");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
