# Ink Flow — Технічна архітектура (Unity, Android + iOS)

> **Для Claude Code.** Це технічна правда проєкту. Ігрова правда — в `docs/ink-flow-core-final.md`; якщо цей файл суперечить йому в питаннях геймдизайну, правий майстер-док. Якщо майстер-док суперечить цьому файлу в питаннях коду — правий цей.
>
> **Головний принцип архітектури:** ігрова логіка не знає, що існує Unity. Усе, що можна протестувати без редактора, тестується без редактора.
>
> Переписано 2026-09-24 після переробки ядра (Сесія 2); оновлено 2026-10-04 (Сесія 5). Старе ядро — краплі, густота, вибухи, бос — скасоване повністю; баків, змішувача, зон і спроб Сесії 2 теж більше немає — картинка піксельна, кроки заповнюються з ліній, забіг зберігається зліпком. Що лишилось сиротами і чому, записано в `docs/implementation-notes.md` (Orphans).

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
| Пули | префаб тримає фіксовану кількість об'єктів (64 блоки, 64 привиди й підсвітки поля; краплі в картинку — пул `DropFlock`); `Instantiate` під час партії — нуль |
| DI-фреймворк | **немає** (свідомо) — композиційний корінь вручну, `GameBootstrap` |
| DOTS/ECS | **немає** (свідомо) — поле 8×8, це десятки об'єктів |
| Бекенд | Unity Gaming Services за інтерфейсами Platform (Фаза 7: рейтинги й публічна вітрина); без нього гра працює повністю, рейтинги кажуть «не підключені» |

---

## 2. Карта збірок (asmdef) і правила залежностей

```
InkFlow.Core          ← ЖОДНИХ посилань, noEngineReferences: true
   ↑
InkFlow.Style         ← Core        (дизайн-токени; лист, який бачить лише UI)
InkFlow.Meta          ← Core        (економіка, галактика, колекція, збереження)
InkFlow.Gameplay      ← Core        (ScriptableObject-обгортки конфігів)
InkFlow.Platform      ← Core + UnityEngine (інтерфейси сервісів + Null/Fake-реалізації; DTO рейтингів — Core; UGS-адаптер за define INKFLOW_UGS)
InkFlow.UI            ← Core + Style + Meta + Platform   (НЕ бачить Gameplay і App)
InkFlow.App           ← усе вище    (композиційний корінь, ServiceLocator, DevPanel)
InkFlow.Editor        ← усе         (збирачі екранів, генератори спрайтів, EconomySimulator)

InkFlow.Core.Tests / InkFlow.Meta.Tests  ← EditMode; ті самі файли ганяє headless-раннер
InkFlow.UI.Tests                         ← UI + Editor (+ Core, Meta, Style, Platform, Gameplay); живий рендер — лише в Unity
Tools/InkFlow.Sim                        ← Core як джерела; бот-прогонник (dotnet)
```

**Жорсткі правила:**

1. **`InkFlow.Core` не посилається ні на що**, включно з `UnityEngine`. Замість `Vector2`/`Color`/`Random` — `GridPos`, `byte`-індекс майстер-палітри та `Rgb`, `IRandomSource`. Перевірка — збірка, не смак.
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
      Model/         Board, GridPos, PieceShape (+PieceDef), Rarity (+Rarities), GameEvent (+GameEventType), MoveResult, InkColor
      Rules/         PlacementRules, LineResolver, TrayGenerator, BoardGeometry, RunLayout, BoardVisual,
                     BoardDanger, BoardAlarmGlow, PictureCanvas, ScoreFormat
      Session/       RunSession, RunSnapshot, PictureProgress, PictureDeck, RunReplay, GameState, GameEvents, InputRouter
      Config/        BalanceData, PieceCatalogData, MasterPalette, PixelPicture, PictureLibrary (+ThemeNames)
      Sim/           RunBot (+BotWeights)
      Random/        IRandomSource, XorShiftRandom
      Galaxy/        PaintKind, PlanetType (дані метагри, що потрібні Core-тестам)
      Rgb            колір у даних без UnityEngine
    Style/           DesignSystem (усі кольори, радіуси, тривалості), PlanetPalette, PaintInfo, RgbExtensions
    Gameplay/        Config/BalanceConfig, EconomyConfig, GalaxyConfig, PictureLibraryAsset — обгортки для інспектора
    Meta/            PlayerState, Economy/ Collection/ Profile/ Rankings/ Shop/ Save/ Levels/ (сирота)
      Galaxy/        GalaxyLayout (+PlanetLayout), SlotLayout, PlanetSurface (+PlanetSlot), GalaxyProgress, PlayerId
      Progress/      GalaxyState (слоти), LevelProgress (сирота)
    Platform/        PlatformServices (інтерфейси), Null/ (Null*, Fake*, LogAnalytics), Ugs/ (UgsIdentity, UgsLeaderboards, UgsShowcase — лише з INKFLOW_UGS)
    UI/
      Level/         EndlessScreen, BoardView, BoardPulse, TrayView, PieceView, BoardFeedback,
                     PictureView, RarityHalo, RarityFrame, RarityNames, DropFlock, CompletionCard, SnapshotBlur
      Planet/        PlanetScreen (+PlanetArgs), PlanetStage, SlotMarker, SlotAtlas
      Collection/    CollectionScreen (+CollectionArgs), CollectionCard
      Common/        AppRouter, NavigationStack, ScreenBase, StyleRefresh, SafeAreaBinder, NestedScrollForwarder, …
      Hub/ Galaxy/ Shop/ Rankings/ Profile/ Levels/ Atoms/
    App/             GameBootstrap, ServiceLocator, DevPanel
    Editor/          Build*Screen, BuildMainScene, BuildUIKit, GenerateUISprites, GenerateFontAsset, RefreshPictureLibrary,
                     PictureViewBuilder (+A1Layers, K1Sprites), StyleSpriteImporter, ScreenRigBase → RunScreenRig,
                     MetaScreenRig<T>, RunScreenshots, MetaScreenshots, UiBuilder, InkFlowBootstrap, DevMenu, EconomySimulator
  _Pictures/         <тема>/<id>.txt — картинки (формат docs/pictures-format.md), 117 штук
  _ScriptableObjects/ Balance/ (BalanceConfig, EconomyConfig, GalaxyConfig)  Style/ (DesignSystem)  Pictures/ (PictureLibrary)
  _Sprites/          UI/ (згенеровані спрайти)  K1Candy/ (копії еталона docs/StyleRef; шари тривоги A1Breathe будує код)
  _Shaders/          InkFlowPlanet, InkFlowZone, InkFlowDarkCanvas
  _Prefabs/          UI/ (атоми)  Screens/ (корені екранів — з них складається Main.unity)
  Scenes/            Main.unity (єдина в Build Settings) + сцени-майстерні кожного екрана
  Tests/EditMode/    Core-тести в корені, Meta/ окремо, UI/ — InkFlow.UI.Tests (живий рендер, лише в Unity)
Tools/               run-core-tests.sh, check-compile.sh, check-*.py, asmdef-refs.py, CoreTestRunner/, InkFlow.Sim/,
                     pictures/ (raster, author, contact_sheet), fetch-fonts.sh, salvage/
docs/                ink-flow-core-final.md (ігрова правда), цей файл, implementation-notes.md, pictures-format.md,
                     StyleRef/ (еталони вигляду: K1Candy, W4DarkCanvas, A1Breathe), screenshots/
```

---

## 4. Шар Core — чиста логіка

Ключові типи (іменування — обов'язкове):

```csharp
public readonly struct GridPos   { int X, Y; }                 // X — стовпець, Y — рядок знизу вгору; сітка картинки у файлі — згори вниз
public sealed class Board        { byte this[GridPos]; IsEmpty; CountEmpty; CountOf(color); Clone; StateHash }  // колір — індекс майстер-палітри, 0 — порожньо
public sealed class PieceShape   { GridPos[] Cells (нормалізовані до початку); Width; Height; Weight }
public readonly struct PieceDef  { PieceShape Shape; byte Color; IsEmpty }
public sealed class PixelPicture { Id, Name, ThemeId, Rarity; Width, Height; this[x, y]; Outline; FillCount (кроків);
                                   StepColor/StepLength/StepPixel/StepOf; FamilyAt; RevealOrder; static Parse(text) }
public sealed class PictureLibrary { Count; this[int]; IndexOf(id); Find(id); Themes; CountOf(rarity) }
```

**`RunSession`** — забіг «Нескінченного»:

```csharp
public sealed class RunSession {
    RunSession(BalanceData, PieceCatalogData, IRandomSource, PictureLibrary? = null, Func<string,bool>? isCollected = null);
    RunSession(BalanceData, PieceCatalogData, PictureLibrary, RunSnapshot, Func<string,bool>? isCollected = null); // зі зліпка
    static bool CanRestore(RunSnapshot, PictureLibrary, PieceCatalogData, BalanceData);  void Capture(RunSnapshot into);
    Board Board; PieceDef[] TrayPieces; PictureProgress Picture; PictureLibrary Library;
    int Score, BestChain, PlacementCount, Round, LinesCleared, PureLinesCleared, PixelsFilled, PixelsWasted,
        PicturesCompleted, TrayRescues, ContinuesUsed;  IReadOnlyList<int> PicturesCollected;
    BoardDanger Danger;                          // None/Warn/Strong для пульсації (§11), з гістерезисом
    MoveResult TryPlace(int trayIndex, GridPos anchor);
    MoveResult ContinueAfterLoss();              // §10: раз за забіг
    MoveResult CompletePictureNow();             // §13: «домалювати одразу» за нафту
    void Restart(); bool TryFindHint(out int, out GridPos); bool NoPieceFits(); bool AnyPieceStuck();
    GameState State; bool IsOver; bool CanContinue;
}
```

`MoveResult` — **повний опис того, що сталося**, як упорядкований список подій зі спільним буфером клітинок:

```
PiecePlaced
→ [LineCleared (клітинки, головний колір, IsPure, кроків на клітинку) → PixelFilled… (крок, родина, клітинка-джерело)] по кожній лінії
→ ComboApplied → ScoreGained
→ (PictureCompleted → PictureStarted → BoardRecolored…/TrayRecolored… за рангами
   | BoardRecolored…/TrayRecolored…, коли родина закінчилась)
→ TrayRefilled | TrayRescued → GameLost
RunContinued — окремий хід (ContinueAfterLoss); CompletePictureNow дає PixelFilled… → PictureCompleted → PictureStarted → перефарбування
```

**Це найважливіше архітектурне рішення проєкту.** Core не анімує — повертає стрічку; `BoardView.PlayEvents` програє поле, `EndlessScreen` — краплі з клітинок у кроки картинки, хвилю перефарбування й картку завершення. Логіка тестується без кадру рендеру, а прогонник ганяє тисячу забігів за секунду.

Решта Core:
- `PictureProgress` — кроки поточної картинки: що заповнено, скільки лишилось на родину, `MostNeededColor` для перефарбування; `PictureDeck` — витяг (§7): невидані першими, за вагами рідкості, не та сама двічі поспіль.
- `TrayGenerator` — мішок; `PlacementRules` — влазить/лінії; `LineResolver` — кроки з лінії; `RunSnapshot` — зліпок забігу (§9).
- `BoardGeometry`/`RunLayout` — розмітка поля й екрана в px макета; `BoardVisual` — видимий стан поля, який в'юха лише дзеркалить; `ScoreFormat` — запис рахунку під колонку.
- `BoardDanger` — оцінка «мало місця» (§11); `BoardAlarmGlow` — формула шарів тривоги A1Breathe; `PictureCanvas` — покриття й гало полотна W4.
- `RunBot` — жадібний бот на реальних правилах, лише для прогонів.

---

## 5. Правила механіки → код (точні формули)

Переклад майстер-доку в код. Кожен пункт — юніт-тест у `InkFlow.Core.Tests`. Усі числа — з `BalanceData` (дефолти в дужках).

### 5.1 Хід

```
TryPlace(i, anchor):
    фігура i не порожня; усі клітинки shape + anchor у межах поля й порожні
→ інакше Accepted = false, нічого не витрачено
клітинки фігури := її колір (індекс палітри); комірка лотка порожніє; +ScorePerPlacedCell (10) за клітинку
```

Обертання фігур немає. Лоток — 3 фігури, поповнюється лише коли всі три поставлено.

### 5.2 Лінії й кроки картинки (§5)

```
після розміщення: усі повні рядки (знизу вгору) і стовпці (зліва направо) — за станом ПІСЛЯ розміщення
вихід кожної лінії — за станом ДО очищення (клітинка на перетині рахується в обидві):
    кожна клітинка родини X заповнює 1 крок родини X — наступний у порядку проявлення
    чиста (усі клітинки однієї родини): PureLineBonus (3) кроків на клітинку
    кроків понад потребу родини → PixelsWasted (згоріли)
очищення всіх ліній одночасно (перетин — один раз)
```

### 5.3 Ланцюг і очки

```
count = кількість ліній за хід; multiplier = ComboMultipliers[min(count, 3) − 1] = 1 / 1.5 / 2
очки за лінію := ⌊ScorePerLine (100) × (чиста ? PureLineScoreBonus (2) : 1) × multiplier⌋   — лише очки; кроки не множаться
BestChain = max(count)
```

### 5.4 Мішок фігур

```
tier = скільки порогів TierRounds (10/20/30) досяг номер лотка
вага форми = SizeWeight(size, tier) × (BagFitOffset (1) + скільки позицій на полі)^BagBias (0.5)
    SizeWeight: 2 клітинки 1.5 − 0.4·tier (≥ 0.3); 3 — 1.8; 4 — 0.7 + 1.1·tier; 5 — те саме, лише з tier ≥ FiveCellFromTier (1)
набір перегенеровується, доки хоч одна фігура не влазить: до MaxTrayAttempts (60), після TrayShrinkAfterAttempts (40) — стеля 3,
далі TrayRescueAttempts (20) двоклітинковими; наостанок — детермінований запобіжник (перша найменша, що влазить)
колір: лише родини поточної картинки, вага = скільки кроків родини ще лишилось (§5); прогресії кольорів немає — їх диктує картинка
```

### 5.5 Картинка, родини, перефарбування (§4–§5, §7)

```
картинка = PixelPicture з бібліотеки: сітка індексів палітри, контур (кроками не фарбується), родини (основний тон + світлий і тіньовий)
крок = step сусідніх пікселів однієї родини в порядку проявлення — обхід у ширину від нижнього центру заливки, спершу вгору;
       відокремлена контуром область — з найближчого до вже проявлених
родина закінчилась → клітинки й фігури цієї родини перефарбовуються в родину з найбільшим залишком (BoardRecolored/TrayRecolored)
усі кроки заповнені → PictureCompleted, PicturesCollected += індекс, PictureStarted(наступна):
    PictureDeck: невидані (не в колекції) першими; кидок рідкості за rarityWeights (45/25/15/9/5/1), рівноймовірно серед
    цієї рідкості, крім щойно закінченої; немає такої рідкості → звичайніша, потім рідкісніша
    поле й лоток перефарбовуються: старі родини за рангом кількості на полі ↔ нові за рангом кроків, що лишились
```

### 5.6 «Мало місця» (§11)

```
тісна форма = форма КАТАЛОГУ, в якої ≤ dangerTightFits (3) позицій на полі
Warn:   тісних ≥ dangerWarnShapes (8) або фігура з лотка нікуди не влазить; гасне лише при тісних ≤ dangerCalmShapes (3) — гістерезис
Strong: тісних ≥ dangerStrongShapes (18) або фігура з лотка застрягла
рівень тримає RunSession.Danger: оновлюється після ходу, рестарту, продовження й відновлення зліпка; після програшу — None
```

### 5.7 Програш, продовження, донат

```
NoPieceFits() — ЄДИНА перевірка живості, після кожного ходу; State = Lost
ContinueAfterLoss(): дозволено, поки ContinuesUsed < ContinuesPerRun (2: перший — за ролик, далі — за нафту, §13); поле чисте, лоток новий, рахунок і картинка лишаються
CompletePictureNow(): усі кроки заповнено, картинка зарахована, наступна витягнута, поле й лоток перефарбовано;
                      форми в лотку, зайнятість поля й рахунок не змінюються (§13 — донат не впливає на проходження)
Restart(): усе з нуля, нова картинка з колоди
```

### 5.8 Зліпок забігу (§9)

```
RunSnapshot: поле, лоток (назви форм + кольори), id картинки й заповнені кроки, лічильники, зібране за забіг
Capture() — на кожен лоток, паузу й вихід у хаб; CanRestore() звіряє бібліотеку й каталог; конструктор зі зліпка — байт у байт той самий стан
програш, фінал і рестарт стирають зліпок; продовження за ролик живе лише в сесії
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
BalanceConfig.asset   → BalanceData:   поле 8×8, лоток 3, мішок (ваги за розміром і tier, BagBias, спроби), PureLineBonus,
                                        ланцюг, очки, рідкість (rarityWeights, rarityGridSizes, rarityMin/MaxColors,
                                        rarityStepTargets ± stepTolerance), родини 4–6, небезпека (dangerTightFits /
                                        WarnShapes / CalmShapes / StrongShapes), ContinuesPerRun, HintIdleSeconds
EconomyConfig.asset   → EconomyData:   ScorePerOil, PictureRewards[6], FinishPictureCosts[6] + FinishPictureMinShare,
                                        ContinueCost, OilPacks[4] (id стору, кількість, бейдж; ціни — зі стору),
                                        InterstitialEveryRuns, RewardAdMultiplier; числа витрат підбирає
                                        EconomySimulation (Meta) — меню Ink Flow → Simulate → Economy (30 days);
                                        поля старого режиму «Рівні» (BaseLevelReward, EndlessMilestones…) — сироти
GalaxyConfig.asset    → GalaxyLayout:  планети по черзі (тип = ідентифікатор у файлі, назва, слоти 4 … 12, місяці,
                                        кільце, фінал) та імена циклів галактик; створюється Bootstrap Assets або
                                        Build Main Scene з дефолтами GalaxyLayout.Default
DesignSystem.asset    → DesignSystem:  усі кольори, радіуси, тривалості; CurrentTokenVersion (20) піднімається,
                                        коли токени змінились, і Build UI Kit перезаписує асет
PictureLibrary.asset  → PictureLibraryAsset (Gameplay): TextAsset[] з Assets/_Pictures/**/*.txt, збирає Refresh Picture Library;
                        GameBootstrap читає їх у Core PictureLibrary через PixelPicture.Parse
```

**Правило:** зміна балансу = зміна `.asset`. Нова картинка = блок `pic(...)` в `Tools/pictures/author.py` → `.txt` в `Assets/_Pictures` → `Refresh Picture Library` (формат — `docs/pictures-format.md`).

---

## 8. Шар Gameplay і Style — обгортки конфігів і дизайн-токени

Від `InkFlow.Gameplay` лишились `BalanceConfig`, `EconomyConfig` і `PictureLibraryAsset` — обгортки для інспектора, які `ToBalanceData()`/`ToEconomyData()`/`ToLibrary()` перетворюють на POCO Core. Ігрове поле живе в UI: партія — такий самий екран, як магазин чи профіль.

`InkFlow.Style` — `DesignSystem` (усі візуальні числа), `PlanetPalette`, `PaintInfo`, `RgbExtensions` (`Rgb.ToColor()` на межі Core → в'юхи). Відкладене застосування токенів з `OnValidate`/`OnEnable` — `StyleRefresh` у `UI/Common`. Палітри: `InkColor` (інтерфейс і фарби планет, не чіпати), `RarityColor(Rarity)` (шість рідкостей), `BlockTint` для кольорів родин картинки (індекси майстер-палітри Core); `Paint(PaintKind)` і `PlanetPalette` — метагра до Фаз 3–4.

**Композиційний корінь** — `GameBootstrap` у `Main.unity`: читає збереження (`PlayerState` з бібліотекою картинок і балансом), реєструє сервіси в `ServiceLocator` (у редакторі й dev-збірках — `FakeAds`), віддає роутеру стан і сервіси. Залежності передаються полями через `Wire()` у збирачах, не `FindObjectOfType`.

---

## 9. Шар UI і навігація

**Одна сцена `Main.unity`**, одинадцять екранів-префабів під одним `NavigationStack`; граф — в `AppRouter`. Шестерня (§15) на кожному екрані — подія `ScreenBase.SettingsRequested`, роутер підписує всі екрани циклом і відкриває `SettingsScreen` з `SettingsArgs(InRun)`. Кожен `Build*Screen` зберігає свій префаб у `_Prefabs/Screens/`, `Build Main Scene` складає з них застосунок і ставить сцену в Build Settings — тому вона завжди остання.

Екран забігу (`EndlessScreen`) зверху вниз, px макета (`RunLayout`, Core): шапка 40 → блок 186: по центру лише картинка на полотні W4DarkCanvas (сторона `runPictureSide` 154 + запас під гало рідкості), праворуч колонка РАХУНОК/РЕКОРД 56–92 → поле 374 (панель на всю ширину мінус бічне поле 8; 64 блоки + 64 привиди) → лоток 86 при низу safe area — усе × K (1080/390). Бракує висоти — стискається спершу блок картинки (до 108), поле й лоток ніколи.

Оверлеї: картка перед забігом («ЦЬОГО ЗАБІГУ · тема» або «ПРОДОВЖЕННЯ · тема» зі зліпка; тап або таймер), картка завершення картинки (знімок екрана розмито, свайп праворуч — у колекцію, ліворуч — геть), картка фіналу у дві фази (поки можна продовжити й ролик готовий — «Продовжити за ролик»/«Завершити»; у фіналі — нагороди, «Подвоїти за ролик», «Домалювати одразу · N нафти», «Ще раз»).

Налаштування (§15): `SettingsScreen` — перемикачі Звук/Музика/Вібрація (`PlayerState.Set…` → файл і `SettingsChanged`; споживачі — `GameAudio` і `SettingsGatedHaptics`), «Додому» (з забігу — `ConfirmPrompt`, далі `SetRoot(hub)`), «Заново» лише в забігу (роутер: `ClearRun` + `EndlessScreen.DiscardSuspendedRun` + `Pop`; схований «Заново» не лишає дірки — `LayoutActions`). Шестерня в забігу не перезапускає забіг: екран лишається в стеку під налаштуваннями, `OnExit` пише зліпок і ставить `_suspended`, `OnEnter` повертає ту саму сесію без картки перед забігом; `SetRoot(hub)` з «Додому» — другий `OnExit` знімає призупинення, наступний вхід іде зі зліпка з карткою «ПРОДОВЖЕННЯ» (`RunSuspendTests`), «Колекція», приховати профіль, мова лише з ≥ 2 локалями, політика й підтримка з `AppConfig` («скоро», поки порожньо), версія.

Профіль (§14): `ProfileScreen` — візитка (крапля-аватар якорем зверху з півотом по центру, олівець → `NickPrompt` із правилами `NickRules` від роутера: підказка про межі, рядок помилки замість закриття; нік одним рядком з авторозміром), ряд аватарів `AvatarSet` (прості градієнтні кола, тап → `SetAvatar`, обрана обведена), вітринна картинка (`PictureView.ShowCompleted` лише коли картинка змінилась; порожньо — рамка зі знаком питання; підпис каже, обрана чи автоматична; «Обрати з колекції» → `CollectionArgs.ForShowcase()` → `SetShowcasePicture` + `Pop`). Драбини звань, статистики, палітри, бейджів і вітрини планет немає — ні на екрані, ні в коді.

Рейтинги (§16): `RankingsScreen` — лише «Світ», сегменти «Планети»/«Галактики», таби періоду; запит сторінки в `ILeaderboardService` на вхід і на зміну фільтра (застарілі відповіді відкидаються за номером запиту); блок стану замість подіуму, поки таблиці немає («Завантажую…», «Немає з'єднання», «Рейтинги ще не підключені», «Таблиця не відповіла»); подіум топ-3, віртуалізований список, картка «Ти» зі справжніми числами (`RankPlayer.You`; місце «#—», поки сервер не відповів). Тап по гравцю → `IShowcaseService.Fetch` → `GalaxyArgs.ForVisitor(showcase)` → той самий `GalaxyScreen` у перегляді: планети з вітрини (`GalaxyProgress.FromShowcase`), картка гостя (аватар, нік або «Гравець-інкогніто», вітринна картинка), без кнопки й пагінації; інкогніто не відкривається.

Галактика (§12): `GalaxyScreen` (карусель планет поточного циклу, «Відкрити») → `PlanetScreen` (слоти на кулі: порожній → `CollectionScreen` у режимі вибору → `AppRouter.TryPlaceInSlot` + `Pop`; зайнятий → «Замінити / Повернути»; усі зайняті → «планета ожила», остання — «галактика завершена») → колекція — окремий екран із віртуалізованою сіткою й фільтрами. Слоти й картки малюються з одного атласу (`SlotAtlas`) чотирма шарами — бюджет викликів тримає `MetaScreenRig.EstimateBatches`.

Правила в'ю (перевіряє `Tools/check-ui-animation.py`): щокадрова анімація — лише `localPosition/localScale/localRotation` і `CanvasRenderer`; маска й гало картинки — `SetPixels32/Apply` з одного `LateUpdate`; тряска поля рухає окремий вузол `Board/Shake`; ніколи `sizeDelta`, `anchoredPosition` чи `Image.color` у циклі.

`GameEvents` і `IGameCommands` живуть у **Core** — саме тому UI бачить стан партії, не посилаючись на Gameplay. `SafeAreaBinder` на корені кожного екрана.

---

## 10. Шар Meta — економіка, галактика, колекція, збереження

```csharp
public sealed class PlayerState {                  // рантайм — правда, файл — зліпок
    Wallet Wallet; RewardCalculator Rewards; DailyLimitTracker DailyLimit;
    PictureCollection Collection; PictureLibrary Library; BalanceData Balance; RunSnapshot? SavedRun;
    RunReward CompleteRun(in RunSummary, DateTime);  // очки → нафта × денний, картинки → нафта за рідкістю, рекорди, партія дня
    long DoubleRunReward(long); long RewardPicture(Rarity);
    bool CollectPicture(string id, DateTime);        // одразу у файл
    void SaveRun(RunSession); void ClearRun();       // зліпок забігу (§9)
    long FinishPictureCost(Rarity, float remaining); bool TryFinishPicture(Rarity, float remaining);   // §13, за нафту
    long ContinueCost; bool TryContinueRun();        // §13: продовжити після програшу за нафту
    // чим продовжувати — ContinueOffer.Decide(continuesUsed, continuesPerRun, adReady, hasWallet) → None/Ad/Oil (Meta, без Unity)
    IReadOnlyList<OilPack> OilPacks; void GrantPurchasedOil(OilPack);   // §13: магазин — стор підтвердив, нафта у файл
    SettingsData Settings; event SettingsChanged; void SetSound/SetMusic/SetVibration/SetProfileHidden(bool);   // §15, одразу у файл
    int AvatarId; void SetAvatar(int); bool SetNick(raw, NickRules, out NickVerdict);                 // §14, одразу у файл; відмова лишає старий нік
    string? ShowcasePictureId; bool SetShowcasePicture(id); event ProfileChanged;                    // обрана, поки в колекції; інакше остання зібрана
    bool EnforceNickRules(NickRules); bool IsShowcaseChosen;                                        // старий нік поза правилами → типовий; вітрина обрана чи автоматична
    event GalaxyChanged; int PlanetsDone; int GalaxiesDone;                                          // §16: слоти змінились; лічильники з усіх циклів
    long RankValue(RankMetric, RankPeriod, DateTime); bool RollOverWeek(DateTime);                   // «цей тиждень» — приріст від бази (WeekBaseline, файл RankWeek v10)
    int ShowcaseGalaxy; PublicShowcase BuildShowcase(playerId, DateTime);                            // у хмару йде лише це; прихований профіль — лише прапорець і лічильники
    // NickRules(min, max, bannedWords) з AppConfig: Normalize (очищення) / Check / Fold (транслітерація, цифри-двійники); корінь — з початку слова
    // AvatarSet — по краплі на колір чорнила; RankingRow.AvatarColor — один колір аватара для рядка й картки «Ти»
    bool ShouldShowInterstitial;
    GalaxyLayout Layout; int CurrentGalaxy; int FreeCopies(pictureId); bool CanEditPlanet(galaxy, planetId);   // §12
    bool TryPlaceInSlot(galaxy, planetId, slot, pictureId, DateTime); bool ClearSlot(galaxy, planetId, slot);
    long CompleteLevel(...);                         // режим «Рівні» — сирота
    void Persist();
}
```

- **Колекція** — `id → (скільки разів, коли вперше)`; рекорд колекції = різних (§8). У рейтингах метрики «Колекція» немає (§16).
- **Слоти планет (§12)** — список «галактика + планета + слот + картинка + коли» (`GalaxyData.Slots`); одна зібрана копія — один слот; стани планет і галактик — похідні (`GalaxyState`, `GalaxyProgress.FromSave`). Планета відкрита, якщо вона перша, одразу за ожилою або з картинками (`GalaxyState.IsPlanetOpen`). Записи, яких розкладка не адресує (слоти чи планети, яких уже немає в `GalaxyConfig`), у файлі лишаються, але в прогресі й зайнятих копіях не рахуються (`IsAddressed`): усе, що каже «скільки зайнято», бере розкладку.
- **Ідентифікатори у файлі — назви, не індекси** (картинки, планети): бібліотека росте темами.
- **Нафта витрачається лише на дві дії** (§13): «домалювати одразу» і «продовжити після програшу»; магазин продає лише нафту, ціна — зі стору (`IIapService.Query`), у грі немає жодного числа-ціни.

### Збереження — з міграціями з першого дня

`SaveFile.Version` — завжди перше поле; поточна **v9** (v5 — піксельні картинки й колекція за id, v6 — зліпок забігу `RunSnapshot`, v7 — кроки у зліпку, v8 — слоти планет: розміщення → слоти, літри → нафта, зони — геть; v9 — полів фарби у файлі більше немає). Старі поля для кроку v7→v8 живуть у `Meta/Save/LegacySave.cs` (`Legacy.LegacyPaintSave`): сховище парсить файл до v8 удруге в цю форму й передає `Migrate(save, context, legacy)`; `MigrationContext` несе розкладку галактики з конфігу. Кожна міграція — окрема функція в `SaveMigrations`, покрита тестом «з кожної попередньої версії відкривається без втрат». JSON, атомарний запис (`save.tmp` → `File.Replace`), `persistentDataPath`. Нечитабельний файл не стирається: `JsonSaveStorage` відкладає його в `save.json.failed-<UTC>` копією, а якщо файл не читається навіть для копіювання — перейменуванням; не вдалось і це — сховище не пише поверх оригіналу (`LoadFailedWithoutBackup`). Точки автозбереження: кінець забігу (`CompleteRun`), кожна зібрана картинка (`CollectPicture`), зліпок на кожен лоток, паузу й вихід у хаб (`SaveRun`), покупка, фарбування, розміщення, `OnApplicationPause(true)`.

---

## 11. Шар Platform — абстракція над iOS/Android

Уся платформозалежність — тільки тут. Геймплей і UI не містять `#if UNITY_IOS`.

```csharp
public interface IHapticService      { void Light(); void Medium(); void Heavy(); void Chain(int depth); }
public interface IAnalyticsService   { void Track(string evt, IDictionary<string,object>? props = null); }
public interface IAdsService         { bool IsRewardedReady; void ShowRewarded(Action<bool> done);
                                       bool IsInterstitialReady; void ShowInterstitial(Action done); }
public interface IIapService         { bool IsAvailable; void Query(IReadOnlyList<string> ids, Action<IReadOnlyList<StoreProduct>> done);
                                       void Buy(string productId, Action<PurchaseResult> done); }   // StoreProduct: id + локалізована ціна рядком
public interface IReviewService      { void RequestReview(); }
public interface INotificationService{ void Schedule(...); void CancelAll(); }
// §16 — рейтинги; DTO (RankMetric, RankPeriod, LeaderboardPage/Entry, LeaderboardStatus, PublicShowcase, MockRankings) живуть у Core
public interface IIdentityService    { bool IsSignedIn; string PlayerId; void SignIn(Action<bool> done); }
public interface ILeaderboardService { bool IsAvailable; void Fetch(RankMetric, RankPeriod, int limit, Action<LeaderboardPage> done);
                                       void Submit(RankMetric, RankPeriod, long value, Action<bool>? done = null); }   // тиждень — приріст, за весь час — лічильник
public interface IShowcaseService    { bool IsAvailable; void Publish(PublicShowcase, Action<bool>? done = null);
                                       void Fetch(string playerId, Action<PublicShowcase?> done); }
```

| Інтерфейс | Android | iOS | До релізу |
|---|---|---|---|
| Haptics | `Vibrator` + `VibrationEffect` | Core Haptics | `NullHaptics` |
| Analytics | UGS / Firebase | те саме | `LogAnalytics` |
| Ads | Unity Ads | Unity Ads + **ATT-запит** | `NullAds` (реліз) / `FakeAds` (редактор, dev) |
| IAP | Unity IAP | Unity IAP | `FakeIap` (редактор, dev: ціни §13 рядками, покупка вдається) / `NullIap` (реліз без SDK: цін немає, кнопки сплять) |
| Review | In-App Review | `SKStoreReviewController` | `NullReview` |
| Identity | UGS Authentication (анонімно) | те саме | `FakeIdentity` (редактор, dev) / `NullIdentity` (реліз без UGS) |
| Leaderboards | UGS Leaderboards: `planets_week`/`planets_all`/`galaxies_week`/`galaxies_all` (ідентифікатори в `AppConfig`; тижневі — зі скиданням щопонеділка 00:00 UTC; метадані нік/аватар/інкогніто) | те саме | `FakeLeaderboards` (таблиця макета, `Offline`) / `NullLeaderboards` («не підключені») |
| Showcase | UGS Cloud Save, ключ `showcase` з публічним доступом | те саме | `FakeShowcase` (у пам'яті) / `NullShowcase` |

Синхронізація (§16): `App/RankingsSync` (без Unity, під headless-тестом) — події стану `GalaxyChanged`/`ProfileChanged`/`SettingsChanged` лише ставлять прапорець; `Flush` (раз на кадр із бутстрапа) будує `PlayerState.BuildShowcase`, порівнює з останньою опублікованою й шле лише зміни: вітрину — коли змінилась, кожне з чотирьох значень (дві метрики × два періоди; тиждень — приріст, за весь час — лічильник) — коли змінилось. Вхід повторюється, доки не вдасться (офлайн-старт, повернення в застосунок). Локальний файл — правда; прихований профіль публікує лише прапорець і лічильники — і у вітрину, і в метадані таблиць. Екран рейтингів читає хмарний id гравця з `IIdentityService` у момент запиту, а не знімком. Null/Fake рейтингових сервісів — `Platform/Null/RankingsServices.cs` без UnityEngine; UGS-адаптер компілюється лише з define `INKFLOW_UGS` і пакетами `com.unity.services.{core,authentication,leaderboards,cloudsave}`.

Точки §17 у грі: продовжити після програшу за ролик (перший раз за забіг; далі — за нафту, §13), подвоїти нафту за ролик, інтерстиціал раз на `InterstitialEveryRuns` забігів при виході з картки фіналу. «Домалювати одразу» — за нафту (§13). Магазин (§13) — чотири пакети нафти через `IIapService.Query`/`Buy`: ціна на кнопці — рядок стору. Без реальних сервісів кнопки роликів не з'являються, а магазин показує пакети з неактивними кнопками — гра повністю грабельна без SDK.

---

## 12. Мобільна специфіка: продуктивність і сумісність

**Бюджет:** 60 fps на iPhone SE 2 / Snapdragon 6-серії; ≤35 draw calls на екран; ≤150 МБ RAM; холодний старт ≤3 с; **нуль GC-алокацій під час ходу**.

- Core працює з масивами й структурами; `MoveResult` — переиспользуемий буфер зі спільним списком клітинок, не новий список щоходу; бот і сесія не алокують у циклі ходу.
- Поле (K1Candy) — на клітинку чотири спрайти в чотирьох окремих шарах (лунка, гало, блок, блиск), порожня клітинка — вимкнені об'єкти; видимий стан — чиста модель `BoardVisual` (Core). Слоти планети — так само чотири шари (плями, панелі, рамки, пікселі) і один атлас `SlotAtlas` на всі картинки екрана (буфер пікселів живе з атласом; колекція збирає атлас раз на вхід, фільтр лише вибирає клітинки); порожні плями — два спільні матеріали, які тримає й нищить `PlanetStage`. Вхід на екран — один `Apply()` (`ScreenBase.Entering` глушить `OnEnable` під час `OnEnter`). `MetaScreenRig.EstimateBatches` рахує весь канвас стенда разом із фоном; `runs` — оцінка в порядку обходу, не точна межа. Картинка — один `RawImage` із шейдером `InkFlow/DarkCanvas` (арт і маска розміру арту, гало на сітці полотна; покриття й гало рахує Core `PictureCanvas`), маска й гало оновлюються `SetPixels32/Apply` з одного `LateUpdate`.
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
| EditMode, без Unity API | `InkFlow.Core.Tests` | формули §5, мішок, лінії й кроки, картинки й уся бібліотека (117 файлів), колода, зліпок, небезпека, геометрія й розкладка, формат рахунку, видимий стан поля, покриття й гало полотна, формула тривоги, бот, відсутність старого ядра |
| EditMode | `InkFlow.Meta.Tests` | економіка забігу й магазину, симуляція економіки, колекція, слоти, ліміти, міграції v1→v10, профіль (аватар, правила ніка, вітрина), рейтинги (сторінка, мок, база тижня, публічна вітрина), відсутність економіки фарби, налаштування й посилання |
| Headless CLI | `Tools/run-core-tests.sh` | ті самі NUnit-файли через dotnet, коли редактор відкритий — **355 тестів** (разом із `InkFlow.Services.Tests`: Fake-сервіси рейтингів і `RankingsSync`) |
| EditMode, живий рендер | `InkFlow.UI.Tests` (`Assets/Tests/EditMode/UI`) | **55 тестів** на зібраних префабах через стенди `RunScreenRig` і `MetaScreenRig<T>`: розкладка поля в усіх сценаріях і 4 пристроях, тривога (видимість, порядок шарів, формула = PNG еталона, Strong сильніша за Warn), рендер W4 піксель у піксель, картинка над полем і гало рідкості; планета зі слотами (порожня, половина, фінальна з 12 картинками — бюджет викликів малювання, завершена галактика без листа, невідома картинка як зайнятий слот), колекція (лише зібране, фільтр рідкості без перебудови атласу, притлумлення без вільних копій), галактика (режим перегляду без кнопок, «Відкрити» з фокуса, завершені цикли стрілками), `GalaxyConfig` = дефолтна розкладка, магазин (ціна — рядок стору, покупка через стор зараховує пакет, без стору кнопки сплять), налаштування (перемикачі пишуть у стан, «Заново» лише в забігу, мова лише з двома локалями, «скоро» без адреси, гаптика за перемикачем мовчить). Лише в Unity: редактор закритий або batch на копії проєкту |
| Бот-прогони | `Tools/InkFlow.Sim` | 1000 забігів за секунду; цифри в `docs/implementation-notes.md` |

**Обов'язкові тести-запобіжники:**
1. Мішок ніколи не видає набір, у якому нічого не влазить, поки влазить двоклітинкова (тест + метрика «несправедливих смертей» у прогонах = 0).
2. Той самий сід + ті самі ходи = байт-в-байт той самий стан.
3. У Core немає жодного типу й події старого ядра — ні крапель, ні баків зі змішувачем (`OldCoreAbsenceTests`, рефлексією).
4. Бібліотека тримає таблицю §6 (`PictureLibraryTests`): сітка, тони 10–16, родини 4–6, кроки в межах цілі ± 35 %; майстер-палітра — контраст на фоні поля.
5. Витяг за рідкістю тримає ваги (45/25/15/9/5/1), віддає невидані першими й не повторює щойно закінчену.
6. Зліпок забігу відновлює сесію байт у байт (`RunSnapshotTests`).
7. Міграція збереження з кожної попередньої версії відкриває файл без втрат.
8. Продовження після програшу — рівно раз за забіг; «домалювати» не чіпає поле, лоток і рахунок.
9. У порожній клітинці поля немає активної графіки (`BoardVisualTests`).
10. Готова картинка покриває рівно свій арт, не більше й не менше (`PictureCanvasTests` на всій бібліотеці).
11. Самоперевірка розкладки бачить навмисно зсунуте полотно (`BoardLayoutTests.ShiftedCanvas_IsReportedAsAFault`).
12. Одна зібрана копія — один слот; планета оживає лише коли зайняті всі слоти, редагувати можна лише поточну галактику й відкриті планети (`PlayerStateTests`, `PersistenceTests`, `GalaxyProgressTests`).
13. Міграція v7→v8 не губить колекцію, нафту й рекорд: розміщення стають слотами по черзі (копій у слотах не більше, ніж зібрано), літри — нафтою за курсом конфігу.
14. Відкрита порожня планета не замикається, коли з попередньої забирають картинку; записи поза розкладкою (зменшені слоти, прибрана планета) не рахуються в прогрес і не тримають копій; знімати картинки із завершеної галактики не можна (`GalaxyProgressTests`, `PlayerStateTests`).
15. Від економіки фарби не лишилось нічого: у Core/Meta немає типів і членів про фарбу, літри, мензурки, набори; у файлі — старих полів; у коді гри — файлів магазину фарб (`PaintEconomyAbsenceTests`; виняток — `Legacy` для міграції).
16. Пропорції економіки (`EconomySimulationTests`): епічна наполовину — 1.5–4 забіги доходу, продовжити — 0.3–1 забігу, найменший пакет — 4–14 забігів; гравець може дозволити собі більшість бажаних домальовувань і не тоне в нафті.
17. Перемикачі §15 зберігаються одразу й повідомляють споживачів; те саме значення — ні запису, ні події; гаптика за вимкненою «Вібрацією» не доходить до платформи (`SettingsTests`, `SettingsScreenTests`).

Перед комітом: `bash Tools/check-compile.sh` (компілює response-файлами Unity), `python3 Tools/check-ui-animation.py`, `check-glyphs.py`, `check-navigation.py`.

---

## 15. Симулятори

**`Tools/InkFlow.Sim`** — CLI на реальному Core (компілює `Assets/_Scripts/Core/**/*.cs` як звичайний .NET-проєкт). Жадібний бот на один хід із шумом; три пресети (`--bot sloppy|default|careful`), довільні ваги (`--weights`), час на хід для оцінки хвилин на картинку (`--seconds-per-move`), власна тека картинок (`--pictures`). Виводить розміщення, лінії, частку чистих, кроки заповнені й згорілі, картинки за забіг і за рідкістю, хвилини на картинку, небезпеку (частка ходів із попередженням і за скільки ходів до програшу воно з'явилось; `--danger-csv` — сирі ознаки кожного ходу), несправедливі смерті (код повернення 1, якщо є). Це нижня межа гравця: бот не планує колір через кілька лотків.

**`EconomySimulator`** (редактор) — 30 днів життя гравця через `CompleteLevel`; на новий забіг (`CompleteRun`) ще не переведений.

---

## 16. Git-workflow і `CLAUDE.md`

- Гілка на фазу (зараз робота йде на `main`); push — після «ок» автора на кожній контрольній точці; окремий коміт на кожен пункт промту; імперативні повідомлення українською.
- **Git LFS** для `.png`, `.wav`, `.psd`, `.fbx`; Force Text + Visible Meta Files.
- Усі `Assets/_Prefabs/Screens/*.prefab` — згенеровані `Build*Screen`, руками не правляться (`GalaxyScreen.prefab` — не виняток: його відмінності в робочій копії були Unity-пересеріалізацією, а не правкою автора). Перезібрані сцени й префаби комітяться за рішенням автора.
- `CLAUDE.md` у корені — стан проєкту; `docs/implementation-notes.md` — журнал рішень, відступів, сиріт і цифр прогонів.

---

## 17. Відповідність фазам розробки

Фази — з майстер-доку §18.

| Фаза | Стан |
|---|---|
| **0. Документ** | ✅ майстер-док переписано під піксельні картинки (Сесія 3) |
| **1. Фундамент картинок** | ✅ майстер-палітра, `PixelPicture`, бібліотека 117, колода «невидані першими», збереження з міграціями, контактний аркуш |
| **2. Забіг** | ✅ кольори з картинки, кроки з ліній, зв'язне проявлення, перефарбування, завершення зі свайпом; вигляд K1Candy + W4DarkCanvas + A1Breathe, над полем лише картинка (Сесії 3–5) |
| **3. Галактика-вітрина** | ✅ слоти планет замість фарбування зон (Фаза 3, Сесія 6): `GalaxyConfig`, цикли галактик (завершені — вітрина, стрілками в шапці), колекція окремим екраном, v8; літри повернуто нафтою; ревізія закрита |
| **4. Магазин — лише нафта** | ✅ чотири пакети з `EconomyConfig`, ціна зі стору через `IIapService` (`FakeIap`/`NullIap`), нафта лише на «домалювати» й «продовжити», `EconomySimulation`, v9 без полів фарби, `PaintEconomyAbsenceTests` (Фаза 4, Сесія 6) |
| **5. Налаштування** | ✅ шестерня на всіх екранах (`ScreenBase.SettingsRequested`), `SettingsScreen`, перемикачі реально керують `GameAudio` і `SettingsGatedHaptics`, «Додому» з підтвердженням, «Заново» лише в забігу, `AppConfig`/`AppLinks`; ревізія Фаз 4–5 закрита — забіг переживає налаштування без перезапуску (Фаза 5, Сесія 6) |
| **6. Профіль** | ✅ аватар із набору крапель (`AvatarSet`), нік за `NickRules` з `AppConfig` (3–16, фільтр коренів), вітринна картинка з колекції (`ShowcasePictureId`, `CollectionArgs.ForShowcase`); драбину, статистику, палітру, бейджі й вітрину планет видалено; старі баги аватара закриті UI-тестами (Фаза 6, Сесія 6) |
| **7. Рейтинги на справжніх даних** | ✅ інтерфейси Platform (`IIdentityService`, `ILeaderboardService`, `IShowcaseService`) з Null/Fake і UGS-адаптером за define, `RankingsSync` (у хмару лише вітрина й два числа), база тижня v10, екран «Світ» з двома метриками й станом «немає з'єднання», чужа галактика з вітрини, інкогніто (Фаза 7, Сесія 6). Підключення UGS у проєкті — ручні кроки автора |
| **Реліз** | ⛔ Platform-реалізації (реклама, IAP, гаптика), приватність, білди |

---

## 18. Інваріанти, які не можна порушувати

Якщо якась зміна ламає щось із цього — змінюється рішення, а не інваріант.

1. **Core не знає про Unity.** Ніколи.
2. **Фігури не обертаються, лоток — три, поповнення — лише коли всі три поставлено.**
3. **Клітинка родини X заповнює лише крок родини X; чиста лінія дає в рази більше** (`PureLineBonus` ≥ 2); зайве згорає.
4. **Картинка проявляється зв'язно**, і порядок рахує Core, а не в'юха.
5. **Готова картинка на екрані збігається з артом піксель у піксель** — стиль змінює лише відображення.
6. **Система не вбиває гравця сама:** мішок ніколи не видає неможливий набір, поки влазить двоклітинкова; єдина перевірка живості — `NoPieceFits()`.
7. **Забіг зберігається зліпком;** продовження зі зліпка — байт у байт; програш, фінал і рестарт стирають його.
8. **Гра показує картинку до першого ходу.**
9. **Нуль `Instantiate`/`Destroy` під час партії.**
10. **Баланс живе в конфігах**, контент — у `Assets/_Pictures/*.txt`, вигляд — у `DesignSystem`.
11. **Донат не впливає на проходження забігу** — «домалювати» не чіпає поле, рахунок і лоток.
12. **Збереження версіоноване й пишеться атомарно; ідентифікатори у файлі — назви.**
13. **Жодного post-process** (Bloom заборонений); щокадрово в UI — лише `localPosition`/`localScale`/`localRotation` і `CanvasRenderer`.

---

## Швидкий старт для нової сесії Claude Code

1. Прочитай `docs/ink-flow-core-final.md` (ігрова правда) і цей файл (технічна правда).
2. Прочитай `CLAUDE.md` — там стан проєкту й особливості середовища; `docs/implementation-notes.md` — чому саме так.
3. Звір інваріанти §18 перед будь-якою зміною в Core.
4. Нова механіка = спершу тест у `InkFlow.Core.Tests`, потім реалізація, потім вигляд; після — прогін бота.
5. Будь-яке нове число → в конфіг, не в код.
