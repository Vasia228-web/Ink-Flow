using System.Collections.Generic;
using System.IO;
using InkFlow.Style;
using InkFlow.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.Editor
{
    /// <summary>
    /// Спільна частина стендів екранів у Edit Mode: камера з RenderTexture розміру пристрою,
    /// канвас Screen Space – Camera, safe area, виставлена якорями руками (SafeAreaBinder читає
    /// Screen.safeArea, а тут екран — це текстура), фон. Конкретний стенд додає свій префаб.
    ///
    /// Один базовий стенд на всі екрани: два окремі розійшлися б на першій правці, і знімок
    /// планети робився б не тим самим способом, що знімок забігу.
    /// </summary>
    public abstract class ScreenRigBase : System.IDisposable
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
            new Device("ipad_1536x2048", 1536, 2048, 40, 40),
            // Полотно макетів U1Glaze — 402×874 pt (iPhone 16 Pro, @3×): еталон порівнюється піксель у піксель.
            new Device("iphone-16-pro_1206x2622", 1206, 2622, 186, 102)
        };

        /// <summary>Пристрій, на якому макет 402×874 відтворюється 1:1 (контрольні точки U1Glaze).</summary>
        public static Device Mockup => Devices[4];

        /// <summary>iPhone SE — другий розмір контрольних точок (375×667).</summary>
        public static Device Small => Devices[0];

        public const string CosmicPath = "Assets/_Prefabs/UI/CosmicBackground.prefab";
        public const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";

        private readonly GameObject _cameraGo;
        private readonly GameObject _canvasGo;
        private readonly bool _previewWas;
        private readonly bool _suspendedWas;

        public Device Current { get; private set; }
        public Camera Camera { get; }

        /// <summary>Канвас стенда цілком — з фоном і safe area: що на пристрої малюється, те й рахуємо.</summary>
        protected GameObject CanvasRoot => _canvasGo;
        public RectTransform Safe { get; }
        public RenderTexture Target { get; private set; }
        public DesignSystem Design { get; }

        protected ScreenRigBase(Device device, DesignSystem design)
        {
            Design = design;
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>(CosmicPath);

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
            Camera.backgroundColor = design.BackgroundEdge;
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
            scaler.matchWidthOrHeight = UIRoot.MatchFor(device.Width, device.Height);

            if (cosmic != null)
            {
                var bg = (GameObject)PrefabUtility.InstantiatePrefab(cosmic, _canvasGo.transform);
                Stretch(bg.GetComponent<RectTransform>());
            }

            Safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            Safe.SetParent(_canvasGo.transform, false);
            ApplySafeArea(device);
        }

        /// <summary>
        /// Оцінка викликів малювання за алгоритмом батчингу UGUI, а не за порядком обходу: кожна графіка отримує
        /// глибину — найбільшу серед тих, що вже намальовані під нею й ПЕРЕКРИВАЮТЬСЯ з нею (+1, якщо в тієї інша
        /// пара матеріал+текстура); у межах глибини графіки однієї пари йдуть одним викликом. Виклики рахуються
        /// окремо на кожній глибині (сума різних пар по глибинах): UGUI іноді зливає однакову пару на межі двох
        /// глибин, але це залежить від порядку ідентифікаторів матеріалів — тож оцінка свідомо не зливає (зверху). Тому сусіди, що не перекриваються (іконки в рядку, написи на картці),
        /// батчаться разом, хоч в ієрархії й чергуються. Стенсил-маска (`Mask`, з кроку 4 — круг кулі «Сфери»): замасковані
        /// графіки мають свій materialForRendering, тож уже рахуються окремою парою, а зняття маски — ще +1 на кожну
        /// активну маску; `RectMask2D` (обрізання без стенсила) не моделюється. Повністю прозорі графіки не рахуються,
        /// як і в <see cref="EstimateBatches"/>.
        /// </summary>
        public int SimulateBatches() => SimulateBatches(CanvasRoot.transform);

        /// <summary>Те саме для будь-якого кореня — тест алгоритму на відомих розкладах.</summary>
        public static int SimulateBatches(Transform root)
        {
            var items = new List<(Rect rect, (Material, Texture) key, int depth)>();
            var corners = new Vector3[4];
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || !graphic.gameObject.activeInHierarchy)
                    continue;
                var renderer = graphic.canvasRenderer;
                if (renderer.cullTransparentMesh && (renderer.GetAlpha() <= 0.001f || graphic.color.a <= 0.001f))
                    continue;
                graphic.rectTransform.GetWorldCorners(corners);
                var rect = Rect.MinMaxRect(
                    Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                    Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
                var key = (graphic.materialForRendering, graphic.mainTexture);
                var depth = 0;
                foreach (var below in items)
                {
                    // Дотик краями — не перекриття (сусідні клітинки, вкладки в ряд).
                    var overlaps = rect.xMin < below.rect.xMax - 1e-4f && below.rect.xMin < rect.xMax - 1e-4f &&
                                   rect.yMin < below.rect.yMax - 1e-4f && below.rect.yMin < rect.yMax - 1e-4f;
                    if (overlaps)
                        depth = Mathf.Max(depth, below.key.Equals(key) ? below.depth : below.depth + 1);
                }
                items.Add((rect, key, depth));
            }
            var perDepth = new HashSet<(int depth, (Material, Texture) key)>();
            foreach (var item in items)
                perDepth.Add((item.depth, item.key));
            // Маска (стенсил) малює себе (графіка з її матеріалом уже порахована вище — у неї свій materialForRendering,
            // як і в замаскованих) і ще раз — щоб зняти стенсил після дітей: +1 виклик на кожну активну маску.
            var unmask = 0;
            foreach (var mask in root.GetComponentsInChildren<Mask>(false))
                if (mask.isActiveAndEnabled && mask.MaskEnabled())
                    unmask++;
            return perDepth.Count + unmask;
        }

        /// <summary>Префаб екрана під safe area, активний і розтягнутий.</summary>
        protected GameObject InstantiateScreen(GameObject prefab)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Safe);
            go.SetActive(true);
            Stretch(go.GetComponent<RectTransform>());
            return go;
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
            // Match — за співвідношенням сторін, як у грі (UIRoot): інакше стенд брехав би про розкладку.
            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null)
                scaler.matchWidthOrHeight = UIRoot.MatchFor(device.Width, device.Height);
            ApplySafeArea(device);
            Relayout();
        }

        /// <summary>Довільний розмір вікна Game (safe area на весь екран).</summary>
        public void Resize(int width, int height) => SetDevice(new Device($"{width}x{height}", width, height, 0, 0));

        /// <summary>Перерозкладка після зміни розміру — те, що в грі робить LateUpdate за прапорцем.</summary>
        public virtual void Relayout() => Canvas.ForceUpdateCanvases();

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
            var texture = Snapshot();
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>Кадр у пам'ять (читабельна текстура; знищує викликач).</summary>
        public Texture2D Snapshot()
        {
            Render();
            var previous = RenderTexture.active;
            RenderTexture.active = Target;
            var texture = new Texture2D(Target.width, Target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        /// <summary>Світова точка → піксель кадру (початок — лівий нижній кут, як у Texture2D.GetPixel).</summary>
        public Vector2 ToPixel(Vector3 world) => Camera.WorldToScreenPoint(world);

        public virtual void Dispose()
        {
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

        protected static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
