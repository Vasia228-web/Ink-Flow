using System;
using System.Collections.Generic;
using System.IO;
using InkFlow.Style;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace InkFlow.Editor
{
    /// <summary>
    /// Спрайти інтерфейсу U1Glaze: маніфест `Assets/_Sprites/U1Glaze/sprites.json` (копія
    /// `docs/StyleRef/U1Glaze/sprites.json`), завантаження за ім'ям і атлас.
    ///
    /// Чому маніфест, а не інспектор: межі 9-slice у 56 спрайтів і прапорець «тонувати» інакше
    /// довелося б виставляти руками після кожного оновлення еталона. Імпортер
    /// (<see cref="StyleSpriteImporter"/>) читає межі звідси, збирачі беруть спрайт за ім'ям
    /// з маніфесту — файл, тека й розмір їх не обходять.
    ///
    /// PPU: спрайти намальовано в 3× (1 px макета = 3 px текстури), а 1 px макета = 1080/402
    /// reference-одиниць, тож при referencePixelsPerUnit канваса 100 спрайту потрібен
    /// PPU = 300 / (1080/402) ≈ 111.67 — тоді SetNativeSize дає рівно розмір макета × 2.687,
    /// а межі 9-slice беруться з маніфесту без перерахунку (множник 1).
    /// </summary>
    public static class U1GlazeSprites
    {
        public const string Folder = "Assets/_Sprites/U1Glaze";
        public const string ManifestPath = Folder + "/sprites.json";
        public const string AtlasPath = Folder + "/U1Glaze.spriteatlasv2";

        /// <summary>Пікселів текстури на 1 px макета (спрайти 3×).</summary>
        public const float Scale = 3f;

        /// <summary>PPU спрайтів під канвас проєкту (референсний PPU канваса — 100).</summary>
        public static float PixelsPerUnit => Scale * 100f / DesignSystem.MockupToReference;

        [Serializable]
        public sealed class Border
        {
            public int left, top, right, bottom;
        }

        [Serializable]
        public sealed class Entry
        {
            public string name = string.Empty;
            public string file = string.Empty;
            public int[] size_design = Array.Empty<int>();
            public int scale = 3;
            public bool tint;
            public string use = string.Empty;
            public Border? border_px;

            public bool IsSliced => border_px != null &&
                                    (border_px.left > 0 || border_px.top > 0 || border_px.right > 0 || border_px.bottom > 0);

            /// <summary>Розмір у px макета.</summary>
            public Vector2 DesignSize => size_design.Length == 2 ? new Vector2(size_design[0], size_design[1]) : Vector2.zero;

            /// <summary>Розмір у reference-одиницях — те, що дасть SetNativeSize.</summary>
            public Vector2 ReferenceSize => DesignSize * DesignSystem.MockupToReference;

            public string AssetPath => $"{Folder}/{file.Replace("sprites/", string.Empty)}";

            /// <summary>Межі для TextureImporter.spriteBorder: (left, bottom, right, top).</summary>
            public Vector4 ImporterBorder => border_px == null
                ? Vector4.zero
                : new Vector4(border_px.left, border_px.bottom, border_px.right, border_px.top);
        }

        [Serializable]
        private sealed class Manifest
        {
            public List<Entry> items = new List<Entry>();
        }

        private static Dictionary<string, Entry>? _byName;
        private static Dictionary<string, Entry>? _byPath;

        /// <summary>Усі записи маніфесту (кеш на сесію редактора; <see cref="Reload"/> скидає).</summary>
        public static IReadOnlyCollection<Entry> All => Index().Values;

        public static void Reload()
        {
            _byName = null;
            _byPath = null;
        }

        private static Dictionary<string, Entry> Index()
        {
            if (_byName != null)
                return _byName;
            var full = Path.GetFullPath(ManifestPath);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Немає маніфесту спрайтів U1Glaze: {ManifestPath}. Скопіюй docs/StyleRef/U1Glaze/sprites.json.");
            // JsonUtility не читає масив на верхньому рівні — загортаємо.
            var manifest = JsonUtility.FromJson<Manifest>("{\"items\":" + File.ReadAllText(full) + "}");
            _byName = new Dictionary<string, Entry>(StringComparer.Ordinal);
            _byPath = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in manifest.items)
            {
                _byName[entry.name] = entry;
                _byPath[entry.AssetPath] = entry;
            }
            return _byName;
        }

        /// <summary>Запис маніфесту за ім'ям; кидає, якщо такого спрайта в еталоні немає.</summary>
        public static Entry Of(string name)
        {
            if (Index().TryGetValue(name, out var entry))
                return entry;
            throw new KeyNotFoundException($"У sprites.json немає спрайта «{name}».");
        }

        /// <summary>Запис за шляхом асета — для імпортера.</summary>
        public static Entry? OfPath(string assetPath)
        {
            Index();
            return _byPath!.TryGetValue(assetPath, out var entry) ? entry : null;
        }

        /// <summary>Спрайт за ім'ям з маніфесту. null — файл ще не імпортовано.</summary>
        public static Sprite? Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Of(name).AssetPath);

        /// <summary>Як <see cref="Load"/>, але кричить у консоль, якщо спрайта немає, — збирач не має мовчки лишити порожню картинку.</summary>
        public static Sprite Require(string name)
        {
            var sprite = Load(name);
            if (sprite == null)
                Debug.LogError($"[InkFlow] Спрайт U1Glaze «{name}» не імпортовано ({Of(name).AssetPath}). Спершу: Ink Flow → Setup → Import U1Glaze Sprites.");
            return sprite!;
        }

        /// <summary>Перечитує імпорт усіх спрайтів теки за маніфестом і збирає атлас.</summary>
        [MenuItem("Ink Flow/Setup/Import U1Glaze Sprites")]
        public static void ImportAll()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;
            Reload();
            var missing = new List<string>();
            foreach (var entry in All)
            {
                if (!File.Exists(Path.GetFullPath(entry.AssetPath)))
                {
                    missing.Add(entry.AssetPath);
                    continue;
                }
                AssetDatabase.ImportAsset(entry.AssetPath, ImportAssetOptions.ForceUpdate);
            }
            if (missing.Count > 0)
                Debug.LogError($"[InkFlow] У {Folder} бракує {missing.Count} файлів з маніфесту:\n  " + string.Join("\n  ", missing));
            BuildAtlas();
            Debug.Log($"[InkFlow] U1Glaze: {All.Count - missing.Count} спрайтів імпортовано з PPU {PixelsPerUnit:0.00}, атлас {AtlasPath}.");
        }

        /// <summary>Найбільша сторона сторінки атласу (див. <see cref="BuildAtlas"/>).</summary>
        public const int AtlasMaxSize = 4096;

        /// <summary>
        /// Один атлас на всі спрайти U1Glaze: сотня окремих текстур розірвали б батчинг UGUI
        /// (кожна зміна текстури — окремий виклик), а бюджет — ≤ 35 на екран. ASTC 4×4 на
        /// мобільних; у редакторі атлас пакується теж (SpriteAtlasV2), інакше оцінка
        /// батчів у стендах рахувала б кожну текстуру окремо.
        ///
        /// Сторінка — до <see cref="AtlasMaxSize"/>: 106 спрайтів 3× займають ≈ 6.3 Мпікс і в 2048² не
        /// вміщались — пакувальник розкладав їх на ДВІ сторінки навмання, і кожна зміна сторінки між
        /// сусідніми шарами (тінь панелі на одній, заливка на іншій) ставала окремим викликом: накладки
        /// забігу давали 39–48 викликів при бюджеті 35 (крок 2). Одна сторінка 4096×2048 — та сама пам'ять,
        /// що дві 2048² (ASTC 4×4 — 1 байт на піксель, 8 МБ).
        /// </summary>
        public static void BuildAtlas()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
            {
                Debug.Log($"[InkFlow] Sprite Packer: {EditorSettings.spritePackerMode} → SpriteAtlasV2, щоб атлас працював і в редакторі.");
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            }

            // Атлас перестворюється з нуля щоразу: один пакований об'єкт — сама тека, тож і дублів не буде,
            // і нові спрайти з маніфесту підхопляться без правок.
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Folder);
            var asset = new SpriteAtlasAsset();
            asset.Add(new UnityEngine.Object[] { folder });
            SpriteAtlasAsset.Save(asset, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
            if (importer == null)
            {
                Debug.LogError($"[InkFlow] Не вдалося відкрити імпортер атласу {AtlasPath}.");
                return;
            }

            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;

            var texture = importer.textureSettings;
            texture.sRGB = true;
            texture.filterMode = FilterMode.Bilinear;
            texture.generateMipMaps = false;
            texture.readable = false;
            importer.textureSettings = texture;

            importer.includeInBuild = true;

            foreach (var platform in new[] { "Android", "iPhone" })
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = AtlasMaxSize,
                    format = TextureImporterFormat.ASTC_4x4,
                    compressionQuality = 100
                });
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                maxTextureSize = AtlasMaxSize,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Uncompressed
            });

            importer.SaveAndReimport();
        }
    }
}
