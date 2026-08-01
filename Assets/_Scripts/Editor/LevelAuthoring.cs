using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Gameplay;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Стартові рівні, описані кодом і згенеровані в .asset (§7).
    /// Далі левел-дизайнер редагує їх у Inspector — код більше не потрібен.
    /// Кожен рівень зобов'язаний бути розв'язним: це доводить LevelSolver (§15),
    /// а не «здається, проходиться».
    /// </summary>
    public static class LevelAuthoring
    {
        public const string LevelsFolder = "Assets/_ScriptableObjects/Levels";

        private sealed class Layout
        {
            public int Id;
            public int Width;
            public int Height;
            public int Colors;
            public int Moves;
            public PuzzleGoal Goal = PuzzleGoal.Clear;
            public int BossSegments = 5;
            public List<LevelDefinition.CellEntry> Cells = new List<LevelDefinition.CellEntry>();
        }

        private static LevelDefinition.CellEntry Cell(int x, int y, InkColor color, int density,
            CellFlags flags = CellFlags.None) =>
            new LevelDefinition.CellEntry { x = x, y = y, color = color, density = density, flags = flags };

        private static readonly Layout[] Levels =
        {
            // 001 — навчання злиттю: одна фарба, вибухів немає, ціль «одна крапля».
            new Layout
            {
                Id = 1, Width = 4, Height = 4, Colors = 1, Moves = 5,
                Cells =
                {
                    Cell(1, 1, InkColor.Magenta, 1), Cell(2, 1, InkColor.Magenta, 1),
                    Cell(1, 2, InkColor.Magenta, 2), Cell(2, 2, InkColor.Magenta, 2)
                }
            },

            // 002 — перший вибух: злиття 4+6 дає рівно поріг, гравець бачить хрест і фарбування.
            new Layout
            {
                Id = 2, Width = 5, Height = 5, Colors = 2, Moves = 8,
                Cells =
                {
                    Cell(1, 2, InkColor.Magenta, 4), Cell(2, 2, InkColor.Magenta, 6),
                    Cell(3, 2, InkColor.Cyan, 1), Cell(2, 3, InkColor.Cyan, 1),
                    Cell(2, 1, InkColor.Cyan, 1), Cell(1, 3, InkColor.Cyan, 1)
                }
            },

            // 003 — ланцюг: важкий вибух підпалює сусіда того ж кольору.
            new Layout
            {
                Id = 3, Width = 5, Height = 5, Colors = 2, Moves = 10,
                Cells =
                {
                    Cell(1, 1, InkColor.Magenta, 5), Cell(2, 1, InkColor.Magenta, 6),
                    Cell(3, 1, InkColor.Magenta, 9),
                    Cell(1, 3, InkColor.Cyan, 2), Cell(2, 3, InkColor.Cyan, 2),
                    Cell(3, 3, InkColor.Cyan, 1)
                }
            }
        };

        [MenuItem("Ink Flow/Setup/Create Starter Levels")]
        public static void CreateStarterLevels()
        {
            InkFlowBootstrap.EnsureFolder(LevelsFolder);

            foreach (var layout in Levels)
            {
                var path = $"{LevelsFolder}/Level_{layout.Id:D3}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                Apply(asset, layout);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[InkFlow] Стартові рівні готові: {Levels.Length} шт. у {LevelsFolder}");
        }

        private static void Apply(LevelDefinition asset, Layout layout)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("levelId").intValue = layout.Id;
            so.FindProperty("width").intValue = layout.Width;
            so.FindProperty("height").intValue = layout.Height;
            so.FindProperty("colorsCount").intValue = layout.Colors;
            so.FindProperty("maxMoves").intValue = layout.Moves;
            so.FindProperty("goal").enumValueIndex = (int)layout.Goal;
            so.FindProperty("bossSegments").intValue = layout.BossSegments;

            var cells = so.FindProperty("startingCells");
            cells.arraySize = layout.Cells.Count;
            for (var i = 0; i < layout.Cells.Count; i++)
            {
                var entry = cells.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("x").intValue = layout.Cells[i].x;
                entry.FindPropertyRelative("y").intValue = layout.Cells[i].y;
                entry.FindPropertyRelative("color").enumValueIndex = (int)layout.Cells[i].color;
                entry.FindPropertyRelative("density").intValue = layout.Cells[i].density;
                entry.FindPropertyRelative("flags").intValue = (int)layout.Cells[i].flags;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Шляхи всіх рівнів — потрібні бутстрапу (Addressables) і тесту розв'язності.</summary>
        public static string[] LevelAssetPaths()
        {
            var paths = new string[Levels.Length];
            for (var i = 0; i < Levels.Length; i++)
                paths[i] = $"{LevelsFolder}/Level_{Levels[i].Id:D3}.asset";
            return paths;
        }
    }
}
