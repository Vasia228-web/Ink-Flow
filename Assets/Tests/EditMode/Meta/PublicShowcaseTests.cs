using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// §16: у хмару йде лише публічна вітрина — нік, аватар, лічильники, слоти показуваної галактики й
    /// вітринна картинка; прихований профіль — лише прапорець і лічильники. Чужа галактика з вітрини
    /// рахується тим самим правилом, що й своя, і в ній немає редагування.
    /// </summary>
    public sealed class PublicShowcaseTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        private static void CompletePlanet(PlayerState state, int index)
        {
            var planet = state.Layout.Planets[index];
            for (var i = 0; i < planet.Slots; i++)
            {
                var id = $"pic-{index}-{i}";
                state.CollectPicture(id, Today);
                Assert.IsTrue(state.TryPlaceInSlot(state.CurrentGalaxy, planet.Id, i, id, Today));
            }
        }

        [Test]
        public void BuildShowcase_CarriesProfileCountsAndSlots()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.SetAvatar(2);
            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, state.Layout.Planets[0].Id, 1, "whale", Today));
            CompletePlanet(state, 0);

            var showcase = state.BuildShowcase("p-7", Today);

            Assert.AreEqual("p-7", showcase.PlayerId);
            Assert.AreEqual(state.Nick, showcase.Nick);
            Assert.AreEqual(2, showcase.AvatarId);
            Assert.IsFalse(showcase.Incognito);
            Assert.AreEqual(1, showcase.PlanetsDone);
            Assert.AreEqual(0, showcase.GalaxiesDone);
            Assert.AreEqual(0, showcase.Galaxy);
            Assert.AreEqual(state.Layout.Planets[0].Slots, showcase.Slots.Count, "усі слоти першої планети");
            Assert.AreEqual(state.ShowcasePictureId, showcase.ShowcasePictureId);
            Assert.IsTrue(showcase.UpdatedUtc.StartsWith("2026-10-07"), showcase.UpdatedUtc);
        }

        [Test]
        public void BuildShowcase_HiddenProfile_SendsNoNickNoPicturesNoSlots()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            CompletePlanet(state, 0);
            state.SetProfileHidden(true);

            var showcase = state.BuildShowcase("p-7", Today);

            Assert.IsTrue(showcase.Incognito);
            Assert.AreEqual(string.Empty, showcase.Nick, "нік прихованого профілю в хмару не йде");
            Assert.IsNull(showcase.ShowcasePictureId);
            Assert.AreEqual(0, showcase.Slots.Count);
            Assert.AreEqual(1, showcase.PlanetsDone, "лічильники для таблиці лишаються");
        }

        [Test]
        public void ShowcaseGalaxy_IsTheCurrentOne_UntilItIsEmptyAfterAFinishedCycle()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            Assert.AreEqual(0, state.ShowcaseGalaxy, "новий гравець — перша галактика, хоч і порожня");
            for (var i = 0; i < state.Layout.Planets.Count; i++)
                CompletePlanet(state, i);
            Assert.AreEqual(1, state.CurrentGalaxy);
            Assert.AreEqual(0, state.ShowcaseGalaxy, "нова галактика порожня — показуємо завершену");

            state.CollectPicture("comet", Today);
            Assert.IsTrue(state.TryPlaceInSlot(1, state.Layout.Planets[0].Id, 0, "comet", Today));
            Assert.AreEqual(1, state.ShowcaseGalaxy, "у новій уже є картинка — показуємо її");
            Assert.AreEqual(1, state.BuildShowcase("p", Today).Slots.Count);
        }

        [Test]
        public void FromShowcase_UsesTheSameRuleAsOwnGalaxy()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            CompletePlanet(state, 0);
            CompletePlanet(state, 1);
            state.CollectPicture("owl", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, state.Layout.Planets[2].Id, 0, "owl", Today));

            var galaxy = GalaxyProgress.FromShowcase(state.BuildShowcase("p", Today), state.Layout);

            Assert.AreEqual(PlanetState.Done, galaxy.Planets[0].State);
            Assert.AreEqual(PlanetState.Done, galaxy.Planets[1].State);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[2].State);
            Assert.AreEqual(1, galaxy.Planets[2].FilledSlots);
            Assert.AreEqual(PlanetState.Locked, galaxy.Planets[3].State);
            Assert.AreEqual(2, galaxy.DoneCount);
            Assert.AreEqual(state.Layout.NameOf(0), galaxy.Name);

            var hidden = GalaxyProgress.FromShowcase(PublicShowcase.Hidden("p", 5, 0, ""), state.Layout);
            Assert.AreEqual(0, hidden.DoneCount, "прихований профіль — слотів немає");
        }

        [Test]
        public void MockShowcase_HasPlanetsDoneInOrder()
        {
            var layout = GalaxyLayout.Default;
            var ids = new System.Collections.Generic.List<string>();
            var slots = new System.Collections.Generic.List<int>();
            foreach (var planet in layout.Planets) { ids.Add(planet.Id); slots.Add(planet.Slots); }

            var showcase = MockRankings.ShowcaseFor(MockRankings.PlayerIdOf(1), ids, slots);
            Assert.AreEqual("Гелій", showcase.Nick);
            Assert.AreEqual(4, showcase.PlanetsDone);
            var galaxy = GalaxyProgress.FromShowcase(showcase, layout);
            Assert.AreEqual(4, galaxy.DoneCount);
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[4].State, "п'ята відкрита, бо попередня ожила");
        }

        [Test]
        public void GalaxyChanged_FiresOnSlotChanges()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var fired = 0;
            state.GalaxyChanged += () => fired++;
            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, state.Layout.Planets[0].Id, 0, "whale", Today));
            Assert.AreEqual(1, fired);
            Assert.IsTrue(state.ClearSlot(0, state.Layout.Planets[0].Id, 0, Today));
            Assert.AreEqual(2, fired);
            Assert.IsFalse(state.TryPlaceInSlot(0, state.Layout.Planets[0].Id, 0, "no-such", Today), "незібрану не поставиш");
            Assert.AreEqual(2, fired, "відмова — без події");
        }
    }
}
