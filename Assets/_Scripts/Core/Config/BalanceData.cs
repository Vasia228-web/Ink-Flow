using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// POCO-дзеркало BalanceConfig.asset. ЖОДНЕ балансне число не живе в коді —
    /// зміна балансу має бути зміною .asset, а не перекомпіляцією, інакше прогонник
    /// (Tools/InkFlow.Sim) неможливий, а без прогонника баланс — вгадування.
    ///
    /// Значення за замовчуванням — стартові гіпотези з майстер-доку §19; вони існують,
    /// щоб було з чого починати прогони, а не щоб їх захищати.
    /// </summary>
    public sealed class BalanceData
    {
        public BalanceData(
            int gridWidth = 8,
            int gridHeight = 8,
            int traySize = 3,
            int minPieceSize = 2,
            int maxPieceSize = 5,
            int[]? tierRounds = null,
            float bigPieceBaseWeight = 0.7f,
            float bigPieceWeightPerTier = 1.1f,
            float midPieceWeight = 1.8f,
            float smallPieceBaseWeight = 1.5f,
            float smallPieceWeightDropPerTier = 0.4f,
            float smallPieceMinWeight = 0.3f,
            int fiveCellFromTier = 1,
            float bagBias = 0.5f,
            float bagFitOffset = 1f,
            int maxTrayAttempts = 60,
            int trayShrinkAfterAttempts = 40,
            int trayRescueAttempts = 20,
            int pureLineBonus = 3,
            float[]? comboMultipliers = null,
            int scorePerPlacedCell = 10,
            int scorePerLine = 100,
            int pureLineScoreBonus = 2,
            int hintIdleSeconds = 5,
            int[]? rarityWeights = null,
            int[]? rarityGridSizes = null,
            int[]? rarityMinColors = null,
            int[]? rarityMaxColors = null,
            int continuesPerRun = 2,
            int[]? rarityStepTargets = null,
            float stepTolerance = 0.35f,
            int minFamilies = 4,
            int maxFamilies = 6,
            int dangerTightFits = 3,
            int dangerWarnShapes = 8,
            int dangerCalmShapes = 3,
            int dangerStrongShapes = 18)
        {
            if (gridWidth < 2 || gridHeight < 2)
                throw new ArgumentOutOfRangeException(nameof(gridWidth), "Поле мінімум 2×2.");
            if (traySize < 1)
                throw new ArgumentOutOfRangeException(nameof(traySize));
            if (minPieceSize < 1 || maxPieceSize < minPieceSize)
                throw new ArgumentOutOfRangeException(nameof(minPieceSize));
            if (pureLineBonus < 1)
                throw new ArgumentOutOfRangeException(nameof(pureLineBonus));
            if (maxTrayAttempts < 1 || trayRescueAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTrayAttempts));
            if (continuesPerRun < 0)
                throw new ArgumentOutOfRangeException(nameof(continuesPerRun));

            _rarityWeights = Six(rarityWeights, new[] { 45, 25, 15, 9, 5, 1 }, nameof(rarityWeights));
            _rarityGridSizes = Six(rarityGridSizes, new[] { 34, 34, 34, 34, 42, 42 }, nameof(rarityGridSizes));
            _rarityMinColors = Six(rarityMinColors, new[] { 10, 10, 11, 12, 13, 14 }, nameof(rarityMinColors));
            _rarityMaxColors = Six(rarityMaxColors, new[] { 16, 16, 16, 16, 18, 18 }, nameof(rarityMaxColors));
            _rarityStepTargets = Six(rarityStepTargets, new[] { 60, 70, 85, 110, 150, 240 }, nameof(rarityStepTargets));
            if (stepTolerance < 0f || stepTolerance >= 1f)
                throw new ArgumentOutOfRangeException(nameof(stepTolerance));
            if (minFamilies < 1 || maxFamilies < minFamilies)
                throw new ArgumentOutOfRangeException(nameof(minFamilies));
            if (dangerTightFits < 0)
                throw new ArgumentOutOfRangeException(nameof(dangerTightFits));
            if (dangerWarnShapes < 1 || dangerCalmShapes < 0 || dangerCalmShapes >= dangerWarnShapes || dangerStrongShapes < dangerWarnShapes)
                throw new ArgumentOutOfRangeException(nameof(dangerWarnShapes), "Потрібно: 0 ≤ гасне < попередження ≤ сильна.");
            StepTolerance = stepTolerance;
            MinFamilies = minFamilies;
            MaxFamilies = maxFamilies;
            DangerTightFits = dangerTightFits;
            DangerWarnShapes = dangerWarnShapes;
            DangerCalmShapes = dangerCalmShapes;
            DangerStrongShapes = dangerStrongShapes;
            RarityWeightTotal = 0;
            for (var i = 0; i < Rarities.Count; i++)
            {
                if (_rarityWeights[i] < 0 || _rarityGridSizes[i] < 1 || _rarityMinColors[i] < 1 || _rarityMaxColors[i] < _rarityMinColors[i]
                    || _rarityStepTargets[i] < 1)
                    throw new ArgumentOutOfRangeException(nameof(rarityWeights), "Таблиця рідкості зламана.");
                RarityWeightTotal += _rarityWeights[i];
            }
            if (RarityWeightTotal <= 0)
                throw new ArgumentOutOfRangeException(nameof(rarityWeights), "Хоч одна вага має бути додатною.");

            GridWidth = gridWidth;
            GridHeight = gridHeight;
            TraySize = traySize;
            MinPieceSize = minPieceSize;
            MaxPieceSize = maxPieceSize;
            TierRounds = tierRounds is null || tierRounds.Length == 0 ? new[] { 10, 20, 30 } : tierRounds;
            BigPieceBaseWeight = bigPieceBaseWeight;
            BigPieceWeightPerTier = bigPieceWeightPerTier;
            MidPieceWeight = midPieceWeight;
            SmallPieceBaseWeight = smallPieceBaseWeight;
            SmallPieceWeightDropPerTier = smallPieceWeightDropPerTier;
            SmallPieceMinWeight = smallPieceMinWeight;
            FiveCellFromTier = fiveCellFromTier;
            BagBias = bagBias;
            BagFitOffset = bagFitOffset;
            MaxTrayAttempts = maxTrayAttempts;
            TrayShrinkAfterAttempts = trayShrinkAfterAttempts;
            TrayRescueAttempts = trayRescueAttempts;
            PureLineBonus = pureLineBonus;
            ComboMultipliers = comboMultipliers is null || comboMultipliers.Length == 0
                ? new[] { 1f, 1.5f, 2f }
                : comboMultipliers;
            ScorePerPlacedCell = scorePerPlacedCell;
            ScorePerLine = scorePerLine;
            PureLineScoreBonus = pureLineScoreBonus;
            HintIdleSeconds = hintIdleSeconds;
            ContinuesPerRun = continuesPerRun;
        }

        private static int[] Six(int[]? given, int[] fallback, string name)
        {
            if (given is null || given.Length == 0)
                return fallback;
            if (given.Length != Rarities.Count)
                throw new ArgumentOutOfRangeException(name, "Шість значень: звичайна … космічна.");
            return given;
        }

        // ── Поле й лоток (§2) ──

        public int GridWidth { get; }
        public int GridHeight { get; }

        /// <summary>Скільки фігур у руці. Лоток поповнюється, лише коли ПОРОЖНІЙ (§2).</summary>
        public int TraySize { get; }

        public int MinPieceSize { get; }
        public int MaxPieceSize { get; }

        // ── Прогресія (§2): раунд = виданий лоток ──

        public int[] TierRounds { get; }
        public float BigPieceBaseWeight { get; }
        public float BigPieceWeightPerTier { get; }
        public float MidPieceWeight { get; }
        public float SmallPieceBaseWeight { get; }
        public float SmallPieceWeightDropPerTier { get; }
        public float SmallPieceMinWeight { get; }
        public int FiveCellFromTier { get; }

        // ── Мішок (§2: «дивиться на форму вільного місця») ──

        /// <summary>вага = вага_розміру × (BagFitOffset + позицій)^BagBias.</summary>
        public float BagBias { get; }
        public float BagFitOffset { get; }
        public int MaxTrayAttempts { get; }
        public int TrayShrinkAfterAttempts { get; }
        public int TrayRescueAttempts { get; }

        // ── Пікселі (§5, §19) ──

        /// <summary>«Чиста одноколірна лінія дає ×3 пікселів» — на клітинку.</summary>
        public int PureLineBonus { get; }

        /// <summary>Множник ланцюга за кількістю ліній за хід — до очок: [0] — одна лінія, [1] — дві, далі — стеля.</summary>
        public float[] ComboMultipliers { get; }

        // ── Очки (§10) ──

        public int ScorePerPlacedCell { get; }
        public int ScorePerLine { get; }
        public int PureLineScoreBonus { get; }

        /// <summary>Стільки секунд без ходу — і гра підсвічує одну валідну позицію.</summary>
        public int HintIdleSeconds { get; }

        // ── Рідкість (§6, §19) ──

        private readonly int[] _rarityWeights;
        private readonly int[] _rarityGridSizes;
        private readonly int[] _rarityMinColors;
        private readonly int[] _rarityMaxColors;
        private readonly int[] _rarityStepTargets;

        /// <summary>Шанси у ваговій формі: 45 / 25 / 15 / 9 / 5 / 1. Індекс — (int)<see cref="Rarity"/>.</summary>
        public IReadOnlyList<int> RarityWeights => _rarityWeights;
        public int RarityWeightTotal { get; }

        /// <summary>Найбільша сітка файлу картинки цієї рідкості (§4: ~32 px + контурне кільце; легендарна й космічна — до 40).</summary>
        public int GridSizeFor(Rarity rarity) => _rarityGridSizes[(int)rarity];

        /// <summary>Скільки тонів має картинка (§4: 10–16; рідкісніша — детальніша).</summary>
        public int MinColorsFor(Rarity rarity) => _rarityMinColors[(int)rarity];
        public int MaxColorsFor(Rarity rarity) => _rarityMaxColors[(int)rarity];

        /// <summary>Ціль кроків до завершення (§6, §19) — стільки, скільки було пікселів у попередній редакції.</summary>
        public int StepTargetFor(Rarity rarity) => _rarityStepTargets[(int)rarity];

        /// <summary>Допуск на ціль кроків: картинка з (1 ± допуск) × ціль — у нормі.</summary>
        public float StepTolerance { get; }

        /// <summary>Родин (ігрових кольорів) на картинку (§3): 4–6.</summary>
        public int MinFamilies { get; }
        public int MaxFamilies { get; }

        // ── Пульсація «мало місця» (§11) ──

        /// <summary>Форма каталогу «тісна», якщо їй лишилось не більше стількох місць на полі.</summary>
        public int DangerTightFits { get; }

        /// <summary>Попередження вмикається, коли тісних форм щонайменше стільки (або фігура з руки застрягла).</summary>
        public int DangerWarnShapes { get; }

        /// <summary>Попередження гасне, лише коли тісних форм знову не більше стількох (гістерезис).</summary>
        public int DangerCalmShapes { get; }

        /// <summary>Сильна пульсація: тісних форм щонайменше стільки, або фігура з руки вже нікуди не влазить.</summary>
        public int DangerStrongShapes { get; }

        /// <summary>Рідкість за кидком у [0, RarityWeightTotal).</summary>
        public Rarity RarityFor(int roll)
        {
            if (roll < 0 || roll >= RarityWeightTotal)
                throw new ArgumentOutOfRangeException(nameof(roll));
            var acc = 0;
            for (var i = 0; i < _rarityWeights.Length; i++)
            {
                acc += _rarityWeights[i];
                if (roll < acc)
                    return (Rarity)i;
            }
            return Rarity.Common;
        }

        // ── Продовження (§10) ──

        /// <summary>Скільки разів за забіг можна продовжити після програшу: перший — за ролик, далі — за нафту (§13).</summary>
        public int ContinuesPerRun { get; }

        // ── Похідні ──

        public int TierFor(int round)
        {
            var tier = 0;
            for (var i = 0; i < TierRounds.Length; i++)
                if (round >= TierRounds[i])
                    tier++;
            return tier;
        }

        public float SizeWeight(int size, int tier)
        {
            if (size < MinPieceSize || size > MaxPieceSize)
                return 0f;
            if (size >= 5 && tier < FiveCellFromTier)
                return 0f;
            if (size >= 4)
                return BigPieceBaseWeight + tier * BigPieceWeightPerTier;
            if (size == 3)
                return MidPieceWeight;
            var small = SmallPieceBaseWeight - tier * SmallPieceWeightDropPerTier;
            return small < SmallPieceMinWeight ? SmallPieceMinWeight : small;
        }

        public int SizeCapFor(int tier) => tier >= FiveCellFromTier ? MaxPieceSize : Math.Min(4, MaxPieceSize);

        public float ComboFor(int lineCount)
        {
            if (lineCount <= 0)
                return 0f;
            var index = lineCount - 1;
            return index < ComboMultipliers.Length
                ? ComboMultipliers[index]
                : ComboMultipliers[ComboMultipliers.Length - 1];
        }

        public static readonly BalanceData Default = new BalanceData();
    }
}
