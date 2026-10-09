#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlow.App
{
    /// <summary>
    /// Панель для тестування метагри. Існує ТІЛЬКИ в редакторі й Development
    /// Build — увесь файл під `#if`, тож у релізний білд не потрапляє жодного
    /// байта.
    ///
    /// Навіщо: перевірити денний ліміт, порожній профіль чи накопичення без неї
    /// означало б грати вручну годинами й чекати справжнього завтра.
    ///
    /// Малюється через IMGUI (`OnGUI`), а не UGUI, свідомо: панель не має
    /// потрапляти в канвас гри, ділити з ним EventSystem чи їхати разом із
    /// його розкладкою. Продуктивність IMGUI тут не має значення — вона
    /// відкрита лише коли її відкрили.
    ///
    /// Відкриття: довгий тап (0.7 с) у ЛІВИЙ ВЕРХНІЙ кут екрана або
    /// Ink Flow → Debug → Toggle Dev Panel.
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;

        [Tooltip("Скільки треба тримати палець у куті, щоб панель відкрилась.")]
        [SerializeField, Min(0.2f)] private float holdToOpen = 0.7f;

        [Tooltip("Розмір кутової зони у частках екрана.")]
        [SerializeField, Range(0.05f, 0.3f)] private float cornerFraction = 0.12f;

        private static DevPanel? _instance;

        private bool _open;
        private float _holdSince = -1f;
        private Vector2 _scroll;
        private string _oilInput = "500";
        private string _starsInput = "3";
        private string _nickInput = string.Empty;
        private int _paintIndex;
        private string _status = string.Empty;

        private PlayerState? State => bootstrap != null ? bootstrap.State : null;

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;
        }

        /// <summary>Точка входу для меню редактора.</summary>
        public static void ToggleFromMenu()
        {
            if (_instance == null)
            {
                Debug.LogWarning("[InkFlow] Дев-панель доступна лише в Play Mode на сцені Main.");
                return;
            }

            _instance._open = !_instance._open;
        }

        private void Update()
        {
            // Через Input System, а НЕ через старий UnityEngine.Input: у
            // Player Settings стоїть Active Input Handling = Input System
            // Package, і старий клас там кидає InvalidOperationException —
            // щокадру, тобто сотнями за секунду.
            if (!TryGetPress(out var point))
            {
                _holdSince = -1f;
                return;
            }

            var inCorner = point.x < Screen.width * cornerFraction &&
                           point.y > Screen.height * (1f - cornerFraction);

            if (!inCorner)
            {
                _holdSince = -1f;
                return;
            }

            if (_holdSince < 0f)
                _holdSince = Time.unscaledTime;
            else if (Time.unscaledTime - _holdSince >= holdToOpen)
            {
                _open = !_open;
                _holdSince = -1f;
            }
        }

        /// <summary>
        /// Чи є зараз натискання і де. Читаємо і дотик, і мишу: у Simulator
        /// працює друге, на пристрої — перше.
        /// </summary>
        private static bool TryGetPress(out Vector2 point)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                point = touch.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                point = mouse.position.ReadValue();
                return true;
            }

            point = default;
            return false;
        }

        private void OnGUI()
        {
            if (!_open)
                return;

            var width = Mathf.Min(Screen.width * 0.92f, 640f);
            var height = Screen.height * 0.8f;
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.1f, width, height);

            GUI.Box(rect, "Ink Flow · дев-панель");
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 26f, rect.width - 20f, rect.height - 36f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            // Працює і без стану гравця: перевірка вигляду не має залежати від збереження.
            DrawPulseActions();

            var state = State;
            if (state == null)
            {
                GUILayout.Label("GameBootstrap не підв'язаний або стан ще не завантажено.");
            }
            else
            {
                DrawState(state);
                DrawEconomyActions(state);
                DrawProgressActions(state);
                DrawDailyLimitActions(state);
                DrawSaveActions(state);
            }

            if (_status.Length > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"→ {_status}");
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Закрити"))
                _open = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Пульсація «мало місця» (§11): показати рівень вручну, окремо від умови — щоб відрізнити
        /// «не малюється» від «не спрацьовує». Кожну зміну рівня, яку порахувала гра, видно в консолі
        /// («[InkFlow] Пульсація: …») і в рядку нижче.
        /// </summary>
        private void DrawPulseActions()
        {
            GUILayout.Label("Пульсація «мало місця»");
            var forced = EndlessScreen.DebugPulse;
            GUILayout.Label(forced.HasValue ? $"  вручну: {forced.Value}" : "  рахує гра");
            GUILayout.Label($"  гра: {EndlessScreen.LastDanger}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пульсація Warn"))
                SetPulse(DangerLevel.Warn);
            if (GUILayout.Button("Пульсація Strong"))
                SetPulse(DangerLevel.Strong);
            if (GUILayout.Button("Вимкнути"))
                SetPulse(DangerLevel.None);
            if (GUILayout.Button("Як у грі"))
                SetPulse(null);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private void SetPulse(DangerLevel? level)
        {
            EndlessScreen.SetDebugPulse(level);
            _status = level.HasValue ? $"пульсація вручну: {level.Value}" : "пульсація: як у грі";
        }

        private void DrawState(PlayerState state)
        {
            GUILayout.Label("── Стан ──");
            GUILayout.Label($"Нік: {state.Nick}");
            GUILayout.Label($"Нафта: {state.Wallet.OilDrops}");
            GUILayout.Label($"Галактика {state.CurrentGalaxy + 1}: у слотах {GalaxyState.TotalFilled(state.Galaxy, state.Layout)} картинок," +
                            $"планет ожило {GalaxyState.CompletedPlanets(state.Galaxy, state.Layout)}, " +
                            $"галактик завершено {GalaxyState.CompletedGalaxies(state.Galaxy, state.Layout)}");
            GUILayout.Label($"Рівні: пройдено до {LevelProgress.HighestCleared(state.Progress)}, " +
                            $"зірок {LevelProgress.TotalStars(state.Progress)}");
            GUILayout.Label($"Рекорд Нескінченного: {state.Progress.EndlessRecord}");
            GUILayout.Label($"Денний ліміт: {state.DailyLimit.PlaysToday} партій, " +
                            $"множник ×{state.DailyLimit.RewardMultiplier:0.##}, " +
                            $"доба {state.DailyLimit.CurrentDayUtc:yyyy-MM-dd}");

            // Режим «Рівні» вимкнено: картка в хабі веде на заглушку, а карта й
            // прогрес лишаються у файлі (документ §10).
            GUILayout.Label("⚠ Режим «Рівні» — заглушка «Скоро»; прогрес рівнів лише зберігається");
        }

        private void DrawEconomyActions(PlayerState state)
        {
            GUILayout.Space(6f);
            GUILayout.Label("── Економіка ──");

            GUILayout.BeginHorizontal();
            _oilInput = GUILayout.TextField(_oilInput, GUILayout.Width(90f));
            if (GUILayout.Button("Додати нафту") && long.TryParse(_oilInput, out var oil))
            {
                state.Wallet.Add(oil, RewardSource.Debug);
                state.Persist();
                Report($"+{oil} нафти");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawProgressActions(PlayerState state)
        {
            GUILayout.Space(6f);
            GUILayout.Label("── Прогрес ──");

            GUILayout.BeginHorizontal();
            _starsInput = GUILayout.TextField(_starsInput, GUILayout.Width(50f));
            if (GUILayout.Button("Відкрити всі рівні з N зірками") &&
                int.TryParse(_starsInput, out var stars))
            {
                for (var id = 1; id <= LevelMap.MockTotal; id++)
                    LevelProgress.Record(state.Progress, id, stars);
                state.Persist();
                Report($"усі {LevelMap.MockTotal} рівнів по {stars}★");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пройти наступний рівень"))
            {
                var next = LevelProgress.HighestCleared(state.Progress) + 1;
                var reward = state.CompleteLevel(next, 3, false, System.DateTime.UtcNow);
                Report($"рівень {next} пройдено на 3★, +{reward} нафти");
            }

            if (GUILayout.Button("Скинути рівні"))
            {
                state.Progress.Levels.Clear();
                state.Persist();
                Report("прогрес рівнів очищено");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("── Картинки ──");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зібрати випадкову картинку"))
            {
                var library = state.Library;
                var pick = library[UnityEngine.Random.Range(0, library.Count)];
                var isNew = state.CollectPicture(pick.Id, System.DateTime.UtcNow);
                Report($"«{pick.Name}» у колекції{(isNew ? " (нова)" : "")}: різних {state.Collection.Distinct}");
            }
            if (GUILayout.Button("Скинути збережений забіг"))
            {
                state.ClearRun();
                Report("перерваного забігу немає — наступний почнеться з нової картинки");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Заповнити поточну планету"))
            {
                // §12: збираємо стільки копій, скільки порожніх слотів, і ставимо їх — планета оживає.
                var galaxy = state.CurrentGalaxy;
                var index = GalaxyState.CurrentPlanetIndex(state.Galaxy, galaxy, state.Layout);
                if (index >= state.Layout.Planets.Count)
                    Report("усі планети цієї галактики вже ожили");
                else
                {
                    var planet = state.Layout.Planets[index];
                    var placed = 0;
                    for (var slot = 0; slot < planet.Slots; slot++)
                    {
                        if (GalaxyState.PictureAt(state.Galaxy, galaxy, planet.Id, slot) != null)
                            continue;
                        var pick = state.Library[UnityEngine.Random.Range(0, state.Library.Count)];
                        state.CollectPicture(pick.Id, System.DateTime.UtcNow);
                        if (state.TryPlaceInSlot(galaxy, planet.Id, slot, pick.Id, System.DateTime.UtcNow))
                            placed++;
                    }
                    Report($"{planet.Name}: поставлено {placed} картинок, планета ожила");
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Очистити колекцію й слоти"))
            {
                // Живі об'єкти — джерело правди, файл — зліпок: чистимо колекцію й слоти в стані,
                // Persist згортає їх у файл. Екрани перечитують стан у OnEnter — перезапуск не потрібен.
                state.Collection.Clear();
                state.Galaxy.Slots.Clear();
                state.Persist();
                Report("колекцію й слоти планет очищено — гаманець і рекорди на місці");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _nickInput = GUILayout.TextField(_nickInput.Length > 0 ? _nickInput : state.Nick,
                GUILayout.Width(140f));
            if (GUILayout.Button("Змінити нік"))
            {
                // Ті самі правила §14, що й у діалозі: дев-панель не має заводити в гру нік, якого гра не приймає.
                if (state.SetNick(_nickInput, NickRules.Default, out var verdict))
                    Report($"нік → {state.Nick}");
                else
                    Report($"нік відхилено: {NickPrompt.Message(verdict, NickRules.Default)}");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDailyLimitActions(PlayerState state)
        {
            GUILayout.Space(6f);
            GUILayout.Label("── Денний ліміт ──");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути лічильник"))
            {
                state.DailyLimit.DebugResetDay();
                state.Persist();
                Report("денний ліміт обнулено");
            }

            // Без цього ліміт 10/10 не перевірити, не чекаючи справжнього завтра.
            if (GUILayout.Button("Промотати добу вперед"))
            {
                state.DailyLimit.RollOverIfNeeded(System.DateTime.UtcNow.Date.AddDays(1));
                state.Persist();
                Report("наступна доба — лічильник скинуто");
            }

            if (GUILayout.Button("Вичерпати ліміт"))
            {
                for (var i = 0; i < state.Economy.FullRewardPlays; i++)
                    state.DailyLimit.RegisterPlay(System.DateTime.UtcNow);
                state.Persist();
                Report($"зіграно {state.DailyLimit.PlaysToday} — множник " +
                       $"×{state.DailyLimit.RewardMultiplier:0.##}");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSaveActions(PlayerState state)
        {
            GUILayout.Space(6f);
            GUILayout.Label("── Збереження ──");
            GUILayout.Label(JsonSaveStorage.DefaultPath);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Відкрити папку"))
            {
                Application.OpenURL($"file://{Application.persistentDataPath}");
                Report("відкрито теку збережень");
            }

            if (GUILayout.Button("Зберегти зараз"))
            {
                state.Persist();
                Report("файл записано");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("СКИНУТИ ЗБЕРЕЖЕННЯ (новий гравець)"))
            {
                bootstrap!.ResetSaveAndRestart();
                Report("збереження видалено — гру перезапущено як новий профіль");
            }
            GUI.color = Color.white;
        }

        private void Report(string message)
        {
            _status = message;
            Debug.Log($"[InkFlow · dev] {message}");
        }
    }
}
#endif
