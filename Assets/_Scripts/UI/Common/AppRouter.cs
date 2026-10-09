using InkFlow.Core;
using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Граф навігації застосунку — ЄДИНЕ місце, де записано, що куди веде.
    ///
    /// Екрани про маршрути не знають: вони лише повідомляють про намір
    /// (`BackRequested`, `PlayRequested`, `TabRequested`…), а рішення ухвалюється
    /// тут. Тому маршрут можна змінити, не чіпаючи жодного екрана, і навпаки —
    /// екран лишається запускним окремою сценою.
    ///
    /// Правило стека: вглиб — <see cref="NavigationStack.Push"/>, назад —
    /// <see cref="NavigationStack.Pop"/>, «наступний рівень» —
    /// <see cref="NavigationStack.Replace"/>. Рестарт партії стека не чіпає взагалі:
    /// сесія скидається на місці.
    /// </summary>
    public sealed class AppRouter : MonoBehaviour
    {
        [SerializeField] private NavigationStack navigation;

        [Header("Екрани")]
        [SerializeField] private HubScreen hub;
        [SerializeField] private LevelMapScreen levelMap;
        [SerializeField] private ComingSoonScreen comingSoon;
        [SerializeField] private EndlessScreen endless;
        [SerializeField] private GalaxyScreen galaxy;
        [SerializeField] private PlanetScreen planet;
        [SerializeField] private CollectionScreen collection;
        [SerializeField] private ShopScreen shop;
        [SerializeField] private RankingsScreen rankings;
        [SerializeField] private ProfileScreen profile;
        [SerializeField] private SettingsScreen settings;

        [Header("Діалоги")]
        [SerializeField] private NickPrompt? nickPrompt;
        [SerializeField] private ConfirmPrompt? confirmPrompt;

        private BalanceData _balance = BalanceData.Default;
        private PlayerState? _state;
        private AppLinks _links = AppLinks.Default;

        /// <summary>Стан гравця — його ж роздаємо екранам.</summary>
        public PlayerState? State => _state;

        /// <summary>
        /// Стан і числа, які екранам не належить створювати самим. Підставляє
        /// композиційний корінь — сам роутер живе в UI і про `BalanceConfig.asset`
        /// нічого не знає.
        ///
        /// Роздаємо ОДИН екземпляр стану всім екранам: кожен, хто зробив би собі
        /// копію, показував би застарілі числа після покупки в сусідньому екрані.
        /// </summary>
        public void Configure(BalanceData balance, PlayerState state, AppLinks? links = null, NickRules? nickRules = null)
        {
            _balance = balance;
            _state = state;
            _links = links ?? AppLinks.Default;
            _nickRules = nickRules ?? NickRules.Default;
            settings?.BindLinks(_links);
            nickPrompt?.Bind(_nickRules);
            // Нік зі старого файлу, що не проходить нинішні правила (§14), — на типовий, ще до першого екрана.
            state.EnforceNickRules(_nickRules);

            hub?.BindState(state);
            levelMap?.BindState(state);
            galaxy?.BindState(state);
            planet?.BindState(state);
            collection?.BindState(state);
            shop?.BindState(state);
            rankings?.BindState(state);
            profile?.BindState(state);
            endless?.BindState(state);
            settings?.BindState(state);
        }

        /// <summary>Платформні сервіси для кнопок §9 — лише туди, де вони потрібні.</summary>
        public void BindServices(InkFlow.Platform.IAdsService? ads, InkFlow.Platform.IIapService? iap,
            InkFlow.Platform.ILeaderboardService? leaderboards = null, InkFlow.Platform.IShowcaseService? showcases = null,
            InkFlow.Platform.IIdentityService? identity = null, int leaderboardPageSize = 50)
        {
            endless?.BindServices(ads);
            shop?.BindServices(iap);
            rankings?.BindServices(leaderboards, showcases, identity, leaderboardPageSize);
        }

        private void Awake() => WireGraph();

        private void Start()
        {
            if (navigation == null || hub == null)
            {
                Debug.LogError("[InkFlow] AppRouter без NavigationStack або хаба — навігації не буде.");
                return;
            }

            // Без стану екрани мовчки показують мокові числа — найгірший вид
            // поломки, бо виглядає правдоподібно. Кричимо.
            if (_state == null)
                Debug.LogError(
                    "[InkFlow] AppRouter.Configure не викликано — екрани покажуть МОКОВІ дані " +
                    "(нік «Нова», 1250 нафти, рекорд 8420). Перевір, чи підв'язаний GameBootstrap.router " +
                    "у Main.unity, і перезбери сцену: Ink Flow → Setup → Build Main Scene.");

            HideAll();
            navigation.SetRoot(hub);
        }

        /// <summary>
        /// Гасимо всі екрани перед стартом. Вони лежать у сцені активними, щоб
        /// бутстрап міг застосувати стиль, — але показувати одразу дев'ять
        /// не можна.
        /// </summary>
        private void HideAll()
        {
            foreach (var screen in AllScreens())
                if (screen != null && screen.gameObject.activeSelf)
                    screen.gameObject.SetActive(false);
        }

        private ScreenBase?[] AllScreens() => new ScreenBase?[]
        {
            hub, levelMap, comingSoon, endless, galaxy, planet, collection, shop, rankings, profile, settings
        };

        private void WireGraph()
        {
            // ── Хаб ──
            if (hub != null)
            {
                // Картка «Рівні» лишається, але веде на заглушку «Скоро» (документ §10):
                // карта рівнів у сцені є, та вхід у неї вимкнено, поки режим не
                // переписано на новому ядрі.
                hub.LevelsRequested += () => Push(comingSoon);
                hub.EndlessRequested += () => Push(endless, new EndlessArgs(_balance));
                hub.TabRequested += OnHubTab;
                // Блок профілю в шапці веде ТОЧНО тим самим маршрутом, що й
                // вкладка внизу — інакше два входи в один екран рано чи пізно
                // розійшлися б аргументами.
                hub.ProfileRequested += () => OnHubTab("profile");
            }

            // ── Карта рівнів (осиротіла, вхід вимкнено) ──
            if (levelMap != null)
            {
                levelMap.BackRequested += Pop;
                levelMap.PlayRequested += id =>
                    Debug.Log($"[InkFlow] Рівень {id}: режим «Рівні» ще не реалізовано на новому ядрі.");
            }

            if (comingSoon != null)
                comingSoon.BackRequested += Pop;

            if (endless != null)
                endless.BackRequested += Pop;

            // ── Галактика, планета-вітрина й колекція (§12) ──
            if (galaxy != null)
            {
                galaxy.BackRequested += Pop;
                galaxy.OpenRequested += (galaxyIndex, planetIndex) => Push(planet, new PlanetArgs(galaxyIndex, planetIndex));
            }

            if (planet != null)
            {
                planet.BackRequested += Pop;
                // Колекція лягає ПОВЕРХ планети: вибрав картинку — Pop повертає на ту саму планету,
                // і вона перечитує слоти зі збереження в OnEnter.
                planet.CollectionRequested += args => Push(collection, args);
                planet.NextPlanetRequested += Pop;
            }

            if (collection != null)
            {
                collection.BackRequested += Pop;
                collection.PicturePicked += (args, pictureId) =>
                {
                    // Постановка — одна дія стану: перевірка копій, запис і збереження разом.
                    // Вітрина профілю (§14) — той самий екран, інша дія стану; профіль перечитає її в OnEnter.
                    if (_state != null && args.Showcase)
                        _state.SetShowcasePicture(pictureId);
                    else if (_state != null && args.PlanetId != null)
                        _state.TryPlaceInSlot(args.Galaxy, args.PlanetId, args.Slot, pictureId, System.DateTime.UtcNow);
                    Pop();
                };
            }

            // ── Решта вкладок ──
            if (shop != null)
                shop.BackRequested += Pop;

            if (rankings != null)
            {
                rankings.BackRequested += Pop;
                // Чужа галактика — той самий екран у режимі перегляду з вітриною гравця (§16).
                // Pop із неї поверне саме в Рейтинги: стек так і влаштований.
                rankings.PlayerOpened += args => Push(galaxy, args);
            }

            if (profile != null)
            {
                profile.BackRequested += Pop;
                profile.NickEditRequested += OpenNickPrompt;
                // Вітринна картинка — з колекції в режимі вибору; Pop повертає в профіль.
                profile.ShowcasePickRequested += () => Push(collection, CollectionArgs.ForShowcase());
            }

            // ── Налаштування (§15): шестерня на кожному екрані — одна подія базового екрана ──
            foreach (var screen in AllScreens())
                if (screen != null && !ReferenceEquals(screen, settings))
                    screen.SettingsRequested += OpenSettings;

            if (settings != null)
            {
                settings.BackRequested += Pop;
                settings.CollectionRequested += () => Push(collection, new CollectionArgs());
                settings.HomeRequested += GoHome;
                settings.RestartRequested += () =>
                {
                    // «Заново» є лише в забігу. Спершу стираємо зліпок і призупинений забіг, потім знімаємо
                    // налаштування: OnEnter забігу без них сам стартує НОВУ сесію — один прохід замість
                    // «відновити стару, а тоді перезапустити».
                    _state?.ClearRun();
                    endless?.DiscardSuspendedRun();
                    Pop();
                };
            }
        }

        /// <summary>Чи відкрито налаштування поверх забігу: тоді є «Заново», а «Додому» питає підтвердження.</summary>
        private bool InRun => navigation != null && endless != null && ReferenceEquals(navigation.Current, endless);

        private bool _settingsFromRun;
        private NickRules _nickRules = NickRules.Default;

        private void OpenSettings()
        {
            if (settings == null)
            {
                Debug.LogError("[InkFlow] SettingsScreen не підв'язаний — перезбери: Ink Flow → Setup → Build Settings Screen → Build Main Scene.");
                return;
            }
            // Запам'ятовуємо в момент відкриття: коли натиснуть «Додому», верхнім екраном будуть уже налаштування.
            _settingsFromRun = InRun;
            Push(settings, new SettingsArgs(_settingsFromRun));
        }

        /// <summary>
        /// «Додому» — у хаб із чистим стеком. З забігу — з підтвердженням (§15); забіг при цьому
        /// зберігається (OnExit екрана пише зліпок), тож гравець нічого не втрачає.
        /// </summary>
        private void GoHome()
        {
            if (_settingsFromRun && confirmPrompt != null)
            {
                confirmPrompt.Show("Вийти в меню?", "Забіг збережеться — продовжиш пізніше.", "Вийти", ResetToHub);
                return;
            }
            ResetToHub();
        }

        private void ResetToHub()
        {
            if (navigation == null || hub == null)
                return;
            navigation.SetRoot(hub);
        }

        /// <summary>
        /// Зміна ніка. Окремого онбординг-екрана поки немає — тут простий
        /// діалог поверх профілю; коли робитимемо перший запуск цілком,
        /// він переїде туди.
        /// </summary>
        private void OpenNickPrompt()
        {
            if (_state == null || nickPrompt == null)
                return;

            nickPrompt.Show(_state.Nick, nick =>
            {
                // Діалог уже перевірив нік за тими самими правилами; стан перевіряє ще раз — він джерело правди.
                if (!_state.SetNick(nick, _nickRules, out _))
                    return;

                // Нік видно і в хабі, і в рейтингах — перечитуємо обидва.
                hub?.Refresh();
                profile?.Refresh();
            });
        }

        private void OnHubTab(string id)
        {
            switch (id)
            {
                case "galaxy":
                    Push(galaxy, GalaxyArgs.Own);
                    break;
                case "shop":
                    Push(shop);
                    break;
                case "ranks":
                    Push(rankings);
                    break;
                case "profile":
                    Push(profile);
                    break;
                default:
                    Debug.LogWarning($"[InkFlow] Невідома вкладка навігації: {id}");
                    break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Дев-панель: після скидання збереження повертаємось у хаб із чистим стеком.</summary>
        public void RestartFromHub()
        {
            if (navigation == null || hub == null)
                return;
            HideAll();
            navigation.SetRoot(hub);
        }
#endif

        private void Push(ScreenBase? screen, ScreenArgs? args = null)
        {
            if (screen == null)
            {
                Debug.LogError("[InkFlow] Перехід у непідв'язаний екран — перевір посилання в AppRouter.");
                return;
            }

            navigation?.Push(screen, args);
        }

        private void Pop() => navigation?.Pop();
    }
}
