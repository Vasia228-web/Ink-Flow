using System.Collections.Generic;
using System.IO;
using InkFlow.Core;
using InkFlow.UI;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Робить маски зон картинок із креслень <see cref="PictureCatalogData"/>: одна PNG
    /// на зону (округлені клітинки з проміжком, як у прототипі v3), усі спрайти картинки
    /// одного розміру — і каталог <see cref="PictureArtCatalog"/> з центрами й радіусами
    /// зон для фронту заливки. Меню: Ink Flow → Setup → Generate Picture Art.
    ///
    /// Core пікселів не бачить: креслення — єдине джерело і для правил, і для картинки.
    /// </summary>
    public static class GeneratePictureArt
    {
        private const string Folder = "Assets/_Sprites/Pictures";
        private const string AssetFolder = "Assets/_ScriptableObjects/Pictures";
        public const string CatalogPath = AssetFolder + "/PictureArt.asset";

        /// <summary>Клітинка креслення в пікселях і проміжок між клітинками.</summary>
        private const int Cell = 16;
        private const int Gap = 2;
        private const float Radius = 4f;

        [MenuItem("Ink Flow/Setup/Generate Picture Art")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            InkFlowBootstrap.EnsureFolder(Folder);
            InkFlowBootstrap.EnsureFolder(AssetFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<PictureArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PictureArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new List<PictureArtCatalog.PictureArt>();
            var pictures = PictureCatalogData.Default;
            for (var p = 0; p < pictures.Count; p++)
                entries.Add(Build(pictures[p]));

            catalog.Pictures = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InkFlow] Маски картинок згенеровано: {pictures.Count} картинок у {Folder}, каталог {CatalogPath}.");
        }

        private static PictureArtCatalog.PictureArt Build(PictureDef def)
        {
            var step = Cell + Gap;
            var width = def.Width * step + Gap;
            var height = def.Height * step + Gap;
            var entry = new PictureArtCatalog.PictureArt { id = def.Id };
            var sprites = new Sprite[def.ZoneCount];
            var origins = new Vector2[def.ZoneCount];
            var extents = new float[def.ZoneCount];

            for (var z = 0; z < def.ZoneCount; z++)
            {
                var cells = def.CellsOf(z);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color[width * height];
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color(1f, 1f, 1f, 0f);

                var centroid = Vector2.zero;
                foreach (var cell in cells)
                {
                    // Y креслення — згори вниз; Y текстури — знизу вгору.
                    var x0 = Gap + cell.X * step;
                    var y0 = Gap + (def.Height - 1 - cell.Y) * step;
                    PaintRoundedCell(pixels, width, x0, y0);
                    centroid += new Vector2((x0 + Cell * 0.5f) / width, (y0 + Cell * 0.5f) / height);
                }
                centroid /= cells.Count;

                var extent = 0f;
                foreach (var cell in cells)
                {
                    var cx = (Gap + cell.X * step + Cell * 0.5f) / width;
                    var cy = (Gap + (def.Height - 1 - cell.Y) * step + Cell * 0.5f) / height;
                    var d = Vector2.Distance(centroid, new Vector2(cx, cy));
                    if (d > extent) extent = d;
                }
                // Плюс пів діагоналі клітинки: фронт має накрити її кути, не лише центр.
                extent += 0.71f * Cell / width;

                texture.SetPixels(pixels);
                texture.Apply();
                var file = $"{def.Id}_{z}.png";
                sprites[z] = WriteSprite(file, texture);
                origins[z] = centroid;
                extents[z] = Mathf.Max(extent, 0.05f);
            }

            entry.zones = sprites;
            entry.origins = origins;
            entry.extents = extents;
            return entry;
        }

        /// <summary>Округлений квадрат Cell×Cell з антиаліасингом у 1 px по краю.</summary>
        private static void PaintRoundedCell(Color[] pixels, int width, int x0, int y0)
        {
            for (var y = 0; y < Cell; y++)
                for (var x = 0; x < Cell; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var dx = Mathf.Max(Radius - px, px - (Cell - Radius), 0f);
                    var dy = Mathf.Max(Radius - py, py - (Cell - Radius), 0f);
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(Radius - d + 0.5f);
                    if (a <= 0f)
                        continue;
                    pixels[(y0 + y) * width + x0 + x] = new Color(1f, 1f, 1f, a);
                }
        }

        private static Sprite WriteSprite(string fileName, Texture2D texture)
        {
            var path = $"{Folder}/{fileName}";
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
