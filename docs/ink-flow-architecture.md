# Ink Flow — Технічна архітектура (Unity, Android + iOS)

> **Для Claude Code.** Це технічна правда проєкту. Ігрова правда — в `docs/ink-flow-core-final.md`; якщо цей файл суперечить йому в питаннях геймдизайну, правий майстер-док. Якщо майстер-док суперечить цьому файлу в питаннях коду — правий цей.
>
> **Головний принцип архітектури:** ігрова логіка не знає, що існує Unity. Усе, що можна протестувати без редактора, тестується без редактора.
>
> Переписано 2026-09-24 після переробки ядра (Сесія 2). Старе ядро — краплі, густота, вибухи, бос — скасоване повністю; що від нього лишилось і чому, записано в `docs/implementation-notes.md` (Orphans).

---

## Зміст

1. Стек і платформи
2. Карта збірок (asmdef) і правила залежностей
3. Структура папок
4. Шар Core — чиста логіка
5. Правила механіки → код (точні формули)
6. Детермінізм і RNG
7. Дані та конфіги (data-driven)
8. Шар Gameplay і Style — обгортки конфігів і дизайн-токени
9. Шар UI і навігація
10. Шар Meta — економіка, галактика, колекція, збереження
11. Шар Platform — абстракція над iOS/Android
12. Мобільна специфіка: продуктивність і сумісність
13. Налаштування білдів (Android / iOS)
14. Тестування
15. Симулятори
16. Git-workflow і `CLAUDE.md`
17. Відповідність фазам розробки
18. Інваріанти, які не можна порушувати

---

## 1. Стек і платформи

| Пункт | Рішення |
|---|---|
| Движок | Unity 6 (6000.5.4f1), 2D URP |
| Мова | C# 9, nullable enabled (через `csc.rsp` у кожній збірці) |
| Цільові платформи | Android (Google Play), iOS (App Store) — **один код, нуль `#if` у геймплеї** |
| Скриптинг | IL2CPP, .NET Standard 2.1 (без `init`, без `record`) |
| Інпут | Unity Input System; перетягування фігур — `IPointerDownHandler`/`IDragHandler` на комірках лотка |
| Асети | Addressables лишились у проєкті, але новий контент (картинки) — звичайні асети, згенеровані редактором |
| Пули | префаб тримає фіксовану кількість об'єктів (64 блоки поля, 20 зон картинки); `Instantiate` під час партії — нуль |
| DI-фреймворк | **немає** (свідомо) — композиційний корінь вручну, `GameBootstrap` |
| DOTS/ECS | **немає** (свідомо) — поле 8×8, це десятки об'єктів |
| Бекенд | немає до Фази 6; далі Unity Gaming Services |

---

## 2. Карта збірок (asmdef) і правила залежностей

```
InkFlow.Core          ← ЖОДНИХ посилань, noEngineReferences: true
   ↑
InkFlow.Style         ← Core        (дизайн-токени; лист, який бачить лише UI)
InkFlow.Meta          ← Core        (економіка, галактика, колекція, збереження)
InkFlow.Gameplay      ← Core        (ScriptableObject-обгортки конфігів)
InkFlow.Platform      ← UnityEngine (інтерфейси сервісів + Null/Fake-реалізації)
InkFlow.UI            ← Core + Style + Meta + Platform   (НЕ бачить Gameplay і App)
InkFlow.App           ← усе вище    (композиційний корінь, ServiceLocator, DevPanel)
InkFlow.Editor        ← усе         (збирачі екранів, генератори спрайтів, EconomySimulator)

InkFlow.Core.Tests / InkFlow.Meta.Tests  ← EditMode; ті самі файли ганяє headless-раннер
Tools/InkFlow.Sim                        ← Core як джерела; бот-прогонник (dotnet)
```

**Жорсткі правила:**

1. **`InkFlow.Core` не посилається ні на що**, включно з `UnityEngine`. Замість `Vector2`/`Color`/`Random` — `GridPos`, `Pigment`/`Hue`/`Rgb`, `IRandomSource`. Перевірка — збірка, не смак.
2. **UI не бачить Gameplay.** Баланс і стан гравця приходять від композиційного кореня (`EndlessArgs.Balance`, `AppRouter.Configure/BindServices`). Екран не має права на `BalanceData.Default` у грі.
3. **Meta не знає про поле.** Забіг завершився → віддав `RunSummary` (очки, ланцюг, картинки за рідкістю). Meta не має уявлення, що таке фігура.
4. **Platform — тільки інтерфейси назовні.** Реалізацію знає лише `InkFlow.App`; UI отримує сервіси через `BindServices`, а не через локатор.
5. **Посилання asmdef — лише прямі, не транзитивні.** `Tools/asmdef-refs.py` дзеркалить це.
6. Залежності односторонні. Циклів немає. Якщо хочеться цикл — потрібна подія.

---

## 3. Структура папок

```
Assets/
  _Scripts/
    Core/            (asmdef InkFlow.Core)
      Model/         Board, Pigment, Hue, GridPos, PieceShape (+PieceDef), Rarity,
                     GameEvent (+GameEventType), MoveResult, InkColor, Rgb
      Rules/         PlacementRules, LineResolver, TrayGenerator, TankSet, Mixer, BoardGeometry
      Session/       RunSession, PictureProgress, PictureDeck, UnfinishedPicture (+PictureStart),
                     RunReplay, GameState, GameEvents, InputRouter
      Config/        BalanceData, PieceCatalogData, PictureDef (+ZoneDef), ThemeDef, PictureCatalogData
      Sim/           RunBot (+BotWeights)
      Random/        IRandomSource, XorShiftRandom
      Galaxy/        PaintKind, PlanetType (дані метагри, що потрібні Core-тестам)
    Style/           DesignSystem (усі кольори, радіуси, тривалості), StyleRefresh
    Gameplay/        Config/BalanceConfig, Config/EconomyConfig — обгортки для інспектора
    Meta/            Economy/ Galaxy/ Progress/ Collection/ Profile/ Rankings/ Shop/ Save/ PlayerState
    Platform/        PlatformServices (інтерфейси), Null/ (Null*, Fake*, LogAnalytics)
    UI/
      Level/         EndlessScreen, BoardView, BoardPulse, TrayView, PieceView, BoardFeedback,
                     PictureView, DropFlock, CompletionCard, SnapshotBlur, RarityFrame, RarityNames
      Paint/         PaintScreen, PlanetStage, ZoneMarker, PlacementMarker, PaintSwatch
      Hub/ Galaxy/ Shop/ Rankings/ Profile/ LevelMap/ Common/ Atoms/
    App/             GameBootstrap, ServiceLocator, DevPanel
    Editor/          Build*Screen, BuildMainScene, BuildUIKit, GenerateUISprites, RefreshPictureLibrary,
                     PictureViewBuilder (+K1Sprites), StyleSpriteImporter, RunScreenshots, UiBuilder,
                     InkFlowBootstrap, EconomySimulator
  _Pictures/         <тема>/<id>.txt — картинки (формат docs/pictures-format.md), 117 штук
  _ScriptableObjects/ Balance/ (BalanceConfig, EconomyConfig)  Style/ (DesignSystem)  Pictures/ (PictureLibrary)
  _Sprites/          UI/ (згенеровані спрайти)  K1Candy/ і P2Watercolor/ (копії еталонів docs/StyleRef)
  _Shaders/          InkFlowPlanet, InkFlowZone, InkFlowWatercolor
  _Prefabs/          UI/ (атоми)  Screens/ (корені екранів — з них складається Main.unity)
  Scenes/            Main.unity (єдина в Build Settings) + сцени-майстерні кожного екрана
  Tests/EditMode/    Core-тести в корені, Meta/ окремо
Tools/               run-core-tests.sh, check-compile.sh, check-*.py, CoreTestRunner/, InkFlow.Sim/, pictures/ (raster, author, contact_sheet), salvage/
docs/                ink-flow-core-final.md (ігрова правда), цей файл, implementation-notes.md, pictures-format.md, StyleRef/ (еталони вигляду), screenshots/
```

---

## 4. Шар Core — чиста логіка

Ключові типи (іменування — обов'язкове):

```csharp
public enum Pigment : byte { None, Blue, Red, Yellow }          // фарба фігур і баків
public enum Hue : byte { None, Blue, Red, Yellow, Green, Orange, Purple, Brown }   // що виходить зі змішувача
public readonly struct GridPos { int X, Y; }                    // X — стовпець, Y — рядок; поле знизу вгору,
                                                                // креслення картинки — згори вниз
public sealed class Board        { Pigment this[GridPos]; IsRowFull/IsColumnFull; CountEmpty; Clone; StateHash }
public sealed class PieceShape   { GridPos[] Cells (нормалізовані до початку); Width; Height; Weight }
public readonly struct PieceDef  { PieceShape Shape; Pigment Pigment; IsEmpty }
```

**`RunSession`** — забіг «Нескінченного»:

```csharp
public sealed class RunSession {
    RunSession(BalanceData, PieceCatalogData, IRandomSource, PictureCatalogData? = null, PictureStart? = null);
    Board Board; PieceDef[] TrayPieces; TankSet Tanks; Mixer Mixer; PictureProgress Picture;
    int Score, BestChain, PlacementCount, Round, LinesCleared, PureLinesCleared, PaintYielded, PaintMissed;
    int Splashes; IReadOnlyList<int> SplashesByHue; int PicturesCompleted; IReadOnlyList<int> PicturesCollected;
    bool StartedWithCarried; int CarriedIndex; int AttemptsLeft; int ContinuesUsed; bool CanContinue;
    Pigment WantedPigment;                       // з активної зони картинки — для першої фігури лотка
    MoveResult TryPlace(int trayIndex, GridPos anchor);
    MoveResult ContinueAfterLoss();              // §9: раз за забіг
    MoveResult CompletePictureNow();             // §9: донат
    void Restart(); bool TryFindHint(out int, out GridPos); bool NoPieceFits(); bool AnyPieceStuck();
    GameState State; bool IsOver; bool HaloWarning;
}
```

`MoveResult` — **повний опис того, що сталося**, як упорядкований список подій зі спільним буфером клітинок:

```
PiecePlaced → LineCleared (по одній, з вибіркою клітинок, пігментом, чистотою, фарбою) → ComboApplied
→ ScoreGained → PaintPoured (по одній на лінію) → [TankDrained ×≤3 → MixerFired → ZoneFilled… |
SplashMissed → PictureCompleted → PictureStarted] (повторюється на кожен виплеск)
→ TrayRefilled | TrayRescued → GameLost
```

**Це найважливіше архітектурне рішення проєкту.** Core не анімує — повертає стрічку; `BoardView.PlayEvents` програє поле, `EndlessScreen.PlayPaint` — баки, змішувач і картинку (виплеск летить у зону і мазок грає, коли долетів). Логіка тестується без кадру рендеру, а прогонник ганяє тисячу партій за секунду.

Решта Core:
- `TankSet` — три баки без стелі; `Mixer` — четвертий, спрацьовує при `Total ≥ MixerSplashSize`.
- `PictureProgress` — рівні зон поточної картинки; `PictureDeck` — витяг за рідкістю; `UnfinishedPicture` — правило §7.
- `TrayGenerator` — мішок; `PlacementRules` — влазить/лінії; `LineResolver` — вихід фарби; `BoardGeometry` — розмітка поля для в'ю в px макета.
- `RunBot` — жадібний бот на реальних правилах, лише для прогонів.

---

## 5. Правила механіки → код (точні формули)

Переклад майстер-доку в код. Кожен пункт — юніт-тест у `InkFlow.Core.Tests`. Усі числа — з `BalanceData` (дефолти в дужках).

### 5.1 Хід

```
TryPlace(i, anchor):
    фігура i не порожня; усі клітинки shape + anchor у межах поля й порожні
→ інакше Accepted = false, нічого не витрачено
клітинки фігури := її пігмент; комірка лотка порожніє; +ScorePerPlacedCell (10) за клітинку
```

Обертання фігур немає. Лоток — 3 фігури, поповнюється лише коли всі три поставлено.

### 5.2 Лінії й фарба

```
після розміщення: усі повні рядки (знизу вгору) і стовпці (зліва направо) — за станом ПІСЛЯ розміщення
вихід кожної лінії — за станом ДО очищення (клітинка на перетині рахується в обидві):
    чиста (один пігмент):  ⌊довжина ÷ MixedDivisor(2)⌋ × PureLineBonus(3) = 12 за вісімку → у свій бак
    мішана:                ⌊домінантних ÷ MixedDivisor⌋ → у бак домінантного;
                           нічия → бак, де фарби менше; далі — порядок Blue, Red, Yellow
очищення всіх ліній одночасно (перетин — один раз)
```

### 5.3 Ланцюг і очки

```
count = кількість ліній за хід; multiplier = ComboMultipliers[min(count, 3) − 1] = 1 / 1.5 / 2
фарба лінії  := ⌊вихід × multiplier⌋
очки за лінію := ⌊ScorePerLine(100) × (чиста ? PureLineScoreBonus(2) : 1) × multiplier⌋
BestChain = max(count)
```

### 5.4 Мішок фігур

```
tier = скільки порогів TierRounds(10/20/30) досяг номер лотка
вага форми = SizeWeight(size, tier) × (BagFitOffset(1) + скільки позицій на полі)^BagBias(0.5)
    SizeWeight: 2 клітинки 1.5 − 0.4·tier (≥0.3); 3 — 1.8; 4 — 0.7 + 1.1·tier; 5 — те саме, лише з tier ≥ 1
набір перегенеровується, доки хоч одна фігура не влазить: до 60 спроб, після 40 — стеля 3 клітинки,
далі 20 «рятувальних» двоклітинковими; наостанок — детермінований запобіжник (перша найменша, що влазить)
колір: серія одного пігменту з шансом ColorStreakByTier (0.5/0.35/0.2/0.1), інакше — зважено на
користь пігменту, якого на полі менше (ColorScarcityWeight 0.7); перша фігура — WantedPigment картинки
```

### 5.5 Змішувач

```
CanFire = Tanks.Total ≥ MixerSplashSize(8); спрацьовує, доки CanFire
забирає рівно виплеск, пропорційно до рівнів (метод найбільших остач: сума точна, бак не в мінус)
відтінок — з пропорції В БАКАХ до зливу (те, що показувало прев'ю):
    частка одного ≥ MixDominantShare(0.6)         → чистий цей
    інакше помітних (частка ≥ MixMinorShare 0.25): 3 → Brown; 2 → Secondary(a,b); 1 → чистий
Secondary: Blue+Yellow=Green, Red+Yellow=Orange, Blue+Red=Purple
```

### 5.6 Картинка

```
стеля зони = MixerSplashSize × clamp(round(клітинок ÷ CellsPerSplash(12)), 1, MaxSplashesPerZone(3))
виплеск відтінку H → перша незалита зона з відтінком H; залишок → наступна зона з H; далі — пропав (SplashMissed)
усі зони повні → PictureCompleted, PicturesCollected += індекс, PictureStarted(наступна)
наступна: кидок рідкості за RarityWeights (70/25/5), рівноймовірно серед цієї рідкості, крім щойно
закінченої; немає такої рідкості → звичайніша, потім рідкісніша
WantedPigment: чистий відтінок — його пігмент; вторинний — той із двох, якого в баках менше; Brown — найменший
```

### 5.7 Незавершена картинка (§7)

```
Track(index, filled)  — на кожне спрацювання змішувача: реєструє першу картинку з краплею фарби; чужу не бере
Complete(index)       — закриває
Settle(index, filled, wasCarried) — кінець забігу:
    немає незавершеної й є фарба → зареєструвати, AttemptsUsed = 0
    та сама й wasCarried → AttemptsUsed++; ≥ UnfinishedAttempts(3) → анулювати (true)
наступний забіг починається з неї: RunSession(…, PictureStart(index, filled, AttemptsLeft))
```

### 5.8 Програш і продовження

```
NoPieceFits() — ЄДИНА перевірка живості, після кожного ходу; State = Lost
HaloWarning = CountEmpty < HaloWarningFreeCells(20) || хоч одна фігура з руки нікуди не влазить
ContinueAfterLoss(): дозволено, поки ContinuesUsed < ContinuesPerRun(1); поле чисте, лоток новий,
                     рахунок/баки/картинка лишаються
Restart(): усе з нуля, нова картинка з колоди (незавершена — лише при новому старті екрана)
```

---

## 6. Детермінізм і RNG

**`UnityEngine.Random` заборонений у Core.** Тільки `IRandomSource` (`XorShiftRandom`).

| Що | Джерело сіда |
|---|---|
| Забіг | `DateTime.UtcNow.Ticks` при старті; `RunReplay` зберігає сід + ходи |
| Бот-прогони | `seed + i × 2654435761` на партію; шум бота — окремий сід |
| Тести | фіксований сід у `TestBoard.NewSession(seed)` |

Наслідок: партію можна відтворити, зберігши сід і список ходів (`RunReplay`) — баг-репорт «надішли сід» і майбутня античит-перевірка рекордів. Тест: той самий сід + ті самі ходи = байт-в-байт той самий стан (`Board.StateHash`).

---

## 7. Дані та конфіги (data-driven)

**Жодного балансного числа в коді.** Усі — у ScriptableObject-конфігах із POCO-дзеркалами в Core/Meta.

```
BalanceConfig.asset   → BalanceData:   поле, лоток, мішок (ваги, пороги, спроби), вихід ліній, ланцюг, очки,
                                        попередження, змішувач (виплеск, домінанта, помітність), картинка
                                        (клітинок на виплеск, стеля зони), незавершена (спроби), продовження,
                                        ваги рідкості
EconomyConfig.asset   → EconomyData:   стартове, нагорода за рівень (старий режим), денний ліміт,
                                        ScorePerOil, PictureRewards[3], InterstitialEveryRuns, RewardAdMultiplier
DesignSystem.asset    → DesignSystem:  усі кольори, радіуси, тривалості; CurrentTokenVersion піднімається,
                                        коли токени змінились, і Build UI Kit перезаписує асет
PictureCatalogData    → у Core, кодом: теми й креслення картинок (символи = зони). Це дані контенту,
                        а не баланс; одне джерело для правил, тестів і генератора масок
PictureArt.asset      → маски зон + центри й радіуси, згенеровані Generate Picture Art із креслень
```

**Правило:** зміна балансу = зміна `.asset`. Нова картинка = креслення в `PictureCatalogData` + `Generate Picture Art`.

---

## 8. Шар Gameplay і Style — обгортки конфігів і дизайн-токени

Від `InkFlow.Gameplay` лишились лише `BalanceConfig` і `EconomyConfig` — обгортки для інспектора, які `ToBalanceData()`/`ToEconomyData()` перетворюють на POCO. Ігрове поле живе в UI: партія — такий самий екран, як магазин чи профіль.

`InkFlow.Style` — `DesignSystem` (усі візуальні числа) і `StyleRefresh` (відкладене застосування з `OnValidate`/`OnEnable`). Палітри: `InkColor` (інтерфейс і фарби планет, не чіпати), `PigmentColor(Pigment)` (фігури й баки), `HueColor(Hue)` (виплески й зони картинок), `RarityColor(Rarity)`.

**Композиційний корінь** — `GameBootstrap` у `Main.unity`: читає збереження (`PlayerState` з колодою картинок і балансом), реєструє сервіси в `ServiceLocator` (у редакторі й dev-збірках — `FakeAds`), віддає роутеру стан і сервіси. Залежності передаються полями через `Wire()` у збирачах, не `FindObjectOfType`.

---

## 9. Шар UI і навігація

**Одна сцена `Main.unity`**, дев'ять екранів-префабів під одним `NavigationStack`; граф — в `AppRouter`. Кожен `Build*Screen` зберігає свій префаб у `_Prefabs/Screens/`, `Build Main Scene` складає з них застосунок і ставить сцену в Build Settings — тому вона завжди остання.

Екран партії (`EndlessScreen`) зверху вниз: шапка 40 → блок 156 (ліворуч картинка 150: плитка з назвою в колір рідкості, квадрат зон, «СПРОБ · N», підпис «ЗОНА · ВІДТІНОК»; праворуч капсули РАХУНОК/РЕКОРД над трьома баками й змішувачем) → поле 358 (64 блоки + 64 привиди) → лоток 86 (три комірки з фігурами) — усе в px макета × K (1080/390).

Оверлеї: картка перед забігом («ЦЬОГО ЗАБІГУ · тема», готова картинка, рідкість, тап або 2.6 с), картка фіналу у дві фази (продовжити/завершити → нагороди, галерея зібраного, чипи §9).

Правила в'ю (перевіряє `Tools/check-ui-animation.py`): щокадрова анімація — лише `localPosition/localScale/localRotation` і `CanvasRenderer`; заливка баків і зон — `localScale` та властивість матеріалу (`_Fill`), ніколи `sizeDelta` чи `Image.color` у циклі. Черга спрацювань змішувача не блокує поле.

`GameEvents` і `IGameCommands` живуть у **Core** — саме тому UI бачить стан партії, не посилаючись на Gameplay. `SafeAreaBinder` на корені кожного екрана.

---

## 10. Шар Meta — економіка, галактика, колекція, збереження

```csharp
public sealed class PlayerState {                  // рантайм — правда, файл — зліпок
    Wallet Wallet; PaintStock Paints; RewardCalculator Rewards; DailyLimitTracker DailyLimit;
    PictureCollection Collection; UnfinishedPicture Unfinished; PictureStart? RunStart;
    RunReward CompleteRun(in RunSummary, DateTime);  // очки → нафта × денний, картинки → нафта, рекорди, партія дня
    bool CollectPicture(string id, DateTime);        // одразу у файл; закриває незавершену
    void TrackUnfinished(int, IReadOnlyList<int>);   // на кожне спрацювання змішувача
    bool SettleUnfinished(int, IReadOnlyList<int>, bool wasCarried);
    long DoubleRunReward(long); long RewardPicture(Rarity); void RestoreUnfinishedAttempt(int, IReadOnlyList<int>);
    bool PlacePicture(planetId, pictureId, lon, lat); bool RemoveLastPlacement(planetId);
    long CompleteLevel(...); bool PaintZone(...);     // режим «Рівні» й фарбування планет — без змін
}
```

- **Колекція** — `id → (скільки разів, коли вперше)`; рекорд колекції = різних (§8) — це і є метрика «Колекція» в Рейтингах.
- **Розміщення** — список «планета + картинка + довгота/широта», кілька на планету; редактор — «Зняти останню».
- **Ідентифікатори у файлі — назви, не індекси** (картинки, фарби, планети): колода росте темами.

### Збереження — з міграціями з першого дня

`SaveFile.Version` — завжди перше поле; поточна **v4** (колекція, незавершена, розміщення, найдовший ланцюг, забіги). Кожна міграція — окрема функція в `SaveMigrations`, покрита тестом «з кожної попередньої версії відкривається без втрат». JSON, атомарний запис (`save.tmp` → `File.Replace`), `persistentDataPath`. Точки автозбереження: кінець забігу, кожна домальована картинка, кожне спрацювання змішувача (прогрес незавершеної), покупка, фарбування, розміщення, `OnApplicationPause(true)`.

---

## 11. Шар Platform — абстракція над iOS/Android

Уся платформозалежність — тільки тут. Геймплей і UI не містять `#if UNITY_IOS`.

```csharp
public interface IHapticService      { void Light(); void Medium(); void Heavy(); void Chain(int depth); }
public interface IAnalyticsService   { void Track(string evt, IDictionary<string,object>? props = null); }
public interface IAdsService         { bool IsRewardedReady; void ShowRewarded(Action<bool> done);
                                       bool IsInterstitialReady; void ShowInterstitial(Action done); }
public interface IIapService         { void Buy(string productId, Action<PurchaseResult> done); }
public interface IReviewService      { void RequestReview(); }
public interface INotificationService{ void Schedule(...); void CancelAll(); }
```

| Інтерфейс | Android | iOS | До релізу |
|---|---|---|---|
| Haptics | `Vibrator` + `VibrationEffect` | Core Haptics | `NullHaptics` |
| Analytics | UGS / Firebase | те саме | `LogAnalytics` |
| Ads | Unity Ads | Unity Ads + **ATT-запит** | `NullAds` (реліз) / `FakeAds` (редактор, dev) |
| IAP | Unity IAP | Unity IAP | `FakeIap` (відмовляє) |
| Review | In-App Review | `SKStoreReviewController` | `NullReview` |

Точки §9 у грі: продовжити після програшу (раз за забіг), подвоїти нафту, повернути анульовану рідкісну/легендарну, інтерстиціал раз на `InterstitialEveryRuns` забігів при виході з картки фіналу, донат `finish_picture`. Без реальних сервісів кнопки просто не з'являються — гра повністю грабельна без SDK.

---

## 12. Мобільна специфіка: продуктивність і сумісність

**Бюджет:** 60 fps на iPhone SE 2 / Snapdragon 6-серії; ≤35 draw calls на екран; ≤150 МБ RAM; холодний старт ≤3 с; **нуль GC-алокацій під час ходу**.

- Core працює з масивами й структурами; `MoveResult` — переиспользуемий буфер зі спільним списком клітинок, не новий список щоходу; бот і сесія не алокують у циклі ходу.
- Поле (K1Candy) — на клітинку чотири спрайти в чотирьох окремих шарах (лунка, гало, блок, блиск), порожня клітинка — вимкнені об'єкти; видимий стан — чиста модель `BoardVisual` (Core). Картинка — один `RawImage` із шейдером `InkFlow/Watercolor` (арт + маска 32×32, папір 512²), маска анімується `SetPixels32/Apply` з одного `LateUpdate`.
- Планети — шейдер `InkFlow/Planet` (дві октави шуму, обертання через `_Time`), зони планети — `InkFlow/Zone`; накладки картинок проєктуються стадією, як зони.
- Один атлас UI-спрайтів, один шрифт (Nunito з кирилицею; піктограми — спрайти, не гліфи).
- **Ніякого post-process Bloom.** ASTC для обох платформ. `targetFrameRate = 60`.
- `OnApplicationPause(true)` — єдиний надійний момент зберегтися.

---

## 13. Налаштування білдів

**Спільне:** IL2CPP, .NET Standard 2.1, Managed Stripping `Medium` (з `link.xml`), portrait-only, `Main.unity` — єдина сцена в Build Settings.

**Android:** AAB для Play, APK для тестів; ARM64 only; Min API 24+; Target API — вимога Play на дату релізу; keystore поза репозиторієм; Data Safety form.

**iOS:** Min iOS 13+; `PrivacyInfo.xcprivacy` обов'язковий; ATT-запит — лише якщо реклама використовує IDFA; експорт Xcode-проєкту скриптом.

**Версіонування з першого тестового білду:** SemVer + монотонний `versionCode`/`buildNumber`.

---

## 14. Тестування

| Рівень | Де | Що покриває |
|---|---|---|
| EditMode, без Unity API | `InkFlow.Core.Tests` | усі формули §5, мішок, змішувач, картинки, колода, незавершені, бот, геометрія, відсутність старого ядра |
| EditMode | `InkFlow.Meta.Tests` | економіка забігу, колекція, розміщення, ліміти, міграції, профіль, рейтинги |
| Headless CLI | `Tools/run-core-tests.sh` | ті самі NUnit-файли через dotnet, коли редактор відкритий — **260 тестів** |
| Бот-прогони | `Tools/InkFlow.Sim` | 1000 партій за секунду; цифри в `docs/implementation-notes.md` |

**Обов'язкові тести-запобіжники:**
1. Мішок ніколи не видає набір, у якому нічого не влазить, поки влазить двоклітинкова (тест + метрика «несправедливих смертей» у прогонах = 0).
2. Той самий сід + ті самі ходи = байт-в-байт той самий стан.
3. У Core немає жодного типу й події старого ядра (`OldCoreAbsenceTests`, рефлексією).
4. Колода тримає таблицю §6: звичайна 4–6 зон і один вторинний відтінок, рідкісна 8–12, легендарна 15+, одна легендарна на тему, квадратні креслення, кожна зона має клітинки.
5. Витяг за рідкістю тримає 70/25/5 на 20 000 витягів і не повторює щойно закінчену.
6. Незавершена: перший провал не витрачає спроби, три перенесені забіги анулюють, чужа картинка не реєструється, поки є ця.
7. Міграція збереження з кожної попередньої версії відкриває файл без втрат.
8. Продовження після програшу — рівно раз за забіг; донат «домалювати» не чіпає поле й рахунок.

Перед комітом: `bash Tools/check-compile.sh` (компілює response-файлами Unity), `python3 Tools/check-ui-animation.py`, `check-glyphs.py`, `check-navigation.py`.

---

## 15. Симулятори

**`Tools/InkFlow.Sim`** — CLI на реальному Core (компілює `Assets/_Scripts/Core/**/*.cs` як звичайний .NET-проєкт). Жадібний бот на один хід із шумом; три пресети (`--bot sloppy|default|careful`), довільні ваги (`--weights`), перенесення незавершених між послідовними забігами (типово ввімкнено, `--no-carry`). Виводить розміщення, лінії, частку чистих, виплески й їхні відтінки, картинки, зони, фарбу мимо, рідкість, врятовано/анульовано, «тиск», несправедливі смерті (код повернення 1, якщо є). Це нижня межа гравця: бот не планує колір через кілька лотків, і саме тому міри «мимо» в нього ~45–60 %.

**`EconomySimulator`** (редактор) — 30 днів життя гравця через `CompleteLevel`; на новий забіг (`CompleteRun`) ще не переведений.

`LevelSolver` старого ядра видалено разом із рівнями; режим «Рівні» — «Скоро».

---

## 16. Git-workflow і `CLAUDE.md`

- Гілка на фазу (зараз `core-v2`); окремий коміт на кожен крок §11 майстер-доку; імперативні повідомлення українською.
- **Git LFS** для `.png`, `.wav`, `.psd`, `.fbx`; Force Text + Visible Meta Files.
- `Assets/_Prefabs/Screens/GalaxyScreen.prefab` — автор править сам; у коміти сесії не входив.
- `CLAUDE.md` у корені — стан проєкту; `docs/implementation-notes.md` — журнал рішень, відступів, сиріт і цифр прогонів.

---

## 17. Відповідність фазам розробки

| Фаза | Стан |
|---|---|
| **1. Ядро** | ✅ переписано (Сесія 2, кроки 1–4): поле, мішок, лінії, баки, змішувач, картинки; стрічка подій; 8×8 |
| **2. Feel** | ✅ для нового ядра: привид, зрив ліній, «+фарба», струмені й виплеск змішувача, мазок і спалах картинки; звук/гаптика — через `BoardFeedback` |
| **3. Метагра** | ✅ прив'язано (крок 7): очки → нафта, колекція, розміщення, рекорд колекції в рейтингах; «Рівні» — «Скоро» |
| **4. Баланс** | ⚠️ цифри є (прогони кроків 1–6), але руками не грано; важелі записано в нотатках |
| **5. Реліз** | ⛔ Platform-реалізації, приватність, білди |
| **6. Соціалка** | ⛔ UGS, тижневі зрізи рейтингів |

---

## 18. Інваріанти, які не можна порушувати

Якщо якась зміна ламає щось із цього — змінюється рішення, а не інваріант.

1. **Core не знає про Unity.** Ніколи.
2. **Фігури не обертаються, лоток — три, поповнення — лише коли всі три поставлено.**
3. **Чиста лінія дає в рази більше фарби, ніж мішана** (`PureLineBonus` ≥ 2), і фарба йде лише в бак свого кольору.
4. **Змішувач спрацьовує сам** при накопиченні на виплеск; відтінок — лише з пропорції в баках (§4). Гравець нічого не тапає.
5. **Виплеск лягає лише в зону свого відтінку**; непотрібний відтінок нічого не малює.
6. **Система не вбиває гравця сама:** мішок ніколи не видає неможливий набір, поки влазить двоклітинкова; єдина перевірка живості — `NoPieceFits()`.
7. **Незавершена — одна одночасно, три перенесені забіги, гарантовано перша наступного забігу.**
8. **Гра показує картинку до першого ходу.**
9. **Нуль `Instantiate`/`Destroy` під час партії.**
10. **Баланс живе в конфігах**, контент — у кресленнях Core.
11. **Донат не впливає на проходження забігу** — «домалювати» не чіпає поле, рахунок і лоток.
12. **Збереження версіоноване й пишеться атомарно; ідентифікатори у файлі — назви.**

---

## Швидкий старт для нової сесії Claude Code

1. Прочитай `docs/ink-flow-core-final.md` (ігрова правда) і цей файл (технічна правда).
2. Прочитай `CLAUDE.md` — там стан проєкту й особливості середовища; `docs/implementation-notes.md` — чому саме так.
3. Звір інваріанти §18 перед будь-якою зміною в Core.
4. Нова механіка = спершу тест у `InkFlow.Core.Tests`, потім реалізація, потім вигляд; після — прогін бота.
5. Будь-яке нове число → в конфіг, не в код.
