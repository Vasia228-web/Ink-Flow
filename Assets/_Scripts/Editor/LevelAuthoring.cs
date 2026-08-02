using InkFlow.Core;
using InkFlow.Gameplay;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Генерує .asset рівнів із розкладок, що лежать у Core (StarterLevels).
    ///
    /// Джерело правди ОДНЕ — `InkFlow.Core.StarterLevels`. Ним же користуються тест
    /// розв'язності (§14.5) і симулятор (§15). Якщо описати рівні ще й тут, «перевірений»
    /// рівень і рівень у грі розійдуться — що вже одного разу сталося.
    /// Далі левел-дизайнер редагує згенеровані асети в Inspector, код не потрібен.
    /// </summary>
    public static class LevelAuthoring
    {
        public const string LevelsFolder = "Assets/_ScriptableObjects/Levels";

        [MenuItem("Ink Flow/Setup/Create Starter Levels")]
        public static void CreateStarterLevels()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            InkFlowBootstrap.EnsureFolder(LevelsFolder);

            var levels = StarterLevels.All();
            foreach (var level in levels)
            {
                var path = PathFor(level.LevelId);
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                Apply(asset, level);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[InkFlow] Стартові рівні готові: {levels.Length} шт. у {LevelsFolder}");
        }

        private static void Apply(LevelDefinition asset, LevelData level)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("levelId").intValue = level.LevelId;
            so.FindProperty("width").intValue = level.Width;
            so.FindProperty("height").intValue = level.Height;
            so.FindProperty("colorsCount").intValue = level.ColorsCount;
            so.FindProperty("maxMoves").intValue = level.MaxMoves;
            so.FindProperty("goal").intValue = (int)level.Goal;
            so.FindProperty("bossSegments").intValue = level.BossSegments;

            var cells = so.FindProperty("startingCells");
            cells.arraySize = level.StartingCells.Count;
            for (var i = 0; i < level.StartingCells.Count; i++)
            {
                var seed = level.StartingCells[i];
                var entry = cells.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("x").intValue = seed.Position.X;
                entry.FindPropertyRelative("y").intValue = seed.Position.Y;
                entry.FindPropertyRelative("color").intValue = (int)seed.Color;
                entry.FindPropertyRelative("density").intValue = seed.Density;
                entry.FindPropertyRelative("flags").intValue = (int)seed.Flags;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string PathFor(int levelId) => $"{LevelsFolder}/Level_{levelId:D3}.asset";

        /// <summary>Шляхи всіх рівнів — потрібні бутстрапу (Addressables) і перевіркам.</summary>
        public static string[] LevelAssetPaths()
        {
            var levels = StarterLevels.All();
            var paths = new string[levels.Length];
            for (var i = 0; i < levels.Length; i++)
                paths[i] = PathFor(levels[i].LevelId);
            return paths;
        }
    }
}
