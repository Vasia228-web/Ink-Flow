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
    /// Стенд екрана забігу в Edit Mode: камера з RenderTexture розміру пристрою, канвас
    /// Screen Space – Camera, safe area, виставлена якорями руками (SafeAreaBinder читає
    /// Screen.safeArea, а тут екран — це текстура), і живий префаб EndlessScreen. Стани
    /// ставляться через EndlessScreen.Preview* — без корутин, тому кадр можна знімати одразу.
    ///
    /// Один стенд на дві задачі: утиліта знімків (<see cref="RunScreenshots"/>) і UI-тести
    /// розкладки поля (Assets/Tests/EditMode/UI). Два окремі стенди розійшлися б на першій
    /// правці, і тест перевіряв би не те, що бачить автор на знімку.
    /// </summary>
    public sealed class RunScreenRig : System.IDisposable
    {
        /// <summary>Роздільність і safe area (px згори / знизу) пристрою.</summary>
        public readonly struct Device
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly int Top;
            public readonly int Bottom;

            public Device(string name, int width, int height, int top, int bottom)
            {
                Name = name;
                Width = width;
                Height = height;
                Top = top;
                Bottom = bottom;
            }

            public override string ToString() => Name;
        }

        /// <summary>Роздільності з промту й safe area типових пристроїв.</summary>
        public static readonly Device[] Devices =
        {
            new Device("iphone-se_750x1334", 750, 1334, 40, 0),
            new Device("iphone-15-pro_1179x2556", 1179, 2556, 177, 102),
            new Device("android_1080x2400", 1080, 2400, 0, 66),
            new Device("ipad_1536x2048", 1536, 2048, 40, 40)
        };

        public const string PrefabPath = "Assets/_Prefabs/Screens/EndlessScreen.prefab";
        public const string CosmicPath = "Assets/_Prefabs/UI/CosmicBackground.prefab";
        public const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        public const string LibraryPath = "Assets/_ScriptableObjects/Pictures/PictureLibrary.asset";
        public const string BalancePath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";

        private readonly GameObject _cameraGo;
        private readonly GameObject _canvasGo;
        private readonly GameObject _screenGo;
        private readonly bool _previewWas;
        private readonly bool _suspendedWas;

        public Device Current { get; private set; }
        public Camera Camera { get; }
        public RectTransform Safe { get; }
        public EndlessScreen Screen { get; }
        public BoardView Board { get; }
        public RenderTexture Target { get; private set; }
        public DesignSystem Design { get; }
        public PictureLibrary Library { get; }
        public BalanceData Balance { get; }
        public PlayerState Player { get; }

        /// <summary>Чи є з чого будувати стенд: префаб екрана й дизайн-система.</summary>
        public static bool Available =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath) != null;

        public static RunScreenRig Create(Device device) => new RunScreenRig(device);

        private RunScreenRig(Device device)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>(CosmicPath);
            Design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            if (prefab == null || Design == null)
                throw new System.InvalidOperationException($"Немає {PrefabPath} або {DesignSystemPath} — спершу Build Endless Screen.");
            var libraryAsset = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(LibraryPath);
            var balanceAsset = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            Library = libraryAsset != null ? libraryAsset.ToLibrary() : PictureLibrary.LoadFromDirectory(Path.GetFullPath("Assets/_Pictures"));
            Balance = balanceAsset != null ? balanceAsset.ToBalanceData() : BalanceData.Default;

            _previewWas = PictureView.EditorPreview;
            _suspendedWas = StyleRefresh.Suspended;
            PictureView.EditorPreview = true;
            StyleRefresh.Suspended = true;

            Current = device;
            Target = NewTarget(device);
            _cameraGo = new GameObject("RigCamera");
            Camera = _cameraGo.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Design.BackgroundEdge;
            Camera.targetTexture = Target;
            Camera.cullingMask = ~0;
            _cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            _canvasGo = new GameObject("RigCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera;
            canvas.planeDistance = 10f;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UIRoot.ReferenceWidth, UIRoot.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UIRoot.MatchWidthOrHeight;

            if (cosmic != null)
            {
                var bg = (GameObject)PrefabUtility.InstantiatePrefab(cosmic, _canvasGo.transform);
                Stretch(bg.GetComponent<RectTransform>());
            }

            Safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            Safe.SetParent(_canvasGo.transform, false);
            ApplySafeArea(device);

            _screenGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Safe);
            _screenGo.SetActive(true);
            Stretch(_screenGo.GetComponent<RectTransform>());
            // Без «??»: для об'єктів Unity він не бачить знищеного («fake null») об'єкта.
            var screen = _screenGo.GetComponent<EndlessScreen>();
            if (screen == null)
                throw new System.InvalidOperationException("У префабі немає EndlessScreen.");
            Screen = screen;
            var board = _screenGo.GetComponentInChildren<BoardView>(true);
            if (board == null)
                throw new System.InvalidOperationException("У префабі немає BoardView.");
            Board = board;

            Player = PlayerState.NewPlayer(EconomyData.Default, null, Library, Balance);
            Player.Wallet.Add(500, RewardSource.Debug);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Інший пристрій на тому самому екрані — як зміна в Device Simulator: нова текстура, нова safe area, перерозкладка.</summary>
        public void SetDevice(Device device)
        {
            Current = device;
            var old = Target;
            Target = NewTarget(device);
            Camera.targetTexture = Target;
            if (old != null)
                Object.DestroyImmediate(old);
            ApplySafeArea(device);
            Relayout();
        }

        /// <summary>Довільний розмір вікна Game (safe area на весь екран).</summary>
        public void Resize(int width, int height) => SetDevice(new Device($"{width}x{height}", width, height, 0, 0));

        /// <summary>Перерозкладка після зміни розміру — те, що в грі робить LateUpdate за прапорцем.</summary>
        public void Relayout()
        {
            Canvas.ForceUpdateCanvases();
            Screen.Layout();
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Сесія після стількох ходів бота; <paramref name="stopWhen"/> зупиняє раніше.</summary>
        public RunSession NewSession(int botMoves, uint seed = 4242u, System.Func<RunSession, bool>? stopWhen = null)
        {
            var session = new RunSession(Balance, PieceCatalogData.Default, new XorShiftRandom(seed), Library);
            var bot = new RunBot();
            for (var i = 0; i < botMoves && !session.IsOver && bot.TryChooseMove(session, out var index, out var anchor); i++)
            {
                if (stopWhen != null && stopWhen(session))
                    break;
                session.TryPlace(index, anchor);
            }
            return session;
        }

        /// <summary>Та сама партія, відновлена зі зліпка (§9) — як після закриття застосунку.</summary>
        public RunSession Restored(RunSession source)
        {
            var snapshot = new RunSnapshot();
            source.Capture(snapshot);
            return new RunSession(Balance, PieceCatalogData.Default, Library, snapshot);
        }

        /// <summary>Показує сесію як є (без картки перед забігом) і розкладає під поточний пристрій.</summary>
        public void Show(RunSession session)
        {
            Canvas.ForceUpdateCanvases();
            Screen.PreviewSession(session, Player);
            Relayout();
        }

        public void Render()
        {
            Canvas.ForceUpdateCanvases();
            Camera.Render();
            Canvas.ForceUpdateCanvases();
            Camera.Render();
        }

        /// <summary>Кадр у PNG. Рендерить сам.</summary>
        public void SavePng(string path)
        {
            Render();
            var previous = RenderTexture.active;
            RenderTexture.active = Target;
            var texture = new Texture2D(Target.width, Target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>Самоперевірка поля (див. <see cref="BoardView.FindLayoutFault"/>): null — усе на місці.</summary>
        public string? LayoutFault()
        {
            Canvas.ForceUpdateCanvases();
            return Board.FindLayoutFault(Safe);
        }

        public void Dispose()
        {
            if (_screenGo != null) Object.DestroyImmediate(_screenGo);
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            if (Camera != null) Camera.targetTexture = null;
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (Target != null) Object.DestroyImmediate(Target);
            PictureView.EditorPreview = _previewWas;
            StyleRefresh.Suspended = _suspendedWas;
        }

        private void ApplySafeArea(Device device)
        {
            Safe.anchorMin = new Vector2(0f, (float)device.Bottom / device.Height);
            Safe.anchorMax = new Vector2(1f, 1f - (float)device.Top / device.Height);
            Safe.offsetMin = Vector2.zero;
            Safe.offsetMax = Vector2.zero;
        }

        private static RenderTexture NewTarget(Device device) =>
            new RenderTexture(device.Width, device.Height, 24, RenderTextureFormat.ARGB32) { name = "RigTarget" };

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
