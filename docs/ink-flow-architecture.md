# Ink Flow — Технічна архітектура (Unity, Android + iOS)

> **Для Claude Code.** Це технічна правда проєкту. Ігрова правда — в `ink-flow-master-doc.md`; якщо цей файл суперечить йому в питаннях геймдизайну, правий майстер-док. Якщо майстер-док суперечить цьому файлу в питаннях коду — правий цей.
>
> **Головний принцип архітектури:** ігрова логіка не знає, що існує Unity. Усе, що можна протестувати без редактора, тестується без редактора.

---

## Зміст

1. Стек і платформи
2. Карта збірок (asmdef) і правила залежностей
3. Структура папок
4. Шар Core — чиста логіка
5. Правила механіки → код (точні формули)
6. Детермінізм і RNG
7. Дані та конфіги (data-driven)
8. Шар Gameplay — міст між Core і Unity
9. Шар UI і навігація
10. Шар Meta — економіка, галактика, збереження
11. Шар Platform — абстракція над iOS/Android
12. Мобільна специфіка: продуктивність і сумісність
13. Налаштування білдів (Android / iOS)
14. Тестування
15. Симулятори (Фаза 4)
16. Git-workflow і `CLAUDE.md`
17. Відповідність фазам розробки
18. Інваріанти, які не можна порушувати

---

## 1. Стек і платформи

| Пункт | Рішення |
|---|---|
| Движок | Unity 6 LTS (6000.x), 2D URP |
| Мова | C# 9+, nullable enabled в Core |
| Цільові платформи | Android (Google Play), iOS (App Store) — **один код, нуль `#if` у геймплеї** |
| Скриптинг | IL2CPP, .NET Standard 2.1 |
| Інпут | Unity Input System (новий), один `.inputactions` на всі платформи |
| Асети | Addressables (рівні, планети, палітри) |
| Пули | `UnityEngine.Pool.ObjectPool<T>` |
| DI-фреймворк | **немає** (свідомо) — композиційний корінь вручну, див. §8 |
| DOTS/ECS | **немає** (свідомо) — сітка максимум 7×7, це десятки об'єктів |
| Бекенд | немає до Фази 6; далі Unity Gaming Services |

**Чому без DI і DOTS.** Проєкт має один екран геймплею й ≤49 клітинок. DI-контейнер додав би час старту й магію в стектрейсах, ECS — складність без виграшу. Ручна композиція в одному місці (`GameBootstrap`) читається краще й дебажиться швидше. До цього питання повертаємось на Фазі 3, якщо метагра розростеться.

---

## 2. Карта збірок (asmdef) і правила залежностей

```
InkFlow.Core          ← ЖОДНИХ посилань. Не знає про UnityEngine.
   ↑
InkFlow.Gameplay      ← Core + UnityEngine
   ↑
InkFlow.UI            ← Core + UnityEngine (НЕ бачить Gameplay напряму)
InkFlow.Meta          ← Core + UnityEngine
InkFlow.Platform      ← UnityEngine (інтерфейси + нативні реалізації)
InkFlow.App           ← усе вище (композиційний корінь, сцени, навігація)

InkFlow.Core.Tests        ← Core (EditMode, без Unity API)
InkFlow.Gameplay.Tests    ← Core + Gameplay (PlayMode)
InkFlow.Meta.Tests        ← Core + Meta (EditMode)
InkFlow.Editor           ← усе (тільки редактор: бутстрап, інструменти, симулятори)
```

**Жорсткі правила:**

1. **`InkFlow.Core` не посилається ні на що**, включно з `UnityEngine`. Заборонені `Vector2`, `Color`, `Random`, `Debug.Log`, корутини, `MonoBehaviour`. Замість них — власні `GridPos`, `InkColor` (enum), `IRandomSource`, `ILogSink`.
   *Перевірка:* якщо в Core з'явився `using UnityEngine` — це помилка збірки, а не питання смаку.
2. **UI не викликає Gameplay напряму.** Обмін — через події (`GameEvents`) і команди (`IGameCommands`). UI шле наміри, отримує стани.
3. **Meta не знає про Gameplay.** Режим завершився → віддав `GameResult` у Meta. Meta не має уявлення, що таке крапля.
4. **Platform — тільки інтерфейси назовні.** Ніхто, крім `InkFlow.App`, не знає, яка там реалізація.
5. Залежності односторонні. Циклів немає. Якщо хочеться цикл — потрібна подія.

---

## 3. Структура папок

```
Assets/
  _Scripts/
    Core/            (asmdef InkFlow.Core)
      Model/         GridModel, Cell, InkColor, GridPos
      Rules/         MergeRules, BurstResolver, MoveValidator, DeadlockDetector
      Session/       GameSession, PuzzleSession, EndlessSession, BossSession
      Boss/          BossModel, BossAction, BossTelegraph
      Scoring/       ScoreCalculator, StarCalculator
      Random/        IRandomSource, XorShiftRandom
      Config/        (POCO-дзеркала конфігів: BalanceData, LevelData)
    Gameplay/        (asmdef InkFlow.Gameplay)
      Views/         GridView, CellView, BossView, SplashView
      Pools/         CellPool, ParticlePool
      Input/         SwipeInput, InputRouter
      Feel/          MergeAnimator, BurstAnimator, ShakeController, HapticTrigger
      GameEvents.cs
    UI/              (asmdef InkFlow.UI)
      Hub/ Levels/ Endless/ Shop/ Galaxy/ Ranks/ Profile/
      Common/        ScreenBase, NavigationStack, SafeAreaBinder, CurrencyWidget
    Meta/            (asmdef InkFlow.Meta)
      Economy/       Wallet, RewardCalculator, DailyLimitTracker
      Paints/        PaintInventory, PaintCatalog, PaintTier
      Galaxy/        GalaxyModel, PlanetModel, ZoneModel, PaintService
      Progress/      LevelProgress, StarLedger, Achievements
      Save/          SaveFile, SaveMigrations, ISaveStorage, JsonSaveStorage
    Platform/        (asmdef InkFlow.Platform)
      IHapticService, IAnalyticsService, IAdsService, IIapService,
      IReviewService, INotificationService
      Android/ iOS/ Editor/   (реалізації + Null-заглушки)
    App/             (asmdef InkFlow.App)
      GameBootstrap.cs, ServiceLocator.cs, SceneRouter.cs, FeatureFlags.cs
  _ScriptableObjects/
    Balance/  Paints/  Levels/  Planets/
  _Prefabs/     Cell, Boss, Splash, UI-екрани
  _Sprites/ _Audio/ _Materials/
  Scenes/       Boot.unity, Meta.unity, Game.unity
  Tests/        EditMode/  PlayMode/
  Plugins/      Android/  iOS/
```

---

## 4. Шар Core — чиста логіка

Ключові типи (сигнатури орієнтовні, іменування — обов'язкове):

```csharp
public readonly struct GridPos { public readonly int X, Y; }

public enum InkColor : byte { None = 0, Magenta, Cyan, Amber, Lime, Violet, Rose }

public struct Cell {
    public InkColor Color;
    public int Density;
    public CellFlags Flags;   // Empty | Ice | Wall | Blot | Heavy
    public bool IsEmpty => Color == InkColor.None;
}

public sealed class GridModel {
    public int Width { get; }
    public int Height { get; }
    public Cell this[GridPos p] { get; set; }
    public IEnumerable<GridPos> Neighbors(GridPos p);   // 4-directional, порядок фіксований
}
```

**Порядок сусідів завжди один і той самий: Up, Right, Down, Left.** Від цього залежить детермінізм ланцюгів — ніколи не міняти.

```csharp
public sealed class GameSession {          // базовий стан партії
    public GridModel Grid { get; }
    public int MovesLeft { get; }
    public MoveResult ApplyMove(GridPos from, GridPos to);
    public GameState State { get; }        // Playing | Won | Lost | Deadlock
}
```

`MoveResult` — **повний опис того, що сталося**, у вигляді впорядкованого списку подій:

```csharp
public sealed class MoveResult {
    public bool Accepted;                  // false = різні кольори, хід не витрачено
    public IReadOnlyList<GameEvent> Events; // Merge, Burst, Splash, Blur, Repaint,
                                            // BossHit, BossAction, Refill, Chain
    public int ChainDepth;
    public int ScoreGained;
}
```

**Це найважливіше архітектурне рішення всього проєкту.** Core не анімує — він повертає стрічку подій. `GridView` програє її послідовно з таймінгами. Завдяки цьому:
- логіка тестується без єдиного кадру рендеру;
- анімації можна прискорити/вимкнути, не чіпаючи правила;
- симулятор Фази 4 ганяє мільйони ходів за секунди.

---

## 5. Правила механіки → код (точні формули)

Це переклад майстер-доку в код. Кожен рядок = окремий юніт-тест.

### 5.1 Хід

```
IsValidMove(from, to):
    to є 4-сусідом from
    обидві не порожні, без Wall/Blot/Ice-блокування
    Grid[from].Color == Grid[to].Color
→ інакше Accepted = false, MovesLeft НЕ зменшується
```

### 5.2 Злиття

```
Grid[to].Density += Grid[from].Density
Grid[from] = empty
if Grid[to].Density >= threshold → Burst(to, force: Grid[to].Density)
```

### 5.3 Вибух — універсальна зона

```csharp
const int CROSS = 4;                              // зона завжди 4 клітинки
int PaintPower(int force)  => Clamp(force / 10, 1, threshold - 1);   // стеля 9
int SplashCount(int force) => force / 15;                            // 1 за кожні повні 15
```

Зона — **завжди** хрест (Up, Right, Down, Left). Форма **не залежить** від сили. Ніколи.

Гало для бризок — кільце Мангеттенської відстані рівно 2 від центру:

```csharp
IEnumerable<GridPos> Halo(GridPos c) => усі p, де |p.X-c.X| + |p.Y-c.Y| == 2;
```

### 5.4 Ефект на клітинку

```
власна клітинка → empty
для кожної клітинки хреста:
    empty        → Color = burstColor, Density = PaintPower
    той же колір → Density += PaintPower           → може дати ланцюговий Burst
    інший колір  → Density -= PaintPower           // РОЗМИВАННЯ
                   if Density <= 0 → Color = burstColor, Density = 1
для кожної бризки (SplashCount разів, ціль з Halo):
    той самий алгоритм, але PaintPower = 1
за межами сітки → подія OutOfBounds (на бос-рівні верхній край → BossHit)
```

### 5.5 Ланцюги

Обробка через **чергу FIFO**, не рекурсію. Порядок: клітинки хреста в порядку Up→Right→Down→Left, потім бризки в порядку генерації.

```csharp
const int MaxChainBursts = 64;   // запобіжник від нескінченного ланцюга
```

Ліміт існує тому, що щільне монохромне поле теоретично може зациклитись. При досягненні — ланцюг зупиняється, пишеться подія `ChainTruncated`. Тест на це обов'язковий.

### 5.6 Бос

```csharp
int SegmentsPainted(int force) => force >= 35 ? 3 : force >= 20 ? 2 : 1;   // стеля 3
```

- Влучання лише через `OutOfBounds` за **верхній** край. Жодних окремих правил стрільби.
- Вибух іншого кольору по пофарбованому сегменту → `Repaint` (сегмент стає новим кольором, лічильник `PlayerRepaints++` — від нього залежить третя зірка).
- Бос діє на кожному 3-му **прийнятому** ході (`Accepted == true`).
- **Телеграф:** намір обирається на ході N−1 і зберігається в `BossModel.PendingAction`. UI показує іконку. На ході N дія виконується — вона вже визначена, тож жодного «рандому в обличчя».
- `Sponge` не може обрати той самий сегмент двічі поспіль (`LastSpongeSegment`).

### 5.7 Перемога / поразка / тупик

```csharp
bool HasAnyMove()  => існує пара 4-сусідів однакового кольору;   // ЄДИНА перевірка живості
bool IsPuzzleWon() => distinctColors <= 1 || dropCount <= 1;
```

- Перемога перевіряється **після повного завершення ланцюга**, не в середині.
- Puzzle: `MovesLeft == 0 && !Won` → Lost. `!HasAnyMove()` → Deadlock (теж програш, окрема подія для чесного повідомлення).
- **Вибух не обов'язковий для перемоги.** Жодної перевірки «чи був burst».

### 5.8 Endless

```
після ходу: Refill() — нові краплі падають зверху в порожні клітинки
кінець партії: !HasAnyMove() && немає порожніх клітинок
захист: if (!HasAnyMove() && є порожні) → Refill()
        if (після Refill !HasAnyMove()) → перегенерувати кольори долитих крапель
        цикл до MaxRefillAttempts (напр. 32), потім — детермінований fallback:
        примусово поставити пару однакових сусідів
```

Fallback має існувати завжди: **система не має права вбити гравця сама** (інваріант §18).

### 5.9 Приплив (Endless)

```csharp
int TideLevel   => burstsTotal / 10;                       // +1 кожні 10 вибухів
int MinDensity  => Clamp(1 + TideLevel, 1, threshold - 1); // мінімальна густота нових крапель
```

---

## 6. Детермінізм і RNG

**`UnityEngine.Random` заборонений у Core і Gameplay-логіці.** Тільки для чисто косметичних частинок.

```csharp
public interface IRandomSource { int Next(int maxExclusive); }
public sealed class XorShiftRandom : IRandomSource   // власна реалізація, стабільна між платформами
```

| Режим | Джерело сіда |
|---|---|
| Puzzle | `levelId` + `attemptIndex`? **Ні** — тільки `levelId`. Те саме рішення = той самий результат завжди |
| Endless | `DateTime.UtcNow.Ticks` при старті партії, зберігається в `RunReplay` |
| Симулятор | сід передається ззовні |

**Чому це критично:** три зірки в Puzzle — обіцянка гравцю, що майстерність вирішує. Якщо бризки випадкові, ідеальне рішення іноді не спрацює — і гравець відчує обман. У Endless рандом навпаки бажаний.

Наслідок: партію можна відтворити, зберігши сід + список ходів (`RunReplay`). Це дає безкоштовний баг-репорт («надішли сід») і майбутню античит-перевірку рекордів.

---

## 7. Дані та конфіги (data-driven)

**Жодного балансного числа в коді.** Усі — у ScriptableObject-конфігах, які мають POCO-дзеркала в Core.

```
BalanceConfig.asset       threshold, paintPowerDivisor(10), splashDivisor(15),
                          maxChainBursts, tideStep, bossSegmentThresholds(20/35),
                          starThresholds(0.2 / 0.4)
EconomyConfig.asset       базові нагороди, dailyFullRewardPlays(10), reducedRate(0.25),
                          endlessRewardCurve, milestones[], bossMultiplier(3)
PaintCatalog.asset        список фарб: id, tier, ціна за літр, тип рендеру
LevelDefinition.asset     id, width, height, moves, стартова розкладка, goal, isBoss, seed
PlanetDefinition.asset    id, тип, список зон (id, назва, об'єм у літрах, маска)
```

**Правило:** зміна балансу = зміна `.asset`, ніколи не перекомпіляція. Це умова того, щоб Фаза 4 взагалі була можлива.

Рівні й планети роздаються через **Addressables** (групи `Levels`, `Planets`, `Paints`) — щоб на Фазі 6 додавати контент без нового білду в сторах.

---

## 8. Шар Gameplay — міст між Core і Unity

**`GridView`** — єдиний, хто перетворює `MoveResult.Events` у видовище:

```csharp
IEnumerator PlayEvents(MoveResult result) {
    foreach (var e in result.Events) {
        switch (e) {
            case MergeEvent m:  yield return _merge.Play(m);   break;
            case BurstEvent b:  yield return _burst.Play(b);   break;
            ...
        }
    }
}
```

Під час програвання інпут заблокований (`InputRouter.Locked`). Швидкість — з конфігу, тож `AnimationSpeed = 0` дає миттєвий режим для тестів і для «швидкого рестарту <300 мс».

**Пулінг обов'язковий з першого дня.** Жодного `Instantiate`/`Destroy` під час партії — тільки `CellPool`, `ParticlePool`, `SplashPool`. На мобільних GC-спайк = помітний фриз.

**Композиційний корінь** — `GameBootstrap` у сцені `Boot`: створює сервіси, реєструє їх у простому `ServiceLocator`, і далі всі залежності передаються **конструкторами/ін'єкцією полів у Awake**, а не пошуком через `FindObjectOfType`.

---

## 9. Шар UI і навігація

**Три сцени, не більше:**

| Сцена | Що містить |
|---|---|
| `Boot` | ініціалізація сервісів, завантаження збереження, перехід далі. Порожня візуально |
| `Meta` | хаб, карта рівнів, магазин, галактика, рейтинги, профіль (усі — префаби-екрани) |
| `Game` | поле, HUD, бос |

Екрани всередині `Meta` — **не сцени**, а префаби, якими керує `NavigationStack` (push/pop). Причина: перехід між вкладками має бути миттєвим, а завантаження сцени на слабкому Android — це 200–400 мс і чорний кадр.

```csharp
public abstract class ScreenBase : MonoBehaviour {
    public virtual void OnEnter(ScreenArgs args);
    public virtual void OnExit();
}
```

**Перегляд чужої галактики** — той самий `GalaxyScreen` з `GalaxyArgs { bool ReadOnly; PlayerId Owner; }`. Другого екрана не існує. У ReadOnly ховаються панель фарб і кнопки заливки.

**Safe area обов'язково:** `SafeAreaBinder` на кореневому `RectTransform` кожного екрана — інакше на iPhone з Dynamic Island і на Android з жестовою навігацією UI ріжеться. Тестувати на 19.5:9, 20:9 і 4:3 (планшети).

---

## 10. Шар Meta — економіка, галактика, збереження

```csharp
public sealed class Wallet         { long OilDrops; bool TrySpend(long); void Add(long, RewardSource); }
public sealed class PaintInventory { float GetLiters(PaintId); bool TryConsume(PaintId, float); }
public sealed class PaintService   { PaintResult PaintZone(PlanetId, ZoneId, PaintId); }
public sealed class DailyLimitTracker { int PlaysToday; float RewardMultiplier => PlaysToday < 10 ? 1f : 0.25f; }
```

Літри — `float` з округленням до 0.1 при показі; зона коштує 1–3 л (з `PlanetDefinition`).

### Збереження — з міграціями з першого дня

```csharp
public sealed class SaveFile {
    public int Version;              // ЗАВЖДИ перше поле
    public WalletData Wallet;
    public PaintsData Paints;
    public GalaxyData Galaxy;
    public ProgressData Progress;
    public SettingsData Settings;
}

public static class SaveMigrations {
    // v1 → v2 → v3 ... кожна міграція — окрема функція, покрита тестом
}
```

- Шлях: `Application.persistentDataPath` (працює однаково на iOS/Android).
- **Атомарний запис:** пишемо в `save.tmp` → `File.Replace` на `save.json`. Інакше вбитий застосунок під час запису = втрачений прогрес гравця. На iOS додатково виставити прапорець «не бекапити» для тимчасових файлів.
- Автозбереження: після кожної партії, покупки, фарбування зони і в `OnApplicationPause(true)` — на мобільних це єдиний надійний момент, `OnApplicationQuit` часто не викликається.
- Формат JSON (не BinaryFormatter — він заборонений і небезпечний). Легке обфускування — пізніше, разом із хмарним збереженням.

---

## 11. Шар Platform — абстракція над iOS/Android

Уся платформозалежність живе **тільки тут**. Геймплей і UI не містять жодного `#if UNITY_IOS`.

```csharp
public interface IHapticService     { void Light(); void Medium(); void Heavy(); void Chain(int depth); }
public interface IAnalyticsService  { void Track(string evt, IDictionary<string,object> props = null); }
public interface IAdsService        { bool IsRewardedReady { get; } void ShowRewarded(Action<bool> done); }
public interface IIapService        { Task<PurchaseResult> Buy(string productId); }
public interface IReviewService     { void RequestReview(); }
public interface INotificationService{ void Schedule(...); }
```

| Інтерфейс | Android | iOS | До релізу |
|---|---|---|---|
| Haptics | `Vibrator` + `VibrationEffect` (API 26+), амплітуда | `UIImpactFeedbackGenerator` / Core Haptics | `NullHaptics` |
| Analytics | UGS Analytics / Firebase | те саме | `LogAnalytics` (у консоль) |
| Ads | Unity Ads | Unity Ads + **ATT-запит** перед ініціалізацією | `NullAds` |
| IAP | Unity IAP (Google Play Billing) | Unity IAP (StoreKit) | `FakeIap` |
| Review | Google Play In-App Review | `SKStoreReviewController` | `NullReview` |

**Правило:** кожен інтерфейс має `Null*`-реалізацію. Гра повністю грабельна без жодного SDK — це і швидкість ітерацій, і страховка, якщо якийсь SDK ламає білд.

`Handheld.Vibrate()` **не використовувати**: на iOS це грубий «дзиж» замість тактильного відгуку, який нам потрібен для merge/burst.

---

## 12. Мобільна специфіка: продуктивність і сумісність

**Бюджет:** 60 fps на пристрої рівня iPhone SE 2 / Snapdragon 6-серії; ≤35 draw calls на екран; ≤150 МБ RAM; холодний старт до інтерактиву ≤3 с; **нуль GC-алокацій під час ходу**.

Як це тримати:
- Core працює зі `struct`-ами і масивами, без LINQ у гарячих шляхах і без `foreach` по `Dictionary` в апдейті.
- Події `MoveResult` — переиспользуемий буфер, не новий список щоходу.
- Спрайти крапель — один атлас, один матеріал; числа густоти — TextMeshPro з одним шрифтовим атласом (важливо: **кирилиця має бути в атласі**, інакше ловимо той самий warning про відсутній гліф).
- Частинки — пул, `maxParticles` обмежений, без Collision-модуля.
- `Application.targetFrameRate = 60`; на дуже слабких пристроях — `QualityLevel = Low` (менше частинок, вимкнений bloom).
- URP: вимкнути HDR і MSAA, якщо не потрібні; Bloom — тільки на середньому+ рівні якості.
- Стиснення текстур: **ASTC** для обох платформ.
- Роздільність: `Screen.SetResolution` з обмеженням по висоті для дуже щільних екранів (економія GPU без видимої втрати).

**Життєвий цикл застосунку** (частий баг на мобільних): `OnApplicationPause(true)` → пауза таймерів, збереження, стоп аудіо. `OnApplicationFocus` на Android спрацьовує інакше, ніж на iOS, — покладатись тільки на `Pause`.

---

## 13. Налаштування білдів

**Спільне:** IL2CPP, .NET Standard 2.1, Managed Stripping `Medium` (з `link.xml` для типів, які серіалізуються), portrait-only, Splash вимкнений (Unity Personal — залишити).

**Android**
- Формат: **AAB** для Play, APK для тестів на пристрої.
- Архітектури: **ARM64 only** (ARMv7 більше не потрібен і подвоює розмір).
- Min API: 24+ (перевірити актуальні вимоги Play на момент релізу).
- Target API: **той, якого вимагає Google Play на дату релізу** — вимога змінюється щороку, звірити перед білдом.
- Keystore — поза репозиторієм, шлях через змінні середовища.
- Data Safety form у Play Console.

**iOS**
- Min iOS: 13+ (звірити з вимогами App Store на дату релізу).
- **`PrivacyInfo.xcprivacy`** обов'язковий — оголосити використання `persistentDataPath`/UserDefaults та SDK-трекери.
- **ATT-запит** — тільки якщо реклама використовує IDFA; текст пояснення обов'язковий.
- Bitcode вимкнений (Apple його більше не вимагає), `Push Notifications` capability — лише коли реально додамо сповіщення.
- Експорт Xcode-проєкту скриптом (`PostProcessBuild`), щоб ручних кроків не було.

**Версіонування з першого тестового білду:** `versionName` (SemVer) + монотонний `versionCode`/`buildNumber`, який ніколи не зменшується. Автоінкремент у білд-скрипті.

---

## 14. Тестування

| Рівень | Де | Що покриває |
|---|---|---|
| EditMode, без Unity API | `InkFlow.Core.Tests` | усі формули §5, ланцюги, тупики, бос, RNG-детермінізм |
| EditMode | `InkFlow.Meta.Tests` | економіка, ліміти, міграції збережень |
| PlayMode | `InkFlow.Gameplay.Tests` | програвання подій, пули (нуль `Instantiate` під час партії), інпут |
| Headless CLI | `Tools/run-core-tests.sh` | ті самі NUnit-файли через dotnet, коли редактор відкритий |

**Обов'язкові тести-запобіжники** (кожен ловив реальний баг у цьому жанрі):
1. `PaintPower` ніколи не ≥ threshold (інакше самопідтримний ланцюг).
2. Щільне монохромне поле не дає нескінченний ланцюг (`MaxChainBursts`).
3. Той самий сід + та сама послідовність ходів = байт-в-байт той самий результат.
4. Endless ніколи не повертає стан «є порожні клітинки, але ходів немає».
5. Кожен `LevelDefinition` розв'язний (див. §15) — тест над усією Addressables-групою.
6. Міграція збереження з кожної попередньої версії відкриває файл без втрат.
7. Вибух у кутку не кидає виняток (вихід за межі сітки).

---

## 15. Симулятори (Фаза 4)

Два CLI/редакторні інструменти, які працюють **на реальному коді Core**, не на копії правил:

**`LevelSolver`** — пошук у ширину/A* по стану сітки:
- доводить, що рівень розв'язний у межах ходів;
- знаходить мінімальну кількість ходів → з неї виводяться пороги 2★/3★;
- будує звіт «складність» (розгалуженість дерева рішень).
*Нерозв'язний рівень не має права потрапити в збірку — це перевіряється в тесті, а не очима.*

**`EconomySimulator`** — 30 днів життя «середнього» гравця:
- скільки нафти зароблено/витрачено, коли відкривається кожен тір фарб;
- чи не впирається гравець у стіну, чи не тоне в надлишку;
- вихід — CSV + графік, ціни в `EconomyConfig` підбираються за ним, а не на око.

---

## 16. Git-workflow і `CLAUDE.md`

- Гілка на фазу: `phase-1-core`, `phase-2-feel`, ...; окремий коміт на кожен пункт фази, повідомлення — імперативом українською або англійською, послідовно.
- **Git LFS** для `.png`, `.wav`, `.psd`, `.fbx` — `.gitattributes` налаштувати до першого арту, інакше репозиторій розпухне назавжди.
- `.gitignore` — стандартний Unity + `/Builds`, `/Logs`, keystore, `*.xcodeproj` артефакти.
- **Force Text** серіалізація + Visible Meta Files — інакше мерджити сцени неможливо.
- `CLAUDE.md` у корені — джерело правди про **стан** проєкту: що зроблено, що наступне, відомі особливості середовища. Оновлювати в кінці кожної фази, не лишати знімком першого дня.

---

## 17. Відповідність фазам розробки

| Фаза | Що з цієї архітектури будується |
|---|---|
| **1. Ядро** | asmdef-структура, Core повністю (§4–6), `MoveResult`-події, пули, базовий `GridView`, свайп-інпут, 3 рівні, HUD, Core.Tests |
| **2. Feel** | `Feel/`-аніматори, частинки через пул, звук, `IHapticService` з реальними реалізаціями, прев'ю зони, near-miss, комбо, тряска |
| **3. Метагра** | `InkFlow.Meta` цілком, збереження з міграціями, `NavigationStack` і всі екрани, Addressables-групи, `FeatureFlags.SocialEnabled = false` |
| **4. Баланс** | `LevelSolver`, `EconomySimulator`, локальний лог подій економіки, підбір конфігів |
| **5. Реліз** | §13 цілком, `Null*`→реальні Platform-сервіси, приватність, аналітика, crash reporting, тести на реальних low-end пристроях |
| **6. Соціалка** | UGS (Auth, Cloud Save, Leaderboards, Friends), `ReadOnly`-галактика по мережі, процедурні галактики від сіда, лайв-опс |

---

## 18. Інваріанти, які не можна порушувати

Якщо якась зміна ламає щось із цього — змінюється рішення, а не інваріант.

1. **Core не знає про Unity.** Ніколи.
2. **Зона вибуху — завжди хрест із 4 клітинок**, незалежно від сили.
3. **`PaintPower ≤ threshold − 1`.** Вибух не може створити краплю, яка лопне без рішення гравця.
4. **Чужий колір розмивається, а не конвертується миттєво.**
5. **Єдина перевірка живості поля — `HasAnyMove()`.** Вибух не є умовою перемоги.
6. **Система не вбиває гравця сама:** у Endless завжди є fallback, у Puzzle кожен рівень доведено розв'язним. Помиляється гравець — не гра.
7. **У Puzzle рандом детермінований від сіда рівня.**
8. **Бос телеграфує дію за хід наперед**, завжди.
9. **Нуль `Instantiate`/`Destroy` під час партії.**
10. **Баланс живе в конфігах**, не в коді.
11. **Донат не впливає на проходження режимів** — жодного API, що дає перевагу за гроші.
12. **Збереження версіоноване й пишеться атомарно.**

---

## Швидкий старт для нової сесії Claude Code

1. Прочитай `ink-flow-master-doc.md` (ігрова правда) і цей файл (технічна правда).
2. Прочитай `CLAUDE.md` — там стан проєкту на зараз.
3. Звір інваріанти §18 перед будь-якою зміною в Core.
4. Нова механіка = спершу тест у `InkFlow.Core.Tests`, потім реалізація, потім вигляд.
5. Будь-яке нове число → в конфіг, не в код.
