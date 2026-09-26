using System.Collections.Generic;
using System.IO;
using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.Meta;
using InkFlow.Style;
using InkFlow.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.Editor
{
    /// <summary>
    /// Знімки екрана забігу з редактора (без Play Mode): чотири роздільності × сім станів —
    /// порожнє поле, середина гри, майже повне поле з пульсацією, рахунок 1 234 567 і
    /// 987 654 321, готова картинка, екран кінця забігу. Меню: Ink Flow → Debug → Capture Run
    /// Screenshots. Пише PNG у docs/screenshots/ — для порівняння з еталонами docs/StyleRef/.
    ///
    /// Як: префаб екрана інстанціюється під канвас Screen Space – Camera, камера рендерить у
    /// RenderTexture потрібного розміру; safe area кожного пристрою задається якорями руками
    /// (SafeAreaBinder читає Screen.safeArea, а в редакторі це вікно Game). Стани ставляться
    /// через EndlessScreen.Preview* — без корутин, тому кадр можна знімати одразу.
    /// Batch: Unity -batchmode -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.RunScreenshots.CaptureAll -quit
    /// </summary>
    public static class RunScreenshots
    {
        private const string PrefabPath = "Assets/_Prefabs/Screens/EndlessScreen.prefab";
        private const string CosmicPath = "Assets/_Prefabs/UI/CosmicBackground.prefab";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string LibraryPath = "Assets/_ScriptableObjects/Pictures/PictureLibrary.asset";
        private const string BalancePath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";
        private const string OutputFolder = "docs/screenshots";

        /// <summary>Роздільності з промту й safe area (px згори / знизу) типових пристроїв.</summary>
        private static readonly (string name, int w, int h, int top, int bottom)[] Devices =
        {
            ("iphone-se_750x1334", 750, 1334, 40, 0),
            ("iphone-15-pro_1179x2556", 1179, 2556, 177, 102),
            ("android_1080x2400", 1080, 2400, 0, 66),
            ("ipad_1536x2048", 1536, 2048, 40, 40)
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
                "Assets/_Sprites/K1Candy/block-base.png", "Assets/_Sprites/K1Candy/block-highlight.png", "Assets/_Sprites/K1Candy/glow.png",
                PictureViewBuilder.PaperPath
            })
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            RefreshPictureLibrary.Refresh();
            BuildEndlessScreen.Build();
            CaptureAll();
        }

        [MenuItem("Ink Flow/Debug/Capture Run Screenshots")]
        public static void CaptureAll()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>(CosmicPath);
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var libraryAsset = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(LibraryPath);
            var balanceAsset = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            if (prefab == null || design == null)
            {
                Debug.LogError($"[InkFlow] Знімки: немає {PrefabPath} або {DesignSystemPath} — спершу Build Endless Screen.");
                return;
            }
            var library = libraryAsset != null ? libraryAsset.ToLibrary() : PictureLibrary.LoadFromDirectory(Path.GetFullPath("Assets/_Pictures"));
            var balance = balanceAsset != null ? balanceAsset.ToBalanceData() : BalanceData.Default;

            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            var written = new List<string>();
            PictureView.EditorPreview = true;
            StyleRefresh.Suspended = true;
            try
            {
                foreach (var device in Devices)
                    foreach (var state in new[] { "empty", "mid", "danger", "score-1234567", "score-987654321", "completed", "over" })
                        written.Add(Capture(prefab, cosmic, design, library, balance, device, state));
            }
            finally
            {
                PictureView.EditorPreview = false;
                StyleRefresh.Suspended = false;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Знімки ({written.Count}) у {OutputFolder}:\n  " + string.Join("\n  ", written));
        }

        private static string Capture(GameObject prefab, GameObject? cosmic, DesignSystem design, PictureLibrary library,
            BalanceData balance, (string name, int w, int h, int top, int bottom) device, string state)
        {
            var rt = new RenderTexture(device.w, device.h, 24, RenderTextureFormat.ARGB32) { name = "Shot" };
            var camGo = new GameObject("ShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = design.BackgroundEdge;
            cam.targetTexture = rt;
            cam.cullingMask = ~0;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("ShotCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UIRoot.ReferenceWidth, UIRoot.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UIRoot.MatchWidthOrHeight;

            GameObject? screenGo = null;
            try
            {
                if (cosmic != null)
                {
                    var bg = (GameObject)PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);
                    Stretch(bg.GetComponent<RectTransform>(), 0f, 1f);
                }

                // Safe area руками: SafeAreaBinder бере Screen.safeArea, а тут екран — це RenderTexture.
                var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
                safe.SetParent(canvasGo.transform, false);
                safe.anchorMin = new Vector2(0f, (float)device.bottom / device.h);
                safe.anchorMax = new Vector2(1f, 1f - (float)device.top / device.h);
                safe.offsetMin = Vector2.zero;
                safe.offsetMax = Vector2.zero;

                screenGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safe);
                screenGo.SetActive(true);
                Stretch(screenGo.GetComponent<RectTransform>(), 0f, 1f);
                var screen = screenGo.GetComponent<EndlessScreen>();
                if (screen == null)
                    throw new System.InvalidOperationException("У префабі немає EndlessScreen.");

                var playerState = PlayerState.NewPlayer(EconomyData.Default, null, library, balance);
                playerState.Wallet.Add(500, RewardSource.Debug);
                var session = SessionFor(state, library, balance);
                Canvas.ForceUpdateCanvases();
                screen.PreviewSession(session, playerState);
                screen.Layout();
                switch (state)
                {
                    case "danger": screen.PreviewPulse(DangerLevel.Strong); break;
                    case "score-1234567": screen.PreviewStats(1_234_567, 987_654); break;
                    case "score-987654321": screen.PreviewStats(987_654_321, 999_999_999); break;
                    case "completed": screen.PreviewCompletion(); break;
                    case "over": screen.PreviewOver(); break;
                }

                Canvas.ForceUpdateCanvases();
                cam.Render();
                Canvas.ForceUpdateCanvases();
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var texture = new Texture2D(device.w, device.h, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, device.w, device.h), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                var path = Path.Combine(OutputFolder, $"{device.name}_{state}.png");
                File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                return path;
            }
            finally
            {
                if (screenGo != null) Object.DestroyImmediate(screenGo);
                Object.DestroyImmediate(canvasGo);
                cam.targetTexture = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>Сесія для стану: порожня, середина гри (25 ходів бота), небезпека (бот грає до сильної пульсації).</summary>
        private static RunSession SessionFor(string state, PictureLibrary library, BalanceData balance)
        {
            var session = new RunSession(balance, PieceCatalogData.Default, new XorShiftRandom(4242u), library);
            var bot = new RunBot();
            var moves = state switch { "mid" => 25, "score-1234567" => 12, "score-987654321" => 12, "completed" => 30, "over" => 10_000, "danger" => 10_000, _ => 0 };
            for (var i = 0; i < moves && !session.IsOver && bot.TryChooseMove(session, out var index, out var anchor); i++)
            {
                if (state == "danger" && session.Danger.Level == DangerLevel.Strong)
                    break;
                session.TryPlace(index, anchor);
                if (state == "over" && session.IsOver)
                    break;
            }
            if (state == "danger" && session.Danger.Level == DangerLevel.None)
                Debug.LogWarning("[InkFlow] Знімки: бот не дійшов до сильної пульсації — стан «danger» показує рівень з кнопки.");
            return session;
        }

        private static void Stretch(RectTransform rect, float min, float max)
        {
            rect.anchorMin = new Vector2(min, min);
            rect.anchorMax = new Vector2(max, max);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
