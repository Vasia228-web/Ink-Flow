# Запас: покупка нафти за реальні гроші (магазин, стор, «+» на капсулі)

**У запасі з 2026-10-15 — рішення автора:** гра виходить без покупки нафти; інтегруємо, коли гра масштабуватиметься і автор скаже «інтегруємо». До того в грі немає магазину, пакетів, стору, вкладки «Магазин» у навігації й бірюзового «+» на капсулі нафти.

**Що лишилось у грі (не чіпали):** сама нафта — заробляється за очки забігу (з денним лімітом) і домальовані картинки, подвоюється роликом, витрачається на «домалювати одразу» і «продовжити після програшу» (перше продовження — за ролик, далі — за нафту). Капсула нафти в шапках показує баланс, лише без «+». Це тримають `OilSpendingTests`, `EconomySimulationTests`, `RunEconomyTests`.

Останній коміт, де все це працювало в грі: **`b9d6379`**. Будь-який файл того стану: `git show b9d6379:<шлях>`.

## Що в запасі

Файли лежать тут із тими самими шляхами, що були в грі (з `.meta` там, де вони були — GUID скриптів збережено):

| Файл | Що робить |
|---|---|
| `Assets/_Scripts/UI/Shop/ShopScreen.cs` | Екран «МАГАЗИН»: чотири пакети з конфігу, ціни — рядком зі стору (`IIapService.Query`), «купити» → `PlayerState.GrantPurchasedOil`; рядки стану «Дізнаємось ціни…», «Купівля…», «Магазин недоступний — спробуй пізніше», «Купівля не вдалася: …», «+N нафти». |
| `Assets/_Scripts/UI/Shop/OilPackCard.cs` | Картка пакета: крапля росте з пакетом, кількість, бейдж («Популярне» гарячий / «Найвигідніше»), кнопка з ціною (риска, поки стор не відповів). |
| `Assets/_Scripts/Meta/Shop/OilPack.cs` | Модель пакета (`Id`, `Amount`, `Badge`, `Hot`) і `OilPack.Defaults()`: `oil_1000`, `oil_10000` (Популярне, hot), `oil_100000` (Найвигідніше), `oil_500000`. Ціни в грі немає свідомо — її знає стор. |
| `Assets/_Scripts/Platform/IapServices.cs` | Зібраний з двох файлів гри: `StoreProduct`, `PurchaseResult`, `IIapService` (з `PlatformServices.cs`) і `NullIap` / `FakeIap` (з `Null/NullServices.cs`; Fake: $5 / $25 / $50 / $100, покупка миттєва). Можна покласти цілим у `Assets/_Scripts/Platform/`. |
| `Assets/_Scripts/Editor/BuildShopScreen.cs` | Збирач: `Ink Flow → Setup → Build Shop Screen` → `Assets/Scenes/Shop.unity` + `Assets/_Prefabs/Screens/ShopScreen.prefab` (обидва згенеровані — видалені, збирач створить знову). Стиль — старий «Ink Flow v2». |
| `Assets/Tests/EditMode/Meta/OilShopTests.cs` | Headless: чотири пакети майстер-доку, у пакеті немає ціни, пошук за id, покупка зараховує рівно пакет і пише файл, порожній магазин відкидається. (Тести витрат із цього файлу лишились у грі як `OilSpendingTests`.) |
| `Assets/Tests/EditMode/UI/ShopScreenTests.cs` | UI на живому рендері: ціни зі стору, покупка через стор, без стору кнопки сплять і екран каже чому. |
| `Assets/_Sprites/U1Glaze/oil/oil_pack_1…4.png`, `controls/capsule_plus.png`, `icons/icon_restore.png` | Спрайти U1Glaze, потрібні лише магазину: ілюстрації пакетів, бірюзова плитка «+» капсули, іконка «Відновити покупки». Атлас `U1Glaze.spriteatlasv2` пакує всю теку, тож у грі вони їхали б у збірку невидимими (пакети — ~10 % площі атласу). |
| `sprites-entries.json` | Їхні записи маніфесту `sprites.json` (межі, розміри, призначення) — вирізані з ігрової копії `Assets/_Sprites/U1Glaze/sprites.json`; еталон `docs/StyleRef/U1Glaze/sprites.json` не чіпали. |
| `wiring.patch` | Усе, що прибрано зі спільних файлів гри, — `git diff` у момент вирізання. |

Видалено як згенероване (є в `b9d6379`): сцена `Shop.unity`, префаб `ShopScreen.prefab`, іконка вкладки `Assets/_Sprites/UI/icon-shop.png` (малювала `GenerateUISprites.CreateShopIcon` — у патчі), знімки `docs/screenshots/meta/*_shop.png` і `*_shop-offline.png`.

## Що прибрано зі спільних файлів (точки підключення)

Повний текст — у `wiring.patch`. Коротко:

- **Платформа:** `PlatformServices.cs` — `StoreProduct`, `PurchaseResult`, `IIapService`; `Null/NullServices.cs` — `NullIap`, `FakeIap` (обидва — у `IapServices.cs` цієї теки); `App/ServiceLocator.cs` — прапорець `FeatureFlags.IapEnabled` (ніхто не читав).
- **Композиційний корінь:** `App/GameBootstrap.cs` — реєстрація `IIapService` (`FakeIap` у редакторі й dev-збірках, `NullIap` у релізі) і аргумент `ServiceLocator.Get<IIapService>()` у `router.BindServices(…)`.
- **Навігація:** `UI/Common/AppRouter.cs` — поле `shop`, `shop?.BindState`, параметр `IIapService? iap` у `BindServices` і `shop?.BindServices(iap)`, екран у `AllScreens()`, `shop.BackRequested += Pop`, вкладка `case "shop": Push(shop)`.
- **Стан і економіка (Meta):** `PlayerState` — `OilPacks` і `GrantPurchasedOil(pack)` (гаманець + `Persist()` одразу — «закрити гру після покупки не має коштувати куплених крапель»); `EconomyData` — параметр конструктора `oilPacks` (останній), перевірка «Магазин без пакетів нафти», `OilPacks`, `FindOilPack(productId)`; `Wallet.cs` — `RewardSource.Purchase` (був першим у переліку; у файл джерело не пишеться, тож порядок нічого не ламає); `EconomySimulation` — метрика `SmallestPackInRuns` («найменший пакет = N забігів доходу»), тест тримав її в межах **4…14 забігів**.
- **Конфіг:** `Gameplay/Config/EconomyConfig.cs` — `OilPackEntry` (id, amount, badge, hot), список `oilPacks` з дефолтами `OilPack.Defaults()`, `ToPacks()` з голосним фолбеком на дефолти. (`EconomyConfig.asset` полів пакетів не мав — брались дефолти.)
- **Токени:** `Style/DesignSystem.cs` — `fontSizeShopPrice` 40, `shopOnLightText` #1C0F33, `shopHotGlowAlpha` 0.22, `oilDropSizes` {46, 58, 72, 88} px макета і `OilDropSize(i)`. Спільні токени з «Shop» у назві (`FontSizeShopCard`, `LetterSpacingShopTitle`, `ShopTabActiveFrom/To`) лишились — ними користуються інші екрани.
- **Капсула нафти (U1Glaze, крок 0):** `UI/Common/CurrencyWidget.cs` — `plusButton`, `plusIcon`, подія `PlusClicked` (роутер її так і не підписав — «+» був мертвою кнопкою), `SetPlusVisible`, тон іконки `TealInk`; `Editor/BuildUIKit.cs` — плитка «+» (прозорий ловець тапу 44×46 = `TapMin` × висота капсули, `capsule_plus` 32, `icon_plus` 16, `Button` без переходу, `PressScale` на плитці), правий відступ капсули 1 (тепер 14 — без «+» число впиралось би в заокруглення).
- **Хаб (старий стиль):** `Editor/BuildHubScreen.cs` — четверта вкладка «Магазин» (`id "shop"`, `icon-shop`, акцент `AccentPrimary`, шари «фарба у відрі» з відблиском); `UI/Hub/NavBar.cs`, `HubScreen.cs` — лише коментарі.
- **Редактор:** `BuildMainScene.cs` — `"ShopScreen"` у списку префабів і `("shop", …)` у підв'язці роутера; `MetaScreenshots.cs` — стани `shop` (FakeIap) і `shop-offline` (NullIap), `BuildShopScreen.Build()`; `EconomySimulator.cs` — «найменший пакет» у звіті; `GenerateUISprites.cs` — `CreateShopIcon`; `BuildComponentsScreen.cs` — підпис «капсула: «+» відкриває магазин».
- **Тести:** `EconomySimulationTests` — межі найменшого пакета.

## Майстер-док §13 на момент вирізання

> Один екран, чотири пакети: 1 000 за $5, 10 000 за $25, 100 000 за $50, 500 000 за $100. Покупка через `IIapService`; ціна на кнопці — з локалізованого рядка стору, не зашита.

(Абзац про витрати нафти лишився в §13 майстер-доку — він про гру, не про покупку.)

## Вигляд у U1Glaze (на момент вирізання ще не перемальований — це був крок 6)

- Еталони: `docs/StyleRef/U1Glaze/preview/S07Shop_01.png`, `S07Shop_02.png`, `S12SmallScreen_08.png`; SCREENS.md «S07Shop · Магазин». Спрайти лише магазину (`oil_pack_1…4`, `capsule_plus`, `icon_restore`) — у цій теці; загальні (`icon_plus`, `badge_pink`, `badge_amber`, `button_coral`, `button_glow_coral`, `panel_card`) лишились у наборі гри.
- Вхід — «+» на капсулі нафти (SPEC: «Магазин відкривається через «+» на капсулі нафти»), не вкладка: пропозиція Сесії 1 — подія `ScreenBase.ShopRequested` (як `SettingsRequested`, роутер підписує циклом), на самому магазині «+» інертний.
- Шапка: «назад» `icon_button` 46 + `icon_back`, капсула з «+», шестерня; під шапкою «Магазин» `ui-h1` по центру і «Нафта, щоб домалювати картинку одразу або продовжити забіг» `ui-body` ink-muted.
- Картка пакета: `panel_card` + `panel_shadow`, ілюстрація `oil_pack_1…4` (136×100, однаковий розмір у всіх), рядок `oil_drop` + число `ui-h2`, кнопка `button_coral` з ціною (чотири коралові на екрані — виняток із правила «одна коралова»), бейджі `badge_pink` «ПОПУЛЯРНЕ» (hot) / `badge_amber` «НАЙВИГІДНІШЕ» нависають над кутом картки; гаряча — бурштинова обводка без гало.
- «Відновити покупки» з макета автор прибрав ще 2026-10-12 (нафта — споживна покупка, відновлювати нею нічого).
- Подробиці замірів — карта Сесії 1: `../reports/2026-10-12-u1glaze-session1-map.html`, розділ S07Shop.

## Як повернути (чекліст)

1. Скопіювати файли з цієї теки назад у `Assets/…` (з `.meta`), записи з `sprites-entries.json` — назад у `Assets/_Sprites/U1Glaze/sprites.json` (порядок не важить), далі `Import U1Glaze Sprites`. `IapServices.cs` — цілим у `Assets/_Scripts/Platform/` або розкласти назад по `PlatformServices.cs` / `Null/NullServices.cs` (headless-раннер компілює `PlatformServices.cs` явним шляхом — інтерфейс має бути або там, або файл треба додати в `Tools/CoreTestRunner/CoreTestRunner.csproj`).
2. Точки підключення: `git apply -R Reserve/OilShop/wiring.patch` — якщо код відтоді не змінився; інакше вручну за списком вище. Навігація на той час, найпевніше, уже U1Glaze (Грати · Галактика · Колекція · Рейтинги) — тоді вкладку «Магазин» НЕ повертати, а вхід зробити «+» на капсулі.
3. Магазин перемалювати в U1Glaze фабриками `U1` за S07Shop (старий `BuildShopScreen` — стиль v2), додати в `MetaScreenshots.AuditEnforced`.
4. Тести: прибрати `OilPurchaseAbsenceTests`; повернути `OilShopTests`, `ShopScreenTests`, межі `SmallestPackInRuns` в `EconomySimulationTests`.
5. `bash Tools/check-compile.sh`, `bash Tools/run-core-tests.sh`, `python3 Tools/check-navigation.py`, `check-glyphs.py`, `check-ui-animation.py`.
6. У редакторі: `Build UI Kit` (капсула з «+») → `Build Shop Screen` → екрани з капсулою → `Build Main Scene`; знімки `shop`/`shop-offline`.
7. Реліз: товари `oil_1000/10000/100000/500000` у App Store Connect і Google Play (споживні), пакет Unity IAP і справжня реалізація `IIapService`; майстер-док §13, CLAUDE.md, архідок. Цю теку — видалити.
