using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.Tests.UI
{
    /// <summary>
    /// Головний екран U1Glaze (крок 1, S01Home): вміщається без переповнень і дрібних тапів на обох пристроях
    /// контрольної точки, кнопки кажуть роутеру те, що мають, кнопка профілю накриває аватар (стара вада: тап по
    /// кулі не відкривав профіль), бейджі — з відмінками й нерозривними пробілами, довгий нік обрізається «…»,
    /// а 999 999 999 нафти не переноситься; мініатюра — картинка забігу, інакше вітринна, інакше схована.
    /// </summary>
    public sealed class HubScreenTests
    {
        private static MetaScreenRig<HubScreen> Rig(ScreenRigBase.Device device)
        {
            Assert.IsTrue(MetaScreenRig<ScreenBase>.Available("HubScreen"), "спершу Build Hub Screen");
            return MetaScreenRig<HubScreen>.Create(device, "HubScreen");
        }

        private static PlayerState Collected(MetaScreenRig<HubScreen> rig, int count)
        {
            var player = rig.NewPlayer();
            var now = System.DateTime.UtcNow;
            for (var i = 0; i < count; i++)
                player.CollectPicture(rig.Library[i % rig.Library.Count].Id, now);
            return player;
        }

        [Test]
        public void Hub_FitsWithoutOverflowOrSmallTaps_OnBothCheckpointDevices()
        {
            foreach (var device in new[] { ScreenRigBase.Mockup, ScreenRigBase.Small })
            {
                using var rig = Rig(device);
                var player = Collected(rig, 14);
                player.Progress.EndlessRecord = 7980;
                player.SetShowcasePicture(rig.Library[0].Id);
                player.Wallet.Add(999_999_999, RewardSource.Debug);
                player.SetNick("Космічний_Маляр2", NickRules.Default, out _);
                rig.Enter(player, ScreenArgs.Empty);
                rig.Render();

                var problems = UiAudit.All(rig.Safe);
                Assert.AreEqual(0, problems.Count, $"{device.Name}:\n" + string.Join("\n", problems));
                Assert.AreEqual(new[] { NavBar.Play, NavBar.Galaxy, NavBar.Collection, NavBar.Ranks }, rig.Screen.PreviewNavBar!.PreviewIds);
                Assert.AreEqual(NavBar.Play, rig.Screen.PreviewNavBar.ActiveId, "на головному активна «Грати»");
                // Навігація — рівно 76 px макета: гнучкою по висоті вона ділила залишок із заставкою й росла до пів екрана.
                var navHeight = ((RectTransform)rig.Screen.PreviewNavBar.transform).rect.height;
                Assert.AreEqual(76f * InkFlow.Style.DesignSystem.MockupToReference, navHeight, 1.5f, $"{device.Name}: висота навігації");
            }
        }

        [Test]
        public void Buttons_TellTheRouter_PlayTabsAndProfile()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            rig.Enter(rig.NewPlayer(), ScreenArgs.Empty);
            // У Edit Mode рушій не кличе Awake звичайного MonoBehaviour — підписки кнопок робимо, як у грі, самі
            // (спершу навігація, потім екран, що підписується на неї).
            foreach (var bar in rig.Safe.GetComponentsInChildren<NavBar>(true))
                Awaken(bar);
            Awaken(rig.Screen);
            var played = 0;
            var profile = 0;
            var tabs = new List<string>();
            rig.Screen.EndlessRequested += () => played++;
            rig.Screen.ProfileRequested += () => profile++;
            rig.Screen.TabRequested += id => tabs.Add(id);

            rig.Screen.PreviewPlay();
            rig.Screen.PreviewProfile();
            foreach (var id in new[] { NavBar.Galaxy, NavBar.Collection, NavBar.Ranks, NavBar.Play })
                rig.Screen.PreviewNavBar!.PreviewTap(id);

            Assert.AreEqual(1, played, "«Грати» — EndlessRequested");
            Assert.AreEqual(1, profile, "блок аватара з ніком — ProfileRequested");
            Assert.AreEqual(new[] { NavBar.Galaxy, NavBar.Collection, NavBar.Ranks, NavBar.Play }, tabs.ToArray());
        }

        [Test]
        public void ProfileButton_CoversTheAvatar_SoATapOnTheBallOpensProfile()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            rig.Enter(rig.NewPlayer(), ScreenArgs.Empty);
            rig.Render();
            var avatar = rig.Screen.PreviewAvatar!;
            var button = rig.Screen.PreviewProfileButton!;
            var corners = new Vector3[4];
            avatar.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = button.InverseTransformPoint(corner);
                var inside = local.x >= button.rect.xMin - 0.5f && local.x <= button.rect.xMax + 0.5f &&
                             local.y >= button.rect.yMin - 0.5f && local.y <= button.rect.yMax + 0.5f;
                Assert.IsTrue(inside,
                    $"кут аватара {local} поза кнопкою профілю {button.rect} — тап по кулі не відкриє профіль");
            }
        }

        [Test]
        public void Badges_UseUkrainianPlurals_AndANonBreakingRecord()
        {
            using var rig = Rig(ScreenRigBase.Mockup);
            foreach (var (count, expected) in new[] { (0, "0 КАРТИНОК"), (1, "1 КАРТИНКА"), (2, "2 КАРТИНКИ"), (5, "5 КАРТИНОК"), (21, "21 КАРТИНКА") })
            {
                var player = Collected(rig, count);
                player.Progress.EndlessRecord = 7980;
                rig.Enter(player, ScreenArgs.Empty);
                Assert.AreEqual(expected, rig.Screen.PreviewPicturesBadge, $"{count} різних картинок");
                Assert.AreEqual("РЕКОРД 7 980", rig.Screen.PreviewRecordBadge, "розряди — нерозривний пробіл, без ком культури");
            }
        }

        [Test]
        public void LongNick_IsCutWithEllipsis_WhileTheOilCapsuleKeepsTheWholeNumber()
        {
            using var rig = Rig(ScreenRigBase.Small);
            var player = rig.NewPlayer();
            player.SetNick("Космічний_Маляр2", NickRules.Default, out _);
            player.Wallet.Add(999_999_999, RewardSource.Debug);
            rig.Enter(player, ScreenArgs.Empty);
            rig.Render();

            var nick = rig.Screen.PreviewNick!;
            nick.ForceMeshUpdate();
            Assert.IsTrue(nick.isTextTruncated, "довгий нік на 375 — обрізаний «…», а не переповнений");
            var amount = rig.Safe.GetComponentsInChildren<CurrencyWidget>()[0].GetComponentInChildren<TMPro.TMP_Text>();
            amount.ForceMeshUpdate();
            Assert.AreEqual(1, amount.textInfo.lineCount, "999 999 999 — один рядок");
            Assert.IsFalse(amount.isTextTruncated, "число в капсулі не обрізається — капсула росте");
            StringAssert.Contains("999 999 999", amount.text);
        }

        [Test]
        public void Thumbnail_ShowsTheRunPicture_ElseTheShowcase_ElseNothing()
        {
            using var rig = Rig(ScreenRigBase.Mockup);

            var fresh = rig.NewPlayer();
            rig.Enter(fresh, ScreenArgs.Empty);
            Assert.IsNull(rig.Screen.PreviewThumbnail, "ні забігу, ні вітрини — мініатюри немає");

            var withShowcase = Collected(rig, 3);
            withShowcase.SetShowcasePicture(rig.Library[2].Id);
            rig.Enter(withShowcase, ScreenArgs.Empty);
            Assert.AreEqual(rig.Library[2].Id, rig.Screen.PreviewThumbnail?.Id, "без забігу — вітринна картинка");

            var session = new RunSession(withShowcase.Balance, PieceCatalogData.Default, new XorShiftRandom(11u), withShowcase.Library);
            withShowcase.SaveRun(session);
            Assert.IsNotNull(withShowcase.SavedRun, "зліпок забігу записано");
            rig.Enter(withShowcase, ScreenArgs.Empty);
            Assert.AreEqual(session.Picture.Picture.Id, rig.Screen.PreviewThumbnail?.Id, "перерваний забіг важливіший за вітрину");
        }

        [Test]
        public void SetRootWith_PutsTheRootUnderTheTab_AndTabsDoNotGrowTheStack()
        {
            // Тестова збірка — лише редактор, а з неї Unity не дає повісити власний MonoBehaviour; тож екрани —
            // справжні рантайм-екрани без побічних дій на вході (вітрина). Вхід = активний, вихід = згаслий.
            var go = new GameObject("Stack", typeof(RectTransform));
            try
            {
                var stack = go.AddComponent<NavigationStack>();
                var hub = Screen(go, "Hub");
                var galaxy = Screen(go, "Galaxy");
                var collection = Screen(go, "Collection");
                stack.SetRoot(hub);
                Assert.IsTrue(hub.gameObject.activeSelf);

                stack.SetRootWith(hub, galaxy);
                Assert.AreEqual(2, stack.Depth, "хаб → вкладка");
                Assert.AreSame(galaxy, stack.Current);
                Assert.IsFalse(hub.gameObject.activeSelf, "корінь під вкладкою лежить згаслим, у нього не заходили");
                Assert.IsTrue(galaxy.gameObject.activeSelf);

                stack.SetRootWith(hub, collection);
                Assert.AreEqual(2, stack.Depth, "перемикання вкладок не роздуває стек");
                Assert.IsFalse(galaxy.gameObject.activeSelf, "попередня вкладка вийшла");
                Assert.IsTrue(collection.gameObject.activeSelf);

                stack.Pop();
                Assert.AreSame(hub, stack.Current, "«назад» з вкладки — на головний");
                Assert.IsTrue(hub.gameObject.activeSelf);
                Assert.AreEqual(1, stack.Depth);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BatchSimulation_BatchesNonOverlappingNeighbours_AndSplitsOverlappingOnes()
        {
            // Алгоритм UGUI: сусіди, що не перекриваються, батчаться навіть через чужий матеріал між ними;
            // перекриття змушує малювати по черзі.
            var root = new GameObject("Batches", typeof(RectTransform), typeof(Canvas));
            var other = new Material(Shader.Find("UI/Default"));
            try
            {
                Image Square(string name, float x, Material? material)
                {
                    var go = new GameObject(name, typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    var image = go.AddComponent<Image>();
                    image.material = material;
                    image.rectTransform.anchoredPosition = new Vector2(x, 0f);
                    image.rectTransform.sizeDelta = new Vector2(100f, 100f);
                    return image;
                }
                Square("A1", 0f, null);
                Square("B", 200f, other);
                Square("A2", 400f, null);
                Assert.AreEqual(2, MetaScreenRig<ScreenBase>.SimulateBatches(root.transform), "A · B · A без перекриття — два виклики");

                Object.DestroyImmediate(root.transform.GetChild(2).gameObject);
                Square("A2", 250f, null); // тепер A2 лежить на B
                Assert.AreEqual(3, MetaScreenRig<ScreenBase>.SimulateBatches(root.transform), "A під B під A — три виклики");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(other);
            }
        }

        private static void Awaken(Component component) =>
            component.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(component, null);

        private static ScreenBase Screen(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<ComponentsShowcase>();
        }
    }
}
