using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Gameplay;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Збирає всі Assets/_Pictures/**/*.txt у PictureLibrary.asset (документ §7).
    /// Меню: Ink Flow → Setup → Refresh Picture Library. Викликається і з Bootstrap Assets.
    /// Перевіряє кожну картинку тим самим парсером, що й гра: зламаний файл не потрапить в асет.
    /// </summary>
    public static class RefreshPictureLibrary
    {
        public const string AssetPath = "Assets/_ScriptableObjects/Pictures/PictureLibrary.asset";
        public const string PicturesFolder = "Assets/_Pictures";

        [MenuItem("Ink Flow/Setup/Refresh Picture Library")]
        public static void Refresh()
        {
            InkFlowBootstrap.EnsureFolder("Assets/_ScriptableObjects/Pictures");
            var asset = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PictureLibraryAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { PicturesFolder });
            var paths = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }
            paths.Sort(System.StringComparer.Ordinal);

            var texts = new List<TextAsset>(paths.Count);
            var counts = new int[Rarities.Count];
            var broken = 0;
            foreach (var path in paths)
            {
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (text == null)
                    continue;
                try
                {
                    var picture = PixelPicture.Parse(text.text, text.name);
                    counts[(int)picture.Rarity]++;
                    texts.Add(text);
                }
                catch (System.ArgumentException e)
                {
                    broken++;
                    Debug.LogError($"[InkFlow] {path}: {e.Message}", text);
                }
            }

            var so = new SerializedObject(asset);
            var list = so.FindProperty("pictures");
            list.arraySize = texts.Count;
            for (var i = 0; i < texts.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var summary = new List<string>();
            for (var r = 0; r < Rarities.Count; r++)
                summary.Add($"{Rarities.IdOf((Rarity)r)} {counts[r]}");
            Debug.Log($"[InkFlow] Бібліотека картинок: {texts.Count} ({string.Join(", ", summary)})" +
                      (broken > 0 ? $"; зламаних пропущено: {broken}" : "") + $" → {AssetPath}");
        }
    }
}
