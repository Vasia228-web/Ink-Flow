using System;

namespace InkFlow.Core
{
    /// <summary>
    /// POCO-дзеркало BalanceConfig.asset. ЖОДНЕ балансне число не живе в коді —
    /// зміна балансу має бути зміною .asset, а не перекомпіляцією, інакше прогонник
    /// (Tools/InkFlow.Sim) неможливий, а без прогонника баланс — вгадування.
    ///
    /// Значення за замовчуванням — стартові гіпотези: документ §12 там, де він дає
    /// число, і мертвий код прототипу v3 там, де документ мовчить (прийнято автором
    /// 2026-09-23). Вони існують, щоб було з чого починати прогони, а не щоб їх захищати.
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
            float[]? colorStreakByTier = null,
            float colorScarcityWeight = 0.7f,
            int maxTrayAttempts = 60,
            int trayShrinkAfterAttempts = 40,
            int trayRescueAttempts = 20,
            int mixedDivisor = 2,
            int pureLineBonus = 3,
            float[]? comboMultipliers = null,
            int scorePerPlacedCell = 10,
            int scorePerLine = 100,
            int pureLineScoreBonus = 2,
            int haloWarningFreeCells = 20,
            int hintIdleSeconds = 5,
            int mixerSplashSize = 8,
            float mixDominantShare = 0.6f,
            float mixMinorShare = 0.25f,
            int cellsPerSplash = 12,
            int maxSplashesPerZone = 3)
        {
            if (cellsPerSplash < 1)
                throw new ArgumentOutOfRangeException(nameof(cellsPerSplash));
            if (maxSplashesPerZone < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSplashesPerZone));
            if (mixerSplashSize < 1)
                throw new ArgumentOutOfRangeException(nameof(mixerSplashSize));
            if (mixDominantShare <= 0.5f || mixDominantShare > 1f)
                throw new ArgumentOutOfRangeException(nameof(mixDominantShare), "Домінувати може лише більша половина.");
            if (mixMinorShare <= 0f || mixMinorShare > 1f / 3f)
                throw new ArgumentOutOfRangeException(nameof(mixMinorShare), "Поріг помітності не більший за третину, інакше є пропорції без відтінку.");
            if (gridWidth < 2 || gridHeight < 2)
                throw new ArgumentOutOfRangeException(nameof(gridWidth), "Поле мінімум 2×2.");
            if (traySize < 1)
                throw new ArgumentOutOfRangeException(nameof(traySize));
            if (minPieceSize < 1 || maxPieceSize < minPieceSize)
                throw new ArgumentOutOfRangeException(nameof(minPieceSize));
            if (mixedDivisor < 1)
                throw new ArgumentOutOfRangeException(nameof(mixedDivisor));
            if (pureLineBonus < 1)
                throw new ArgumentOutOfRangeException(nameof(pureLineBonus));
            if (maxTrayAttempts < 1 || trayRescueAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTrayAttempts));

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
            ColorStreakByTier = colorStreakByTier is null || colorStreakByTier.Length == 0
                ? new[] { 0.5f, 0.35f, 0.2f, 0.1f }
                : colorStreakByTier;
            ColorScarcityWeight = colorScarcityWeight;
            MaxTrayAttempts = maxTrayAttempts;
            TrayShrinkAfterAttempts = trayShrinkAfterAttempts;
            TrayRescueAttempts = trayRescueAttempts;
            MixedDivisor = mixedDivisor;
            PureLineBonus = pureLineBonus;
            ComboMultipliers = comboMultipliers is null || comboMultipliers.Length == 0
                ? new[] { 1f, 1.5f, 2f }
                : comboMultipliers;
            ScorePerPlacedCell = scorePerPlacedCell;
            ScorePerLine = scorePerLine;
            PureLineScoreBonus = pureLineScoreBonus;
            HaloWarningFreeCells = haloWarningFreeCells;
            HintIdleSeconds = hintIdleSeconds;
            MixerSplashSize = mixerSplashSize;
            MixDominantShare = mixDominantShare;
            MixMinorShare = mixMinorShare;
            CellsPerSplash = cellsPerSplash;
            MaxSplashesPerZone = maxSplashesPerZone;
        }

        // ── Картинка (§5, §12) ──

        /// <summary>
        /// Скільки клітинок креслення «коштують» один виплеск. Стеля зони — ЦІЛЕ число
        /// виплесків (§12: «~1 виплеск на зону»): маленька зона — один, велика — два-три,
        /// і тоді після першого видно часткову заливку (§4). Дробова стеля втрачала б
        /// решту кожного виплеску просто так.
        /// </summary>
        public int CellsPerSplash { get; }

        /// <summary>Найбільша зона просить не більше стількох виплесків.</summary>
        public int MaxSplashesPerZone { get; }

        /// <summary>Стеля зони у фарбі: від одного до MaxSplashesPerZone виплесків.</summary>
        public int ZoneCapacity(int cells)
        {
            var splashes = (int)Math.Round((double)cells / CellsPerSplash, MidpointRounding.AwayFromZero);
            if (splashes < 1) splashes = 1;
            if (splashes > MaxSplashesPerZone) splashes = MaxSplashesPerZone;
            return splashes * MixerSplashSize;
        }

        // ── Змішувач (§4, §12) ──

        /// <summary>
        /// Скільки фарби йде в один виплеск. Це ж — стеля бака на індикаторі: після ходу
        /// в трьох баках разом завжди менше за виплеск. 8 = дві-три мішані лінії або
        /// дві третини чистої вісімки; змішувач спрацьовує приблизно раз на лоток.
        /// </summary>
        public int MixerSplashSize { get; }

        /// <summary>Частка одного пігменту, від якої виплеск — чистий цей колір (§4: «один колір домінує»).</summary>
        public float MixDominantShare { get; }

        /// <summary>Частка, нижче якої пігмент у пропорції не помічається. Двоє помітних — вторинний, троє — коричневий.</summary>
        public float MixMinorShare { get; }

        // ── Поле й лоток (§2) ──

        public int GridWidth { get; }
        public int GridHeight { get; }

        /// <summary>Скільки фігур у руці. Лоток поповнюється, лише коли ПОРОЖНІЙ (§2).</summary>
        public int TraySize { get; }

        public int MinPieceSize { get; }
        public int MaxPieceSize { get; }

        // ── Прогресія (§8): раунд = виданий лоток ──

        /// <summary>Раунди, з яких починається кожен наступний рівень складності.</summary>
        public int[] TierRounds { get; }

        /// <summary>Вага фігур 4+ клітинок: база плюс приріст на рівень складності.</summary>
        public float BigPieceBaseWeight { get; }
        public float BigPieceWeightPerTier { get; }

        /// <summary>Вага триклітинкових — стала.</summary>
        public float MidPieceWeight { get; }

        /// <summary>Вага двоклітинкових спадає з рівнем складності до мінімуму.</summary>
        public float SmallPieceBaseWeight { get; }
        public float SmallPieceWeightDropPerTier { get; }
        public float SmallPieceMinWeight { get; }

        /// <summary>З якого рівня складності в мішку з'являються п'ятиклітинкові.</summary>
        public int FiveCellFromTier { get; }

        // ── Мішок (§8: «дивиться на форму вільного місця») ──

        /// <summary>
        /// Показник ступеня, з яким кількість позицій фігури входить у її вагу:
        /// вага = вага_розміру × (BagFitOffset + позицій)^BagBias. Нуль — мішок не дивиться
        /// на поле; одиниця — вага прямо пропорційна кількості місць.
        /// </summary>
        public float BagBias { get; }

        /// <summary>Зсув, що лишає шанс і фігурі, яка зараз не влазить нікуди.</summary>
        public float BagFitOffset { get; }

        /// <summary>
        /// Ймовірність, що фігура повторить пігмент попередньої, за рівнем складності.
        /// Спадає: на початку довгі серії одного кольору трапляються часто, далі — рідше (§8).
        /// </summary>
        public float[] ColorStreakByTier { get; }

        /// <summary>Наскільки мішок віддає перевагу пігменту, якого на полі менше.</summary>
        public float ColorScarcityWeight { get; }

        /// <summary>Скільки разів мішок пробує чесний набір, перш ніж зменшувати фігури.</summary>
        public int MaxTrayAttempts { get; }

        /// <summary>З цієї спроби стеля розміру падає до трьох клітинок.</summary>
        public int TrayShrinkAfterAttempts { get; }

        /// <summary>Скільки спроб із двоклітинковими, перш ніж визнати, що місця немає.</summary>
        public int TrayRescueAttempts { get; }

        // ── Фарба (§3, §12) ──

        /// <summary>Мішана лінія дає ⌊домінантних ÷ це число⌋ (прототип v3).</summary>
        public int MixedDivisor { get; }

        /// <summary>«Бонус за чистий рядок ×3 до фарби» (§12).</summary>
        public int PureLineBonus { get; }

        /// <summary>Множник ланцюга за кількістю ліній за хід: [0] — одна лінія, [1] — дві, далі — стеля.</summary>
        public float[] ComboMultipliers { get; }

        // ── Очки (§8: рекорд очок і найдовший ланцюг) ──

        public int ScorePerPlacedCell { get; }
        public int ScorePerLine { get; }
        public int PureLineScoreBonus { get; }

        // ── Відчуття ──

        /// <summary>
        /// Менше цієї кількості вільних клітинок — поле світиться попереджувальним гало
        /// (друга умова гало — фігура з руки, що нікуди не влазить, — не число, а факт).
        /// 20, а не 12: у прогонах партія гине з ~25 вільними, поле «діряве», а не повне.
        /// </summary>
        public int HaloWarningFreeCells { get; }

        /// <summary>Стільки секунд без ходу — і гра підсвічує одну валідну позицію.</summary>
        public int HintIdleSeconds { get; }

        /// <summary>Рівень складності за номером раунду (лотка): скільки порогів пройдено.</summary>
        public int TierFor(int round)
        {
            var tier = 0;
            for (var i = 0; i < TierRounds.Length; i++)
                if (round >= TierRounds[i])
                    tier++;
            return tier;
        }

        /// <summary>Вага розміру фігури на цьому рівні складності (формула прототипу v3).</summary>
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

        /// <summary>Стеля розміру фігури на рівні складності.</summary>
        public int SizeCapFor(int tier) => tier >= FiveCellFromTier ? MaxPieceSize : Math.Min(4, MaxPieceSize);

        /// <summary>Ймовірність серії одного кольору на рівні складності. Понад таблицю — останнє значення.</summary>
        public float StreakChance(int tier)
        {
            if (tier < 0) tier = 0;
            return tier < ColorStreakByTier.Length
                ? ColorStreakByTier[tier]
                : ColorStreakByTier[ColorStreakByTier.Length - 1];
        }

        /// <summary>Множник ланцюга для заданої кількості ліній за хід. Понад таблицю — стеля.</summary>
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
