using System;

namespace InkFlow.Meta
{
    /// <summary>Підсумок партії, який Gameplay віддає в Meta. Крапель чорнила тут немає (§2 правило 3).</summary>
    public readonly struct GameResult
    {
        public int LevelId { get; }
        public bool Won { get; }
        public int Stars { get; }
        public bool IsBoss { get; }
        public int Score { get; }

        public GameResult(int levelId, bool won, int stars, bool isBoss = false, int score = 0)
        {
            LevelId = levelId;
            Won = won;
            Stars = stars;
            IsBoss = isBoss;
            Score = score;
        }
    }

    /// <summary>
    /// Нагороди (майстер-док §4, §5, §7). Усі числа мають приїжджати з EconomyConfig —
    /// тут лише формули, які симулятор Фази 4 ганяє на 30 днів життя гравця.
    /// </summary>
    public sealed class RewardCalculator
    {
        private readonly long _baseLevelReward;
        private readonly int _bossMultiplier;
        private readonly long[] _endlessMilestones;
        private readonly long _scorePerOil;
        private readonly long[] _pictureRewards;

        public RewardCalculator(long baseLevelReward = 20, int bossMultiplier = 3, long[]? endlessMilestones = null,
            long scorePerOil = 100, long[]? pictureRewards = null)
        {
            _baseLevelReward = baseLevelReward;
            _bossMultiplier = bossMultiplier;
            _endlessMilestones = endlessMilestones ?? new long[] { 5000, 10000, 25000, 50000 };
            _scorePerOil = scorePerOil < 1 ? 1 : scorePerOil;
            _pictureRewards = pictureRewards ?? new long[] { 10, 30, 100 };
        }

        /// <summary>
        /// З конфіга економіки — саме так його створює композиційний корінь.
        /// Конструктор із числами лишається для тестів.
        /// </summary>
        public RewardCalculator(EconomyData economy)
            : this(economy.BaseLevelReward, economy.BossMultiplier, economy.EndlessMilestones,
                economy.ScorePerOil, economy.PictureRewards)
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

        /// <summary>Нафта за рівень = база × зірки (× 3 на босі) × денний множник.</summary>
        public long ForLevel(in GameResult result, float dailyMultiplier)
        {
            if (!result.Won || result.Stars <= 0)
                return 0;

            var reward = _baseLevelReward * result.Stars;
            if (result.IsBoss)
                reward *= _bossMultiplier;
            return (long)Math.Floor(reward * dailyMultiplier);
        }

        /// <summary>
        /// Endless платить за ПЕРЕВИЩЕННЯ власного рекорду: на старті рекорди б'ються легко
        /// й дешево, далі — рідше й дорожче. Без рекорду виплати немає.
        /// </summary>
        public long ForEndlessRecord(int newScore, int previousRecord)
        {
            if (newScore <= previousRecord)
                return 0;
            var delta = newScore - previousRecord;
            return (long)Math.Floor(Math.Sqrt(delta));
        }

        /// <summary>Одноразові віхи — щоб режим платив і тоді, коли рекорд уже високий.</summary>
        public long ForMilestones(int newScore, int previousRecord)
        {
            long total = 0;
            foreach (var milestone in _endlessMilestones)
                if (previousRecord < milestone && newScore >= milestone)
                    total += milestone / 100;
            return total;
        }
    }
}
