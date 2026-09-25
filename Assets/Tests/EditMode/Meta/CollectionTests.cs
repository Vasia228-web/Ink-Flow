using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    public sealed class CollectionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Add_CountsRepeatsAndDistinctSeparately()
        {
            var collection = new PictureCollection();
            Assert.IsTrue(collection.Add("whale", Now));
            Assert.IsFalse(collection.Add("whale", Now.AddMinutes(5)), "повтор — не нова");
            Assert.IsTrue(collection.Add("comet", Now.AddMinutes(9)));

            Assert.AreEqual(2, collection.Distinct, "§8: рекорд колекції — різні картинки");
            Assert.AreEqual(3, collection.Total);
            Assert.AreEqual(2, collection.CountOf("whale"));
            Assert.AreEqual(0, collection.CountOf("dragon"));
            Assert.AreEqual(Now, collection.Get("whale")!.FirstUtc, "час першого збирання не зсувається");
            Assert.AreEqual("whale", collection.Ids[0]);
        }

        [Test]
        public void SaveAndLoad_RoundTrip()
        {
            var collection = new PictureCollection();
            collection.Add("whale", Now);
            collection.Add("whale", Now);
            collection.Add("galaxy", Now.AddDays(1));

            var data = new CollectionData();
            PictureCollection.Save(collection, data);
            Assert.AreEqual(2, data.Pictures.Count);

            var restored = PictureCollection.Load(data);
            Assert.AreEqual(2, restored.CountOf("whale"));
            Assert.AreEqual(1, restored.CountOf("galaxy"));
            Assert.AreEqual(Now.AddDays(1), restored.Get("galaxy")!.FirstUtc);
        }

        [Test]
        public void Load_SkipsBrokenRecords()
        {
            var data = new CollectionData();
            data.Pictures.Add(new CollectedPicture { PictureId = "", Count = 3 });
            data.Pictures.Add(new CollectedPicture { PictureId = "owl", Count = 0 });
            data.Pictures.Add(new CollectedPicture { PictureId = "owl", Count = 2, FirstUtc = "не дата" });
            var restored = PictureCollection.Load(data);
            Assert.AreEqual(1, restored.Distinct);
            Assert.AreEqual(2, restored.CountOf("owl"));
            Assert.AreEqual(DateTime.MinValue, restored.Get("owl")!.FirstUtc);
        }

        [Test]
        public void Migration_V3_GetsAnEmptyCollection()
        {
            var save = new SaveFile { Version = 3, Collection = null! };
            var migrated = SaveMigrations.Migrate(save);
            Assert.AreEqual(SaveFile.CurrentVersion, migrated.Version);
            Assert.IsNotNull(migrated.Collection);
            Assert.AreEqual(0, migrated.Collection.Pictures.Count);
        }

        [Test]
        public void SavedRun_SurvivesAReloadAndContinuesFromTheSamePlace()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage, TestLibrary.Real);
            Assert.IsNull(state.SavedRun, "новий гравець — забігу немає");

            var session = new RunSession(state.Balance, PieceCatalogData.Default, new XorShiftRandom(11u), state.Library, state.Collection.Has);
            var bot = new RunBot();
            for (var i = 0; i < 6 && bot.TryChooseMove(session, out var index, out var anchor); i++)
                session.TryPlace(index, anchor);
            state.SaveRun(session);
            Assert.AreEqual(1, storage.Writes, "§9: зліпок пишеться одразу");

            var reloaded = new PlayerState(storage.Load(), EconomyData.Default, storage, TestLibrary.Real);
            Assert.IsNotNull(reloaded.SavedRun);
            var restored = new RunSession(reloaded.Balance, PieceCatalogData.Default, reloaded.Library, reloaded.SavedRun!, reloaded.Collection.Has);
            Assert.AreEqual(session.Board.StateHash(), restored.Board.StateHash());
            Assert.AreEqual(session.Score, restored.Score);
            Assert.AreEqual(session.Picture.Picture.Id, restored.Picture.Picture.Id);
            Assert.AreEqual(session.Picture.FilledCount, restored.Picture.FilledCount);

            reloaded.ClearRun();
            Assert.IsNull(reloaded.SavedRun);
            Assert.IsNull(new PlayerState(storage.Load(), EconomyData.Default, storage, TestLibrary.Real).SavedRun, "програш чистить файл");
        }

        [Test]
        public void SavedRun_FromAnotherLibraryIsIgnored()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage, TestLibrary.Real);
            var session = new RunSession(state.Balance, PieceCatalogData.Default, new XorShiftRandom(5u), state.Library);
            state.SaveRun(session);

            var withoutPictures = new PlayerState(storage.Load(), EconomyData.Default, storage);
            Assert.IsNull(withoutPictures.SavedRun, "картинки немає в запасній бібліотеці — зліпок тихо пропускається");
        }

        [Test]
        public void SaveRun_OfAFinishedRunClearsInstead()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage, TestLibrary.Real);
            var session = new RunSession(state.Balance, PieceCatalogData.Default, new XorShiftRandom(21u), state.Library);
            var bot = new RunBot();
            var guard = 0;
            while (!session.IsOver && guard++ < 5000 && bot.TryChooseMove(session, out var index, out var anchor))
                session.TryPlace(index, anchor);
            Assert.IsTrue(session.IsOver);
            state.SaveRun(session);
            Assert.IsNull(state.SavedRun, "програний забіг не зберігається");
        }

        [Test]
        public void Migration_V4_AndV5_DropTheOldUnfinishedAndAddTheRun()
        {
            var save = new SaveFile { Version = 4, Run = null! };
            save.Collection.Pictures.Add(new CollectedPicture { PictureId = "whale", Count = 2, FirstUtc = Now.ToString("o") });
            save.Profile.AvatarId = 7;
            var migrated = SaveMigrations.Migrate(save);
            Assert.AreEqual(6, migrated.Version);
            Assert.IsNotNull(migrated.Run);
            Assert.IsTrue(migrated.Run.IsEmpty, "зліпка забігу в старому файлі не було");
            Assert.AreEqual(1, migrated.Collection.Pictures.Count, "колекція — лише назви, лишається");
            Assert.AreEqual(0, migrated.Profile.AvatarId);
            Assert.AreEqual(string.Empty, migrated.Profile.ShowcasePictureId);
        }

        [Test]
        public void PlayerState_CollectPicture_PersistsImmediately()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);

            Assert.IsTrue(state.CollectPicture("whale", Now));
            Assert.AreEqual(1, storage.Writes, "зібране пишеться одразу — програш нічого не відбирає");
            Assert.IsFalse(state.CollectPicture("whale", Now));

            var reloaded = new PlayerState(storage.Load(), EconomyData.Default, storage);
            Assert.AreEqual(2, reloaded.Collection.CountOf("whale"));
            Assert.AreEqual(1, reloaded.Collection.Distinct);
        }
    }
}
