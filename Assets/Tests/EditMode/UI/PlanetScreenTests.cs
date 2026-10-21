using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.Style;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Планета U1Glaze (крок 4) на живому рендері: розкладка за порогом («Сфера» / «Горизонт»), комірки показують
    /// те, що в збереженні, форма комірки на кулі — та сама матриця, що в еталоні (дві вкладені RectTransform), дії й
    /// «планета ожила», автоперевірка й бюджет викликів на обох пристроях контрольної точки.
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

        private static MetaScreenRig<PlanetScreen> Rig(ScreenRigBase.Device device) => MetaScreenRig<PlanetScreen>.Create(device, "PlanetScreen");

        private static List<SlotCell> Bound(PlanetScreen screen)
        {
            var list = new List<SlotCell>();
            foreach (var cell in screen.PreviewCells)
                if (cell != null && cell.Slot != null)
                    list.Add(cell);
            return list;
        }

        [Test]
        public void UpToTheThreshold_Sphere_Above_Horizon()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer();
            var threshold = rig.Design.SphereMaxSlots;
            for (var i = 0; i < player.Layout.Planets.Count; i++)
            {
                rig.Enter(player, new PlanetArgs(0, i));
                var slots = player.Layout.Planets[i].Slots;
                var expected = slots <= threshold ? PlanetLook.Sphere : PlanetLook.Horizon;
                Assert.AreEqual(expected, rig.Screen.PreviewLook, $"{player.Layout.Planets[i].Name} ({slots})");
                Assert.AreEqual(slots, Bound(rig.Screen).Count, "по комірці на слот");
                Assert.AreEqual(expected == PlanetLook.Horizon ? slots : 0, rig.Screen.PreviewHorizonPips,
                    "мітки рядком — лише на «Горизонті» (на «Сфері» — кільцем навколо кулі)");
            }
        }

        [Test]
        public void HalfPlanet_ShowsSavedPicturesInTheirCells_FromOneAtlas()
        {
            using var rig = Rig(ScreenRigBase.Devices[2]);
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0, count: 2);
            rig.Enter(player, new PlanetArgs(0, 0));

            var bound = Bound(rig.Screen);
            Assert.IsTrue(bound[0].IsFilled && bound[1].IsFilled, "картинки стоять у слотах 0 і 1 — як у збереженні");
            Assert.IsFalse(bound[2].IsFilled);
            StringAssert.Contains("2 / 4", rig.Screen.PreviewCounter);
            StringAssert.Contains("комірки заповнено", rig.Screen.PreviewCounter, "«2 / 4 комірки заповнено» (S04_01)");

            // Пікселі всіх зайнятих комірок — з ОДНОЇ текстури (атлас): інакше кожна картинка — окремий виклик.
            var textures = new HashSet<Texture>();
            foreach (var raw in rig.Screen.GetComponentsInChildren<RawImage>(false))
                if (raw.texture != null)
                    textures.Add(raw.texture);
            Assert.AreEqual(1, textures.Count, "усі картинки комірок малюються з одного атласу");
        }

        [Test]
        public void SphereCells_TakeTheReferenceShape_FromTwoNestedTransforms()
        {
            // Еталон globe_slots.py (порт GlobeSlots): центр, прозорість і матриця 2×2 кожної комірки. На екрані форма —
            // зовнішній RectTransform (t1, масштаб) × внутрішній (t2); тут перевіряємо, що композиція дає ту саму M.
            using var rig = Rig(ScreenRigBase.Mockup);
            var layout = MetaScreenshots.MockupGalaxy(PlanetType.Ice, "Кріос", 8);
            var player = rig.NewPlayer(layout);
            rig.FillPlanet(player, 0, count: 5);
            rig.Enter(player, new PlanetArgs(0, 0));
            rig.Screen.PreviewRotation(30f);
            rig.Render();

            var globe = rig.Screen.PreviewGlobe!;
            var reference = GlobeSlots.Layout(8, PlanetLooks.SeedOf(PlanetType.Ice));
            var radius = globe.BaseDiameter * 0.5f;
            foreach (var cell in Bound(rig.Screen))
            {
                var i = cell.Slot!.Index;
                var p = GlobeSlots.Project(reference[i], 30.0, rig.Design.SphereAxisTilt);
                var where = $"комірка {i}";
                var position = cell.transform.localPosition;
                Assert.AreEqual((float)p.X * radius, position.x, 0.5f, where + ": x");
                Assert.AreEqual((float)p.Y * radius, position.y, 0.5f, where + ": y");
                Assert.AreEqual((float)GlobeSlots.Alpha(p.Depth), cell.GetComponent<CanvasGroup>().alpha, 1e-3f, where + ": прозорість");

                // Осі внутрішнього квадрата в координатах шару комірок, поділені на бокс комірки, — стовпці M.
                var inner = cell.transform.GetChild(0);
                var parent = cell.transform.parent;
                var box = (float)reference[i].Size * radius * SlotCell.BoxPerSide;
                var ax = parent.InverseTransformVector(inner.TransformVector(Vector3.right * cell.BoxSide)) / box;
                var ay = parent.InverseTransformVector(inner.TransformVector(Vector3.up * cell.BoxSide)) / box;
                Assert.AreEqual((float)p.M00, ax.x, 2e-3f, where + ": m00");
                Assert.AreEqual((float)p.M10, ax.y, 2e-3f, where + ": m10");
                Assert.AreEqual((float)p.M01, ay.x, 2e-3f, where + ": m01");
                Assert.AreEqual((float)p.M11, ay.y, 2e-3f, where + ": m11");
            }
        }

        [Test]
        public void TapOnTheFrontCell_PicksIt_AndTheBackStaysOut()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer(MetaScreenshots.MockupGalaxy(PlanetType.Ice, "Кріос", 8));
            rig.Enter(player, new PlanetArgs(0, 0));
            rig.Screen.PreviewRotation(195f);
            var globe = rig.Screen.PreviewGlobe!;
            var reference = GlobeSlots.Layout(8, 3);
            var radius = globe.BaseDiameter * 0.5f;
            for (var i = 0; i < reference.Length; i++)
            {
                var p = GlobeSlots.Project(reference[i], 195.0, rig.Design.SphereAxisTilt);
                var hit = globe.Pick(new Vector2((float)p.X * radius, (float)p.Y * radius), out _);
                if (p.Depth > 0.3)
                    Assert.AreEqual(i, hit?.Index, $"комірка {i} спереду — влучання по її центру");
                else if (p.Depth <= 0.05)
                    Assert.AreNotEqual(i, hit?.Index, $"комірка {i} на звороті — у неї не влучити");
            }
        }

        [Test]
        public void FinishedGalaxy_IsAShowcase_TapExplains_ActionIsOff()
        {
            using var rig = Rig(ScreenRigBase.Devices[1]);
            var player = rig.NewPlayer();
            for (var p = 0; p < player.Layout.Planets.Count; p++)
                rig.FillPlanet(player, p, pictureOffset: p * 5);
            Assert.AreEqual(1, player.CurrentGalaxy);

            rig.Enter(player, new PlanetArgs(0, 0));
            rig.Screen.PreviewTap(0);

            Assert.IsFalse(rig.Screen.PreviewSheetOpen, "завершену галактику не редагують — листа немає");
            StringAssert.Contains("завершена", rig.Screen.PreviewHint, "але пояснення є");
            Assert.IsTrue(rig.Screen.PreviewHintFits, "…і воно не обрізане «…» (ревізія кроку 4)");
            Assert.IsFalse(rig.Screen.PreviewPlaceButton!.Interactable, "«Поставити картинку» вимкнена");
        }

        [Test]
        public void UnknownPicture_ShowsAnOccupiedCellWithoutPixels()
        {
            using var rig = Rig(ScreenRigBase.Devices[0]);
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0, count: 1);
            GalaxyState.Set(player.Galaxy, 0, player.Layout.Planets[0].Id, 2, "no-such-picture", System.DateTime.UtcNow);

            rig.Enter(player, new PlanetArgs(0, 0));

            var bound = Bound(rig.Screen);
            Assert.IsTrue(bound[0].IsFilled && bound[0].ShowsPixels, "відома картинка — з пікселями");
            Assert.IsTrue(bound[2].IsFilled, "невідома — комірка зайнята");
            Assert.IsFalse(bound[2].ShowsPixels, "…але малювати нічого");
            Assert.IsFalse(bound[1].IsFilled);
        }

        [Test]
        public void FilledCell_OpensTheSheet_ReturnFreesTheSlot_EmptyGoesToTheCollection()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0, count: 2);
            CollectionArgs? asked = null;
            rig.Screen.CollectionRequested += args => asked = args;
            rig.Enter(player, new PlanetArgs(0, 0));

            rig.Screen.PreviewTap(1);
            Assert.IsTrue(rig.Screen.PreviewSheetOpen, "зайнята — лист «Замінити / Повернути»");
            Assert.IsTrue(Bound(rig.Screen)[1].IsSelected, "вибрана комірка підсвічена");

            rig.Screen.PreviewRemove();
            Assert.IsFalse(rig.Screen.PreviewSheetOpen);
            Assert.AreEqual(1, player.Galaxy.Slots.Count, "картинка повернулась у колекцію — слот вільний");
            Assert.IsFalse(Bound(rig.Screen)[1].IsFilled);

            rig.Screen.PreviewTap(2);
            Assert.IsNotNull(asked, "порожня — колекція");
            Assert.AreEqual(2, asked!.Slot, "…для цієї самої комірки");

            asked = null;
            rig.Screen.PreviewPlaceButton!.Button.onClick.Invoke();
            Assert.AreEqual(1, asked!.Slot, "«Поставити картинку» — перша порожня");
        }

        [Test]
        public void Horizon_UsesTheScreenWidth_LikeTheMockup()
        {
            // Ревізія кроку 4: ділянка вужча за екран на поля стовпчика (370 з 402), і ряди виходили на 8–12 % дрібнішими
            // за S05_01. Сцена «Горизонту» — на всю ширину екрана: дальній ряд 12 комірок — 60 px.
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer(MetaScreenshots.MockupGalaxy(PlanetType.Rocky, "Рудокам", 12));
            rig.Enter(player, new PlanetArgs(0, 0));
            rig.Render();
            var scene = rig.Screen.PreviewHorizon!.Scene;
            foreach (var slot in scene.Slots)
                if (slot.Row == 0)
                    Assert.AreEqual(60.0 * scene.Scale, slot.Size, 1.0, "дальній ряд — як на S05_01");
        }

        [Test]
        public void ACollectionTrip_LeftAnotherWay_DoesNotCelebrateTheNextPlanet()
        {
            // Ревізія кроку 4: пішли в колекцію з планети A (прапорець «повертаємось»), вийшли шестернею → «Додому»
            // (без входу назад), потім відкрили ожилу планету B — свята бути не повинно.
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0);
            rig.FillPlanet(player, 1, count: 2);
            rig.Enter(player, new PlanetArgs(0, 1));
            rig.Screen.PreviewTap(3); // порожня — у колекцію
            rig.Screen.PreviewEnter(new PlanetArgs(0, 0)); // новий вхід, не Pop
            Assert.IsFalse(rig.Screen.PreviewAliveShown, "ожила давно — не святкуємо вдруге");
        }

        [Test]
        public void Alive_ShowsThePanel_InsteadOfTheBottom_AndNamesTheNextPlanet()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer(MetaScreenshots.MockupGalaxy(PlanetType.Ice, "Кріос", 8));
            rig.FillPlanet(player, 0);
            rig.Enter(player, new PlanetArgs(0, 0));
            Assert.IsFalse(rig.Screen.PreviewAliveShown, "вхід на вже ожилу планету не святкує вдруге");

            rig.Screen.PreviewAlive();
            Assert.IsTrue(rig.Screen.PreviewAliveShown);
            StringAssert.Contains("Сатурнія", rig.Screen.PreviewAliveBody, "наступна планета — на ім'я");
            Assert.IsFalse(rig.Screen.PreviewPlaceButton!.gameObject.activeInHierarchy, "низ — панель замість дії");
        }

        [Test]
        public void EveryState_Fits_PassesTheAudit_AndKeepsTheBudget_OnBothCheckpointDevices()
        {
            foreach (var device in new[] { ScreenRigBase.Mockup, ScreenRigBase.Small })
                foreach (var (state, slots, filled) in new[]
                         {
                             ("sphere", 8, 5), ("sphere-sheet", 8, 5), ("sphere-alive", 8, 8),
                             ("horizon", 12, 7), ("horizon", 20, 13), ("horizon-sheet", 12, 7), ("horizon-alive", 12, 12),
                         })
                {
                    using var rig = Rig(device);
                    var sphere = state.StartsWith("sphere");
                    var player = rig.NewPlayer(MetaScreenshots.MockupGalaxy(sphere ? PlanetType.Ice : PlanetType.Rocky,
                        sphere ? "Кріос" : "Рудокам", slots));
                    player.Wallet.Add(999_999_999, RewardSource.Debug); // найдовше число в капсулі
                    rig.FillPlanet(player, 0, count: filled, pictureOffset: 3);
                    rig.Enter(player, new PlanetArgs(0, 0));
                    if (state.EndsWith("-sheet")) rig.Screen.PreviewSheet(1);
                    if (state.EndsWith("-alive")) rig.Screen.PreviewAlive();
                    rig.Render();

                    var where = $"{device.Name} / {state} {slots}";
                    var problems = UiAudit.All(rig.Safe);
                    Assert.AreEqual(0, problems.Count, $"{where}:\n" + string.Join("\n", problems));
                    Assert.LessOrEqual(rig.SimulateBatches(), MetaScreenshots.DrawCallBudget, $"{where}: виклики малювання");

                    if (sphere)
                    {
                        // Куля не більша за частку ширини екрана й уміщається разом із дугою в ділянку.
                        var d = rig.Screen.PreviewGlobe!.BaseDiameter * rig.Screen.PreviewGlobe.Fit / DesignSystem.MockupToReference;
                        Assert.Greater(d, 180f, $"{where}: куля {d:0} px — замала");
                        Assert.LessOrEqual(d, 300f, $"{where}: куля {d:0} px");
                    }
                    else
                    {
                        // «Горизонт»: усі комірки — у межах екрана (бічні поля 16) і не перекриваються.
                        var scene = rig.Screen.PreviewHorizon!.Scene;
                        Assert.AreEqual(slots, scene.Slots.Length, where);
                        Assert.IsTrue(HorizonLayout.NoOverlaps(scene.Slots), $"{where}: комірки перекриваються");
                        var safeWidth = rig.Safe.rect.width / DesignSystem.MockupToReference;
                        foreach (var slot in scene.Slots)
                            Assert.LessOrEqual(Mathf.Abs((float)slot.X) + (float)HorizonLayout.HalfExtent(slot), safeWidth * 0.5f - 16f + 0.5f,
                                $"{where}: комірка за бічним полем");
                    }
                }
        }

        [Test]
        public void VisitorPlanet_IsReadOnly_WithAFooterInsteadOfTheAction()
        {
            // S09_02: чужа «Сфера» — слоти з вітрини, порожні лунки без «+», низ «Планета гравця … · N / M», дії немає.
            using var rig = MetaScreenRig<PlanetScreen>.Create(ScreenRigBase.Devices[0], "PlanetScreen");
            var player = rig.NewPlayer();
            var planet = player.Layout.Planets[0];
            var slots = new List<ShowcaseSlot>();
            for (var s = 0; s < planet.Slots - 1; s++)
                slots.Add(new ShowcaseSlot(planet.Id, s, rig.Library[s].Id));
            var showcase = new PublicShowcase("guest", "Вега", 0, false, null, 0, 0, 0, slots, "");

            rig.Enter(player, PlanetArgs.ForVisitor(showcase, 0));

            Assert.IsTrue(rig.Screen.PreviewVisitorFooterShown, "низ гостя");
            Assert.IsFalse(rig.Screen.PreviewActionShown, "«Поставити картинку» немає");
            Assert.AreEqual($"Планета гравця Вега · {planet.Slots - 1} / {planet.Slots}", rig.Screen.PreviewVisitorTitle);
            Assert.AreEqual(0, player.Galaxy.Slots.Count, "мій файл не чіпали");
        }
    }
}
