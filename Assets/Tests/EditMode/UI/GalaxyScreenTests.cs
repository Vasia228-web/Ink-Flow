using InkFlow.Editor;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Огляд галактики (§12) на живому рендері: режим перегляду чужої галактики без редагування,
    /// а завершені цикли своєї — досяжні стрілками, а не зниклі.
    /// Потрібен зібраний префаб — Ink Flow → Setup → Build Galaxy Screen.
    /// </summary>
    public sealed class GalaxyScreenTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<GalaxyScreen>.Available("GalaxyScreen"))
                Assert.Ignore("Немає префаба екрана галактики — спершу Ink Flow → Setup → Build Galaxy Screen.");
        }

        [Test]
        public void ReadOnlyGalaxy_HasNoEditingControls_AndNeverOpensAPlanet()
        {
            using var rig = MetaScreenRig<GalaxyScreen>.Create(ScreenRigBase.Devices[0], "GalaxyScreen");
            var player = rig.NewPlayer();
            var opened = 0;
            rig.Screen.OpenRequested += (_, _) => opened++;

            rig.Enter(player, new GalaxyArgs(new PlayerId("someone-else"), readOnly: true));

            var button = rig.Screen.PreviewOpenButton;
            Assert.IsNotNull(button);
            Assert.IsFalse(button!.gameObject.activeSelf, "у чужій галактиці кнопки «Відкрити» немає");
            Assert.IsFalse(rig.Screen.PreviewPaginationShown, "і пагінації теж");
            Assert.IsFalse(rig.Screen.PreviewArrowsShown, "і стрілок циклів");
            Assert.IsFalse(rig.Screen.PreviewCarousel!.PreviewShown, "прев'ю наступної галактики — лише у своїй");

            button.onClick.Invoke();
            Assert.AreEqual(0, opened, "навіть натиснута кнопка нікуди не веде");
        }

        [Test]
        public void VisitorGalaxy_ComesFromTheShowcase_WithAVisitorCard()
        {
            using var rig = MetaScreenRig<GalaxyScreen>.Create(ScreenRigBase.Devices[0], "GalaxyScreen");
            var player = rig.NewPlayer();
            var opened = 0;
            rig.Screen.OpenRequested += (_, _) => opened++;

            // Гість: дві планети ожили, у третій стоїть одна картинка, вітрина — відома бібліотеці картинка.
            var layout = player.Layout;
            var slots = new System.Collections.Generic.List<InkFlow.Core.ShowcaseSlot>();
            for (var p = 0; p < 2; p++)
                for (var s = 0; s < layout.Planets[p].Slots; s++)
                    slots.Add(new InkFlow.Core.ShowcaseSlot(layout.Planets[p].Id, s, rig.Library[s % rig.Library.Count].Id));
            slots.Add(new InkFlow.Core.ShowcaseSlot(layout.Planets[2].Id, 0, rig.Library[3].Id));
            var showcase = new InkFlow.Core.PublicShowcase("guest-1", "Гелій", 1, false, rig.Library[5].Id, 2, 0, 0, slots, "");

            rig.Enter(player, GalaxyArgs.ForVisitor(showcase));

            var galaxy = rig.Screen.PreviewGalaxy!;
            Assert.AreEqual(2, galaxy.DoneCount, "планети з вітрини, не з мого файлу");
            Assert.AreEqual(InkFlow.Core.PlanetState.Current, galaxy.Planets[2].State);
            Assert.AreEqual(1, galaxy.Planets[2].FilledSlots);
            Assert.IsTrue(rig.Screen.PreviewVisitorShown, "картка гостя");
            Assert.AreEqual("Гелій", rig.Screen.PreviewVisitorNick);
            Assert.AreEqual(rig.Library[5].Id, rig.Screen.PreviewVisitorPictureId, "вітринна картинка гостя");
            Assert.IsTrue(rig.Screen.PreviewVisitorHint.StartsWith("Вітрина ·"), rig.Screen.PreviewVisitorHint);
            Assert.IsTrue(rig.Screen.PreviewGalaxyTitle.StartsWith("ГЕЛІЙ ·"), rig.Screen.PreviewGalaxyTitle + " — шапка каже, чия це галактика");
            Assert.AreEqual(1, rig.Screen.PreviewFocus, "гість бачить останню ожилу планету, а не порожню наступну");
            Assert.Greater(rig.Screen.PreviewVisitorHintVisibleCharacters, 8, "підпис гостя справді намальований, а не схований тісним прямокутником");
            Assert.IsFalse(rig.Screen.PreviewOpenButton!.gameObject.activeSelf, "редагування немає");
            Assert.IsFalse(rig.Screen.PreviewPaginationShown);
            rig.Screen.PreviewOpenButton.onClick.Invoke();
            Assert.AreEqual(0, opened, "у чужій галактиці планета не відкривається");

            // Прихований профіль: нік не показуємо, картинки немає, планети не видно.
            rig.Enter(player, GalaxyArgs.ForVisitor(InkFlow.Core.PublicShowcase.Hidden("guest-2", 5, 1, "")));
            Assert.AreEqual("Гравець-інкогніто", rig.Screen.PreviewVisitorNick);
            Assert.AreEqual("ГРАВЕЦЬ-ІНКОГНІТО", rig.Screen.PreviewGalaxyTitle);
            Assert.IsNull(rig.Screen.PreviewVisitorPictureId);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.DoneCount);

            // Своя галактика після гостя — без картки.
            rig.Enter(player, GalaxyArgs.Own);
            Assert.IsFalse(rig.Screen.PreviewVisitorShown);
        }

        [Test]
        public void OwnGalaxy_OpensThePlanetInFocus()
        {
            using var rig = MetaScreenRig<GalaxyScreen>.Create(ScreenRigBase.Devices[1], "GalaxyScreen");
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0);
            (int galaxy, int planet)? opened = null;
            rig.Screen.OpenRequested += (g, p) => opened = (g, p);

            rig.Enter(player, GalaxyArgs.Own);

            Assert.IsTrue(rig.Screen.PreviewOpenButton!.gameObject.activeSelf);
            Assert.IsFalse(rig.Screen.PreviewArrowsShown, "жодної завершеної галактики — гортати нічого");
            rig.Screen.PreviewOpenButton.onClick.Invoke();
            Assert.AreEqual((0, 1), opened, "фокус — на другій планеті, яку щойно відкрила перша");
        }

        [Test]
        public void FinishedGalaxy_StaysReachableThroughTheArrows()
        {
            using var rig = MetaScreenRig<GalaxyScreen>.Create(ScreenRigBase.Devices[2], "GalaxyScreen");
            var player = rig.NewPlayer();
            for (var p = 0; p < player.Layout.Planets.Count; p++)
                rig.FillPlanet(player, p, pictureOffset: p * 9);
            Assert.AreEqual(1, player.CurrentGalaxy);
            (int galaxy, int planet)? opened = null;
            rig.Screen.OpenRequested += (g, p) => opened = (g, p);

            rig.Enter(player, GalaxyArgs.Own);
            Assert.AreEqual(1, rig.Screen.PreviewGalaxy!.Index, "вхід — у поточну, другу");
            Assert.IsTrue(rig.Screen.PreviewArrowsShown, "є завершена галактика — є куди гортати");

            rig.Screen.PreviewShiftGalaxy(-1);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.Index, "стрілка назад — Галактика I");
            Assert.IsTrue(rig.Screen.PreviewGalaxy.IsComplete);
            rig.Screen.PreviewOpenButton!.onClick.Invoke();
            Assert.AreEqual(0, opened!.Value.galaxy, "планета відкривається саме з першої галактики");

            rig.Screen.PreviewShiftGalaxy(-1);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.Index, "раніше за першу нічого немає");
            rig.Screen.PreviewShiftGalaxy(+5);
            Assert.AreEqual(1, rig.Screen.PreviewGalaxy!.Index, "далі за поточну — теж");
        }
    }
}
