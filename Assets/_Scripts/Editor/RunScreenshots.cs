using System.Collections.Generic;
using System.IO;
using InkFlow.Core;
using InkFlow.Platform;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Знімки екрана забігу з редактора (без Play Mode): п'ять роздільностей × стани — порожнє поле, середина гри,
    /// тривога поля «мало місця» й «останній хід», рахунок 1 234 567 і 987 654 321, картка перед забігом (нова й
    /// продовження), картинку домальовано, підтвердження для Епічної+, програш до фіналу (ролик і нафта), фінал,
    /// «Вийти додому?», після рестарту, після відновлення зліпка, після зміни пристрою, по одному на кожну з шести
    /// рідкостей. Меню: Ink Flow → Debug → Capture Run Screenshots. Пише PNG у docs/screenshots/ — для порівняння з
    /// еталонами docs/StyleRef/U1Glaze/preview.
    ///
    /// Кожен стан проходить самоперевірку поля (<see cref="RunScreenRig.LayoutFault"/>: полотно збігається з панеллю,
    /// лунки всередині панелі, панель усередині safe area), автоперевірку U1Glaze (<see cref="UiAudit"/>: написи,
    /// тапи ≥ 44, сусіди в групах) і бюджет викликів малювання (симуляція батчингу UGUI ≤ 35). Вада — помилка в
    /// консоль, у batch — код виходу 1. Стенд — <see cref="RunScreenRig"/>, той самий, що в UI-тестах.
    /// Batch: Unity -batchmode -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.RunScreenshots.BuildAndCapture -quit
    /// </summary>
    public static class RunScreenshots
    {
        private const string OutputFolder = "docs/screenshots";

        /// <summary>Стани в порядку знімання; назви — суфікси файлів.</summary>
        public static readonly string[] States =
        {
            "empty", "mid", "danger-warn", "danger", "score-1234567", "score-987654321",
            "intro", "intro-resumed", "completed", "completed-confirm", "over-continue", "over-continue-oil", "over",
            "home-confirm", "restart", "restored", "device-change",
            "rarity-common", "rarity-uncommon", "rarity-rare", "rarity-epic", "rarity-legendary", "rarity-cosmic"
        };

        /// <summary>
        /// Batch-режим: імпортувати спрайти стилю, перезібрати екран забігу й зняти всі стани.
        /// Unity -batchmode -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.RunScreenshots.BuildAndCapture -quit
        /// </summary>
        public static void BuildAndCapture()
        {
            foreach (var path in new[]
            {
                "Assets/_Sprites/K1Candy/panel.png", "Assets/_Sprites/K1Candy/tray-slot.png", "Assets/_Sprites/K1Candy/socket.png",
                "Assets/_Sprites/K1Candy/block-base.png", "Assets/_Sprites/K1Candy/block-highlight.png", "Assets/_Sprites/K1Candy/glow.png"
            })
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            RefreshPictureLibrary.Refresh();
            BuildEndlessScreen.Build();
            CaptureAll();
        }

        /// <summary>
        /// Batch: те саме і ще Main.unity — діалог «Вийти додому?» і префаби екранів у застосунку збирає BuildMainScene.
        /// Unity -batchmode -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.RunScreenshots.BuildAndCaptureWithMain -quit
        /// </summary>
        public static void BuildAndCaptureWithMain()
        {
            BuildAndCapture();
            BuildMainScene.Build();
        }

        [MenuItem("Ink Flow/Debug/Capture Run Screenshots")]
        public static void CaptureAll()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;
            if (!RunScreenRig.Available)
            {
                Debug.LogError($"[InkFlow] Знімки: немає {RunScreenRig.PrefabPath} або {RunScreenRig.DesignSystemPath} — спершу Build Endless Screen.");
                return;
            }

            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var written = new List<string>();
            var faults = new List<string>();
            var audit = new List<string>();
            var overBudget = new List<string>();
            var batchLog = new List<string>();
            for (var d = 0; d < RunScreenRig.Devices.Length; d++)
            {
                var device = RunScreenRig.Devices[d];
                // «Зміна пристрою»: екран спершу живе на сусідньому пристрої, потім перемикається на цей.
                var other = RunScreenRig.Devices[(d + 1) % RunScreenRig.Devices.Length];
                foreach (var state in States)
                {
                    using var rig = RunScreenRig.Create(state == "device-change" ? other : device);
                    Prepare(rig, state, device);
                    var fault = rig.LayoutFault();
                    if (fault != null)
                        faults.Add($"{device.Name} / {state}: {fault}");
                    foreach (var problem in rig.Audit())
                        audit.Add($"{device.Name} / {state}: {problem}");
                    rig.Render();
                    var batches = rig.SimulateBatches();
                    batchLog.Add($"{device.Name} / {state}: {batches}");
                    if (batches > MetaScreenshots.DrawCallBudget)
                        overBudget.Add($"{device.Name} / {state}: {batches} викликів (батчинг UGUI) > {MetaScreenshots.DrawCallBudget}");
                    var path = Path.Combine(OutputFolder, $"{device.Name}_{state}.png");
                    rig.SavePng(path);
                    written.Add(path);
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки ({written.Count}) у {OutputFolder}:\n  " + string.Join("\n  ", written));
            Debug.Log("[InkFlow] Виклики малювання забігу (симуляція батчингу UGUI):\n  " + string.Join("\n  ", batchLog));

            var failed = false;
            if (faults.Count == 0)
                Debug.Log($"[InkFlow] Самоперевірка поля: {RunScreenRig.Devices.Length * States.Length} станів, поле всюди на місці.");
            else
            {
                Debug.LogError($"[InkFlow] Самоперевірка поля: {faults.Count} вад —\n  " + string.Join("\n  ", faults));
                failed = true;
            }
            if (audit.Count == 0)
                Debug.Log("[InkFlow] Автоперевірка забігу: чисто.");
            else
            {
                Debug.LogError($"[InkFlow] Автоперевірка забігу: {audit.Count} проблем —\n  " + string.Join("\n  ", audit));
                failed = true;
            }
            if (overBudget.Count > 0)
            {
                Debug.LogError($"[InkFlow] Бюджет викликів малювання ({MetaScreenshots.DrawCallBudget}) перевищено:\n  " + string.Join("\n  ", overBudget));
                failed = true;
            }
            if (failed && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        /// <summary>Ставить стан на стенді. Для «device-change» стенд створено на сусідньому пристрої, а тут він перемикається на потрібний.</summary>
        private static void Prepare(RunScreenRig rig, string state, RunScreenRig.Device device)
        {
            switch (state)
            {
                case "empty":
                    rig.Show(rig.NewSession(0));
                    break;
                case "mid":
                    rig.Show(rig.NewSession(25));
                    break;
                case "danger":
                {
                    var session = rig.NewSession(10_000, stopWhen: s => s.Danger.Level == DangerLevel.Strong);
                    rig.Show(session);
                    if (session.Danger.Level == DangerLevel.None)
                        Debug.LogWarning("[InkFlow] Знімки: бот не дійшов до сильної пульсації — стан «danger» показує рівень з кнопки.");
                    rig.Screen.PreviewPulse(DangerLevel.Strong);
                    break;
                }
                case "danger-warn":
                {
                    var session = rig.NewSession(10_000, stopWhen: s => s.Danger.Level != DangerLevel.None);
                    rig.Show(session);
                    rig.Screen.PreviewPulse(DangerLevel.Warn);
                    break;
                }
                case "score-1234567":
                    rig.Show(rig.NewSession(12));
                    rig.Screen.PreviewStats(1_234_567, 987_654);
                    break;
                case "score-987654321":
                    rig.Show(rig.NewSession(12));
                    rig.Screen.PreviewStats(987_654_321, 999_999_999);
                    break;
                case "intro":
                    rig.Show(rig.NewSession(0));
                    rig.Screen.PreviewIntro(resumed: false);
                    break;
                case "intro-resumed":
                    rig.Show(rig.Restored(rig.NewSession(25)));
                    rig.Screen.PreviewIntro(resumed: true);
                    break;
                case "completed":
                    // S02Run_03 — епічна картинка, як «НЛО» на еталоні.
                    rig.Show(rig.SessionWithRarity(Rarity.Epic, 30));
                    rig.Screen.PreviewCompletion();
                    break;
                case "completed-confirm":
                    rig.Show(rig.SessionWithRarity(Rarity.Epic, 30));
                    rig.Screen.PreviewCompletionDrop();
                    break;
                case "over-continue":
                    // S02Run_05 до фіналу: ролик готовий — перше продовження за ролик (ContinueOffer).
                    rig.BindAds(new FakeAds());
                    rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
                    rig.Screen.PreviewOverContinue();
                    break;
                case "over-continue-oil":
                    // Ролика немає — продовження за нафту (у стенді 500 нафти; ціна з EconomyData).
                    rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
                    rig.Screen.PreviewOverContinue();
                    break;
                case "over":
                    rig.BindAds(new FakeAds());
                    rig.Show(rig.NewSession(10_000, stopWhen: s => s.IsOver));
                    rig.Screen.PreviewOver();
                    break;
                case "home-confirm":
                    rig.Show(rig.NewSession(25));
                    rig.ShowHomeConfirm();
                    break;
                case "restart":
                    // ↺ і «Ще раз»: EndlessScreen.Restart → board.StopAll. Саме тут сітка колись з'їжджала на пів панелі.
                    rig.Show(rig.NewSession(25));
                    rig.Screen.Restart();
                    rig.Show(rig.NewSession(25, 4243u));
                    break;
                case "restored":
                    rig.Show(rig.Restored(rig.NewSession(25)));
                    break;
                case "device-change":
                    rig.Show(rig.NewSession(25));
                    rig.Render();
                    rig.SetDevice(device);
                    break;
                case var rarityState when rarityState.StartsWith("rarity-"):
                    // Гало рідкості з-під картинки: та сама середина гри, картинка потрібної рідкості.
                    if (!Rarities.TryParse(rarityState.Substring("rarity-".Length), out var rarity))
                        throw new System.ArgumentOutOfRangeException(nameof(state), state, "невідома рідкість");
                    rig.Show(rig.SessionWithRarity(rarity, 25));
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(state), state, "невідомий стан знімка");
            }
        }
    }
}
