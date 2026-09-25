using System;

namespace InkFlow.Meta
{
    /// <summary>
    /// Увесь стан гравця в одному місці: файл збереження плюс живі об'єкти,
    /// з якими працюють екрани (§10).
    ///
    /// Навіщо окремий тип, а не голий <see cref="SaveFile"/>: у файлі лежать дані,
    /// а грає гра з поведінкою — гаманець із подією зміни, запас фарб, лічильник
    /// денного ліміту. Тримати і те, і те синхронно вручну в кожному екрані
    /// означало б рано чи пізно записати одне й забути інше.
    ///
    /// Правило: РАНТАЙМ — джерело правди, файл — його зліпок. <see cref="Persist"/>
    /// згортає живі об'єкти назад у файл і пише його, і робиться це в кількох
    /// чітких точках (кінець партії, покупка, фарбування, згортання застосунку),
    /// а не «десь після зміни».
    /// </summary>
    public sealed class PlayerState
    {
        private readonly ISaveStorage? _storage;

        public PlayerState(SaveFile save, EconomyData economy, ISaveStorage? storage = null,
            Core.PictureLibrary? library = null, Core.BalanceData? balance = null)
        {
            File = save ?? throw new ArgumentNullException(nameof(save));
            Economy = economy ?? EconomyData.Default;
            _storage = storage;
            Library = library ?? Core.PictureLibrary.Fallback;
            Balance = balance ?? Core.BalanceData.Default;

            File.Profile ??= new ProfileData();
            File.Wallet ??= new WalletData();
            File.Paints ??= new PaintsData();
            File.Galaxy ??= new GalaxyData();
            File.Progress ??= new ProgressData();
            File.Settings ??= new SettingsData();
            File.Collection ??= new CollectionData();
            File.Run ??= new Core.RunSnapshot();
            File.Galaxy.Placements ??= new System.Collections.Generic.List<PicturePlacement>();

            Wallet = new Wallet(File.Wallet.OilDrops);
            Paints = PaintInventory.Load(File.Paints);
            Collection = PictureCollection.Load(File.Collection);
            Rewards = new RewardCalculator(Economy);
            DailyLimit = new DailyLimitTracker(Economy);
            DailyLimit.Restore(File.Wallet.PlaysToday, File.Wallet.DayUtc);
        }

        public SaveFile File { get; }
        public EconomyData Economy { get; }
        public Wallet Wallet { get; }
        public PaintStock Paints { get; }
        public RewardCalculator Rewards { get; }
        public DailyLimitTracker DailyLimit { get; }

        /// <summary>Зібрані картинки (§5, §10). Рекорд колекції = <see cref="PictureCollection.Distinct"/>.</summary>
        public PictureCollection Collection { get; }

        /// <summary>Бібліотека й баланс, з якими читається файл: зліпок забігу говорить назвами картинок і форм.</summary>
        public Core.PictureLibrary Library { get; }
        public Core.BalanceData Balance { get; }

        /// <summary>
        /// Перерваний забіг (§9), якщо його можна продовжити цією грою; null — починати новий.
        /// Зліпок з іншої версії (картинку прибрали, сітка інша) тихо ігнорується.
        /// </summary>
        public Core.RunSnapshot? SavedRun =>
            Core.RunSession.CanRestore(File.Run, Library, Core.PieceCatalogData.Default, Balance) ? File.Run : null;

        public ProgressData Progress => File.Progress;
        public GalaxyData Galaxy => File.Galaxy;

        public string Nick
        {
            get => File.Profile.Nick is null || File.Profile.Nick.Length == 0
                ? ProfileData.DefaultNick
                : File.Profile.Nick;
            set => File.Profile.Nick = value is null || value.Length == 0
                ? ProfileData.DefaultNick
                : value;
        }

        /// <summary>Стан щойно створеного гравця: усе по нулях, крім явно виданого стартового.</summary>
        public static PlayerState NewPlayer(EconomyData economy, ISaveStorage? storage = null,
            Core.PictureLibrary? library = null, Core.BalanceData? balance = null)
        {
            var state = new PlayerState(new SaveFile(), economy, storage, library, balance);

            if (economy.StarterOil > 0)
                state.Wallet.Add(economy.StarterOil, RewardSource.Debug);

            // Стартова фарба — базовий океанський тон: він же перший у палітрі.
            if (economy.StarterPaintLiters > 0f)
                state.Paints.Set(Core.PaintKind.Ocean, economy.StarterPaintLiters);

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
            File.Wallet.DayUtc = DailyLimit.CurrentDayUtc.ToString("yyyy-MM-dd");
            PaintInventory.Save(Paints, File.Paints);
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
        /// питанням того, який екран що не забув.
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

        /// <summary>§9: інтерстиціал раз на N забігів — після забігу з номером, кратним N.</summary>
        public bool ShouldShowInterstitial =>
            Economy.InterstitialEveryRuns > 0 && Progress.RunsPlayed > 0 &&
            Progress.RunsPlayed % Economy.InterstitialEveryRuns == 0;

        /// <summary>Ставить картинку з колекції на планету (§10) і зберігає. Лише зібрані.</summary>
        public bool PlacePicture(string planetId, string pictureId, float longitude, float latitude)
        {
            if (!Collection.Has(pictureId))
                return false;
            GalaxyState.Place(Galaxy, planetId, pictureId, longitude, latitude);
            Persist();
            return true;
        }

        /// <summary>Знімає останню поставлену на планету картинку.</summary>
        public bool RemoveLastPlacement(string planetId)
        {
            if (!GalaxyState.RemoveLastPlacement(Galaxy, planetId))
                return false;
            Persist();
            return true;
        }

        /// <summary>
        /// Фарбування зони: списує літри, записує факт у галактику, зберігає.
        /// Повертає false і НЕ змінює нічого, якщо фарби не вистачає.
        /// </summary>
        public bool PaintZone(PlanetSurface surface, PlanetZone zone, Core.PaintKind paint)
        {
            if (surface == null || zone == null)
                return false;
            if (!Paints.Spend(paint, zone.Cost))
                return false;

            zone.Painted = paint;
            GalaxyState.Paint(Galaxy, GalaxyState.PlanetId(surface.Type), zone.Id, paint);
            Persist();
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
