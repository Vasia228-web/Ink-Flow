using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.Style;
using NUnit.Framework;
using UnityEngine;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Галактика U1Glaze (крок 3, S03Galaxy, S09ForeignGalaxy_01) на живому рендері: своя — вкладка навігації без «‹»,
    /// чужа — перегляд із карткою гостя без редагування; кільце міток по одній на комірку за номером слота, кольором
    /// своєї картинки; бейдж ожилої й замок замкненої; «Галактику завершено» → нова галактика → «Летіти»; повернення
    /// з планети лишає в тій самій галактиці, нова вкладка — у поточній; завершені цикли досяжні стрілками; усе
    /// вміщається без переповнень на обох пристроях контрольної точки й у бюджеті викликів.
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

        private static MetaScreenRig<GalaxyScreen> Rig(ScreenRigBase.Device device) => MetaScreenRig<GalaxyScreen>.Create(device, "GalaxyScreen");

        /// <summary>Три ожилі, у четвертій три картинки — як S03_01.</summary>
        private static PlayerState Midway(MetaScreenRig<GalaxyScreen> rig)
        {
            var player = rig.NewPlayer();
            player.Wallet.Add(1240, RewardSource.Debug);
            for (var p = 0; p < 3; p++) rig.FillPlanet(player, p, pictureOffset: p * 7);
            rig.FillPlanet(player, 3, count: 3, pictureOffset: 30);
            return player;
        }

        private static PlayerState Everything(MetaScreenRig<GalaxyScreen> rig)
        {
            var player = rig.NewPlayer();
            for (var p = 0; p < player.Layout.Planets.Count; p++)
                rig.FillPlanet(player, p, pictureOffset: p * 9);
            return player;
        }

        [Test]
        public void ReadOnlyGalaxy_HasNoEditingControls_AndNeverOpensAPlanet()
        {
            using var rig = Rig(ScreenRigBase.Devices[0]);
            var player = rig.NewPlayer();
            var opened = 0;
            rig.Screen.OpenRequested += (_, _) => opened++;

            rig.Enter(player, new GalaxyArgs(new PlayerId("someone-else"), readOnly: true));

            var button = rig.Screen.PreviewOpenButton;
            Assert.IsNotNull(button);
            Assert.IsFalse(button!.gameObject.activeInHierarchy, "у чужій галактиці кнопки «Відкрити» немає");
            Assert.IsFalse(rig.Screen.PreviewArrowsShown, "і стрілок циклів");
            Assert.IsNull(rig.Screen.PreviewNavBar, "і навігації: це перегляд, а не вкладка (S09)");
            Assert.IsTrue(rig.Screen.PreviewBackShown, "«‹» — назад у Рейтинги");
            Assert.IsTrue(rig.Screen.PreviewPaginationShown, "крапки планет — як на S09_01");
            Assert.IsFalse(rig.Screen.PreviewCompletedShown);

            button.onClick.Invoke();
            Assert.AreEqual(0, opened, "навіть натиснута кнопка нікуди не веде");
        }

        [Test]
        public void VisitorGalaxy_ComesFromTheShowcase_WithAVisitorCard()
        {
            using var rig = Rig(ScreenRigBase.Devices[0]);
            var player = rig.NewPlayer();

            // Гість: дві планети ожили, у третій стоїть одна картинка.
            var layout = player.Layout;
            var slots = new List<ShowcaseSlot>();
            for (var p = 0; p < 2; p++)
                for (var s = 0; s < layout.Planets[p].Slots; s++)
                    slots.Add(new ShowcaseSlot(layout.Planets[p].Id, s, rig.Library[s % rig.Library.Count].Id));
            slots.Add(new ShowcaseSlot(layout.Planets[2].Id, 0, rig.Library[3].Id));
            var showcase = new PublicShowcase("guest-1", "Гелій", 1, false, rig.Library[5].Id, 2, 0, 0, slots, "");

            rig.Enter(player, GalaxyArgs.ForVisitor(showcase));

            var galaxy = rig.Screen.PreviewGalaxy!;
            Assert.AreEqual(2, galaxy.DoneCount, "планети з вітрини, не з мого файлу");
            Assert.AreEqual(PlanetState.Current, galaxy.Planets[2].State);
            Assert.AreEqual(1, galaxy.Planets[2].FilledSlots);
            Assert.IsTrue(rig.Screen.PreviewVisitorShown, "картка гостя під шапкою (S09_01)");
            Assert.AreEqual("Гелій", rig.Screen.PreviewVisitorNick);
            Assert.AreEqual("галактика гравця", rig.Screen.PreviewVisitorHint);
            Assert.AreEqual("avatar_2", rig.Screen.PreviewVisitorAvatar!.name, "аватар — спрайт за номером гравця");
            Assert.AreEqual(layout.NameOf(0), rig.Screen.PreviewGalaxyTitle, "рядок галактики — без ніку: нік у картці");
            Assert.AreEqual(string.Empty, rig.Screen.PreviewTitle, "замість назви — «‹»");
            Assert.AreEqual(1, rig.Screen.PreviewFocus, "гість бачить останню ожилу планету, а не порожню наступну");
            Assert.IsTrue(rig.Screen.PreviewOpenButton!.gameObject.activeInHierarchy, "S09_01: «Подивитись планету»");
            Assert.AreEqual("Подивитись планету", rig.Screen.PreviewOpenCandy!.Text);
            Assert.AreEqual(CandyButton.Tone.Glass, rig.Screen.PreviewOpenCandy.CurrentTone, "скляна — перегляд, не головна дія");
            PublicShowcase? opened = null;
            var openedPlanet = -1;
            rig.Screen.VisitorPlanetRequested += (visitor, planet) => { opened = visitor; openedPlanet = planet; };
            rig.Screen.PreviewOpenButton.onClick.Invoke();
            Assert.AreSame(showcase, opened, "планета — з тієї самої вітрини");
            Assert.AreEqual(1, openedPlanet, "планета у фокусі");

            // Прихований профіль: нік не показуємо, аватар — інкогніто, планет не видно.
            rig.Enter(player, GalaxyArgs.ForVisitor(PublicShowcase.Hidden("guest-2", 5, 1, "")));
            Assert.AreEqual("Гравець-інкогніто", rig.Screen.PreviewVisitorNick);
            Assert.AreEqual("avatar_incognito", rig.Screen.PreviewVisitorAvatar!.name);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.DoneCount);

            // Своя галактика після гостя — вкладка: без картки й «‹», з навігацією.
            rig.Enter(player, GalaxyArgs.OwnEntry());
            Assert.IsFalse(rig.Screen.PreviewVisitorShown);
            Assert.IsFalse(rig.Screen.PreviewBackShown);
            Assert.IsNotNull(rig.Screen.PreviewNavBar);
        }

        [Test]
        public void OwnGalaxy_IsATab_AndOpensThePlanetInFocus()
        {
            using var rig = Rig(ScreenRigBase.Devices[1]);
            var player = rig.NewPlayer();
            rig.FillPlanet(player, 0);
            (int galaxy, int planet)? opened = null;
            rig.Screen.OpenRequested += (g, p) => opened = (g, p);
            var tabs = new List<string>();
            rig.Screen.TabRequested += tabs.Add;

            rig.Enter(player, GalaxyArgs.OwnEntry());

            Assert.AreEqual("Галактика", rig.Screen.PreviewTitle, "вкладка: назва зліва");
            Assert.IsFalse(rig.Screen.PreviewBackShown, "у вкладки «назад» немає");
            var nav = rig.Screen.PreviewNavBar;
            Assert.IsNotNull(nav);
            Assert.AreEqual(NavBar.Galaxy, nav!.ActiveId, "активна «Галактика»");
            Awaken(nav);
            nav.PreviewTap(NavBar.Collection);
            CollectionAssert.AreEqual(new[] { NavBar.Collection }, tabs, "вкладка каже роутеру, куди");

            Assert.IsTrue(rig.Screen.PreviewOpenButton!.gameObject.activeInHierarchy);
            Assert.IsFalse(rig.Screen.PreviewArrowsShown, "жодної завершеної галактики — гортати нічого");
            rig.Screen.PreviewOpenButton.onClick.Invoke();
            Assert.AreEqual((0, 1), opened, "фокус — на другій планеті, яку щойно відкрила перша");
        }

        [Test]
        public void Pips_OnePerSlot_ByIndex_ColouredByTheirPicture()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = rig.NewPlayer();
            var planet = player.Layout.Planets[0];
            var first = rig.Library[3];
            var third = rig.Library[11];
            foreach (var (slot, picture) in new[] { (0, first), (2, third) })
            {
                player.CollectPicture(picture.Id, System.DateTime.UtcNow);
                Assert.IsTrue(player.TryPlaceInSlot(0, planet.Id, slot, picture.Id, System.DateTime.UtcNow));
            }

            rig.Enter(player, GalaxyArgs.OwnEntry());
            var view = rig.Screen.PreviewCarousel!.PreviewViewOf(0);
            Assert.IsNotNull(view, "перша планета у фокусі");
            Assert.AreEqual(planet.Slots, view!.PreviewPipCount, "по мітці на комірку");
            Assert.IsTrue(view.PreviewPipFilled(0));
            Assert.IsFalse(view.PreviewPipFilled(1), "заповнені — за номером слота, не «на початку»");
            Assert.IsTrue(view.PreviewPipFilled(2));
            Assert.AreEqual(DesignSystem.PaletteColor(first.MainColor), view.PreviewPipColor(0), "головний колір своєї картинки");
            Assert.AreEqual(DesignSystem.PaletteColor(third.MainColor), view.PreviewPipColor(2));

            // Перша мітка — о 12-й, далі за годинником.
            var top = view.PreviewPipPosition(0);
            var next = view.PreviewPipPosition(1);
            Assert.Greater(top.y, 0f);
            Assert.AreEqual(0f, top.x, 0.5f);
            Assert.Greater(next.x, 0f, "друга — праворуч від першої (за годинником)");
            Assert.AreEqual("2 з 4 комірок заповнено", rig.Screen.PreviewPlanetCaption);
        }

        [Test]
        public void DonePlanet_HasTheCheck_LockedHasTheLock_AndAFlatButton()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = Midway(rig);
            rig.Enter(player, GalaxyArgs.OwnEntry());
            Assert.AreEqual(3, rig.Screen.PreviewFocus, "вхід — на поточній планеті");
            Assert.AreEqual($"3 з {player.Layout.Planets[3].Slots} комірок заповнено", rig.Screen.PreviewPlanetCaption);

            rig.Screen.PreviewSetFocus(2);
            var done = rig.Screen.PreviewCarousel!.PreviewViewOf(2)!;
            Assert.IsTrue(done.PreviewDoneBadge, "ожила — бейдж-галочка на планеті (S03_02)");
            Assert.AreEqual($"Усі {player.Layout.Planets[2].Slots} комірок заповнені", rig.Screen.PreviewPlanetCaption);
            Assert.IsTrue(rig.Screen.PreviewOpenCandy!.Interactable);

            rig.Screen.PreviewSetFocus(4);
            var locked = rig.Screen.PreviewCarousel!.PreviewViewOf(4)!;
            Assert.IsTrue(locked.PreviewLockShown, "замкнена — плитка із замком (S03_03)");
            Assert.AreEqual(0, locked.PreviewPipCount, "і без міток");
            Assert.IsFalse(locked.PreviewDoneBadge);
            Assert.AreEqual("Заверши попередню планету", rig.Screen.PreviewPlanetCaption);
            Assert.IsFalse(rig.Screen.PreviewOpenCandy!.Interactable, "кнопка на місці, але плоска й неактивна");
        }

        [Test]
        public void GalaxyCompleted_ShowsTheDialog_ThenTheNewGalaxy_ThenFlies()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            var player = Everything(rig);
            var args = GalaxyArgs.OwnEntry();
            rig.Enter(player, args);
            Assert.IsFalse(rig.Screen.PreviewCompletedShown, "завершене до запуску не святкується");

            rig.Screen.PreviewGalaxyJustCompleted(args);
            Assert.IsTrue(rig.Screen.PreviewCompletedShown, "щойно ожила остання планета — «Галактику завершено» (S03_04)");
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.Index, "під діалогом — та галактика, що завершилась");
            Assert.AreEqual(player.Layout.Planets.Count, rig.Screen.PreviewArcPlanets, "у дузі — усі планети галактики");
            StringAssert.Contains($"Усі {player.Layout.Planets.Count} планет ожили", rig.Screen.PreviewCompletedBody);

            rig.Screen.PreviewNext();
            Assert.IsFalse(rig.Screen.PreviewCompletedShown);
            Assert.IsTrue(rig.Screen.PreviewRevealShown, "«Далі» — нова галактика (S03_05)");
            Assert.AreEqual(player.Layout.TitleOf(1), rig.Screen.PreviewRevealTitle);
            Assert.AreEqual($"{player.Layout.Planets.Count} планет · до {player.Layout.MaxSlots} комірок на планеті", rig.Screen.PreviewRevealSubtitle);
            Assert.IsNull(rig.Screen.PreviewNavBar, "на екрані нової галактики навігації немає");

            rig.Screen.PreviewFly();
            Assert.IsFalse(rig.Screen.PreviewRevealShown);
            Assert.AreEqual(1, rig.Screen.PreviewGalaxy!.Index, "«Летіти» — у карусель нової");
            Assert.AreEqual(0, rig.Screen.PreviewFocus, "на першій планеті");
            Assert.IsNotNull(rig.Screen.PreviewNavBar);

            // Повернення з тими самими аргументами (налаштування, планета) — свято не повторюється.
            rig.Screen.PreviewEnter(args);
            Assert.IsFalse(rig.Screen.PreviewCompletedShown);
            Assert.IsFalse(rig.Screen.PreviewRevealShown);
        }

        [Test]
        public void ANewState_DoesNotInheritTheOldPlayersDialog()
        {
            // Ревізія кроку 3: дев-панель скидає збереження посеред «Галактику завершено» — роутер прив'язує новий стан,
            // і перший же вхід у вкладку показував би «Усі 9 планет ожили» над порожньою грою.
            using var rig = Rig(ScreenRigBase.Mockup);
            var args = GalaxyArgs.OwnEntry();
            rig.Enter(Everything(rig), args);
            rig.Screen.PreviewGalaxyJustCompleted(args);
            Assert.IsTrue(rig.Screen.PreviewCompletedShown);

            rig.Enter(rig.NewPlayer(), GalaxyArgs.OwnEntry());
            Assert.IsFalse(rig.Screen.PreviewCompletedShown, "новий стан — без чужого свята");
            Assert.IsFalse(rig.Screen.PreviewRevealShown);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.Index);
        }

        [Test]
        public void MoonOrbit_StaysInsideTheRingOfPips()
        {
            // Ревізія кроку 3: на орбіті 0.62 діаметра місяць Аквіли (16 px) їхав просто під мітками о 3-й і 9-й.
            // Найдальша точка орбіти разом із пів місяця має лишатись усередині кільця мінус пів мітки — для звичайної
            // планети й для фінальної (місяць — сталих 16 px, мітки й орбіта — частки діаметра).
            var design = ScriptableObject.CreateInstance<DesignSystem>();
            try
            {
                var moonHalf = DesignSystem.FromMockupPx(16f) * 0.5f;
                foreach (var body in new[] { design.PlanetSize, design.PlanetSizeFinale })
                {
                    var moonOuter = body * design.GalaxyMoonOrbit + moonHalf;
                    var pipInner = body * (design.GalaxyPipRing - design.GalaxyPipSize * 0.5f);
                    Assert.Less(moonOuter, pipInner, $"діаметр {body}: місяць до {moonOuter:0}, мітки від {pipInner:0}");
                }
            }
            finally
            {
                Object.DestroyImmediate(design);
            }
        }

        [Test]
        public void ReturnFromThePlanet_KeepsTheBrowsedGalaxy_ANewTabEntryGoesToTheCurrent()
        {
            using var rig = Rig(ScreenRigBase.Devices[2]);
            var player = Everything(rig);
            var tab = GalaxyArgs.OwnEntry();
            rig.Enter(player, tab);
            rig.Screen.PreviewShiftGalaxy(-1);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.Index);
            rig.Screen.PreviewSetFocus(5);
            Assert.AreEqual(5, rig.Screen.PreviewCarousel!.Focus);

            // Pop із планети повертає ТІ САМІ аргументи — гравець лишається там, де гортав: та сама галактика
            // і та сама планета (а не перша — у завершеній галактиці «поточної» немає).
            rig.Screen.PreviewEnter(tab);
            Assert.AreEqual(0, rig.Screen.PreviewGalaxy!.Index, "повернення — у ту саму галактику");
            Assert.AreEqual(5, rig.Screen.PreviewCarousel!.Focus, "повернення — на ту саму планету");

            // Нова вкладка (свіжі аргументи) — у поточну, на поточну планету.
            rig.Screen.PreviewEnter(GalaxyArgs.OwnEntry());
            Assert.AreEqual(1, rig.Screen.PreviewGalaxy!.Index, "новий вхід — у поточну");
            Assert.AreEqual(Mathf.Max(0, rig.Screen.PreviewGalaxy.CurrentIndex), rig.Screen.PreviewCarousel!.Focus, "новий вхід — фокус на поточній планеті");
        }

        [Test]
        public void FinishedGalaxy_StaysReachableThroughTheArrows()
        {
            using var rig = Rig(ScreenRigBase.Devices[2]);
            var player = Everything(rig);
            Assert.AreEqual(1, player.CurrentGalaxy);
            (int galaxy, int planet)? opened = null;
            rig.Screen.OpenRequested += (g, p) => opened = (g, p);

            rig.Enter(player, GalaxyArgs.OwnEntry());
            Assert.AreEqual(1, rig.Screen.PreviewGalaxy!.Index, "вхід — у поточну, другу");
            Assert.IsTrue(rig.Screen.PreviewArrowsShown, "є завершена галактика — є куди гортати");
            Assert.IsTrue(rig.Screen.PreviewPrevGalaxyButton!.interactable);
            Assert.IsFalse(rig.Screen.PreviewNextGalaxyButton!.interactable, "новіша за поточну — немає");

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

        [Test]
        public void EveryState_Fits_PassesTheAudit_AndKeepsTheBudget_OnBothCheckpointDevices()
        {
            foreach (var device in new[] { ScreenRigBase.Mockup, ScreenRigBase.Small })
                foreach (var state in new[] { "mid", "locked", "complete", "reveal", "visitor" })
                {
                    using var rig = Rig(device);
                    var player = state == "complete" || state == "reveal" ? Everything(rig) : Midway(rig);
                    player.Wallet.Add(999_999_999 - 1240, RewardSource.Debug); // найдовше число в капсулі
                    var args = GalaxyArgs.OwnEntry();
                    if (state == "visitor")
                        rig.Enter(player, GalaxyArgs.ForVisitor(new PublicShowcase("v", "Космічний_Маляр2", 3, false, null, 0, 0, 0, null, "")));
                    else
                        rig.Enter(player, args);
                    if (state == "locked") rig.Screen.PreviewSetFocus(4);
                    if (state == "complete" || state == "reveal") rig.Screen.PreviewGalaxyJustCompleted(args);
                    if (state == "reveal") rig.Screen.PreviewNext();
                    rig.Render();

                    var problems = UiAudit.All(rig.Safe);
                    Assert.AreEqual(0, problems.Count, $"{device.Name} / {state}:\n" + string.Join("\n", problems));
                    Assert.LessOrEqual(rig.SimulateBatches(), MetaScreenshots.DrawCallBudget, $"{device.Name} / {state}: виклики малювання");
                    var nav = rig.Screen.PreviewNavBar;
                    if (nav != null)
                        Assert.AreEqual(76f * DesignSystem.MockupToReference, ((RectTransform)nav.transform).rect.height, 1.5f, $"{device.Name}: навігація 76");

                    // Планета — не більша за макет і вміщається разом із кільцем міток у висоту каруселі.
                    var fit = rig.Screen.PreviewCarousel!.Fit;
                    var diameter = rig.Design.PlanetSize * fit / DesignSystem.MockupToReference;
                    Assert.LessOrEqual(diameter, rig.Design.GalaxyPlanetSide + 0.5f, $"{device.Name} / {state}: планета {diameter:0} px");
                    Assert.Greater(diameter, 120f, $"{device.Name} / {state}: планета {diameter:0} px — замала");
                }
        }

        private static void Awaken(Component component) =>
            component.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(component, null);
    }
}
