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
    ///
    /// Живе в `Main.unity` — ТОЧЦІ ВХОДУ застосунку (єдина сцена в Build Settings).
    /// Сам нічого не відкриває: підставляє числа й сервіси в <see cref="AppRouter"/>,
    /// а той уже ставить хаб коренем навігації. Пряме відкриття екрана лишилось
    /// тільки для налагодження — `debugStartLevel` / `debugStartEndless`.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Конфіги")]
        [SerializeField] private BalanceConfig balanceConfig;

        [Header("Сцена")]
        [SerializeField] private LevelCatalog levelCatalog;
        [SerializeField] private AppRouter router;
        [SerializeField] private LevelScreen levelScreen;
        [SerializeField] private EndlessScreen endlessScreen;
        [SerializeField] private BoardFeedback boardFeedback;

        [Header("Налагодження")]
        [Tooltip("Відкрити одразу рівень замість хаба. 0 — звичайний запуск.")]
        [SerializeField, Min(0)] private int debugStartLevel;

        [Tooltip("Відкрити одразу «Нескінченний» замість хаба.")]
        [SerializeField] private bool debugStartEndless;

        [SerializeField] private int endlessWidth = 6;
        [SerializeField] private int endlessHeight = 6;
        [SerializeField, Range(2, 6)] private int endlessColors = 4;

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

            // Роутер живе в UI і про BalanceConfig.asset нічого не знає —
            // числа й гаманець йому підставляємо звідси, ще до Start.
            if (router != null)
                router.Configure(balanceConfig.ToBalanceData(), _wallet, _save.Progress);

            // Нескінченний бере рекорд і нагороди з того самого збереження.
            if (endlessScreen != null)
                endlessScreen.BindEconomy(_wallet, _rewards, _save.Progress);
        }

        /// <summary>
        /// Звичайний запуск нічого не відкриває: корінь навігації ставить
        /// <see cref="AppRouter"/> у своєму Start. Гілки нижче — лише для
        /// налагодження одного екрана без проходу через хаб.
        /// </summary>
        private void Start()
        {
            if (debugStartEndless)
                StartEndless();
            else if (debugStartLevel > 0)
                levelCatalog.Load(debugStartLevel, StartPuzzle, Debug.LogError);
        }

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
        /// Нескінченний. Економіку віддаємо ЯВНО: екран не має доступу до
        /// ServiceLocator (той живе в App), і саме тому не може ані взяти
        /// дефолтний баланс, ані порахувати нагороду за власними числами.
        /// </summary>
        private void StartEndless()
        {
            if (endlessScreen == null)
            {
                Debug.LogError("[InkFlow] debugStartEndless увімкнено, але EndlessScreen не підв'язаний.");
                return;
            }

            endlessScreen.OnEnter(new EndlessArgs(
                balanceConfig.ToBalanceData(),
                new EndlessData(endlessWidth, endlessHeight, endlessColors)));
        }

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

            // Рекорд Нескінченного пише сам екран у _save.Progress — тут лише
            // фіксуємо гаманець і денний ліміт, решта вже в об'єкті збереження.
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
