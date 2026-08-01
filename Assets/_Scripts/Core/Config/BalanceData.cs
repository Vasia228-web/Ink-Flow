using System;

namespace InkFlow.Core
{
    /// <summary>
    /// POCO-дзеркало BalanceConfig.asset (§7). ЖОДНЕ балансне число не живе в коді —
    /// зміна балансу має бути зміною .asset, а не перекомпіляцією, інакше симулятори
    /// Фази 4 неможливі.
    /// Значення за замовчуванням = базовий баланс майстер-доку.
    /// </summary>
    public sealed class BalanceData
    {
        /// <summary>Густота, з якої крапля лопається. Базово 10.</summary>
        public int BurstThreshold { get; }

        /// <summary>Дільник сили фарбування: сила ÷ 10 (§5.3). «Кожна десятка = +1».</summary>
        public int PaintPowerDivisor { get; }

        /// <summary>Дільник бризок: 1 бризка за кожні повні 15 сили (§5.3).</summary>
        public int SplashDivisor { get; }

        /// <summary>Запобіжник від нескінченного ланцюга (§5.5). Базово 64.</summary>
        public int MaxChainBursts { get; }

        /// <summary>Кожні стільки вибухів приплив піднімає мінімальну густоту на 1 (§5.9).</summary>
        public int TideStep { get; }

        /// <summary>Сила вибуху, з якої фарбуються 2 сегменти боса (§5.6).</summary>
        public int BossTwoSegmentForce { get; }

        /// <summary>Сила вибуху, з якої фарбуються 3 сегменти боса — це стеля (§5.6).</summary>
        public int BossThreeSegmentForce { get; }

        /// <summary>Бос діє кожен N-й ПРИЙНЯТИЙ хід (§5.6).</summary>
        public int BossActsEveryMoves { get; }

        /// <summary>Частка ліміту ходів, що має лишитись для 2★ (базово 0.2).</summary>
        public float TwoStarMovesLeftFraction { get; }

        /// <summary>Частка ліміту ходів, що має лишитись для 3★ (базово 0.4).</summary>
        public float ThreeStarMovesLeftFraction { get; }

        /// <summary>Скільки разів Endless пробує дозаправку, поки не з'явиться хід (§5.8).</summary>
        public int MaxRefillAttempts { get; }

        /// <summary>Очки за одиницю густоти при злитті.</summary>
        public int ScorePerMergedDensity { get; }

        /// <summary>Очки за одиницю сили вибуху.</summary>
        public int ScorePerBurstForce { get; }

        public BalanceData(
            int burstThreshold = 10,
            int paintPowerDivisor = 10,
            int splashDivisor = 15,
            int maxChainBursts = 64,
            int tideStep = 10,
            int bossTwoSegmentForce = 20,
            int bossThreeSegmentForce = 35,
            int bossActsEveryMoves = 3,
            float twoStarMovesLeftFraction = 0.2f,
            float threeStarMovesLeftFraction = 0.4f,
            int maxRefillAttempts = 32,
            int scorePerMergedDensity = 1,
            int scorePerBurstForce = 2)
        {
            // Поріг < 2 зробив би ланцюг самопідтримним незалежно від PaintPower.
            if (burstThreshold < 2)
                throw new ArgumentOutOfRangeException(nameof(burstThreshold), "burstThreshold має бути >= 2.");
            if (paintPowerDivisor < 1)
                throw new ArgumentOutOfRangeException(nameof(paintPowerDivisor));
            if (splashDivisor < 1)
                throw new ArgumentOutOfRangeException(nameof(splashDivisor));
            if (maxChainBursts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxChainBursts));
            if (tideStep < 1)
                throw new ArgumentOutOfRangeException(nameof(tideStep));
            if (bossActsEveryMoves < 1)
                throw new ArgumentOutOfRangeException(nameof(bossActsEveryMoves));
            if (maxRefillAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRefillAttempts));

            BurstThreshold = burstThreshold;
            PaintPowerDivisor = paintPowerDivisor;
            SplashDivisor = splashDivisor;
            MaxChainBursts = maxChainBursts;
            TideStep = tideStep;
            BossTwoSegmentForce = bossTwoSegmentForce;
            BossThreeSegmentForce = bossThreeSegmentForce;
            BossActsEveryMoves = bossActsEveryMoves;
            TwoStarMovesLeftFraction = twoStarMovesLeftFraction;
            ThreeStarMovesLeftFraction = threeStarMovesLeftFraction;
            MaxRefillAttempts = maxRefillAttempts;
            ScorePerMergedDensity = scorePerMergedDensity;
            ScorePerBurstForce = scorePerBurstForce;
        }

        /// <summary>Стеля сили фарбування — інваріант §18.3: вибух не може створити краплю, що лопне сама.</summary>
        public int MaxPaintPower => BurstThreshold - 1;

        public static BalanceData Default { get; } = new BalanceData();
    }
}
