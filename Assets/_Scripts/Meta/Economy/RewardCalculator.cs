using System;

namespace InkFlow.Meta
{
    /// <summary>
    /// Нагороди забігу (майстер-док §5, §10). Усі числа мають приїжджати з EconomyConfig —
    /// тут лише формули, які <see cref="EconomySimulation"/> ганяє на 30 днів життя гравця.
    /// Крапель чорнила тут немає (§2 правило 3): Meta бачить лише очки й картинки.
    /// </summary>
    public sealed class RewardCalculator
    {
        private readonly long _scorePerOil;
        private readonly long[] _pictureRewards;

        public RewardCalculator(long scorePerOil = 100, long[]? pictureRewards = null)
        {
            _scorePerOil = scorePerOil < 1 ? 1 : scorePerOil;
            _pictureRewards = pictureRewards ?? EconomyData.DefaultPictureRewards();
        }

        /// <summary>
        /// З конфіга економіки — саме так його створює композиційний корінь.
        /// Конструктор із числами лишається для тестів.
        /// </summary>
        public RewardCalculator(EconomyData economy)
            : this(economy.ScorePerOil, economy.PictureRewards)
        {
        }

        /// <summary>Майстер-док §10: нафта за забіг = очки ÷ ScorePerOil × денний множник.</summary>
        public long ForRun(int score, float dailyMultiplier)
        {
            if (score <= 0)
                return 0;
            return (long)Math.Floor(score / (double)_scorePerOil * dailyMultiplier);
        }

        /// <summary>Нафта за домальовану картинку — за рідкістю, без денного множника: картинка — подія, не фарм.</summary>
        public long ForPicture(Core.Rarity rarity)
        {
            var index = (int)rarity;
            return index >= 0 && index < _pictureRewards.Length ? _pictureRewards[index] : 0;
        }
    }
}
