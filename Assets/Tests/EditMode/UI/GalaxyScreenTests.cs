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
