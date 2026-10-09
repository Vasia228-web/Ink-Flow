using System;
using System.Collections.Generic;

namespace InkFlow.Meta
{
    /// <summary>
    /// Числа економіки (майстер-док §10, §13). POCO без UnityEngine — щоб економіка
    /// перевірялась headless-тестами разом із рештою Meta.
    ///
    /// У грі приходить із `EconomyConfig.asset` через композиційний корінь.
    /// Дефолти тут — не «магія в коді», а стан нового проєкту: гра має бути
    /// грабельною, навіть якщо асет загубився. Підібрані <see cref="EconomySimulation"/>,
    /// тест <c>EconomySimulationTests</c> тримає пропорції.
    /// </summary>
    public sealed class EconomyData
    {
        public EconomyData(
            long starterOil = 0,
            int fullRewardPlays = 10,
            float reducedRewardRate = 0.25f,
            long scorePerOil = 100,
            long[]? pictureRewards = null,
            int interstitialEveryRuns = 4,
            int rewardAdMultiplier = 2,
            long[]? finishPictureCosts = null,
            float finishPictureMinShare = 0.25f,
            long continueCost = 120,
            IReadOnlyList<OilPack>? oilPacks = null)
        {
            FinishPictureCosts = finishPictureCosts ?? DefaultFinishPictureCosts();
            if (FinishPictureCosts.Length != Core.Rarities.Count)
                throw new ArgumentOutOfRangeException(nameof(finishPictureCosts), "Шість цін: звичайна … космічна.");
            if (finishPictureMinShare < 0f || finishPictureMinShare > 1f)
                throw new ArgumentOutOfRangeException(nameof(finishPictureMinShare));
            FinishPictureMinShare = finishPictureMinShare;
            // Нуль заборонено свідомо: гаманець не списує нуль, і «безплатне» продовження за нафту
            // виглядало б як мертва кнопка. Безплатне продовження — це ролик (§17), не ціна 0.
            if (continueCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(continueCost), "Ціна «продовжити» мусить бути додатною.");
            ContinueCost = continueCost;
            OilPacks = oilPacks ?? OilPack.Defaults();
            if (OilPacks.Count == 0)
                throw new ArgumentException("Магазин без пакетів нафти.", nameof(oilPacks));
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
            FullRewardPlays = fullRewardPlays;
            ReducedRewardRate = reducedRewardRate;
        }

        /// <summary>
        /// Скільки нафти має новий гравець. НУЛЬ свідомо: нафту заробляють забігом,
        /// і гравець бачить зв'язок «граю → заробляю → витрачаю». Стартовий грант цей момент прибрав би.
        /// </summary>
        public long StarterOil { get; }

        /// <summary>Скільки партій на день платять повну нагороду.</summary>
        public int FullRewardPlays { get; }

        /// <summary>Частка нагороди після вичерпання денного ліміту.</summary>
        public float ReducedRewardRate { get; }

        /// <summary>
        /// Майстер-док §10: «очки за забіг → краплі нафти». Скільки очок коштує одна крапля.
        /// При 100 середній забіг бота (~6 200 очок) дає ~62 за очки; разом із картинками — ~150.
        /// </summary>
        public long ScorePerOil { get; }

        /// <summary>Нафта за домальовану картинку за рідкістю (індекс — (int)Rarity): 10 / 20 / 40 / 80 / 160 / 400.</summary>
        public long[] PictureRewards { get; }

        /// <summary>Стартова таблиця (§19): подвоюється з рідкістю, космічна — окрема подія.</summary>
        public static long[] DefaultPictureRewards() => new long[] { 10, 20, 40, 80, 160, 400 };

        /// <summary>
        /// §13: «домалювати картинку одразу» — за нафту, повна ціна за рідкістю. Реальна
        /// ціна пропорційна решті кроків, але не нижче <see cref="FinishPictureMinShare"/>.
        /// </summary>
        public long[] FinishPictureCosts { get; }
        public float FinishPictureMinShare { get; }

        /// <summary>
        /// §19: «епічну можна домалювати раз на кілька забігів, не щозабігу». Забіг приносить ~150
        /// нафти (очки + картинки), картинка в момент програшу заповнена в середньому наполовину —
        /// епічна коштує близько двох забігів, легендарна — трьох, звичайна — третину.
        /// </summary>
        public static long[] DefaultFinishPictureCosts() => new long[] { 100, 180, 300, 600, 1000, 2000 };

        /// <summary>Ціна домалювати цю картинку зараз: частка решти пікселів від повної ціни, знизу обмежена.</summary>
        public long FinishPictureCost(Core.Rarity rarity, float remainingFraction)
        {
            var share = Math.Max(FinishPictureMinShare, Math.Min(1f, remainingFraction));
            return (long)Math.Ceiling(FinishPictureCosts[(int)rarity] * share);
        }

        /// <summary>
        /// §10, §13: «продовжити забіг після програшу» за нафту — коли ролик уже використано
        /// (або реклами немає). Менше за один забіг доходу: продовжити мусить бути простішим рішенням,
        /// ніж домалювати епічну.
        /// </summary>
        public long ContinueCost { get; }

        /// <summary>§13: пакети нафти магазину; ціни — зі стору, тут лише кількості й бейджі.</summary>
        public IReadOnlyList<OilPack> OilPacks { get; }

        /// <summary>Пакет за ідентифікатором товару; null — такого немає.</summary>
        public OilPack? FindOilPack(string productId)
        {
            if (productId is null)
                return null;
            for (var i = 0; i < OilPacks.Count; i++)
                if (string.Equals(OilPacks[i].Id, productId, StringComparison.Ordinal))
                    return OilPacks[i];
            return null;
        }

        /// <summary>§9, §12: інтерстиціал раз на стільки забігів; 0 — ніколи. «Частіше — видаляють гру».</summary>
        public int InterstitialEveryRuns { get; }

        /// <summary>§9: «подвоїти зібране за ролик» — множник до нафти за очки.</summary>
        public int RewardAdMultiplier { get; }

        public static EconomyData Default { get; } = new EconomyData();
    }
}
