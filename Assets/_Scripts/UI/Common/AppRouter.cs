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
        [SerializeField] private LevelScreen level;
        [SerializeField] private EndlessScreen endless;
        [SerializeField] private GalaxyScreen galaxy;
        [SerializeField] private PaintScreen paint;
        [SerializeField] private ShopScreen shop;
        [SerializeField] private RankingsScreen rankings;
        [SerializeField] private ProfileScreen profile;

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
            level?.BindState(state);
        }

        private void Awake() => WireGraph();

        private void Start()
        {
            if (navigation == null || hub == null)
            {
                Debug.LogError("[InkFlow] AppRouter без NavigationStack або хаба — навігації не буде.");
                return;
            }

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
            hub, levelMap, level, endless, galaxy, paint, shop, rankings, profile
        };

        private void WireGraph()
        {
            // ── Хаб ──
            if (hub != null)
            {
                hub.LevelsRequested += () => Push(levelMap);
                hub.EndlessRequested += () => Push(endless, new EndlessArgs(_balance));
                hub.TabRequested += OnHubTab;
            }

            // ── Карта рівнів ──
            if (levelMap != null)
            {
                levelMap.BackRequested += Pop;
                levelMap.PlayRequested += id => Push(level, new LevelArgs(id, null, _balance));
            }

            // ── Партія ──
            if (level != null)
            {
                level.BackRequested += Pop;
                // Результат рівня йде у збереження, а не просто в анімацію зірок:
                // без цього пройдений рівень забувався б при виході з гри.
                // Підсумок партії — одним викликом у стан: зірки, нафта й запис
                // у файл разом. Розкидані по екранах, вони рано чи пізно
                // розійшлися б.
                level.LevelCleared += (id, stars) =>
                    _state?.CompleteLevel(id, stars, isBoss: false, System.DateTime.UtcNow);
                // Заміна, не пуш: після десяти рівнів поспіль «‹» вело б через усі десять.
                level.NextLevelRequested += id =>
                    navigation?.Replace(level, new LevelArgs(id, null, _balance));
            }

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
            }
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
