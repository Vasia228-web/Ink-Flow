using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.Meta;
using InkFlow.Platform;
using InkFlow.UI;
using UnityEngine;

namespace InkFlow.App
{
    /// <summary>
    /// Композиційний корінь. ЄДИНЕ місце, де створюються сервіси й вирішується,
    /// яка реалізація Platform використовується — решта коду знає лише інтерфейси.
    /// Свідомо без DI-контейнера: він додав би час старту й магію в стектрейсах.
    ///
    /// Живе в `Main.unity` — ТОЧЦІ ВХОДУ застосунку (єдина сцена в Build Settings).
    /// Сам нічого не відкриває: підставляє числа й сервіси в <see cref="AppRouter"/>,
    /// а той уже ставить хаб коренем навігації. Пряме відкриття екрана лишилось
    /// тільки для налагодження — `debugStartEndless`.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Конфіги")]
        [SerializeField] private BalanceConfig balanceConfig;
        [SerializeField] private EconomyConfig economyConfig;

        [Header("Сцена")]
        [SerializeField] private AppRouter router;
        [SerializeField] private EndlessScreen endlessScreen;
        [SerializeField] private BoardFeedback boardFeedback;

        [Header("Налагодження")]
        [Tooltip("Відкрити одразу «Нескінченний» замість хаба.")]
        [SerializeField] private bool debugStartEndless;

        private ISaveStorage _storage;
        private PlayerState _state;

        /// <summary>Стан гравця — дев-панель дістає його саме звідси.</summary>
        public PlayerState State => _state;

        private void Awake()
        {
            // Мобільний бюджет: 60 fps і без «сну» під час партії.
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            RegisterPlatformServices();
            LoadSave();

            boardFeedback?.SetHaptics(ServiceLocator.Get<IHapticService>());

            // Роутер живе в UI і про конфіги нічого не знає — стан гравця
            // підставляємо звідси, ще до Start.
            if (router != null)
            {
                router.Configure(balanceConfig.ToBalanceData(), _state);
                router.BindServices(ServiceLocator.Get<IAdsService>(), ServiceLocator.Get<IIapService>());
            }
            else
                Debug.LogError(
                    "[InkFlow] GameBootstrap.router не підв'язаний — стан гравця нікуди не потрапить, " +
                    "і всі екрани покажуть мокові дані. Перезбери: Ink Flow → Setup → Build Main Scene.");
        }

        /// <summary>
        /// Звичайний запуск нічого не відкриває: корінь навігації ставить
        /// <see cref="AppRouter"/> у своєму Start. Гілка нижче — лише для
        /// налагодження одного екрана без проходу через хаб.
        /// </summary>
        private void Start()
        {
            if (debugStartEndless)
                StartEndless();
        }

        /// <summary>
        /// Поки що всі сервіси — Null/Log-реалізації: гра повністю грабельна без жодного SDK.
        /// На Фазі 5 тут з'являться реальні Android/iOS-реалізації, і більше ніде.
        /// </summary>
        private void RegisterPlatformServices()
        {
            ServiceLocator.Register<IHapticService>(new NullHaptics());
            ServiceLocator.Register<IAnalyticsService>(new LogAnalytics());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // У редакторі й dev-збірках реклама «навмання»: кнопки §9 можна пройти руками до SDK.
            ServiceLocator.Register<IAdsService>(new FakeAds());
#else
            ServiceLocator.Register<IAdsService>(new NullAds());
#endif
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

            // Колода й баланс — щоб незавершена картинка читалась назвою, а спроби — з конфіга.
            var pictures = PictureCatalogData.Default;
            var balance = balanceConfig.ToBalanceData();
            _state = _storage.Exists
                ? new PlayerState(_storage.Load(), economy, _storage, pictures, balance)
                : PlayerState.NewPlayer(economy, _storage, pictures, balance);

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
        /// Нескінченний напряму. Баланс приходить із BalanceConfig.asset саме тут:
        /// екран не має права брати BalanceData.Default, інакше правки балансу
        /// нічого не міняли б у грі.
        /// </summary>
        private void StartEndless()
        {
            if (endlessScreen == null)
            {
                Debug.LogError("[InkFlow] debugStartEndless увімкнено, але EndlessScreen не підв'язаний.");
                return;
            }

            endlessScreen.BindServices(ServiceLocator.Get<IAdsService>(), ServiceLocator.Get<IIapService>());
            endlessScreen.OnEnter(new EndlessArgs(balanceConfig.ToBalanceData()));
        }

        /// <summary>
        /// На мобільних це ЄДИНИЙ надійний момент зберегтися: OnApplicationQuit
        /// часто не викликається взагалі.
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
        /// НА МІСЦІ, без перезавантаження сцени.
        /// </summary>
        public void ResetSaveAndRestart()
        {
            _storage?.Delete();

            var economy = economyConfig != null ? economyConfig.ToEconomyData() : EconomyData.Default;
            var balance = balanceConfig.ToBalanceData();
            _state = PlayerState.NewPlayer(economy, _storage, PictureCatalogData.Default, balance);
            _state.Persist();

            ServiceLocator.Register(_state);
            ServiceLocator.Register(_state.Wallet);
            ServiceLocator.Register(_state.DailyLimit);
            ServiceLocator.Register(_state.Rewards);

            if (router != null)
            {
                router.Configure(balance, _state);
                router.BindServices(ServiceLocator.Get<IAdsService>(), ServiceLocator.Get<IIapService>());
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
