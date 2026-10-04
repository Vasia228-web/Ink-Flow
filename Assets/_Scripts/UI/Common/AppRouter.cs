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
        [SerializeField] private PaintScreen paint;
        [SerializeField] private ShopScreen shop;
        [SerializeField] private RankingsScreen rankings;
        [SerializeField] private ProfileScreen profile;

        [Header("Діалоги")]
        [SerializeField] private NickPrompt? nickPrompt;

        private BalanceData _balance = BalanceData.Default;
        private PlayerState? _state;

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
        public void Configure(BalanceData balance, PlayerState state)
        {
            _balance = balance;
            _state = state;

            hub?.BindState(state);
            levelMap?.BindState(state);
            galaxy?.BindState(state);
            paint?.BindState(state);
            shop?.BindState(state);
            rankings?.BindState(state);
            profile?.BindState(state);
            endless?.BindState(state);
        }

        /// <summary>Платформні сервіси для кнопок §9 — лише туди, де вони потрібні.</summary>
        public void BindServices(InkFlow.Platform.IAdsService? ads, InkFlow.Platform.IIapService? iap) =>
            endless?.BindServices(ads, iap);

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
            hub, levelMap, comingSoon, endless, galaxy, paint, shop, rankings, profile
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

            // ── Галактика й фарбування ──
            if (galaxy != null)
            {
                galaxy.BackRequested += Pop;
                galaxy.PaintRequested += index => Push(paint, new PaintArgs(index));
            }

            if (paint != null)
            {
                paint.BackRequested += Pop;
                // «+ Магазин» і «Мало фарби» ведуть в один бік — на вкладку фарб.
                // Повернення саме на фарбування, бо магазин ліг ПОВЕРХ нього.
                paint.ShopRequested += () => Push(shop, new ShopArgs(oilTab: false));
                paint.NextPlanetRequested += Pop;
            }

            // ── Решта вкладок ──
            if (shop != null)
                shop.BackRequested += Pop;

            if (rankings != null)
            {
                rankings.BackRequested += Pop;
                // Чужа галактика — той самий екран у режимі перегляду.
                // Pop із неї поверне саме в Рейтинги: стек так і влаштований.
                rankings.PlayerOpened += args => Push(galaxy, args);
            }

            if (profile != null)
            {
                profile.BackRequested += Pop;
                profile.ShopRequested += () => Push(shop, new ShopArgs(oilTab: false));
                profile.SettingsRequested += () =>
                    Debug.Log("[InkFlow] Налаштування ще не зроблені — екрана немає.");
                profile.NickEditRequested += OpenNickPrompt;
            }
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
                _state.Nick = nick;
                _state.Persist();

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
                    Push(shop, new ShopArgs(oilTab: false));
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
