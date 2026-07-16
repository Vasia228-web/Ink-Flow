# Ink Flow — Unity merge-puzzle prototype

Свайп-based merge-puzzle для Unity 2D (target: Android/iOS). Левел-дизайнери редагують рівні через ScriptableObject-аси в Inspector, не чіпаючи код.

## Стек і ключові рішення

- **Unity 6000.5.4f1**, 2D URP template. Репозиторій = корінь Unity-проєкту.
- **Input System** (`com.unity.inputsystem`) — новий Input System, НЕ legacy `Input.GetMouseButton`. Touch-first, без hover-залежностей. Один Input Actions asset для Editor (mouse) і mobile (touch).
- **Анімації**: Фаза 1 — без анімацій. Для Фази 2 обрано вбудовані Coroutines/AnimationCurve-твіни замість DOTween: одна менша зовнішня залежність, обсяг твінів малий (merge/burst/pulse), а власний легкий tween-хелпер покриває потреби. Переглянути, якщо у Фазі 2/3 твінів стане суттєво більше.
- **ScriptableObjects** для конфігурації рівнів (не JSON) — Inspector-редагування "з коробки".
- **Assembly Definitions**: `Core` (чиста логіка, POCO), `Gameplay`, `UI`, `Tests`. Core НЕ посилається на Gameplay/UI — якщо таке посилання "потрібне", це помилка дизайну.
- **Object Pooling**: `UnityEngine.Pool.ObjectPool<T>` через обгортку `CellPool`. ЖОДНИХ `Instantiate/Destroy` для клітинок/часток поза пулом — GC-паузи на мобільних відчутні під час chain-бурстів.
- **Addressables** для LevelConfig-асетів (група "Levels") — оновлення рівнів/артів окремо від білду, готовність до live-ops.
- **Свідомо НЕ використовуємо**: DI-фреймворк (VContainer/Zenject) — один core-loop, прямі serialized-референси простіші; DOTS/ECS — сітка ≤7×7 цього не потребує.

## Правила гри (джерело правди для балансу)

- Сітка N×N; клітинка порожня або `{color:int, density:int}`.
- Свайп A→B (тільки 4 напрямки): якщо `color A == color B` → merge: `B.density += A.density`, A порожніє. Якщо кольори різні → хід відхилено, хід НЕ витрачається.
- **Burst**: після merge, якщо `B.density >= burstThreshold` (з LevelConfig, базово 10) → B зникає; 4-directional сусіди: порожні фарбуються в color B з density=1; сусіди того ж кольору (ДО вибуху) отримують +1 density. Рекурсивно → chain reaction, лічильник `chainLength` для скору/фідбеку.
- Хід витрачається ТІЛЬКИ якщо стався валідний merge.
- Win-умови (enum `WinConditionType`): `Clear` (SingleColor | SingleCell) або `ScoreAttack` (targetScore за maxMoves; score = сума density усіх burst).
- Поразка: maxMoves вичерпано, ціль не досягнута → одразу активна Retry (реініціалізація GridModel з LevelConfig, без перезавантаження сцени).

## Статус

- **Фаза 0 (інфраструктура)**: ✅ CLAUDE.md, .gitignore/.gitattributes (LFS), git-workflow.
- **Фаза 1 (core loop)**: у роботі, гілка `phase-1-core-loop`. Обсяг: asmdefs, Core-логіка + Edit Mode тести, CellPool, GridView, свайп-інпут, 3 рівні (Addressables), HUD+Retry. БЕЗ анімацій/звуку/метагри.
- Фаза 2 (feel/анімації), Фаза 3 (метагра) — тільки після підтвердження користувача.

## Тести

- Edit Mode тести: `Assets/_Tests/EditMode` (Unity Test Framework, NUnit), покривають merge/burst/chain у `GameRules` без сцени.
- CLI (Unity Editor має бути ЗАКРИТИЙ — batchmode не працює при відкритому проєкті):
  ```
  "/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/MacOS/Unity" \
    -batchmode -projectPath "<repo root>" \
    -runTests -testPlatform EditMode -testResults "$(pwd)/TestResults.xml" | cat
  ```
- Швидка headless-перевірка чистої Core-логіки (без Unity, компілює Core + консольний runner бандленим dotnet):
  ```
  ./Tools/run-core-tests.sh
  ```
- Перед комітом, що зачіпає `GameRules.cs`/`GridModel.cs` — прогнати тести; червоні тести НЕ комітяться.

## Git-workflow

- Окремий коміт після КОЖНОГО завершеного пункту фази; imperative-повідомлення ("Add GameRules merge/burst logic with unit tests").
- Окрема гілка на фазу (`phase-1-core-loop`, `phase-2-feel-animations`); merge у `main` лише після підтвердження користувача.
- Git LFS активний (див. `.gitattributes`): png/psd/wav/mp3/fbx тощо.

## Конвенції

- `GameRules.cs`, `GridModel.cs`, `Cell.cs` — POCO, БЕЗ UnityEngine API (тестованість без сцени). `LevelConfig.cs` — ScriptableObject у Core (єдиний виняток, тільки серіалізація даних, без логіки).
- MonoBehaviour: `[SerializeField]` + приватні поля, не публічні. Ніяких `Find/FindObjectOfType` в Update.
- Рівні: `Assets/_ScriptableObjects/Levels/Level_XXX.asset`, Addressable-група "Levels", адреса `Levels/Level_XXX`.
- Формули density/burst закоментовані в `GameRules.cs` людською мовою — левел-дизайнери правлять LevelConfig без читання коду.
- Editor-утиліти: меню `Ink Flow/...` (bootstrap сцени/префабів/Addressables — `Ink Flow/Setup/Bootstrap Phase 1`).

**Онови цей файл у кінці кожної фази/значної зміни.**
