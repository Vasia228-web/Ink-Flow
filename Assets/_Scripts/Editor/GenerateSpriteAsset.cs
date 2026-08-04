using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace InkFlow.Editor
{
    /// <summary>
    /// Збирає TMP Sprite Asset з наших іконок і вішає його як типовий у TMP Settings.
    /// Меню: Ink Flow → Setup → Generate TMP Sprite Asset.
    ///
    /// Навіщо: ★ немає в Nunito. Прив'язавши спрайт до коду U+2605, ми робимо так,
    /// що звичайний символ ★ у будь-якому тексті рендериться нашою іконкою — без
    /// rich-text розмітки в кожному рядку і без другого шрифту в білді.
    /// </summary>
    public static class GenerateSpriteAsset
    {
        private const string SpriteFolder = "Assets/_Sprites/UI";
        private const string AssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>Іконка → символ, під яким вона підставляється в текст.</summary>
        private static readonly (string file, string name, uint unicode)[] Icons =
        {
            ("icon-star", "star", 0x2605),   // ★
            ("icon-retry", "retry", 0x21BA)  // ↺
        };

        [MenuItem("Ink Flow/Setup/Generate TMP Sprite Asset")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            var sprites = new List<(Sprite sprite, string path, string name, uint unicode)>();
            foreach (var (file, name, unicode) in Icons)
            {
                var path = $"{SpriteFolder}/{file}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogError($"[InkFlow] Немає {path} — спершу Generate UI Sprites.");
                    return;
                }

                sprites.Add((sprite, path, name, unicode));
            }

            AssetDatabase.DeleteAsset(AssetPath);

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "InkFlow Icons";

            // Усі іконки лежать в одній текстурі-атласі: TMP вимагає spriteSheet,
            // а не набір окремих Sprite. Склеюємо їх горизонтально.
            var atlas = BuildAtlas(sprites, out var rects);
            asset.spriteSheet = atlas;
            // Таблиці лише для читання: наповнюємо наявні списки, не присвоюємо нові.
            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();

            for (var i = 0; i < sprites.Count; i++)
            {
                var rect = rects[i];
                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    // Іконка сідає на базову лінію й піднімається на висоту рядка.
                    metrics = new GlyphMetrics(rect.width, rect.height, 0f, rect.height * 0.82f, rect.width),
                    glyphRect = new GlyphRect((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height),
                    scale = 1f,
                    sprite = sprites[i].sprite
                };
                asset.spriteGlyphTable.Add(glyph);

                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(sprites[i].unicode, glyph)
                {
                    name = sprites[i].name,
                    scale = 1f
                });
            }

            asset.UpdateLookupTables();

            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader != null)
            {
                var material = new Material(shader) { name = "InkFlow Icons Material" };
                material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
                asset.material = material;
            }

            AssetDatabase.CreateAsset(asset, AssetPath);
            atlas.name = "InkFlow Icons Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            if (asset.material != null)
                AssetDatabase.AddObjectToAsset(asset.material, asset);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            AssignAsDefault(asset);

            Debug.Log($"[InkFlow] TMP Sprite Asset готовий: {AssetPath}. " +
                      "Символи ★ і ↺ тепер рендеряться іконками в будь-якому тексті.");
        }

        /// <summary>Склеює іконки в одну текстуру-атлас і повертає їхні прямокутники.</summary>
        private static Texture2D BuildAtlas(
            List<(Sprite sprite, string path, string name, uint unicode)> sprites, out List<Rect> rects)
        {
            const int cell = 128;
            var atlas = new Texture2D(cell * sprites.Count, cell, TextureFormat.RGBA32, false)
            {
                name = "InkFlow Icons Atlas",
                filterMode = FilterMode.Bilinear
            };

            var clear = new Color[atlas.width * atlas.height];
            atlas.SetPixels(clear);

            rects = new List<Rect>();
            for (var i = 0; i < sprites.Count; i++)
            {
                // PNG читаємо з диска, а не через sprite.texture: імпортовані текстури
                // не мають CPU-копії (Read/Write вимкнено), і GetPixel* кидає виняток.
                // Вмикати Read/Write заради генерації означало б тягнути зайву копію
                // кожної іконки в пам'ять білда.
                var source = LoadReadable(sprites[i].path);
                var scaled = ScaleTo(source, cell);
                Object.DestroyImmediate(source);
                atlas.SetPixels(i * cell, 0, cell, cell, scaled.GetPixels());
                Object.DestroyImmediate(scaled);
                rects.Add(new Rect(i * cell, 0, cell, cell));
            }

            atlas.Apply();
            return atlas;
        }

        /// <summary>Читає PNG з диска у тимчасову текстуру з доступом до пікселів.</summary>
        private static Texture2D LoadReadable(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(assetPath)));
            return texture;
        }

        private static Texture2D ScaleTo(Texture2D source, int size)
        {
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Робить асет типовим у TMP Settings — інакше кожен TMP_Text довелось би
        /// налаштовувати окремо, і перший же забутий напис дав би порожній квадрат.
        /// </summary>
        private static void AssignAsDefault(TMP_SpriteAsset asset)
        {
            var settings = AssetDatabase.LoadMainAssetAtPath(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"[InkFlow] Немає {TmpSettingsPath} — призначай Sprite Asset вручну.");
                return;
            }

            var so = new SerializedObject(settings);
            var property = so.FindProperty("m_defaultSpriteAsset");
            if (property == null)
                return;

            property.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }
    }
}
