using System;

namespace InkFlow.Meta
{
    /// <summary>Звідки прийшла нафта — для аналітики й антифроду.</summary>
    public enum RewardSource
    {
        LevelClear,
        BossClear,
        EndlessRecord,
        EndlessMilestone,
        Purchase,
        Debug
    }

    /// <summary>
    /// Гаманець «крапель нафти» (майстер-док §7). Meta не знає, що таке крапля чорнила —
    /// вона отримує лише результат партії (§2 правило 3).
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(long initial = 0)
        {
            if (initial < 0)
                throw new ArgumentOutOfRangeException(nameof(initial));
            OilDrops = initial;
        }

        public long OilDrops { get; private set; }

        public event Action<long>? Changed;

        public void Add(long amount, RewardSource source)
        {
            if (amount <= 0)
                return;
            OilDrops += amount;
            Changed?.Invoke(OilDrops);
        }

        public bool TrySpend(long amount)
        {
            if (amount <= 0 || OilDrops < amount)
                return false;
            OilDrops -= amount;
            Changed?.Invoke(OilDrops);
            return true;
        }

        internal void Load(long amount) => OilDrops = Math.Max(0, amount);
    }

    /// <summary>
    /// М'який денний ліміт (майстер-док §4): перші N проходжень на день дають 100%,
    /// далі 25%. Грати ніхто не забороняє — але повертатися щодня вигідно.
    /// Жодних стін «ходи закінчились, чекай».
    /// </summary>
    public sealed class DailyLimitTracker
    {
        private readonly int _fullRewardPlays;
        private readonly float _reducedRate;

        /// <summary>З конфіга економіки — так його створює композиційний корінь.</summary>
        public DailyLimitTracker(EconomyData economy)
            : this(economy.FullRewardPlays, economy.ReducedRewardRate)
        {
        }

        public DailyLimitTracker(int fullRewardPlays = 10, float reducedRate = 0.25f)
        {
            _fullRewardPlays = fullRewardPlays;
            _reducedRate = reducedRate;
        }

        public int PlaysToday { get; private set; }
        public DateTime CurrentDayUtc { get; private set; } = DateTime.UtcNow.Date;

        public float RewardMultiplier => PlaysToday < _fullRewardPlays ? 1f : _reducedRate;

        public void RegisterPlay(DateTime utcNow)
        {
            RollOverIfNeeded(utcNow);
            PlaysToday++;
        }

        public void RollOverIfNeeded(DateTime utcNow)
        {
            if (utcNow.Date == CurrentDayUtc)
                return;
            CurrentDayUtc = utcNow.Date;
            PlaysToday = 0;
        }

        internal void Load(int playsToday, DateTime dayUtc)
        {
            PlaysToday = Math.Max(0, playsToday);
            CurrentDayUtc = dayUtc.Date;
        }
    }
}
