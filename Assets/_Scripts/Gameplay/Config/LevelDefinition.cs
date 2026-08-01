using System.Collections.Generic;
using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// LevelDefinition.asset (§7) — рівень для левел-дизайнера: редагується в Inspector,
    /// роздається через Addressables (група "Levels"), щоб додавати рівні без нового білду.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_000", menuName = "Ink Flow/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [System.Serializable]
        public struct CellEntry
        {
            [Tooltip("Колонка, 0 — ліва.")] public int x;
            [Tooltip("Ряд, 0 — НИЖНІЙ (верхній ряд «дострілює» до боса).")] public int y;
            public InkColor color;
            [Min(1)] public int density;
            [Tooltip("Модифікатори: лід, стіна, клякса, важка крапля.")] public CellFlags flags;
        }

        [Header("Ідентифікація")]
        [Tooltip("Унікальний id рівня. З нього ж сідиться рандом бризок — те саме рішення завжди дає той самий результат.")]
        [SerializeField, Min(1)] private int levelId = 1;

        [Header("Поле")]
        [SerializeField, Range(2, 9)] private int width = 4;
        [SerializeField, Range(2, 9)] private int height = 4;
        [SerializeField, Range(1, 6)] private int colorsCount = 3;

        [Header("Правила рівня")]
        [SerializeField, Min(1)] private int maxMoves = 10;
        [SerializeField] private PuzzleGoal goal = PuzzleGoal.Clear;

        [Tooltip("Скільки сегментів у Клякса (лише для бос-рівня).")]
        [SerializeField, Range(1, 8)] private int bossSegments = 5;

        [Header("Стартова розкладка")]
        [SerializeField] private List<CellEntry> startingCells = new List<CellEntry>();

        public int LevelId => levelId;
        public bool IsBoss => goal == PuzzleGoal.DefeatBoss;

        public LevelData ToLevelData()
        {
            var seeds = new List<CellSeed>(startingCells.Count);
            foreach (var entry in startingCells)
                seeds.Add(new CellSeed(entry.x, entry.y, entry.color, entry.density, entry.flags));

            return new LevelData(levelId, width, height, maxMoves, goal, seeds, colorsCount, bossSegments);
        }

        private void OnValidate()
        {
            for (var i = 0; i < startingCells.Count; i++)
            {
                var entry = startingCells[i];
                entry.x = Mathf.Clamp(entry.x, 0, width - 1);
                entry.y = Mathf.Clamp(entry.y, 0, height - 1);
                entry.density = Mathf.Max(1, entry.density);
                if (entry.color == InkColor.None)
                    entry.color = InkColor.Magenta;
                startingCells[i] = entry;
            }
        }
    }
}
