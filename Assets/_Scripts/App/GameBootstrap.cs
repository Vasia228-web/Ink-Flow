using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.Meta;
using InkFlow.Platform;
using InkFlow.UI;
using UnityEngine;

namespace InkFlow.App
{
    /// <summary>
    /// Композиційний корінь (§8). ЄДИНЕ місце, де створюються сервіси й вирішується,
    /// яка реалізація Platform використовується — решта коду знає лише інтерфейси (§2 правило 4).
    /// Свідомо без DI-контейнера: він додав би час старту й магію в стектрейсах (§1).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Конфіги")]
        [SerializeField] private BalanceConfig balanceConfig;

        [Header("Сцена")]
        [SerializeField] private LevelCatalog levelCatalog;
        [SerializeField] private LevelScreen levelScreen;
        [SerializeField] private BoardFeedback boardFeedback;

        [Header("Старт")]
        [Tooltip("Рівень, що вантажиться при запуску сцени партії.")]
        [SerializeField, Min(1)] private int startLevelId = 1;

        private ISaveStorage _storage;
        private SaveFile _save;
        private Wallet _wallet;
        private DailyLimitTracker _dailyLimit;
        private RewardCalculator _rewards;

        private void Awake()
        {
            // Мобільний бюджет: 60 fps і без «сну» під час партії (§12).
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            RegisterPlatformServices();
            LoadSave();

            boardFeedback?.SetHaptics(ServiceLocator.Get<IHapticService>());
        }

        private void Start() => levelCatalog.Load(startLevelId, StartPuzzle, Debug.LogError);

        /// <summary>
        /// Поки що всі сервіси — Null/Log-реалізації: гра повністю грабельна без жодного SDK.
        /// На Фазі 5 тут з'являться реальні Android/iOS-реалізації, і більше ніде (§11).
        /// </summary>
        private void RegisterPlatformServices()
        {
            ServiceLocator.Register<IHapticService>(new NullHaptics());
            ServiceLocator.Register<IAnalyticsService>(new LogAnalytics());
            ServiceLocator.Register<IAdsService>(new NullAds());
            ServiceLocator.Register<IIapService>(new FakeIap());
            ServiceLocator.Register<IReviewService>(new NullReview());
            ServiceLocator.Register<INotificationService>(new NullNotifications());
        }

        private void LoadSave()
        {
            _storage = new JsonSaveStorage();
            _save = _storage.Load();

            _wallet = new Wallet(_save.Wallet.OilDrops);
            _dailyLimit = new DailyLimitTracker();
            _rewards = new RewardCalculator();

            ServiceLocator.Register(_wallet);
            ServiceLocator.Register(_dailyLimit);
            ServiceLocator.Register(_rewards);
            ServiceLocator.Register(_storage);
        }

        /// <summary>
        /// Баланс приходить із BalanceConfig.asset саме тут: екран не має права
        /// брати BalanceData.Default, інакше правки балансу нічого не міняли б у грі.
        /// </summary>
        private void StartPuzzle(LevelData level) =>
            levelScreen.OnEnter(new LevelArgs(level.LevelId, level, balanceConfig.ToBalanceData()));

        /// <summary>
        /// На мобільних це ЄДИНИЙ надійний момент зберегтися: OnApplicationQuit
        /// часто не викликається взагалі (§10, §12).
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
                PersistSave();
        }

        private void PersistSave()
        {
            if (_storage == null || _save == null)
                return;

            _save.Wallet.OilDrops = _wallet.OilDrops;
            _save.Wallet.PlaysToday = _dailyLimit.PlaysToday;
            _save.Wallet.DayUtc = _dailyLimit.CurrentDayUtc.ToString("yyyy-MM-dd");
            _storage.Save(_save);
        }

        private void OnDestroy()
        {
            PersistSave();
            GameEvents.Clear();
            ServiceLocator.Clear();
        }
    }
}
