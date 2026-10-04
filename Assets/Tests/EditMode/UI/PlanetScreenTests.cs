using System.Collections.Generic;
using InkFlow.Editor;
using InkFlow.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Планета-вітрина (§12) на живому рендері: слоти показують те, що в збереженні, а планета з
    /// усіма дванадцятьма картинками вкладається в бюджет викликів малювання iPhone SE (архідок §12).
    /// Потрібен зібраний префаб — Ink Flow → Setup → Build Planet Screen.
    /// </summary>
    public sealed class PlanetScreenTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<PlanetScreen>.Available("PlanetScreen"))
                Assert.Ignore("Немає префаба екрана планети — спершу Ink Flow → Setup → Build Planet Screen.");
        }

        private static List<SlotMarker> Bound(PlanetScreen screen)
        {
            var list = new List<SlotMarker>();
            foreach (var marker in screen.PreviewMarkers)
                if (marker != null && marker.Slot != null)
                    list.Add(marker);
            return list;
        }

        [Test]
        public void EmptyPlanet_ShowsOneSocketPerSlot_AndNoPictures()
        {
            using var rig = MetaScreenRig<PlanetScreen>.Create(ScreenRigBase.Devices[0], "PlanetScreen");
            var player = rig.NewPlayer();
            rig.Enter(player, new PlanetArgs(0, 0));

            var bound = Bound(rig.Screen);
            Assert.AreEqual(player.Layout.Planets[0].Slots, bound.Count, "маркерів стільки, скільько слотів у планети");
            foreach (var marker in bound)
                Assert.IsFalse(marker.IsFilled, "нова планета — порожні слоти");
        }

        [Test]
        public void HalfPlanet_ShowsSavedPicturesInTheirSlots()
        {
            using var rig = MetaScreenRig<PlanetScreen>.Create(ScreenRigBase.Devices[2], "PlanetScreen");
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0, count: 2);
            rig.Enter(player, new PlanetArgs(0, 0));

            var bound = Bound(rig.Screen);
            var filled = 0;
            foreach (var marker in bound)
                if (marker.IsFilled)
                    filled++;
            Assert.AreEqual(2, filled);
            Assert.IsTrue(bound[0].IsFilled && bound[1].IsFilled, "картинки стоять у слотах 0 і 1 — як у збереженні");

            // Пікселі всіх зайнятих слотів — з ОДНОЇ текстури (атлас): інакше кожна картинка — окремий виклик.
            var textures = new HashSet<Texture>();
            foreach (var raw in rig.Screen.GetComponentsInChildren<RawImage>(false))
                if (raw.texture != null)
                    textures.Add(raw.texture);
            Assert.AreEqual(1, textures.Count, "усі картинки слотів малюються з одного атласу");
        }

        [Test]
        public void FullFinalePlanet_StaysWithinTheDrawCallBudget_OnEveryDevice()
        {
            foreach (var device in ScreenRigBase.Devices)
            {
                using var rig = MetaScreenRig<PlanetScreen>.Create(device, "PlanetScreen");
                var player = rig.NewPlayer();
                for (var p = 0; p < player.Layout.Planets.Count; p++)
                    rig.FillPlanet(player, p, pictureOffset: p * 11);
                var last = player.Layout.Planets.Count - 1;
                // Галактика I щойно завершена — поточною стала II, тож планету просимо явно з першої.
                rig.Enter(player, new PlanetArgs(0, last));

                var bound = Bound(rig.Screen);
                Assert.AreEqual(player.Layout.Planets[last].Slots, bound.Count, device.Name);
                foreach (var marker in bound)
                    Assert.IsTrue(marker.IsFilled, $"{device.Name}: усі слоти фінальної планети зайняті");

                var distinct = rig.EstimateBatches(out var runs, out var graphics);
                Assert.LessOrEqual(runs, MetaScreenshots.DrawCallBudget,
                    $"{device.Name}: {runs} змін пари матеріал+текстура (різних {distinct}, графік {graphics}) — більше за бюджет {MetaScreenshots.DrawCallBudget}");
            }
        }
    }
}
