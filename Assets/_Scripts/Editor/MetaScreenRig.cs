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
    /// Стенд будь-якого екрана метагри (галактика, планета, колекція…) в Edit Mode: префаб
    /// екрана під канвасом стенда, стан гравця з пам'яті, вхід через OnEnter. Коли екрани
    /// анімують вхід корутинами, стенд просить їх цього не робити (<see cref="ScreenBase.PreviewEnter"/>),
    /// бо в Edit Mode корутини не тікають і кадр застиг би на першому їхньому кроці.
    /// </summary>
    public sealed class MetaScreenRig<T> : ScreenRigBase where T : ScreenBase
    {
        public const string LibraryPath = "Assets/_ScriptableObjects/Pictures/PictureLibrary.asset";

        private readonly GameObject _screenGo;

        public T Screen { get; }
        public PictureLibrary Library { get; }

        public static string PrefabPathOf(string prefabName) => $"Assets/_Prefabs/Screens/{prefabName}.prefab";

        /// <summary>Чи є з чого будувати стенд: префаб екрана й дизайн-система.</summary>
        public static bool Available(string prefabName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathOf(prefabName)) != null &&
            AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath) != null;

        public static MetaScreenRig<T> Create(Device device, string prefabName) => new MetaScreenRig<T>(device, prefabName);

        private MetaScreenRig(Device device, string prefabName) : base(device, LoadDesign())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathOf(prefabName));
            if (prefab == null)
                throw new System.InvalidOperationException($"Немає {PrefabPathOf(prefabName)} — спершу Build {prefabName}.");
            var libraryAsset = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(LibraryPath);
            Library = libraryAsset != null ? libraryAsset.ToLibrary() : PictureLibrary.LoadFromDirectory(Path.GetFullPath("Assets/_Pictures"));

            _screenGo = InstantiateScreen(prefab);
            var screen = _screenGo.GetComponent<T>();
            if (screen == null)
                throw new System.InvalidOperationException($"У префабі {prefabName} немає {typeof(T).Name}.");
            Screen = screen;
            Canvas.ForceUpdateCanvases();
        }

        private static DesignSystem LoadDesign()
        {
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            if (design == null)
                throw new System.InvalidOperationException($"Немає {DesignSystemPath} — спершу Build UI Kit.");
            return design;
        }

        /// <summary>Новий гравець із бібліотекою стенда й дефолтною розкладкою галактики.</summary>
        public PlayerState NewPlayer(GalaxyLayout? layout = null) =>
            PlayerState.NewPlayer(EconomyData.Default, null, Library, BalanceData.Default, layout);

        /// <summary>
        /// Збирає й ставить картинки в усі слоти планети <paramref name="planetIndex"/> поточної галактики —
        /// різні картинки по колу бібліотеки, щоб знімок був строкатим.
        /// </summary>
        public void FillPlanet(PlayerState player, int planetIndex, int count = -1, int pictureOffset = 0)
        {
            var planet = player.Layout.Planets[planetIndex];
            var n = count < 0 ? planet.Slots : Mathf.Min(count, planet.Slots);
            var galaxy = player.CurrentGalaxy;
            for (var i = 0; i < n; i++)
            {
                var picture = Library[(pictureOffset + i) % Library.Count];
                player.CollectPicture(picture.Id, System.DateTime.UtcNow);
                if (!player.TryPlaceInSlot(galaxy, planet.Id, i, picture.Id, System.DateTime.UtcNow))
                    throw new System.InvalidOperationException($"{planet.Name}: слот {i} не прийняв картинку — планета замкнена?");
            }
        }

        /// <summary>Підставляє стан і входить на екран без анімацій входу.</summary>
        public void Enter(PlayerState? player, ScreenArgs args)
        {
            if (player != null)
                Screen.BindState(player);
            Canvas.ForceUpdateCanvases();
            Screen.PreviewEnter(args);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Оцінка кількості викликів малювання всього канваса стенда (екран разом із космічним фоном —
        /// на пристрої малюється все): скільки різних пар (матеріал, текстура) серед активних видимих
        /// графік. Канвас батчить графіки з однаковою парою, якщо між ними в глибині не стоїть інша,
        /// тож це нижня межа; <paramref name="runs"/> — скільки разів пара змінюється при обході
        /// ієрархії в порядку малювання. Це ОЦІНКА, не точна верхня межа: UGUI сортує за глибиною
        /// перекриття, і дві сусідні графіки однієї пари можуть розійтись у різні групи. Повністю
        /// прозорі графіки канвас не малює (<c>CanvasRenderer.cullTransparentMesh</c>) — їх не рахуємо.
        /// </summary>
        public int EstimateBatches(out int runs, out int graphics)
        {
            var keys = new HashSet<(Material, Texture)>();
            runs = 0;
            graphics = 0;
            (Material, Texture)? last = null;
            foreach (var graphic in CanvasRoot.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || !graphic.gameObject.activeInHierarchy)
                    continue;
                var renderer = graphic.canvasRenderer;
                if (renderer.cullTransparentMesh && (renderer.GetAlpha() <= 0.001f || graphic.color.a <= 0.001f))
                    continue;
                graphics++;
                var key = (graphic.materialForRendering, graphic.mainTexture);
                keys.Add(key);
                if (last == null || !last.Value.Equals(key))
                {
                    runs++;
                    last = key;
                }
            }
            return keys.Count;
        }

        /// <summary>
        /// Оцінка викликів малювання за алгоритмом батчингу UGUI, а не за порядком обходу: кожна графіка отримує
        /// глибину — найбільшу серед тих, що вже намальовані під нею й ПЕРЕКРИВАЮТЬСЯ з нею (+1, якщо в тієї інша
        /// пара матеріал+текстура); далі графіки сортуються за (глибина, матеріал, текстура), і кожна зміна пари в
        /// цьому порядку — новий виклик. Тому сусіди, що не перекриваються (іконки в рядку, написи на картці),
        /// батчаться разом, хоч в ієрархії й чергуються. Маски/обрізання не моделюються — на екранах U1Glaze
        /// без масок це не важить; повністю прозорі графіки не рахуються, як і в <see cref="EstimateBatches"/>.
        /// </summary>
        public int SimulateBatches() => SimulateBatches(CanvasRoot.transform);

        /// <summary>Те саме для будь-якого кореня — тест алгоритму на відомих розкладах.</summary>
        public static int SimulateBatches(Transform root)
        {
            var items = new List<(Rect rect, (Material, Texture) key, int depth, int order)>();
            // Порядок пар для сортування — за першою появою (ідентифікатор об'єкта в Unity 6.5 застарів).
            var keyOrder = new Dictionary<(Material, Texture), int>();
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
                if (!keyOrder.ContainsKey(key))
                    keyOrder[key] = keyOrder.Count;
                var depth = 0;
                foreach (var below in items)
                {
                    // Дотик краями — не перекриття (сусідні клітинки, вкладки в ряд).
                    var overlaps = rect.xMin < below.rect.xMax - 1e-4f && below.rect.xMin < rect.xMax - 1e-4f &&
                                   rect.yMin < below.rect.yMax - 1e-4f && below.rect.yMin < rect.yMax - 1e-4f;
                    if (overlaps)
                        depth = Mathf.Max(depth, below.key.Equals(key) ? below.depth : below.depth + 1);
                }
                items.Add((rect, key, depth, items.Count));
            }
            items.Sort((a, b) =>
            {
                var byDepth = a.depth.CompareTo(b.depth);
                if (byDepth != 0) return byDepth;
                var byKey = keyOrder[a.key].CompareTo(keyOrder[b.key]);
                return byKey != 0 ? byKey : a.order.CompareTo(b.order);
            });
            var batches = 0;
            (Material, Texture)? last = null;
            foreach (var item in items)
            {
                if (last == null || !last.Value.Equals(item.key))
                {
                    batches++;
                    last = item.key;
                }
            }
            return batches;
        }

        public override void Dispose()
        {
            if (_screenGo != null) Object.DestroyImmediate(_screenGo);
            base.Dispose();
        }
    }
}
