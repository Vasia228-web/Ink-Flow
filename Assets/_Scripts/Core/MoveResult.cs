using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Зміна однієї сусідньої клітинки внаслідок вибуху: порожня пофарбувалась
    /// або однокольорова отримала +1. Знімок стану ПІСЛЯ цього вибуху —
    /// в'ю може відтворити ланцюг крок за кроком.
    /// </summary>
    public readonly struct CellChange
    {
        public GridPos Position { get; }
        public int Color { get; }
        public int DensityAfter { get; }

        /// <summary>true — клітинка була порожня і пофарбувалась; false — мала той самий колір і отримала +1.</summary>
        public bool WasPainted { get; }

        public CellChange(GridPos position, int color, int densityAfter, bool wasPainted)
        {
            Position = position;
            Color = color;
            DensityAfter = densityAfter;
            WasPainted = wasPainted;
        }
    }

    /// <summary>Один вибух у ланцюгу — для скору, анімацій та звуку.</summary>
    public readonly struct BurstRecord
    {
        public GridPos Position { get; }
        public int Color { get; }

        /// <summary>Density клітинки в момент вибуху — саме стільки йде в скор.</summary>
        public int Density { get; }

        /// <summary>Що сталося із сусідами цього вибуху (порядок Up, Right, Down, Left).</summary>
        public IReadOnlyList<CellChange> NeighborChanges { get; }

        public BurstRecord(GridPos position, int color, int density, IReadOnlyList<CellChange> neighborChanges)
        {
            Position = position;
            Color = color;
            Density = density;
            NeighborChanges = neighborChanges ?? Array.Empty<CellChange>();
        }
    }

    /// <summary>
    /// Результат застосування свайпу до моделі. Кажe в'ю/HUD, що сталося:
    /// чи витрачено хід, куди злилась фарба і які вибухи відбулись (у порядку ланцюга).
    /// </summary>
    public sealed class MoveResult
    {
        /// <summary>true — стався валідний merge, хід витрачається. false — «відскок», хід НЕ витрачається.</summary>
        public bool IsValidMerge { get; }

        public GridPos From { get; }
        public GridPos To { get; }

        /// <summary>Вибухи в порядку ланцюгової реакції. Порожній список — merge без burst.</summary>
        public IReadOnlyList<BurstRecord> Bursts { get; }

        /// <summary>Довжина ланцюга — для посилення screen shake/звуку у Фазі 2.</summary>
        public int ChainLength => Bursts.Count;

        /// <summary>Зароблений скор: сума Density усіх burst цього ходу.</summary>
        public int ScoreGained { get; }

        private MoveResult(bool isValidMerge, GridPos from, GridPos to, IReadOnlyList<BurstRecord> bursts)
        {
            IsValidMerge = isValidMerge;
            From = from;
            To = to;
            Bursts = bursts;
            var score = 0;
            foreach (var burst in bursts)
                score += burst.Density;
            ScoreGained = score;
        }

        public static MoveResult Rejected(GridPos from, GridPos to) =>
            new MoveResult(false, from, to, Array.Empty<BurstRecord>());

        public static MoveResult Merged(GridPos from, GridPos to, IReadOnlyList<BurstRecord> bursts) =>
            new MoveResult(true, from, to, bursts ?? Array.Empty<BurstRecord>());
    }
}
