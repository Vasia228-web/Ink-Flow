using System;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Увесь стан гравця в одному місці: файл збереження плюс живі об'єкти,
    /// з якими працюють екрани (§10).
    ///
    /// Навіщо окремий тип, а не голий <see cref="SaveFile"/>: у файлі лежать дані,
    /// а грає гра з поведінкою — гаманець із подією зміни, колекція, лічильник
    /// денного ліміту. Тримати і те, і те синхронно вручну в кожному екрані
    /// означало б рано чи пізно записати одне й забути інше.
    ///
    /// Правило: РАНТАЙМ — джерело правди, файл — його зліпок. <see cref="Persist"/>
    /// згортає живі об'єкти назад у файл і пише його, і робиться це в кількох
    /// чітких точках (кінець забігу, покупка, слот планети, згортання застосунку),
    /// а не «десь після зміни».
    /// </summary>
    public sealed class PlayerState
    {
        private readonly ISaveStorage? _storage;

        public PlayerState(SaveFile save, EconomyData economy, ISaveStorage? storage = null,
            Core.PictureLibrary? library = null, Core.BalanceData? balance = null, GalaxyLayout? layout = null)
        {
            File = save ?? throw new ArgumentNullException(nameof(save));
            Economy = economy ?? EconomyData.Default;
            _storage = storage;
            Library = library ?? Core.PictureLibrary.Fallback;
            Balance = balance ?? Core.BalanceData.Default;
            Layout = layout ?? GalaxyLayout.Default;

            File.Profile ??= new ProfileData();
            File.Wallet ??= new WalletData();
            File.Galaxy ??= new GalaxyData();
            File.Galaxy.Slots ??= new System.Collections.Generic.List<PlanetSlotRecord>();
            File.Progress ??= new ProgressData();
            File.Settings ??= new SettingsData();
            File.Collection ??= new CollectionData();
            File.Run ??= new Core.RunSnapshot();
            File.RankWeek ??= new RankWeekData();

            Wallet = new Wallet(File.Wallet.OilDrops);
            Collection = PictureCollection.Load(File.Collection);
            Rewards = new RewardCalculator(Economy);
            DailyLimit = new DailyLimitTracker(Economy);
            DailyLimit.Restore(File.Wallet.PlaysToday, File.Wallet.DayUtc);
        }

        public SaveFile File { get; }
        public EconomyData Economy { get; }
        public Wallet Wallet { get; }
        public RewardCalculator Rewards { get; }
        public DailyLimitTracker DailyLimit { get; }

        /// <summary>Зібрані картинки (§5, §10): кожна зібрана копія — одна картка, яку можна поставити в слот.</summary>
        public PictureCollection Collection { get; }

        /// <summary>Бібліотека й баланс, з якими читається файл: зліпок забігу говорить назвами картинок і форм.</summary>
        public Core.PictureLibrary Library { get; }
        public Core.BalanceData Balance { get; }

        /// <summary>Розкладка галактики (§12): планети й слоти — з конфігу, цикли нескінченні.</summary>
        public GalaxyLayout Layout { get; }

        /// <summary>
        /// Перерваний забіг (§9), якщо його можна продовжити цією грою; null — починати новий.
        /// Зліпок з іншої версії (картинку прибрали, сітка інша) тихо ігнорується.
        /// </summary>
        public Core.RunSnapshot? SavedRun =>
            Core.RunSession.CanRestore(File.Run, Library, Core.PieceCatalogData.Default, Balance) ? File.Run : null;

        public ProgressData Progress => File.Progress;
        public GalaxyData Galaxy => File.Galaxy;

        // ── Налаштування (§15) ──

        /// <summary>Перемикачі звуку, музики, вібрації й приватності — у файлі, читаються звідси.</summary>
        public SettingsData Settings => File.Settings;

        /// <summary>Щось перемкнули: аудіо й гаптика перечитують стан.</summary>
        public event Action? SettingsChanged;

        public void SetSound(bool on) => ChangeSetting(Settings.Sound != on, () => Settings.Sound = on);
        public void SetMusic(bool on) => ChangeSetting(Settings.Music != on, () => Settings.Music = on);
        public void SetVibration(bool on) => ChangeSetting(Settings.Vibration != on, () => Settings.Vibration = on);

        /// <summary>§15, §16: прихований профіль — у рейтингах «Гравець-інкогніто».</summary>
        public void SetProfileHidden(bool hidden) => ChangeSetting(Settings.ProfileHidden != hidden, () => Settings.ProfileHidden = hidden);

        /// <summary>Перемикач — одразу у файл: закрити гру після нього не має повернути старе значення.</summary>
        private void ChangeSetting(bool changed, Action apply)
        {
            if (!changed)
                return;
            apply();
            Persist();
            SettingsChanged?.Invoke();
        }

        public string Nick
        {
            get => File.Profile.Nick is null || File.Profile.Nick.Length == 0
                ? ProfileData.DefaultNick
                : File.Profile.Nick;
            set => File.Profile.Nick = value is null || value.Length == 0
                ? ProfileData.DefaultNick
                : value;
        }

        // ── Профіль (§14): нік за правилами, аватар із набору, вітринна картинка ──

        /// <summary>Профіль змінився (нік, аватар, вітрина) — хаб і рейтинги перечитують.</summary>
        public event Action? ProfileChanged;

        /// <summary>Аватар — номер у наборі крапель (<see cref="AvatarSet"/>); номер поза набором читається як перший.</summary>
        public int AvatarId => AvatarSet.Clamp(File.Profile.AvatarId);

        public void SetAvatar(int id)
        {
            id = AvatarSet.Clamp(id);
            if (File.Profile.AvatarId == id)
                return;
            File.Profile.AvatarId = id;
            Persist();
            ProfileChanged?.Invoke();
        }

        /// <summary>
        /// Нік за правилами (§14: довжина й фільтр слів — із конфігу). Відмова лишає старий нік і каже чому;
        /// той самий нік — успіх без запису. Сеттер <see cref="Nick"/> лишається для дев-панелі й міграцій.
        /// Не «Try…»: так названі лише дії, що списують нафту (сторож <c>OilIsSpentOnlyOnFinishingAndContinuing</c>).
        /// </summary>
        public bool SetNick(string? raw, NickRules rules, out NickVerdict verdict)
        {
            var nick = NickRules.Normalize(raw);
            verdict = (rules ?? NickRules.Default).Check(nick);
            if (verdict != NickVerdict.Ok)
                return false;
            if (Nick == nick)
                return true;
            File.Profile.Nick = nick;
            Persist();
            ProfileChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Нік зі старого файлу міг не проходити нинішні правила (раніше приймалось усе від одного символу):
        /// такий скидається на типовий, щоб у рейтингах не стояло те, чого гра сама не приймає. Кличе
        /// композиційний корінь, коли правила відомі. Повертає true, якщо нік змінено.
        /// </summary>
        public bool EnforceNickRules(NickRules rules)
        {
            rules ??= NickRules.Default;
            if (rules.Check(Nick) == NickVerdict.Ok)
                return false;
            File.Profile.Nick = ProfileData.DefaultNick;
            Persist();
            ProfileChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Вітринна картинка (§14) — та, яку бачать інші: обрана, поки вона в колекції й у бібліотеці; інакше
        /// остання зібрана з тих, що бібліотека знає; null — показувати нічого. Файл може пам'ятати картинку,
        /// якої вже немає (дев-очищення, прибрана з бібліотеки) — вітрина тоді не порожніє, а показує останню.
        /// </summary>
        public string? ShowcasePictureId
        {
            get
            {
                var chosen = File.Profile.ShowcasePictureId;
                if (!string.IsNullOrEmpty(chosen) && Collection.Has(chosen) && Library.Find(chosen) != null)
                    return chosen;
                var ids = Collection.Ids;
                for (var i = ids.Count - 1; i >= 0; i--)
                    if (Library.Find(ids[i]) != null)
                        return ids[i];
                return null;
            }
        }

        /// <summary>Чи вітрина обрана рукою (а не підставлена як остання зібрана) — екран каже гравцю, що саме він бачить.</summary>
        public bool IsShowcaseChosen
        {
            get
            {
                var shown = ShowcasePictureId;
                return shown != null && File.Profile.ShowcasePictureId == shown;
            }
        }

        /// <summary>На вітрину можна поставити лише зібрану картинку.</summary>
        public bool SetShowcasePicture(string? pictureId)
        {
            if (string.IsNullOrEmpty(pictureId) || !Collection.Has(pictureId!))
                return false;
            if (File.Profile.ShowcasePictureId == pictureId)
                return true;
            File.Profile.ShowcasePictureId = pictureId!;
            Persist();
            ProfileChanged?.Invoke();
            return true;
        }


        // ── Рейтинги (§16): локальні числа — правда; у хмару йде лише вітрина ──

        /// <summary>Слоти планет змінились (постановка, звільнення) — вітрину й таблиці варто оновити.</summary>
        public event Action? GalaxyChanged;

        /// <summary>Планет ожило — з усіх циклів.</summary>
        public int PlanetsDone => GalaxyState.CompletedPlanets(Galaxy, Layout);

        /// <summary>Галактик завершено.</summary>
        public int GalaxiesDone => GalaxyState.CompletedGalaxies(Galaxy, Layout);

        /// <summary>Новий тиждень — нова база приросту; true, якщо базу оновлено (і записано).</summary>
        public bool RollOverWeek(DateTime utcNow)
        {
            if (!WeekBaseline.RollOver(File.RankWeek, utcNow, PlanetsDone, GalaxiesDone))
                return false;
            Persist();
            return true;
        }

        /// <summary>Значення метрики за період: за весь час — лічильник, за тиждень — приріст від бази тижня.</summary>
        public long RankValue(RankMetric metric, RankPeriod period, DateTime utcNow)
        {
            RollOverWeek(utcNow);
            if (period == RankPeriod.AllTime)
                return metric == RankMetric.Planets ? PlanetsDone : GalaxiesDone;
            return WeekBaseline.WeekValue(File.RankWeek, metric, PlanetsDone, GalaxiesDone);
        }

        /// <summary>
        /// Галактика, яку показуємо іншим: поточна, якщо в ній уже стоять картинки, інакше остання
        /// завершена (щойно завершена галактика робить поточною нову, порожню).
        /// </summary>
        public int ShowcaseGalaxy
        {
            get
            {
                var galaxy = CurrentGalaxy;
                if (galaxy > 0 && FilledInGalaxy(galaxy) == 0)
                    galaxy--;
                return galaxy;
            }
        }

        private int FilledInGalaxy(int galaxy)
        {
            var total = 0;
            for (var i = 0; i < Layout.Planets.Count; i++)
                total += GalaxyState.FilledCount(Galaxy, galaxy, Layout.Planets[i].Id, Layout.Planets[i].Slots);
            return total;
        }

        /// <summary>
        /// Публічна вітрина (§16) — єдине, що йде в хмару: нік, аватар, лічильники, слоти показуваної
        /// галактики й вітринна картинка. Прихований профіль (§15) віддає лише прапорець і лічильники.
        /// </summary>
        public PublicShowcase BuildShowcase(string playerId, DateTime utcNow)
        {
            var stamp = utcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            if (Settings.ProfileHidden)
                return PublicShowcase.Hidden(playerId, PlanetsDone, GalaxiesDone, stamp);

            var galaxy = ShowcaseGalaxy;
            var slots = new System.Collections.Generic.List<ShowcaseSlot>();
            foreach (var record in Galaxy.Slots)
                if (record.Galaxy == galaxy && GalaxyState.IsAddressed(record, Layout))
                    slots.Add(new ShowcaseSlot(record.PlanetId, record.Slot, record.PictureId));
            return new PublicShowcase(playerId, Nick, AvatarId, false, ShowcasePictureId, PlanetsDone, GalaxiesDone, galaxy, slots, stamp);
        }

        /// <summary>Стан щойно створеного гравця: усе по нулях, крім явно виданого стартового.</summary>
        public static PlayerState NewPlayer(EconomyData economy, ISaveStorage? storage = null,
            Core.PictureLibrary? library = null, Core.BalanceData? balance = null, GalaxyLayout? layout = null)
        {
            var state = new PlayerState(new SaveFile(), economy, storage, library, balance, layout);

            if (economy.StarterOil > 0)
                state.Wallet.Add(economy.StarterOil, RewardSource.Debug);

            return state;
        }

        /// <summary>
        /// Згортає живі об'єкти у файл і записує його. Викликається у чітких
        /// точках, а не після кожної дрібниці: запис — це файлова операція,
        /// і на слабкому Android робити її щокадру не можна.
        /// </summary>
        public void Persist()
        {
            File.Wallet.OilDrops = Wallet.OilDrops;
            File.Wallet.PlaysToday = DailyLimit.PlaysToday;
            // Інваріантна культура обов'язкова: на телефоні з тайським чи японським календарем «yyyy»
            // дало б 2569, Restore не впізнав би день, і денний ліміт скидався б на кожному запуску.
            File.Wallet.DayUtc = DailyLimit.CurrentDayUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            PictureCollection.Save(Collection, File.Collection);

            _storage?.Save(File);
        }

        /// <summary>§9: зліпок забігу — у файл. Пишеться на паузу, вихід і новий лоток, а не після кожного ходу.</summary>
        public void SaveRun(Core.RunSession session)
        {
            if (session is null) throw new ArgumentNullException(nameof(session));
            if (session.IsOver)
            {
                ClearRun();
                return;
            }
            session.Capture(File.Run);
            Persist();
        }

        /// <summary>Забіг закінчено (програш, фінал, рестарт) — продовжувати нічого.</summary>
        public void ClearRun()
        {
            if (File.Run.IsEmpty)
                return;
            File.Run.Clear();
            Persist();
        }

        /// <summary>
        /// Картинку домальовано в забігу (§5): у колекцію і одразу у файл — програш
        /// через хвилину не має відібрати те, що вже зібрано. Повертає true, якщо нова.
        /// </summary>
        public bool CollectPicture(string pictureId, DateTime utcNow)
        {
            var isNew = Collection.Add(pictureId, utcNow);
            Persist();
            return isNew;
        }

        /// <summary>
        /// Підсумок пройденого рівня: зірки в прогрес, нафта в гаманець, запис у файл.
        /// Одна точка входу — інакше «зірки записались, а нафта ні» ставало б
        /// питанням того, який екран що не забув. Режим «Рівні» — сирота («Скоро»).
        /// </summary>
        public long CompleteLevel(int levelId, int stars, bool isBoss, DateTime utcNow)
        {
            DailyLimit.RollOverIfNeeded(utcNow);

            LevelProgress.Record(Progress, levelId, stars);

            var reward = Rewards.ForLevel(
                new GameResult(levelId, won: stars > 0, stars, isBoss),
                DailyLimit.RewardMultiplier);

            if (reward > 0)
                Wallet.Add(reward, isBoss ? RewardSource.BossClear : RewardSource.LevelClear);

            // Партію рахуємо ПІСЛЯ нарахування: інакше перша ж гра дня платила б
            // за зменшеним множником.
            DailyLimit.RegisterPlay(utcNow);

            Persist();
            return reward;
        }

        /// <summary>
        /// Підсумок забігу «Нескінченного» (майстер-док §10): очки → нафта з денним
        /// множником, картинки → нафта за рідкістю, рекорди — у прогрес, забіг
        /// рахується як партія дня (інакше нафта стала б нескінченною). Одна точка
        /// входу, як і <see cref="CompleteLevel"/>.
        /// </summary>
        public RunReward CompleteRun(in RunSummary run, DateTime utcNow)
        {
            DailyLimit.RollOverIfNeeded(utcNow);

            var forScore = Rewards.ForRun(run.Score, DailyLimit.RewardMultiplier);
            long forPictures = 0;
            for (var r = 0; r < Core.Rarities.Count; r++)
                forPictures += run.DoneOf((Core.Rarity)r) * Rewards.ForPicture((Core.Rarity)r);

            if (forScore > 0)
                Wallet.Add(forScore, RewardSource.RunScore);
            if (forPictures > 0)
                Wallet.Add(forPictures, RewardSource.PictureCompleted);

            var newRecord = run.Score > Progress.EndlessRecord;
            if (newRecord)
                Progress.EndlessRecord = run.Score;
            if (run.BestChain > Progress.BestChain)
                Progress.BestChain = run.BestChain;
            Progress.RunsPlayed++;

            // Партію рахуємо ПІСЛЯ нарахування: інакше перша ж гра дня платила б
            // за зменшеним множником.
            DailyLimit.RegisterPlay(utcNow);

            Persist();
            return new RunReward(forScore, forPictures, newRecord);
        }

        /// <summary>§9: «подвоїти за ролик» — доплата до нафти за очки (×RewardAdMultiplier − 1). Повертає доплату.</summary>
        public long DoubleRunReward(long forScore)
        {
            if (forScore <= 0)
                return 0;
            var bonus = forScore * (Economy.RewardAdMultiplier - 1);
            if (bonus > 0)
            {
                Wallet.Add(bonus, RewardSource.RewardAd);
                Persist();
            }
            return bonus;
        }

        /// <summary>Нафта за картинку, домальовану поза забігом (донат §9): та сама таблиця, що й у CompleteRun.</summary>
        public long RewardPicture(Core.Rarity rarity)
        {
            var reward = Rewards.ForPicture(rarity);
            if (reward > 0)
            {
                Wallet.Add(reward, RewardSource.PictureCompleted);
                Persist();
            }
            return reward;
        }

        /// <summary>§13: ціна «домалювати одразу» для цієї картинки — за рідкістю й решті пікселів.</summary>
        public long FinishPictureCost(Core.Rarity rarity, float remainingFraction) =>
            Economy.FinishPictureCost(rarity, remainingFraction);

        /// <summary>
        /// §13: «домалювати одразу» за нафту. Списує ціну; false і нічого не змінює, якщо
        /// нафти не вистачає. Нафта за домальовану картинку нараховується окремо (RewardPicture).
        /// </summary>
        public bool TryFinishPicture(Core.Rarity rarity, float remainingFraction)
        {
            var cost = FinishPictureCost(rarity, remainingFraction);
            if (!Wallet.TrySpend(cost))
                return false;
            Persist();
            return true;
        }

        /// <summary>§13: ціна «продовжити забіг після програшу» за нафту.</summary>
        public long ContinueCost => Economy.ContinueCost;

        /// <summary>
        /// §10, §13: «продовжити після програшу» за нафту — коли ролик уже використано або реклами
        /// немає. Списує ціну; false і нічого не змінює, якщо нафти не вистачає.
        /// </summary>
        public bool TryContinueRun()
        {
            if (!Wallet.TrySpend(ContinueCost))
                return false;
            Persist();
            return true;
        }

        /// <summary>§13: пакети нафти магазину — з конфігу; ціни приходять зі стору.</summary>
        public System.Collections.Generic.IReadOnlyList<OilPack> OilPacks => Economy.OilPacks;

        /// <summary>
        /// §13: стор підтвердив покупку пакета — нафта в гаманець і одразу у файл: закрити гру
        /// після покупки не має коштувати гравцю куплених крапель.
        /// </summary>
        public void GrantPurchasedOil(OilPack pack)
        {
            if (pack is null) throw new ArgumentNullException(nameof(pack));
            Wallet.Add(pack.Amount, RewardSource.Purchase);
            Persist();
        }

        /// <summary>§9: інтерстиціал раз на N забігів — після забігу з номером, кратним N.</summary>
        public bool ShouldShowInterstitial =>
            Economy.InterstitialEveryRuns > 0 && Progress.RunsPlayed > 0 &&
            Progress.RunsPlayed % Economy.InterstitialEveryRuns == 0;

        // ── Галактика-вітрина (§12) ──

        /// <summary>Поточна галактика — перша незавершена. Лише в ній можна ставити й знімати картинки.</summary>
        public int CurrentGalaxy => GalaxyState.CurrentGalaxy(Galaxy, Layout);

        /// <summary>
        /// Скільки копій цієї картинки ще вільні: зібрано − стоїть у слотах. Одна зібрана копія —
        /// один слот: двічі домалював кота — два слоти, один раз — один (відповідь автора, Сесія 1).
        /// Рахуються лише слоти, які адресує розкладка: запис у слоті, якого в планети вже немає,
        /// копію не тримає.
        /// </summary>
        public int FreeCopies(string pictureId) =>
            Collection.CountOf(pictureId) - GalaxyState.PlacedCopies(Galaxy, pictureId, Layout);

        /// <summary>
        /// Чи можна редагувати планету: лише в поточній галактиці, і лише відкриту
        /// (<see cref="GalaxyState.IsPlanetOpen"/>: першу, будь-яку за ожилою або будь-яку з картинками).
        /// </summary>
        public bool CanEditPlanet(int galaxy, string planetId)
        {
            if (galaxy != CurrentGalaxy)
                return false;
            var index = Layout.IndexOf(planetId);
            return index >= 0 && GalaxyState.IsPlanetOpen(Galaxy, galaxy, Layout, index);
        }

        /// <summary>
        /// Ставить картинку з колекції в слот планети (§12) і зберігає. Зайнятий слот — заміна.
        /// false і нічого не змінює, якщо планета замкнена, слота немає або вільних копій картинки
        /// не лишилось. Та сама картинка в тому самому слоті — true без запису.
        /// </summary>
        public bool TryPlaceInSlot(int galaxy, string planetId, int slot, string pictureId, DateTime utcNow)
        {
            // База «цього тижня» (§16) мусить стояти ДО зміни слотів: інакше перша ожила планета нового
            // гравця не рахувалась би за тиждень, бо базу поставив би вже наступний запит рейтингів.
            // Без окремого Persist: успішна постановка пише файл сама, а відмова нічого не міняє.
            WeekBaseline.RollOver(File.RankWeek, utcNow, PlanetsDone, GalaxiesDone);
            if (pictureId is null || pictureId.Length == 0)
                return false;
            var planet = Layout.Find(planetId);
            if (planet is null || slot < 0 || slot >= planet.Slots)
                return false;
            if (!CanEditPlanet(galaxy, planetId))
                return false;

            var current = GalaxyState.PictureAt(Galaxy, galaxy, planetId, slot);
            if (string.Equals(current, pictureId, StringComparison.Ordinal))
                return true;
            if (FreeCopies(pictureId) <= 0)
                return false;

            GalaxyState.Set(Galaxy, galaxy, planetId, slot, pictureId, utcNow);
            Persist();
            GalaxyChanged?.Invoke();
            return true;
        }

        /// <summary>Повертає картинку зі слота в колекцію (§12) і зберігає. false — слот порожній або планета замкнена.</summary>
        public bool ClearSlot(int galaxy, string planetId, int slot, DateTime utcNow)
        {
            // База тижня — до зміни: «вийняти й поставити назад» на початку тижня не має давати приросту.
            WeekBaseline.RollOver(File.RankWeek, utcNow, PlanetsDone, GalaxiesDone);
            if (!CanEditPlanet(galaxy, planetId))
                return false;
            if (!GalaxyState.Clear(Galaxy, galaxy, planetId, slot))
                return false;
            Persist();
            GalaxyChanged?.Invoke();
            return true;
        }
    }

    /// <summary>Підсумок забігу, який екран віддає в Meta: очки, ланцюг і скільки картинок кожної рідкості закінчено.</summary>
    public readonly struct RunSummary
    {
        private readonly int[]? _doneByRarity;

        /// <param name="doneByRarity">Закінчених картинок за рідкістю, індекс — (int)<see cref="Core.Rarity"/>; null — жодної.</param>
        public RunSummary(int score, int bestChain, int[]? doneByRarity = null)
        {
            if (doneByRarity != null && doneByRarity.Length != Core.Rarities.Count)
                throw new ArgumentOutOfRangeException(nameof(doneByRarity), "Шість лічильників: звичайна … космічна.");
            Score = score;
            BestChain = bestChain;
            _doneByRarity = doneByRarity;
        }

        /// <summary>З індексів зібраних у забігу картинок (<c>RunSession.PicturesCollected</c>) і бібліотеки.</summary>
        public static RunSummary Of(int score, int bestChain, System.Collections.Generic.IReadOnlyList<int> collected, Core.PictureLibrary library)
        {
            if (collected is null) throw new ArgumentNullException(nameof(collected));
            if (library is null) throw new ArgumentNullException(nameof(library));
            var done = new int[Core.Rarities.Count];
            for (var i = 0; i < collected.Count; i++)
                if (collected[i] >= 0 && collected[i] < library.Count)
                    done[(int)library[collected[i]].Rarity]++;
            return new RunSummary(score, bestChain, done);
        }

        public int Score { get; }
        public int BestChain { get; }

        public int DoneOf(Core.Rarity rarity) => _doneByRarity is null ? 0 : _doneByRarity[(int)rarity];

        public int PicturesDone
        {
            get
            {
                if (_doneByRarity is null)
                    return 0;
                var n = 0;
                for (var i = 0; i < _doneByRarity.Length; i++)
                    n += _doneByRarity[i];
                return n;
            }
        }
    }

    /// <summary>Що нарахували за забіг — окремо за очки й за картинки, щоб екран показав обидва.</summary>
    public readonly struct RunReward
    {
        public RunReward(long forScore, long forPictures, bool newRecord)
        {
            ForScore = forScore;
            ForPictures = forPictures;
            NewRecord = newRecord;
        }

        public long ForScore { get; }
        public long ForPictures { get; }
        public long Total => ForScore + ForPictures;
        public bool NewRecord { get; }
    }
}
