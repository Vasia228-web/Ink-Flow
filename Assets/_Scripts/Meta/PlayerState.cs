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
            Core.PictureCatalogData? pictures = null, Core.BalanceData? balance = null)
        {
            File = save ?? throw new ArgumentNullException(nameof(save));
            Economy = economy ?? EconomyData.Default;
            _storage = storage;
            Pictures = pictures ?? Core.PictureCatalogData.Default;
            Balance = balance ?? Core.BalanceData.Default;

            File.Profile ??= new ProfileData();
            File.Wallet ??= new WalletData();
            File.Paints ??= new PaintsData();
            File.Galaxy ??= new GalaxyData();
            File.Progress ??= new ProgressData();
            File.Settings ??= new SettingsData();
            File.Collection ??= new CollectionData();
            File.Collection.Unfinished ??= new UnfinishedData();
            File.Galaxy.Placements ??= new System.Collections.Generic.List<PicturePlacement>();

            Wallet = new Wallet(File.Wallet.OilDrops);
            Paints = PaintInventory.Load(File.Paints);
            Collection = PictureCollection.Load(File.Collection);
            Unfinished = UnfinishedStore.Load(File.Collection.Unfinished, Pictures, Balance);
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

        /// <summary>Колода й баланс, з якими читається файл: незавершена зберігається назвою картинки.</summary>
        public Core.PictureCatalogData Pictures { get; }
        public Core.BalanceData Balance { get; }

        /// <summary>Незавершена картинка (§7): одна, з прогресом і спробами.</summary>
        public Core.UnfinishedPicture Unfinished { get; }

        /// <summary>З чого починати наступний забіг: незавершена, якщо є (§7, гарантоване випадіння).</summary>
        public Core.PictureStart? RunStart => Unfinished.HasPicture
            ? new Core.PictureStart(Unfinished.PictureIndex, Unfinished.Filled, Unfinished.AttemptsLeft)
            : (Core.PictureStart?)null;

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
            Core.PictureCatalogData? pictures = null, Core.BalanceData? balance = null)
        {
            var state = new PlayerState(new SaveFile(), economy, storage, pictures, balance);

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
            UnfinishedStore.Save(Unfinished, Pictures, File.Collection.Unfinished);

            _storage?.Save(File);
        }

        /// <summary>Прогрес картинки на спрацювання змішувача (§7, п. 3) — одразу у файл.</summary>
        public void TrackUnfinished(int pictureIndex, System.Collections.Generic.IReadOnlyList<int> filled)
        {
            Unfinished.Track(pictureIndex, filled);
            Persist();
        }

        /// <summary>Кінець забігу: незавершена реєструється або витрачає спробу. Повертає true, якщо анульовано.</summary>
        public bool SettleUnfinished(int pictureIndex, System.Collections.Generic.IReadOnlyList<int> filled, bool wasCarried)
        {
            var annulled = Unfinished.Settle(pictureIndex, filled, wasCarried);
            Persist();
            return annulled;
        }

        /// <summary>
        /// Картинку домальовано в забігу (§5): у колекцію і одразу у файл — програш
        /// через хвилину не має відібрати те, що вже зібрано. Повертає true, якщо нова.
        /// </summary>
        public bool CollectPicture(string pictureId, DateTime utcNow)
        {
            var isNew = Collection.Add(pictureId, utcNow);
            var index = Pictures.IndexOf(pictureId);
            if (index >= 0)
                Unfinished.Complete(index);
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
            forPictures += run.CommonDone * Rewards.ForPicture(Core.Rarity.Common);
            forPictures += run.RareDone * Rewards.ForPicture(Core.Rarity.Rare);
            forPictures += run.LegendaryDone * Rewards.ForPicture(Core.Rarity.Legendary);

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

        /// <summary>§9: «безкоштовна спроба на рідкісну картинку» — повертає анульовану з однією спробою.</summary>
        public void RestoreUnfinishedAttempt(int pictureIndex, System.Collections.Generic.IReadOnlyList<int> filled)
        {
            Unfinished.Restore(pictureIndex, filled, Balance.UnfinishedAttempts - 1);
            Persist();
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

    /// <summary>Підсумок забігу, який екран віддає в Meta. Кольорів і фігур тут немає.</summary>
    public readonly struct RunSummary
    {
        public RunSummary(int score, int bestChain, int commonDone, int rareDone, int legendaryDone)
        {
            Score = score;
            BestChain = bestChain;
            CommonDone = commonDone;
            RareDone = rareDone;
            LegendaryDone = legendaryDone;
        }

        public int Score { get; }
        public int BestChain { get; }
        public int CommonDone { get; }
        public int RareDone { get; }
        public int LegendaryDone { get; }
        public int PicturesDone => CommonDone + RareDone + LegendaryDone;
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
