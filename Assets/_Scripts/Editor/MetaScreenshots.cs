using System.Collections.Generic;
using System.IO;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.UI;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Знімки екранів метагри з редактора, без Play Mode: п'ять роздільностей (<see cref="ScreenRigBase.Devices"/>)
    /// × стани хаба, галактики, планети, колекції, налаштувань, профілю й рейтингів, плюс сторінки вітрини
    /// компонентів U1Glaze. Меню: Ink Flow → Debug → Capture Meta Screenshots.
    /// Пише PNG у docs/screenshots/meta/. Для планети з усіма слотами логує оцінку викликів
    /// малювання (бюджет iPhone SE ≤ 35, архідок §12); перевищення в batch — код виходу 1.
    /// Batch: Unity -batchmode -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.MetaScreenshots.BuildAndCapture -quit
    /// </summary>
    public static class MetaScreenshots
    {
        private const string OutputFolder = "docs/screenshots/meta";

        /// <summary>Бюджет викликів малювання на екран (архідок §12).</summary>
        public const int DrawCallBudget = 35;

        public static readonly string[] States =
        {
            "hub",
            "galaxy-fresh", "galaxy-mid", "galaxy-second",
            "planet-empty", "planet-half", "planet-full", "planet-sheet",
            "collection-browse", "collection-pick", "collection-empty",
            "settings", "settings-run",
            "profile-fresh", "profile",
            "rankings", "rankings-offline", "galaxy-visitor"
        };

        /// <summary>Екрани, для яких автоперевірка (переповнення написів, тапи ≥ 44) — помилка, а не попередження: ті, що вже на U1Glaze.</summary>
        public static readonly string[] AuditEnforced = { "ComponentsShowcase" };

        /// <summary>Лише вітрина компонентів (крок 0 U1Glaze): збірка + знімки на всіх пристроях.</summary>
        public static void BuildAndCaptureComponents()
        {
            BuildComponentsScreen.Build();
            if (!MetaScreenRig<ScreenBase>.Available("ComponentsShowcase"))
            {
                Debug.LogError("[InkFlow] Немає префаба ComponentsShowcase — вітрину не зібрано.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                CaptureComponents(device, audit, null);
            AssetDatabase.Refresh();
            Finish(overBudget, audit);
        }

        /// <summary>
        /// Вітрина компонентів (S00Components) — сторінками прокрутки <c>components-N</c>. На двох пристроях
        /// контрольної точки (402×874 і 375×667) — УСІ сторінки, щоб у знімках було видно кожен компонент
        /// (спершу знімались три фіксовані, і діалог, рамки рідкості й навігація в кадр не потрапляли); на решті —
        /// лише перша, для масштабу. Автоперевірка — по всьому дереву вітрини, на кожному пристрої.
        /// Старі знімки сторінок, яких тепер немає, видаляються.
        /// </summary>
        private static void CaptureComponents(ScreenRigBase.Device device, List<string> audit, List<string>? written)
        {
            if (!MetaScreenRig<ScreenBase>.Available("ComponentsShowcase"))
                return;
            using var rig = MetaScreenRig<ComponentsShowcase>.Create(device, "ComponentsShowcase");
            rig.Enter(null, ScreenArgs.Empty);
            Canvas.ForceUpdateCanvases();
            var distinct = rig.EstimateBatches(out var runs, out var graphics);
            Debug.Log($"[InkFlow] {device.Name} / components: графік {graphics}, різних пар матеріал+текстура {distinct}, змін пари в порядку обходу {runs}.");
            Audit(rig, "ComponentsShowcase", device.Name, "components", audit);

            var checkpoint = device.Name == ScreenRigBase.Mockup.Name || device.Name == ScreenRigBase.Small.Name;
            var pages = checkpoint ? rig.Screen.Pages : 1;
            for (var page = 0; page < pages; page++)
            {
                var path = Path.Combine(OutputFolder, $"{device.Name}_components-{page + 1}.png");
                rig.Screen.PreviewPage(page);
                rig.SavePng(path);
                written?.Add(path);
            }
            for (var stale = pages + 1; ; stale++)
            {
                var path = Path.GetFullPath(Path.Combine(OutputFolder, $"{device.Name}_components-{stale}.png"));
                if (!File.Exists(path))
                    break;
                File.Delete(path);
            }
        }

        /// <summary>
        /// Batch: лише хаб на всіх пристроях (стан «hub») — швидка перевірка головного екрана без повного
        /// проходу метагри. Хаб ще старого стилю, тож автоперевірка для нього — попередження, не помилка.
        /// </summary>
        public static void BuildAndCaptureHub()
        {
            BuildHubScreen.Build();
            if (!MetaScreenRig<ScreenBase>.Available("HubScreen"))
            {
                Debug.LogError("[InkFlow] Немає префаба HubScreen — хаб не зібрано.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                Capture(device, "hub", Path.Combine(OutputFolder, $"{device.Name}_hub.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Finish(overBudget, audit);
        }

        private static void Finish(List<string> overBudget, List<string> audit)
        {
            var failed = false;
            if (overBudget.Count > 0)
            {
                Debug.LogError($"[InkFlow] Бюджет викликів малювання ({DrawCallBudget}) перевищено:\n  " + string.Join("\n  ", overBudget));
                failed = true;
            }
            if (audit.Count > 0)
            {
                Debug.LogError($"[InkFlow] Автоперевірка (переповнені написи, тапи < 44):\n  " + string.Join("\n  ", audit));
                failed = true;
            }
            if (failed && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        /// <summary>Автоперевірка екрана в стенді: для екранів із <see cref="AuditEnforced"/> — у список помилок, для решти — попередження.</summary>
        private static void Audit(ScreenRigBase rig, string prefabName, string device, string state, List<string> audit)
        {
            var problems = UiAudit.All(rig.Safe);
            if (problems.Count == 0)
                return;
            var lines = string.Join("\n  ", problems);
            if (System.Array.IndexOf(AuditEnforced, prefabName) >= 0)
                audit.Add($"{device} / {state}:\n  {lines}");
            else
                Debug.LogWarning($"[InkFlow] {device} / {state} (ще не на U1Glaze): \n  {lines}");
        }

        public static void BuildAndCapture()
        {
            RefreshPictureLibrary.Refresh();
            BuildHubScreen.Build();
            BuildGalaxyScreen.Build();
            BuildPlanetScreen.Build();
            BuildCollectionScreen.Build();
            BuildSettingsScreen.Build();
            BuildProfileScreen.Build();
            BuildRankingsScreen.Build();
            BuildComponentsScreen.Build();
            CaptureAll();
        }

        [MenuItem("Ink Flow/Debug/Capture Meta Screenshots")]
        public static void CaptureAll()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;
            foreach (var name in new[] { "HubScreen", "GalaxyScreen", "PlanetScreen", "CollectionScreen", "SettingsScreen", "ProfileScreen", "RankingsScreen" })
                if (!MetaScreenRig<ScreenBase>.Available(name))
                {
                    Debug.LogError($"[InkFlow] Знімки метагри: немає {MetaScreenRig<ScreenBase>.PrefabPathOf(name)} — спершу Build {name}.");
                    return;
                }

            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var written = new List<string>();
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in States)
                {
                    var path = Path.Combine(OutputFolder, $"{device.Name}_{state}.png");
                    Capture(device, state, path, overBudget, audit);
                    written.Add(path);
                }
            foreach (var device in ScreenRigBase.Devices)
                CaptureComponents(device, audit, written);

            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки метагри ({written.Count}) у {OutputFolder}:\n  " + string.Join("\n  ", written));
            Finish(overBudget, audit);
        }

        private static void Capture(ScreenRigBase.Device device, string state, string path, List<string> overBudget, List<string> audit)
        {
            switch (state)
            {
                case "hub":
                {
                    // Головний: аватар, нік, нафта (без «+»), лого, одна картка «Нескінченний», три вкладки.
                    using var rig = MetaScreenRig<HubScreen>.Create(device, "HubScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    rig.Enter(player, ScreenArgs.Empty);
                    Audit(rig, "HubScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "galaxy-fresh":
                case "galaxy-mid":
                case "galaxy-second":
                {
                    using var rig = MetaScreenRig<GalaxyScreen>.Create(device, "GalaxyScreen");
                    var player = rig.NewPlayer();
                    if (state == "galaxy-mid")
                    {
                        for (var p = 0; p < 3; p++) rig.FillPlanet(player, p, pictureOffset: p * 7);
                        rig.FillPlanet(player, 3, count: 3, pictureOffset: 30);
                    }
                    else if (state == "galaxy-second")
                        for (var p = 0; p < player.Layout.Planets.Count; p++) rig.FillPlanet(player, p, pictureOffset: p * 9);
                    rig.Enter(player, GalaxyArgs.Own);
                    rig.SavePng(path);
                    break;
                }
                case "planet-empty":
                case "planet-half":
                case "planet-full":
                case "planet-sheet":
                {
                    using var rig = MetaScreenRig<PlanetScreen>.Create(device, "PlanetScreen");
                    var player = rig.NewPlayer();
                    // Повна планета — фінальна з дванадцятьма слотами: для неї треба ожити всім попереднім.
                    var index = state == "planet-full" ? player.Layout.Planets.Count - 1 : 0;
                    if (state == "planet-full")
                        for (var p = 0; p < player.Layout.Planets.Count; p++) rig.FillPlanet(player, p, pictureOffset: p * 11);
                    else if (state == "planet-half" || state == "planet-sheet")
                        rig.FillPlanet(player, 0, count: 2, pictureOffset: 5);
                    // Повна планета — у ПЕРШІЙ галактиці: після її заповнення поточною стала друга.
                    rig.Enter(player, new PlanetArgs(0, index));
                    if (state == "planet-sheet")
                        rig.Screen.PreviewSheet(0);
                    if (state == "planet-full")
                    {
                        var distinct = rig.EstimateBatches(out var runs, out var graphics);
                        Debug.Log($"[InkFlow] {device.Name} / {state}: графік {graphics}, різних пар матеріал+текстура {distinct}, змін пари в порядку обходу {runs}.");
                        if (runs > DrawCallBudget)
                            overBudget.Add($"{device.Name} / {state}: {runs} змін пари (різних {distinct})");
                    }
                    rig.SavePng(path);
                    break;
                }
                case "settings":
                case "settings-run":
                {
                    // З хаба (без «Заново») і з забігу (з «Заново»); посилання порожні — рядки «скоро».
                    using var rig = MetaScreenRig<SettingsScreen>.Create(device, "SettingsScreen");
                    var player = rig.NewPlayer();
                    rig.Screen.BindLinks(AppLinks.Default);
                    rig.Enter(player, new SettingsArgs(inRun: state == "settings-run"));
                    rig.SavePng(path);
                    break;
                }
                case "rankings":
                case "rankings-offline":
                {
                    // Таблиця «навмання» з обраним аватаром на картці «Ти» і той самий екран без мережі.
                    using var rig = MetaScreenRig<RankingsScreen>.Create(device, "RankingsScreen");
                    var player = rig.NewPlayer();
                    player.SetAvatar(3);
                    rig.FillPlanet(player, 0);
                    var leaderboards = new InkFlow.Platform.FakeLeaderboards { Offline = state == "rankings-offline" };
                    leaderboards.Submit(InkFlow.Core.RankMetric.Planets, InkFlow.Core.RankPeriod.Week, 1);
                    leaderboards.Submit(InkFlow.Core.RankMetric.Planets, InkFlow.Core.RankPeriod.AllTime, 1);
                    rig.Screen.BindServices(leaderboards, new InkFlow.Platform.FakeShowcase(), new InkFlow.Platform.FakeIdentity());
                    rig.Enter(player, ScreenArgs.Empty);
                    rig.SavePng(path);
                    break;
                }
                case "galaxy-visitor":
                {
                    // Чужа галактика з вітрини: планети по порядку, картка гостя з вітринною картинкою.
                    using var rig = MetaScreenRig<GalaxyScreen>.Create(device, "GalaxyScreen");
                    var player = rig.NewPlayer();
                    var layout = player.Layout;
                    var ids = new List<string>();
                    var slots = new List<int>();
                    foreach (var planet in layout.Planets) { ids.Add(planet.Id); slots.Add(planet.Slots); }
                    var mock = InkFlow.Core.MockRankings.ShowcaseFor(InkFlow.Core.MockRankings.PlayerIdOf(1), ids, slots);
                    var visitor = new InkFlow.Core.PublicShowcase(mock.PlayerId, mock.Nick, mock.AvatarId, false, rig.Library[22 % rig.Library.Count].Id,
                        mock.PlanetsDone, mock.GalaxiesDone, mock.Galaxy, mock.Slots, mock.UpdatedUtc);
                    rig.Enter(player, GalaxyArgs.ForVisitor(visitor));
                    rig.SavePng(path);
                    break;
                }
                case "profile-fresh":
                case "profile":
                {
                    // Новий гравець (вітрини ще немає, кнопка спить) і гравець із колекцією, обраним аватаром і вітриною.
                    using var rig = MetaScreenRig<ProfileScreen>.Create(device, "ProfileScreen");
                    var player = rig.NewPlayer();
                    if (state == "profile")
                    {
                        for (var i = 0; i < 7; i++)
                            player.CollectPicture(rig.Library[(i * 11) % rig.Library.Count].Id, System.DateTime.UtcNow);
                        player.SetAvatar(3);
                        player.SetShowcasePicture(rig.Library[22 % rig.Library.Count].Id);
                    }
                    rig.Enter(player, ScreenArgs.Empty);
                    rig.SavePng(path);
                    break;
                }
                default:
                {
                    using var rig = MetaScreenRig<CollectionScreen>.Create(device, "CollectionScreen");
                    var player = rig.NewPlayer();
                    if (state != "collection-empty")
                    {
                        for (var i = 0; i < 20; i++)
                            player.CollectPicture(rig.Library[(i * 5) % rig.Library.Count].Id, System.DateTime.UtcNow);
                        player.CollectPicture(rig.Library[0].Id, System.DateTime.UtcNow);
                        player.TryPlaceInSlot(0, player.Layout.Planets[0].Id, 0, rig.Library[0].Id, System.DateTime.UtcNow);
                    }
                    var args = state == "collection-pick"
                        ? new CollectionArgs(0, player.Layout.Planets[0].Id, 1)
                        : new CollectionArgs();
                    rig.Enter(player, args);
                    rig.SavePng(path);
                    break;
                }
            }
        }
    }
}
