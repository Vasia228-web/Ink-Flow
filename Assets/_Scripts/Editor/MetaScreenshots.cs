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
            "hub", "hub-long", "hub-fresh",
            "galaxy-fresh", "galaxy-mid", "galaxy-done", "galaxy-locked", "galaxy-complete", "galaxy-reveal", "galaxy-second",
            "planet-sphere-4", "planet-sphere-8", "planet-sphere-rotated", "planet-sphere-sheet", "planet-sphere-alive",
            "planet-horizon-12", "planet-horizon-20", "planet-horizon-sheet", "planet-horizon-alive", "planet-showcase",
            "planet-visitor-sphere", "planet-visitor-horizon",
            "collection-browse", "collection-pick", "collection-filter-empty", "collection-empty", "collection-showcase",
            "settings", "settings-run", "settings-links",
            "profile-fresh", "profile", "profile-nick", "profile-nick-error", "profile-nick-max",
            "rankings", "rankings-alltime", "rankings-galaxies", "rankings-offline", "galaxy-visitor"
        };

        /// <summary>Екрани, для яких автоперевірка (переповнення написів, тапи ≥ 44) — помилка, а не попередження: ті, що вже на U1Glaze.</summary>
        public static readonly string[] AuditEnforced = { "ComponentsShowcase", "HubScreen", "GalaxyScreen", "PlanetScreen", "CollectionScreen", "RankingsScreen", "ProfileScreen", "SettingsScreen" };

        /// <summary>Стани налаштувань (крок 9 U1Glaze): S11_01 з хаба, з забігу («Почати заново»), з адресами й двома мовами.</summary>
        public static readonly string[] SettingsStates = { "settings", "settings-run", "settings-links" };

        /// <summary>Знімки налаштувань із уже зібраного префаба (у відкритому редакторі — через MCP).</summary>
        [MenuItem("Ink Flow/Debug/Capture Settings Screenshots")]
        public static void CaptureSettings()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in SettingsStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки налаштувань: {SettingsStates.Length} станів × {ScreenRigBase.Devices.Length} пристроїв у {OutputFolder}.");
            Finish(overBudget, audit);
        }

        /// <summary>Стани профілю (крок 8 U1Glaze): S10_01, новий гравець, діалог ніка S10_02 / 03 (заборонене) / 04 (межа).</summary>
        public static readonly string[] ProfileStates = { "profile", "profile-fresh", "profile-nick", "profile-nick-error", "profile-nick-max" };

        /// <summary>Знімки профілю з уже зібраного префаба (у відкритому редакторі — через MCP).</summary>
        [MenuItem("Ink Flow/Debug/Capture Profile Screenshots")]
        public static void CaptureProfile()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in ProfileStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки профілю: {ProfileStates.Length} станів × {ScreenRigBase.Devices.Length} пристроїв у {OutputFolder}.");
            Finish(overBudget, audit);
        }

        /// <summary>Стани «Ліги» (крок 7 U1Glaze): S08_01 (планети · тиждень), S08_02 (за весь час), S08_03 (галактики), S08_04 (без мережі).</summary>
        public static readonly string[] RankingsStates = { "rankings", "rankings-alltime", "rankings-galaxies", "rankings-offline" };

        /// <summary>Знімки рейтингів із уже зібраного префаба (у відкритому редакторі — через MCP).</summary>
        [MenuItem("Ink Flow/Debug/Capture Rankings Screenshots")]
        public static void CaptureRankings()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in RankingsStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки рейтингів: {RankingsStates.Length} станів × {ScreenRigBase.Devices.Length} пристроїв у {OutputFolder}.");
            Finish(overBudget, audit);
        }

        /// <summary>
        /// Стани колекції (крок 5 U1Glaze): S06_01 (14 різних · 15 усього, копії в слотах), S06_02 (вибір у комірку з
        /// обраною картинкою й картинкою, усі копії якої в слотах), S06_03 (порожній фільтр «Космічна»), новий гравець,
        /// вибір вітринної картинки профілю.
        /// </summary>
        public static readonly string[] CollectionStates =
        {
            "collection-browse", "collection-pick", "collection-filter-empty", "collection-empty", "collection-showcase"
        };

        /// <summary>Картинки макета S06 (за id): довгі назви з м'яким переносом і всі шість рідкостей.</summary>
        private static readonly string[] MockupCollection =
        {
            "trice", "cactus_hat", "cupcake", "cherry", "robot", "unicorn", "flower", "heart", "gem_cosmic",
            "ghost_shy", "alien", "dragon", "fish", "kraken"
        };

        /// <summary>Batch: лише колекція на всіх пристроях з автоперевіркою й бюджетом викликів — КТ кроку 5.</summary>
        public static void BuildAndCaptureCollection()
        {
            RefreshPictureLibrary.Refresh();
            BuildCollectionScreen.Build();
            if (!MetaScreenRig<ScreenBase>.Available("CollectionScreen"))
            {
                Debug.LogError("[InkFlow] Немає префаба CollectionScreen — колекцію не зібрано.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            CaptureCollection();
        }

        /// <summary>Знімки колекції з уже зібраного префаба (у відкритому редакторі — через MCP).</summary>
        [MenuItem("Ink Flow/Debug/Capture Collection Screenshots")]
        public static void CaptureCollection()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in CollectionStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки колекції: {CollectionStates.Length} станів × {ScreenRigBase.Devices.Length} пристроїв у {OutputFolder}.");
            Finish(overBudget, audit);
        }

        /// <summary>Стани галактики (крок 3 U1Glaze): S03_01 … S03_05, нова порожня галактика зі стрілками, гість S09_01.</summary>
        public static readonly string[] GalaxyStates =
        {
            "galaxy-fresh", "galaxy-mid", "galaxy-done", "galaxy-locked", "galaxy-complete", "galaxy-reveal", "galaxy-second", "galaxy-visitor"
        };

        /// <summary>
        /// Стани планети (крок 4 U1Glaze): «Сфера» S04_01 … S04_05 (Кріос, seed 3, поворот як на макеті), «Горизонт»
        /// S05_01 … S05_04 (Рудокам, 12 і 20 комірок) і вітрина завершеної галактики (пояснення замість дії).
        /// </summary>
        public static readonly string[] PlanetStates =
        {
            "planet-sphere-4", "planet-sphere-8", "planet-sphere-rotated", "planet-sphere-sheet", "planet-sphere-alive",
            "planet-horizon-12", "planet-horizon-20", "planet-horizon-sheet", "planet-horizon-alive", "planet-showcase",
            "planet-visitor-sphere", "planet-visitor-horizon"
        };

        /// <summary>Знімки планети з уже зібраного префаба (у відкритому редакторі — через MCP).</summary>
        [MenuItem("Ink Flow/Debug/Capture Planet Screenshots")]
        public static void CapturePlanet()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in PlanetStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            foreach (var device in ScreenRigBase.Devices)
                Capture(device, "galaxy-visitor", Path.Combine(OutputFolder, $"{device.Name}_galaxy-visitor.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки планети: {PlanetStates.Length + 1} станів × {ScreenRigBase.Devices.Length} пристроїв у {OutputFolder}.");
            Finish(overBudget, audit);
        }

        /// <summary>
        /// Вітрина гравця «Вега» для S09: у галактиці III «Сфера» на 8 комірок (6 заповнено) і «Горизонт» на 12 (9) —
        /// картинки з бібліотеки, як на макетах (кактус, квітка, НЛО, капкейк, ракета, черешні, роботик, серце…).
        /// </summary>
        private static PublicShowcase MockupVisitor(GalaxyLayout layout, PictureLibrary library, out int spherePlanet, out int horizonPlanet)
        {
            spherePlanet = horizonPlanet = 0;
            for (var i = 0; i < layout.Planets.Count; i++)
            {
                if (layout.Planets[i].Slots == 8 && spherePlanet == 0) spherePlanet = i;
                if (layout.Planets[i].Slots == 12) horizonPlanet = i;
            }
            var pictures = new[] { "cactus_hat", "flower", "ufo", "cupcake", "rocket", "cherry", "robot", "heart", "trice", "fish", "unicorn", "alien" };
            var slots = new List<ShowcaseSlot>();
            void Fill(int planet, int count)
            {
                for (var s = 0; s < count; s++)
                {
                    var id = pictures[s % pictures.Length];
                    if (library.Find(id) == null) id = library[s % library.Count].Id;
                    slots.Add(new ShowcaseSlot(layout.Planets[planet].Id, s, id));
                }
            }
            for (var p = 0; p < layout.Planets.Count; p++)
                if (p != spherePlanet && p != horizonPlanet && p < horizonPlanet)
                    Fill(p, layout.Planets[p].Slots);
            Fill(spherePlanet, 6);
            Fill(horizonPlanet, 9);
            return new PublicShowcase("mock-0", "Вега", 0, false, null, horizonPlanet, 2, 2, slots, string.Empty);
        }

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
        /// Batch: лише хаб на всіх пристроях (стани hub / hub-long / hub-fresh — S01Home_01, S01Home_02, новий
        /// гравець) — швидка перевірка головного екрана без повного проходу метагри. Хаб на U1Glaze, тож
        /// автоперевірка для нього — помилка batch (<see cref="AuditEnforced"/>).
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
                foreach (var state in new[] { "hub", "hub-long", "hub-fresh" })
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Finish(overBudget, audit);
        }

        /// <summary>
        /// Batch: лише галактика на всіх пристроях (<see cref="GalaxyStates"/>) з автоперевіркою й бюджетом викликів —
        /// контрольна точка кроку 3 без повного проходу метагри.
        /// </summary>
        public static void BuildAndCaptureGalaxy()
        {
            RefreshPictureLibrary.Refresh();
            BuildGalaxyScreen.Build();
            if (!MetaScreenRig<ScreenBase>.Available("GalaxyScreen"))
            {
                Debug.LogError("[InkFlow] Немає префаба GalaxyScreen — галактику не зібрано.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in GalaxyStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Finish(overBudget, audit);
        }

        /// <summary>
        /// Batch: лише планета на всіх пристроях (<see cref="PlanetStates"/>) з автоперевіркою й бюджетом викликів —
        /// контрольна точка кроку 4 без повного проходу метагри.
        /// </summary>
        public static void BuildAndCapturePlanet()
        {
            RefreshPictureLibrary.Refresh();
            BuildPlanetScreen.Build();
            if (!MetaScreenRig<ScreenBase>.Available("PlanetScreen"))
            {
                Debug.LogError("[InkFlow] Немає префаба PlanetScreen — планету не зібрано.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var overBudget = new List<string>();
            var audit = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in PlanetStates)
                    Capture(device, state, Path.Combine(OutputFolder, $"{device.Name}_{state}.png"), overBudget, audit);
            AssetDatabase.Refresh();
            Finish(overBudget, audit);
        }

        /// <summary>
        /// Галактика для знімків планети: перша планета — та, що на макеті (тип задає seed «Сфери»: Кріос — 3, як на
        /// S04), далі ще дві, щоб «ожила» мала наступну планету.
        /// </summary>
        public static GalaxyLayout MockupGalaxy(PlanetType type, string name, int slots) => new GalaxyLayout(new[]
        {
            new PlanetLayout(type, name, slots),
            new PlanetLayout(PlanetType.Rings, "Сатурнія", 8, hasRing: true),
            new PlanetLayout(PlanetType.Gas, "Вихор", 9),
        }, GalaxyLayout.Default.GalaxyNames);

        /// <summary>Виклики малювання (симуляція батчингу UGUI) у лог і в список перевищень.</summary>
        private static void Budget(ScreenRigBase rig, string device, string state, List<string> overBudget)
        {
            rig.Render();
            var batches = rig.SimulateBatches();
            Debug.Log($"[InkFlow] {device} / {state}: викликів за батчингом UGUI {batches}.");
            if (batches > DrawCallBudget)
                overBudget.Add($"{device} / {state}: {batches} викликів (батчинг UGUI) > {DrawCallBudget}");
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
                case "hub-long":
                case "hub-fresh":
                {
                    // Головний (S01Home): hub — 14 картинок, рекорд 7 980, вітринна Рідкісна в мініатюрі (як S01Home_01);
                    // hub-long — нік на межі правил і 999 999 999 нафти (S01Home_02); hub-fresh — новий гравець.
                    using var rig = MetaScreenRig<HubScreen>.Create(device, "HubScreen");
                    var player = rig.NewPlayer();
                    if (state != "hub-fresh")
                    {
                        var now = System.DateTime.UtcNow;
                        string? rare = null;
                        for (var i = 0; i < 14; i++)
                        {
                            var picture = rig.Library[(i * 7) % rig.Library.Count];
                            player.CollectPicture(picture.Id, now);
                            if (rare == null && picture.Rarity == InkFlow.Core.Rarity.Rare)
                                rare = picture.Id;
                        }
                        player.SetShowcasePicture(rare ?? rig.Library[0].Id);
                        player.Progress.EndlessRecord = 7980;
                        player.Wallet.Add(state == "hub-long" ? 999_999_999 : 1240, RewardSource.Debug);
                        if (state == "hub-long")
                            player.SetNick("Космічний_Маляр2", NickRules.Default, out _);
                    }
                    rig.Enter(player, ScreenArgs.Empty);
                    var distinct = rig.EstimateBatches(out var runs, out var graphics);
                    var batches = rig.SimulateBatches();
                    Debug.Log($"[InkFlow] {device.Name} / {state}: графік {graphics}, різних пар матеріал+текстура {distinct}, " +
                              $"змін пари в порядку обходу {runs}, викликів за батчингом UGUI {batches}.");
                    if (batches > DrawCallBudget)
                        overBudget.Add($"{device.Name} / {state}: {batches} викликів (батчинг UGUI) > {DrawCallBudget}");
                    Audit(rig, "HubScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "galaxy-fresh":
                case "galaxy-mid":
                case "galaxy-done":
                case "galaxy-locked":
                case "galaxy-complete":
                case "galaxy-reveal":
                case "galaxy-second":
                {
                    // S03_01 — три ожилі й поточна з трьома картинками (фокус на ній); S03_02/03 — той самий гравець,
                    // фокус на ожилій і на замкненій; S03_04/05 — щойно ожила остання планета галактики (діалог і
                    // нова галактика); galaxy-second — та сама нова галактика без святкування, зі стрілками циклів.
                    using var rig = MetaScreenRig<GalaxyScreen>.Create(device, "GalaxyScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    var everything = state == "galaxy-second" || state == "galaxy-complete" || state == "galaxy-reveal";
                    if (everything)
                        for (var p = 0; p < player.Layout.For(0).Planets.Count; p++) rig.FillPlanet(player, p, pictureOffset: p * 9);
                    else if (state != "galaxy-fresh")
                    {
                        for (var p = 0; p < 3; p++) rig.FillPlanet(player, p, pictureOffset: p * 7);
                        rig.FillPlanet(player, 3, count: 3, pictureOffset: 30);
                    }
                    var args = GalaxyArgs.OwnEntry();
                    rig.Enter(player, args);
                    if (state == "galaxy-done")
                        rig.Screen.PreviewSetFocus(2);
                    else if (state == "galaxy-locked")
                        rig.Screen.PreviewSetFocus(4);
                    else if (state == "galaxy-complete" || state == "galaxy-reveal")
                    {
                        rig.Screen.PreviewGalaxyJustCompleted(args);
                        if (state == "galaxy-reveal")
                            rig.Screen.PreviewNext();
                    }
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "GalaxyScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "planet-sphere-4":
                case "planet-sphere-8":
                case "planet-sphere-rotated":
                case "planet-sphere-sheet":
                case "planet-sphere-alive":
                case "planet-horizon-12":
                case "planet-horizon-20":
                case "planet-horizon-sheet":
                case "planet-horizon-alive":
                {
                    // Макети: «Сфера» — Кріос (seed 3) у тих самих поворотах, «Горизонт» — Рудокам; планета — перша в
                    // галактиці, тож відкрита й редагована.
                    var sphere = state.StartsWith("planet-sphere");
                    var slots = state == "planet-sphere-4" ? 4 : sphere ? 8 : state == "planet-horizon-20" ? 20 : 12;
                    var alive = state.EndsWith("-alive");
                    var filled = alive ? slots : state == "planet-sphere-4" ? 2 : sphere ? 5 : state == "planet-horizon-20" ? 13 : 7;
                    using var rig = MetaScreenRig<PlanetScreen>.Create(device, "PlanetScreen");
                    var player = rig.NewPlayer(MockupGalaxy(sphere ? PlanetType.Ice : PlanetType.Rocky, sphere ? "Кріос" : "Рудокам", slots));
                    player.Wallet.Add(1240, RewardSource.Debug);
                    rig.FillPlanet(player, 0, count: filled, pictureOffset: 3);
                    rig.Enter(player, new PlanetArgs(0, 0));
                    if (sphere)
                        rig.Screen.PreviewRotation(state switch
                        {
                            "planet-sphere-4" => 110f, "planet-sphere-8" => 195f, "planet-sphere-rotated" => 40f,
                            "planet-sphere-sheet" => 95f, _ => 150f,
                        });
                    if (state == "planet-sphere-sheet")
                        rig.Screen.PreviewSheet(2);
                    if (state == "planet-horizon-sheet")
                        rig.Screen.PreviewSheet(1);
                    if (alive)
                        rig.Screen.PreviewAlive();
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "PlanetScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "planet-visitor-sphere":
                case "planet-visitor-horizon":
                {
                    // Чужа планета (S09_02 «Сфера» 6 / 8, S09_03 «Горизонт» 9 / 12): лунки без «+», низ «Планета гравця …».
                    using var rig = MetaScreenRig<PlanetScreen>.Create(device, "PlanetScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    var visitor = MockupVisitor(player.Layout.For(0), rig.Library, out var sphere, out var horizon);
                    rig.Enter(player, PlanetArgs.ForVisitor(visitor, state == "planet-visitor-sphere" ? sphere : horizon));
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "PlanetScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "planet-showcase":
                {
                    // Завершена галактика — вітрина: фінальна планета (12 → «Горизонт»), дія вимкнена, підказка пояснює.
                    using var rig = MetaScreenRig<PlanetScreen>.Create(device, "PlanetScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    for (var p = 0; p < player.Layout.For(0).Planets.Count; p++)
                        rig.FillPlanet(player, p, pictureOffset: p * 11);
                    rig.Enter(player, new PlanetArgs(0, player.Layout.For(0).Planets.Count - 1));
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "PlanetScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "settings":
                case "settings-run":
                case "settings-links":
                {
                    // З хаба (без «Почати заново») і з забігу (з ним); посилання порожні — бейджі «Скоро»;
                    // «settings-links» — адреси й дві мови в конфігу: рядок «Мова» і «›» замість бейджів.
                    using var rig = MetaScreenRig<SettingsScreen>.Create(device, "SettingsScreen");
                    var player = rig.NewPlayer();
                    rig.Screen.BindLinks(state == "settings-links"
                        ? new AppLinks("https://example.com/privacy", "help@example.com", new[] { "uk", "en" })
                        : AppLinks.Default);
                    rig.Enter(player, new SettingsArgs(inRun: state == "settings-run"));
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "SettingsScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "rankings":
                case "rankings-alltime":
                case "rankings-galaxies":
                case "rankings-offline":
                {
                    // «Ліга» (S08): моковий світ із 18 гравців, картка «Ти» зі справжніми числами (1 ожила планета,
                    // аватар 4), гравець — за межами сторінки, тож «ще N до №M» бере сусіда окремо; без мережі — S08_04.
                    using var rig = MetaScreenRig<RankingsScreen>.Create(device, "RankingsScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    player.SetAvatar(3);
                    rig.FillPlanet(player, 0);
                    var leaderboards = new InkFlow.Platform.FakeLeaderboards { Offline = state == "rankings-offline" };
                    leaderboards.Submit(InkFlow.Core.RankMetric.Planets, InkFlow.Core.RankPeriod.Week, 1);
                    leaderboards.Submit(InkFlow.Core.RankMetric.Planets, InkFlow.Core.RankPeriod.AllTime, 1);
                    rig.Screen.BindServices(leaderboards, new InkFlow.Platform.FakeShowcase(), new InkFlow.Platform.FakeIdentity(), 12);
                    rig.Enter(player, ScreenArgs.Empty);
                    if (state == "rankings-alltime")
                        rig.Screen.PreviewSetPeriod(InkFlow.Core.RankPeriod.AllTime);
                    if (state == "rankings-galaxies")
                    {
                        rig.Screen.PreviewSetMetric(InkFlow.Core.RankMetric.Galaxies);
                        rig.Screen.PreviewSetPeriod(InkFlow.Core.RankPeriod.AllTime);
                    }
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "RankingsScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "galaxy-visitor":
                {
                    // Чужа галактика з вітрини (S09_01): планети по порядку, картка гостя під шапкою, «‹» замість назви.
                    using var rig = MetaScreenRig<GalaxyScreen>.Create(device, "GalaxyScreen");
                    var player = rig.NewPlayer();
                    var layout = player.Layout.For(0);
                    var ids = new List<string>();
                    var slots = new List<int>();
                    foreach (var planet in layout.Planets) { ids.Add(planet.Id); slots.Add(planet.Slots); }
                    var mock = InkFlow.Core.MockRankings.ShowcaseFor(InkFlow.Core.MockRankings.PlayerIdOf(1), ids, slots);
                    // Мок кладе в слоти заглушки (бібліотеки Core не знає) — мітки вийшли б нейтральними; справжня
                    // вітрина несе назви картинок, тож і тут — картинки бібліотеки, щоб мітки мали свої кольори (S09_01).
                    var visitorSlots = new List<InkFlow.Core.ShowcaseSlot>();
                    for (var k = 0; k < mock.Slots.Count; k++)
                        visitorSlots.Add(new InkFlow.Core.ShowcaseSlot(mock.Slots[k].PlanetId, mock.Slots[k].Slot,
                            rig.Library[(k * 7) % rig.Library.Count].Id));
                    var visitor = new InkFlow.Core.PublicShowcase(mock.PlayerId, mock.Nick, mock.AvatarId, false, rig.Library[22 % rig.Library.Count].Id,
                        mock.PlanetsDone, mock.GalaxiesDone, mock.Galaxy, visitorSlots, mock.UpdatedUtc);
                    player.Wallet.Add(1240, RewardSource.Debug);
                    rig.Enter(player, GalaxyArgs.ForVisitor(visitor));
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "GalaxyScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
                case "profile-fresh":
                case "profile":
                case "profile-nick":
                case "profile-nick-error":
                case "profile-nick-max":
                {
                    // S10_01: колекція, рожевий аватар, вітрина — «Кактус у сомбреро» (Рідкісна); новий гравець — заглушка.
                    // Діалог ніка — та сама фабрика, що в головній сцені: «Гравець» (S10_02), «Модератор» — заборонене
                    // слово в списку стенда (S10_03), «Космічний_Маляр2» — межа 16 (S10_04).
                    using var rig = MetaScreenRig<ProfileScreen>.Create(device, "ProfileScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    if (state != "profile-fresh")
                    {
                        for (var i = 0; i < 7; i++)
                            player.CollectPicture(rig.Library[(i * 11) % rig.Library.Count].Id, System.DateTime.UtcNow);
                        player.CollectPicture("cactus_hat", System.DateTime.UtcNow);
                        player.SetAvatar(5);
                        player.SetShowcasePicture("cactus_hat");
                    }
                    rig.Enter(player, ScreenArgs.Empty);
                    NickPrompt? prompt = null;
                    if (state.StartsWith("profile-nick", System.StringComparison.Ordinal))
                    {
                        prompt = BuildMainScene.BuildNickPrompt(rig.Safe.gameObject, rig.Design);
                        prompt.Bind(new NickRules(3, 16, new[] { "модератор" }));
                        prompt.Show(player.Nick, _ => { });
                        prompt.PreviewSetText(state == "profile-nick-error" ? "Модератор" : state == "profile-nick-max" ? "Космічний_Маляр2" : "Гравець");
                    }
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "ProfileScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    if (prompt != null)
                        Object.DestroyImmediate(prompt.gameObject);
                    break;
                }
                default:
                {
                    // Колекція (S06): 14 різних · 15 усього (капкейк двічі), у слотах — капкейк і роботик, як на макеті;
                    // порожній фільтр — та сама колекція без космічних, фільтр «Космічна».
                    using var rig = MetaScreenRig<CollectionScreen>.Create(device, "CollectionScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    if (state != "collection-empty")
                    {
                        var now = System.DateTime.UtcNow;
                        foreach (var id in MockupCollection)
                            if (rig.Library.Find(id) != null && (state != "collection-filter-empty" || id != "gem_cosmic"))
                                player.CollectPicture(id, now);
                        player.CollectPicture("cupcake", now);
                        var planet = player.Layout.For(0).Planets[0].Id;
                        player.TryPlaceInSlot(0, planet, 0, "cupcake", now);
                        player.TryPlaceInSlot(0, planet, 1, "robot", now);
                    }
                    var args = state switch
                    {
                        "collection-pick" => new CollectionArgs(0, player.Layout.For(0).Planets[0].Id, 5),
                        "collection-showcase" => CollectionArgs.ForShowcase(),
                        _ => CollectionArgs.Tab()
                    };
                    rig.Enter(player, args);
                    if (state == "collection-pick")
                    {
                        var index = -1;
                        for (var i = 0; i < rig.Screen.VisibleIds.Count; i++)
                            if (rig.Screen.VisibleIds[i] == "cactus_hat") index = i;
                        rig.Screen.PreviewTap(index);
                    }
                    if (state == "collection-filter-empty")
                        rig.Screen.SetRarity((int)InkFlow.Core.Rarity.Cosmic + 1);
                    Canvas.ForceUpdateCanvases();
                    Budget(rig, device.Name, state, overBudget);
                    Audit(rig, "CollectionScreen", device.Name, state, audit);
                    rig.SavePng(path);
                    break;
                }
            }
        }
    }
}
