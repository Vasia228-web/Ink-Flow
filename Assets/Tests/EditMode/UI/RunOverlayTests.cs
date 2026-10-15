using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.Platform;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Забіг U1Glaze (крок 2, S02Run): шапка й дві панелі рахунку поруч із картинкою, накладки-діалоги на затемненні —
    /// картка перед забігом, картинку домальовано з двома кнопками й підтвердженням для Епічної+, програш у дві фази
    /// (рішення автора: один чип продовження «ролик → нафта», «Домалювати» лише у фіналі), «Вийти додому?» з
    /// «Забіг збережеться». Усе вміщається без переповнень, дрібних тапів і налізань на обох пристроях контрольної
    /// точки, на екрані не більше однієї коралової кнопки, бюджет викликів малювання ≤ 35.
    /// </summary>
    public sealed class RunOverlayTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        private static readonly ScreenRigBase.Device[] Checkpoint = { ScreenRigBase.Mockup, ScreenRigBase.Small };

        /// <summary>Стани контрольної точки кроку 2 — ті самі, що знімає RunScreenshots.</summary>
        private static IEnumerable<(string name, System.Action<RunScreenRig> prepare)> States()
        {
            yield return ("mid", rig => rig.Show(rig.NewSession(25)));
            yield return ("big-numbers", rig =>
            {
                rig.Show(rig.NewSession(12));
                rig.Screen.PreviewStats(987_654_321, 999_999_999);
            });
            yield return ("intro", rig =>
            {
                rig.Show(rig.NewSession(0));
                rig.Screen.PreviewIntro(resumed: false);
            });
            yield return ("completed", rig =>
            {
                rig.Show(rig.SessionWithRarity(Rarity.Epic, 30));
                rig.Screen.PreviewCompletion();
            });
            yield return ("completed-confirm", rig =>
            {
                rig.Show(rig.SessionWithRarity(Rarity.Cosmic, 30));
                rig.Screen.PreviewCompletionDrop();
            });
            yield return ("over-continue", rig =>
            {
                rig.BindAds(new FakeAds());
                rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
                rig.Screen.PreviewOverContinue();
            });
            yield return ("over-continue-oil", rig =>
            {
                rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
                rig.Screen.PreviewOverContinue();
            });
            yield return ("over", rig =>
            {
                rig.BindAds(new FakeAds());
                rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
                rig.Screen.PreviewOver();
            });
            yield return ("home-confirm", rig =>
            {
                rig.Show(rig.NewSession(25));
                rig.ShowHomeConfirm();
            });
        }

        [Test]
        public void EveryState_PassesTheAudit_FitsTheScreen_AndKeepsTheBudget_OnBothCheckpointDevices()
        {
            foreach (var device in Checkpoint)
                foreach (var (name, prepare) in States())
                {
                    using var rig = RunScreenRig.Create(device);
                    prepare(rig);
                    rig.Render();
                    var problems = rig.Audit();
                    Assert.AreEqual(0, problems.Count, $"{device.Name} / {name}:\n" + string.Join("\n", problems));
                    Assert.IsNull(rig.LayoutFault(), $"{device.Name} / {name}: поле");
                    Assert.LessOrEqual(rig.SimulateBatches(), MetaScreenshots.DrawCallBudget, $"{device.Name} / {name}: виклики малювання");
                    Assert.LessOrEqual(CoralButtons(rig), 1, $"{device.Name} / {name}: більше однієї коралової кнопки");
                    // Діалог програшу вміщається без стискання (FitOverCard — лише запобіжник).
                    Assert.AreEqual(1f, rig.Screen.PreviewOverScale, 1e-3f, $"{device.Name} / {name}: діалог програшу довелось стиснути");
                    foreach (var panel in ShownDialogPanels(rig))
                        AssertInsideSafe(rig, panel, $"{device.Name} / {name}: {panel.name}");
                }
        }

        [Test]
        public void TopRow_PictureLeft_StatsRight_AlignedWithTheHeader()
        {
            // Не квадратні картинки обох орієнтацій: сама картинка (Pixels), а не її квадрат, притиснута вліво-вгору.
            using var probe = RunScreenRig.Create(ScreenRigBase.Mockup);
            PixelPicture? wide = null, tall = null;
            foreach (var p in probe.Library.Pictures)
            {
                if (wide == null && p.Width > p.Height) wide = p;
                if (tall == null && p.Height > p.Width) tall = p;
            }
            Assert.IsNotNull(wide, "у бібліотеці є широка картинка");
            Assert.IsNotNull(tall, "у бібліотеці є висока картинка");
            foreach (var device in Checkpoint)
                foreach (var picture in new[] { wide!, tall! })
                {
                    using var rig = RunScreenRig.Create(device);
                    rig.Show(rig.NewSession(25, library: new PictureLibrary(new[] { picture })));
                    var back = Bounds((RectTransform)rig.Screen.transform.Find("Header/Back"));
                    var pixels = Bounds((RectTransform)rig.Screen.transform.Find("Picture/Plate/Canvas/Pixels"));
                    var stats = Bounds(rig.Screen.PreviewStatsRoot!);
                    var gear = Bounds((RectTransform)rig.Screen.transform.Find("Header/Settings"));
                    var what = $"{device.Name} / {picture.Id} {picture.Width}×{picture.Height}";
                    var tolerance = back.width * 0.03f;
                    Assert.AreEqual(back.xMin, pixels.xMin, tolerance, $"{what}: картинка врівень із «‹»");
                    Assert.AreEqual(stats.yMax, pixels.yMax, tolerance, $"{what}: верх картинки — врівень із панелями");
                    Assert.AreEqual(gear.xMax, stats.xMax, tolerance, $"{what}: панелі врівень із шестернею");
                    Assert.LessOrEqual(pixels.yMax, back.yMin + tolerance, $"{what}: картинка під шапкою");
                    Assert.Greater(stats.xMin, pixels.xMax, $"{what}: панелі праворуч від картинки");

                    // Обидві панелі однакові за висотою, хоч числа різної довжини.
                    rig.Screen.PreviewStats(1_234_567, 7);
                    Canvas.ForceUpdateCanvases();
                    var score = Bounds((RectTransform)rig.Screen.transform.Find("Stats/Score"));
                    var record = Bounds((RectTransform)rig.Screen.transform.Find("Stats/Record"));
                    Assert.AreEqual(score.height, record.height, tolerance, $"{what}: панелі РАХУНОК і РЕКОРД однакові");
                }
        }

        [Test]
        public void Intro_ShowsKickerBadgeAndOneSwatchPerFamily()
        {
            using var rig = RunScreenRig.Create(ScreenRigBase.Mockup);
            var session = rig.NewSession(0);
            rig.Show(session);
            rig.Screen.PreviewIntro(resumed: false);
            var intro = rig.Screen.transform.Find("RunIntro");
            Assert.IsTrue(intro.gameObject.activeSelf, "картка перед забігом");
            Assert.IsTrue(rig.Screen.PreviewIntroKicker.StartsWith("ЦЕЙ ЗАБІГ · "), rig.Screen.PreviewIntroKicker);
            var badge = intro.GetComponentInChildren<RarityBadgeView>(true);
            Assert.AreEqual(session.Picture.Picture.Rarity, badge.Rarity);
            Assert.AreEqual(RarityNames.Of(session.Picture.Picture.Rarity), badge.Text);
            var swatches = 0;
            foreach (Transform child in intro.Find("Panel/Colors"))
                if (child.name.StartsWith("Swatch") && child.gameObject.activeSelf)
                    swatches++;
            Assert.AreEqual(session.Picture.Picture.FillColors.Count, swatches, "зразків стільки, скільки родин");
            var count = intro.Find("Panel/Colors/Count").GetComponent<TMP_Text>().text;
            Assert.AreEqual(Plural.Count(swatches, "колір", "кольори", "кольорів"), count);

            rig.Screen.PreviewIntro(resumed: true);
            Assert.IsTrue(rig.Screen.PreviewIntroKicker.StartsWith("ПРОДОВЖЕННЯ · "), "продовження зліпка — свій кікер");
        }

        [Test]
        public void Over_BeforeTheFinal_HasOneContinueChip_AdFirst_ThenOil_AndNoFinishOrCoral()
        {
            using var rig = RunScreenRig.Create(ScreenRigBase.Mockup);
            rig.BindAds(new FakeAds());
            rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
            rig.Screen.PreviewOverContinue();
            Assert.IsTrue(rig.Screen.PreviewOverShown);
            Assert.IsTrue(rig.Screen.PreviewContinueAd!.gameObject.activeSelf, "перше продовження — ролик");
            Assert.IsFalse(rig.Screen.PreviewContinueOil!.gameObject.activeSelf, "один чип, не два");
            Assert.IsFalse(rig.Screen.PreviewFinish!.gameObject.activeSelf, "«Домалювати» — лише у фіналі");
            Assert.IsFalse(rig.Screen.PreviewAgain!.gameObject.activeSelf);
            Assert.IsTrue(rig.Screen.PreviewEnd!.gameObject.activeSelf, "«Завершити»");

            using var noAds = RunScreenRig.Create(ScreenRigBase.Mockup);
            noAds.Show(noAds.NewSession(10_000, stopWhen: s => s.IsOver));
            noAds.Screen.PreviewOverContinue();
            Assert.IsFalse(noAds.Screen.PreviewContinueAd!.gameObject.activeSelf);
            Assert.IsTrue(noAds.Screen.PreviewContinueOil!.gameObject.activeSelf, "ролика немає — за нафту");
            Assert.IsTrue(noAds.Screen.PreviewContinueOil.Interactable, "у стенді 500 нафти — вистачає");
        }

        [Test]
        public void Over_End_GoesToTheFinal_WithOneCoralAgainButton()
        {
            using var rig = RunScreenRig.Create(ScreenRigBase.Mockup);
            rig.BindAds(new FakeAds());
            rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
            rig.Screen.PreviewOverContinue();
            Awaken(rig.Screen); // у Edit Mode Awake не біжить — підписки кнопок ставимо самі, клік іде по-справжньому
            rig.Screen.PreviewEndRun();
            Assert.IsTrue(rig.Screen.PreviewOverShown, "той самий діалог — фінал");
            Assert.IsTrue(rig.Screen.PreviewAgain!.gameObject.activeSelf, "«Ще раз»");
            Assert.AreEqual(CandyButton.Tone.Coral, rig.Screen.PreviewAgain.CurrentTone);
            Assert.IsFalse(rig.Screen.PreviewEnd!.gameObject.activeSelf);
            Assert.IsFalse(rig.Screen.PreviewContinueAd!.gameObject.activeSelf, "у фіналі продовжувати нічим");
            Assert.AreEqual(1, CoralButtons(rig));

            // «Домалювати» — у фіналі: картинка намальована частково, нафти в стенді вистачає.
            Assert.IsTrue(rig.Screen.PreviewFinish!.gameObject.activeSelf, "«Домалювати» у фіналі");
            Assert.IsTrue(rig.Screen.PreviewFinish.Interactable, "500 нафти вистачає");
            Assert.IsTrue(rig.Screen.PreviewPictureRowShown);
            var before = rig.Player.Collection.Distinct;
            rig.Screen.PreviewFinish.Button.onClick.Invoke();
            Assert.IsFalse(rig.Screen.PreviewFinish.gameObject.activeSelf, "домальовано — кнопки більше немає");
            Assert.IsTrue(rig.Screen.PreviewPictureRowShown, "рядок картинки лишається й каже, що з нею");
            Assert.AreEqual("Домальовано — у колекції", rig.Screen.PreviewPictureNote);
            Assert.AreEqual(before + 1, rig.Player.Collection.Distinct, "картинка в колекції");
        }

        [Test]
        public void Completion_DropOfEpic_AsksFirst_KeepReturnsToTheCard()
        {
            using var rig = RunScreenRig.Create(ScreenRigBase.Mockup);
            rig.Show(rig.SessionWithRarity(Rarity.Epic, 30));
            var card = rig.Screen.PreviewCompletionCard!;
            var decided = new List<bool>();
            card.Decided += decided.Add;

            rig.Screen.PreviewCompletionDrop();
            Assert.IsTrue(card.IsConfirming, "Епічну — лише після підтвердження");
            Assert.IsFalse(card.IsCardVisible, "під підтвердженням лише забіг (S02Run_04)");
            Assert.AreEqual("Викинути епічну картинку?", card.PreviewConfirmTitle);
            card.PreviewConfirmKeep();
            Assert.IsFalse(card.IsConfirming);
            Assert.IsTrue(card.IsShown && card.IsCardVisible, "«Залишити» повертає до картки");
            Assert.AreEqual(0, decided.Count);

            card.PreviewDrop();
            card.PreviewConfirmDrop();
            Assert.IsFalse(card.IsShown);
            CollectionAssert.AreEqual(new[] { false }, decided, "викинуто");
        }

        [Test]
        public void Completion_DropOfRare_NeedsNoConfirm_AndKeepGoesToTheCollection()
        {
            using var rig = RunScreenRig.Create(ScreenRigBase.Mockup);
            rig.Show(rig.SessionWithRarity(Rarity.Rare, 30));
            var card = rig.Screen.PreviewCompletionCard!;
            var decided = new List<bool>();
            card.Decided += decided.Add;

            rig.Screen.PreviewCompletionDrop();
            Assert.IsFalse(card.IsConfirming, "Рідкісну — без підтвердження");
            Assert.IsFalse(card.IsShown);
            rig.Screen.PreviewCompletion();
            card.PreviewKeep();
            CollectionAssert.AreEqual(new[] { false, true }, decided);
        }

        [Test]
        public void HomeConfirm_SaysTheRunIsSaved_AndPlayOnIsTheSafeTealAction()
        {
            using var rig = RunScreenRig.Create(ScreenRigBase.Small);
            rig.Show(rig.NewSession(25));
            var prompt = rig.ShowHomeConfirm();   // ConfirmPrompt.ShowHomeConfirm — та сама точка, що в AppRouter.GoHome
            Assert.IsTrue(prompt.IsOpen);
            Assert.AreEqual("Вийти додому?", prompt.PreviewTitle);
            StringAssert.Contains("Забіг збережеться", prompt.PreviewMessage);
            Assert.AreEqual("Грати далі", prompt.PreviewCancel);
            Assert.AreEqual("Вийти", prompt.PreviewConfirm);
            var cancel = prompt.transform.Find("Panel/Cancel").GetComponent<CandyButton>();
            Assert.AreEqual(CandyButton.Tone.Teal, cancel.CurrentTone, "безпечна дія — бірюзова");
            // «Грати далі» — справжній клік: закриває діалог і веде туди, куди обіцяє (роутер передає Pop налаштувань).
            Awaken(prompt);
            var confirmed = false;
            var playedOn = false;
            prompt.ShowHomeConfirm(() => confirmed = true, () => playedOn = true);
            cancel.Button.onClick.Invoke();
            Assert.IsFalse(prompt.IsOpen);
            Assert.IsTrue(playedOn, "«Грати далі» повертає в забіг");
            Assert.IsFalse(confirmed);

            prompt.ShowHomeConfirm(() => confirmed = true, () => playedOn = false);
            prompt.transform.Find("Panel/Confirm").GetComponent<CandyButton>().Button.onClick.Invoke();
            Assert.IsTrue(confirmed, "«Вийти» — додому");
            Assert.IsTrue(playedOn, "вихід не кличе «Грати далі»");
            Assert.IsFalse(prompt.IsOpen);
        }

        [Test]
        public void U1GlazeSprites_ShareOneAtlasPage()
        {
            // Дві сторінки атласу розривали батчі: тінь панелі на одній, заливка на іншій — окремий виклик на кожній
            // межі шарів (накладки забігу давали 39–48 викликів при бюджеті 35). Сторінка до 4096 — одна на всі спрайти.
            using var rig = RunScreenRig.Create(ScreenRigBase.Mockup);
            rig.BindAds(new FakeAds());
            rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
            rig.Screen.PreviewOver();
            rig.Render();
            var pages = new HashSet<string>();
            foreach (var image in rig.Safe.GetComponentsInChildren<UnityEngine.UI.Image>(false))
                if (image.sprite != null && image.mainTexture != null && image.mainTexture.name.Contains("U1Glaze"))
                    pages.Add(image.mainTexture.name);
            Assert.AreEqual(1, pages.Count, "сторінки атласу U1Glaze: " + string.Join(", ", pages));
        }

        private static void Awaken(Component component) =>
            component.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(component, null);

        private static int CoralButtons(RunScreenRig rig)
        {
            var count = 0;
            foreach (var button in rig.Safe.GetComponentsInChildren<CandyButton>(false))
                if (button.CurrentTone == CandyButton.Tone.Coral && button.isActiveAndEnabled)
                    count++;
            return count;
        }

        private static IEnumerable<RectTransform> ShownDialogPanels(RunScreenRig rig)
        {
            foreach (var dialog in rig.Safe.GetComponentsInChildren<DialogView>(false))
                if (dialog.transform.Find("Panel") is RectTransform panel)
                    yield return panel;
                else if (dialog.transform.Find("Fit/Panel") is RectTransform fitted)
                    yield return fitted;
            var completion = rig.Screen.PreviewCompletionCard;
            if (completion != null && completion.IsShown && completion.IsCardVisible && completion.transform.Find("Card") is RectTransform card)
                yield return card;
        }

        private static void AssertInsideSafe(RunScreenRig rig, RectTransform panel, string what)
        {
            var safe = Bounds(rig.Safe);
            var box = Bounds(panel);
            var tolerance = safe.width * 0.002f;
            Assert.GreaterOrEqual(box.yMin, safe.yMin - tolerance, $"{what}: виходить за низ");
            Assert.LessOrEqual(box.yMax, safe.yMax + tolerance, $"{what}: виходить за верх");
            Assert.GreaterOrEqual(box.xMin, safe.xMin - tolerance, $"{what}: виходить ліворуч");
            Assert.LessOrEqual(box.xMax, safe.xMax + tolerance, $"{what}: виходить праворуч");
        }

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
