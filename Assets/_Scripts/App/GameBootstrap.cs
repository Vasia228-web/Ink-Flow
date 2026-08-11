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
        [SerializeField] private EconomyConfig economyConfig;

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
        private PlayerState _state;

        /// <summary>Стан гравця — дев-панель дістає його саме звідси.</summary>
        public PlayerState State => _state;

        private void Awake()
        {
            // Мобільний бюджет: 60 fps і без «сну» під час партії (§12).
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            RegisterPlatformServices();
            LoadSave();

            boardFeedback?.SetHaptics(ServiceLocator.Get<IHapticService>());

            // Роутер живе в UI і про конфіги нічого не знає — стан гравця
            // підставляємо звідси, ще до Start.
            if (router != null)
                router.Configure(balanceConfig.ToBalanceData(), _state);
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

        /// <summary>
        /// Читає збереження або створює нового гравця. Міграції вже застосовані
        /// сховищем — сюди приходить файл поточної версії.
        /// </summary>
        private void LoadSave()
        {
            _storage = new JsonSaveStorage();
            var economy = economyConfig != null ? economyConfig.ToEconomyData() : EconomyData.Default;

            _state = _storage.Exists
                ? new PlayerState(_storage.Load(), economy, _storage)
                : PlayerState.NewPlayer(economy, _storage);

            // Новому гравцю файл треба створити одразу: інакше перший же збій
            // до кінця першої партії виглядав би як «гра не запам'ятала нічого».
            if (!_storage.Exists)
                _state.Persist();

            ServiceLocator.Register(_state);
            ServiceLocator.Register(_state.Wallet);
            ServiceLocator.Register(_state.DailyLimit);
            ServiceLocator.Register(_state.Rewards);
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

        private void PersistSave() => _state?.Persist();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Дев-панель: стерти файл і почати як новий гравець. Стан перебудовується
        /// НА МІСЦІ, без перезавантаження сцени — інакше довелось би заново
        /// проходити всю ініціалізацію сервісів, а вони вже зареєстровані.
        /// </summary>
        public void ResetSaveAndRestart()
        {
            _storage?.Delete();

            var economy = economyConfig != null ? economyConfig.ToEconomyData() : EconomyData.Default;
            _state = PlayerState.NewPlayer(economy, _storage);
            _state.Persist();

            ServiceLocator.Register(_state);
            ServiceLocator.Register(_state.Wallet);
            ServiceLocator.Register(_state.DailyLimit);
            ServiceLocator.Register(_state.Rewards);

            if (router != null)
            {
                router.Configure(balanceConfig.ToBalanceData(), _state);
                router.RestartFromHub();
            }
        }
#endif

        private void OnDestroy()
        {
            PersistSave();
            GameEvents.Clear();
            ServiceLocator.Clear();
        }
    }
}
