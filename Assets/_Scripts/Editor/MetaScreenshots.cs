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
    /// Знімки екранів метагри (Фази 3–7) з редактора, без Play Mode: чотири роздільності × стани
    /// галактики, планети, колекції, магазину й налаштувань. Меню: Ink Flow → Debug → Capture Meta Screenshots.
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
            "galaxy-fresh", "galaxy-mid", "galaxy-second",
            "planet-empty", "planet-half", "planet-full", "planet-sheet",
            "collection-browse", "collection-pick", "collection-empty",
            "shop", "shop-offline",
            "settings", "settings-run"
        };

        public static void BuildAndCapture()
        {
            RefreshPictureLibrary.Refresh();
            BuildGalaxyScreen.Build();
            BuildPlanetScreen.Build();
            BuildCollectionScreen.Build();
            BuildShopScreen.Build();
            BuildSettingsScreen.Build();
            CaptureAll();
        }

        [MenuItem("Ink Flow/Debug/Capture Meta Screenshots")]
        public static void CaptureAll()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;
            foreach (var name in new[] { "GalaxyScreen", "PlanetScreen", "CollectionScreen", "ShopScreen", "SettingsScreen" })
                if (!MetaScreenRig<ScreenBase>.Available(name))
                {
                    Debug.LogError($"[InkFlow] Знімки метагри: немає {MetaScreenRig<ScreenBase>.PrefabPathOf(name)} — спершу Build {name}.");
                    return;
                }

            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var written = new List<string>();
            var overBudget = new List<string>();
            foreach (var device in ScreenRigBase.Devices)
                foreach (var state in States)
                {
                    var path = Path.Combine(OutputFolder, $"{device.Name}_{state}.png");
                    Capture(device, state, path, overBudget);
                    written.Add(path);
                }

            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки метагри ({written.Count}) у {OutputFolder}:\n  " + string.Join("\n  ", written));
            if (overBudget.Count == 0)
                return;
            Debug.LogError($"[InkFlow] Бюджет викликів малювання ({DrawCallBudget}) перевищено:\n  " + string.Join("\n  ", overBudget));
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static void Capture(ScreenRigBase.Device device, string state, string path, List<string> overBudget)
        {
            switch (state)
            {
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
                case "shop":
                case "shop-offline":
                {
                    // Зі стором (FakeIap: ціни §13 рядками) і без нього (NullIap: кнопки сплять, екран каже чому).
                    using var rig = MetaScreenRig<ShopScreen>.Create(device, "ShopScreen");
                    var player = rig.NewPlayer();
                    player.Wallet.Add(1240, RewardSource.Debug);
                    rig.Screen.BindServices(state == "shop"
                        ? new InkFlow.Platform.FakeIap()
                        : new InkFlow.Platform.NullIap());
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
