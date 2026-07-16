using System.Collections.Generic;
using UnityEngine;

namespace InkFlow.Core
{
    /// <summary>
    /// Конфігурація рівня для левел-дизайнерів — редагується в Inspector, без коду.
    /// Єдиний клас Core з UnityEngine-залежністю: тільки серіалізація даних, жодної логіки.
    /// Уся логіка працює з POCO-знімком LevelData (див. ToLevelData()).
    /// </summary>
    [CreateAssetMenu(fileName = "Level_000", menuName = "Ink Flow/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [System.Serializable]
        public struct CellEntry
        {
            [Tooltip("Ряд (0 — нижній).")]
            public int row;

            [Tooltip("Колонка (0 — ліва).")]
            public int col;

            [Tooltip("Індекс кольору: 0..colorsCount-1.")]
            public int color;

            [Tooltip("Стартова густота фарби (мінімум 1). Що ближче до burstThreshold, то ближче клітинка до вибуху.")]
            [Min(1)] public int density;
        }

        [Header("Сітка")]
        [Tooltip("Розмір сітки N×N.")]
        [SerializeField, Range(2, 9)] private int gridSize = 5;

        [Tooltip("Скільки кольорів використовує рівень.")]
        [SerializeField, Range(1, 6)] private int colorsCount = 3;

        [Header("Баланс")]
        [Tooltip("Поріг лопання: клітинка вибухає, щойно її густота досягає цього значення. " +
                 "Менший поріг = частіші вибухи і легші ланцюги. Мінімум 2.")]
        [SerializeField, Min(2)] private int burstThreshold = 10;

        [Tooltip("Ліміт ходів. Витрачаються лише валідні merge — відскоки безкоштовні.")]
        [SerializeField, Min(1)] private int maxMoves = 20;

        [Header("Умова перемоги")]
        [SerializeField] private WinConditionType winConditionType = WinConditionType.ClearSingleColor;

        [Tooltip("Тільки для ScoreAttack: цільовий скор (скор = сума густот усіх клітинок, що лопнули).")]
        [SerializeField, Min(0)] private int targetScore;

        [Header("Стартова розстановка")]
        [SerializeField] private List<CellEntry> startingCells = new List<CellEntry>();

        public int GridSize => gridSize;
        public int ColorsCount => colorsCount;
        public int BurstThreshold => burstThreshold;
        public int MaxMoves => maxMoves;
        public WinConditionType WinConditionType => winConditionType;
        public int TargetScore => targetScore;
        public IReadOnlyList<CellEntry> StartingCells => startingCells;

        /// <summary>POCO-знімок для GridModel/GameSession — логіка не торкається ScriptableObject.</summary>
        public LevelData ToLevelData()
        {
            var seeds = new List<CellSeed>(startingCells.Count);
            foreach (var entry in startingCells)
                seeds.Add(new CellSeed(entry.row, entry.col, entry.color, entry.density));

            return new LevelData(
                gridSize, colorsCount, burstThreshold, maxMoves,
                winConditionType, targetScore, seeds);
        }

        private void OnValidate()
        {
            for (var i = 0; i < startingCells.Count; i++)
            {
                var entry = startingCells[i];
                entry.row = Mathf.Clamp(entry.row, 0, gridSize - 1);
                entry.col = Mathf.Clamp(entry.col, 0, gridSize - 1);
                entry.color = Mathf.Clamp(entry.color, 0, colorsCount - 1);
                entry.density = Mathf.Max(1, entry.density);
                startingCells[i] = entry;
            }
        }
    }
}
