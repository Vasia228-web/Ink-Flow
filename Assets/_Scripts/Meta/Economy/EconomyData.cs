using System;
namespace InkFlow.Meta
{
    /// <summary>
    /// Числа економіки (майстер-док §7). POCO без UnityEngine — щоб економіка
    /// перевірялась headless-тестами разом із рештою Meta.
    ///
    /// У грі приходить із `EconomyConfig.asset` через композиційний корінь.
    /// Дефолти тут — не «магія в коді», а стан нового проєкту: гра має бути
    /// грабельною, навіть якщо асет загубився.
    /// </summary>
    public sealed class EconomyData
    {
        public EconomyData(
            long starterOil = 0,
            float starterPaintLiters = 0f,
            long baseLevelReward = 20,
            int bossMultiplier = 3,
            int fullRewardPlays = 10,
            float reducedRewardRate = 0.25f,
            long[]? endlessMilestones = null,
            long scorePerOil = 100,
            long[]? pictureRewards = null,
            int interstitialEveryRuns = 4,
            int rewardAdMultiplier = 2,
            long[]? finishPictureCosts = null,
            float finishPictureMinShare = 0.25f)
        {
            FinishPictureCosts = finishPictureCosts ?? DefaultFinishPictureCosts();
            if (FinishPictureCosts.Length != Core.Rarities.Count)
                throw new ArgumentOutOfRangeException(nameof(finishPictureCosts), "Шість цін: звичайна … космічна.");
            if (finishPictureMinShare < 0f || finishPictureMinShare > 1f)
                throw new ArgumentOutOfRangeException(nameof(finishPictureMinShare));
            FinishPictureMinShare = finishPictureMinShare;
            if (interstitialEveryRuns < 0)
                throw new ArgumentOutOfRangeException(nameof(interstitialEveryRuns));
            if (rewardAdMultiplier < 1)
                throw new ArgumentOutOfRangeException(nameof(rewardAdMultiplier));
            InterstitialEveryRuns = interstitialEveryRuns;
            RewardAdMultiplier = rewardAdMultiplier;
            if (scorePerOil < 1)
                throw new ArgumentOutOfRangeException(nameof(scorePerOil));
            ScorePerOil = scorePerOil;
            PictureRewards = pictureRewards ?? DefaultPictureRewards();
            if (PictureRewards.Length != Core.Rarities.Count)
                throw new ArgumentOutOfRangeException(nameof(pictureRewards), "Шість виплат: звичайна … космічна.");
            StarterOil = starterOil;
            StarterPaintLiters = starterPaintLiters;
            BaseLevelReward = baseLevelReward;
            BossMultiplier = bossMultiplier;
            FullRewardPlays = fullRewardPlays;
            ReducedRewardRate = reducedRewardRate;
            EndlessMilestones = endlessMilestones ?? new long[] { 5000, 10000, 25000, 50000 };
        }

        /// <summary>
        /// Скільки нафти має новий гравець. НУЛЬ свідомо: перший пройдений рівень
        /// дає 20, найдешевша фарба коштує 12 за літр, найдешевша зона — 1 літр.
        /// Тобто цикл «граю → купую → фарбую» замикається за одну партію, і гравець
        /// бачить зв'язок між ними. Стартовий грант цей момент прибрав би.
        /// </summary>
        public long StarterOil { get; }

        /// <summary>Скільки літрів базової фарби має новий гравець. Теж нуль — з тієї ж причини.</summary>
        public float StarterPaintLiters { get; }

        /// <summary>База нагороди за рівень: множиться на зірки й денний коефіцієнт.</summary>
        public long BaseLevelReward { get; }

        /// <summary>Множник нагороди на бос-рівні.</summary>
        public int BossMultiplier { get; }

        /// <summary>Скільки партій на день платять повну нагороду.</summary>
        public int FullRewardPlays { get; }

        /// <summary>Частка нагороди після вичерпання денного ліміту.</summary>
        public float ReducedRewardRate { get; }

        /// <summary>Одноразові віхи рахунку в Нескінченному (старий режим; лишається для «Рівнів»).</summary>
        public long[] EndlessMilestones { get; }

        /// <summary>
        /// Майстер-док §10: «очки за забіг → краплі нафти». Скільки очок коштує одна
        /// крапля. При 100 середня партія бота (~4 400 очок) дає ~44 — два-три літри
        /// найдешевшої фарби, тобто одну-дві зони планети.
        /// </summary>
        public long ScorePerOil { get; }

        /// <summary>Нафта за домальовану картинку за рідкістю (індекс — (int)Rarity): 10 / 20 / 40 / 80 / 160 / 400.</summary>
        public long[] PictureRewards { get; }

        /// <summary>Стартова таблиця (§19: «з EconomySimulator»): подвоюється з рідкістю, космічна — окрема подія.</summary>
        public static long[] DefaultPictureRewards() => new long[] { 10, 20, 40, 80, 160, 400 };

        /// <summary>
        /// §13: «домалювати картинку одразу» — за нафту, повна ціна за рідкістю. Реальна
        /// ціна пропорційна решті пікселів, але не нижче <see cref="FinishPictureMinShare"/>.
        /// </summary>
        public long[] FinishPictureCosts { get; }
        public float FinishPictureMinShare { get; }

        /// <summary>§19: епічну можна домалювати раз на кілька забігів (забіг дає ~60–70 нафти).</summary>
        public static long[] DefaultFinishPictureCosts() => new long[] { 40, 80, 150, 250, 400, 800 };

        /// <summary>Ціна домалювати цю картинку зараз: частка решти пікселів від повної ціни, знизу обмежена.</summary>
        public long FinishPictureCost(Core.Rarity rarity, float remainingFraction)
        {
            var share = Math.Max(FinishPictureMinShare, Math.Min(1f, remainingFraction));
            return (long)Math.Ceiling(FinishPictureCosts[(int)rarity] * share);
        }

        /// <summary>§9, §12: інтерстиціал раз на стільки забігів; 0 — ніколи. «Частіше — видаляють гру».</summary>
        public int InterstitialEveryRuns { get; }

        /// <summary>§9: «подвоїти зібране за ролик» — множник до нафти за очки.</summary>
        public int RewardAdMultiplier { get; }

        public static EconomyData Default { get; } = new EconomyData();
    }
}
