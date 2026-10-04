using System;
using System.IO;
using System.Text.RegularExpressions;
using InkFlow.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Запобіжник збереження: нечитабельний файл не стирається мовчки, а лишає копію поруч.
    /// Лише в Unity — сховище тягне JsonUtility, headless-раннер цей файл виключає
    /// (CoreTestRunner.csproj).
    /// </summary>
    public sealed class JsonSaveStorageTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "inkflow-save-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void Load_OfABrokenFile_StartsFresh_ButKeepsACopy()
        {
            var storage = new JsonSaveStorage(_dir);
            const string broken = "{ це не json";
            File.WriteAllText(storage.Path_, broken);
            LogAssert.Expect(LogType.Error, new Regex("Не вдалося прочитати збереження"));

            var loaded = storage.Load();

            Assert.AreEqual(SaveFile.CurrentVersion, loaded.Version);
            var copies = storage.FailedCopies();
            Assert.AreEqual(1, copies.Length);
            Assert.AreEqual(broken, File.ReadAllText(copies[0]));
            Assert.IsTrue(File.Exists(storage.Path_), "сам файл на диску не чіпаємо");
        }

        [Test]
        public void Load_OfANewerVersion_KeepsACopy_Too()
        {
            // Старіша збірка гри відкриває файл новішої: міграція відмовляється, але дані не гинуть.
            var storage = new JsonSaveStorage(_dir);
            var json = JsonUtility.ToJson(new SaveFile { Version = SaveFile.CurrentVersion + 50 });
            File.WriteAllText(storage.Path_, json);
            LogAssert.Expect(LogType.Error, new Regex("Не вдалося прочитати збереження"));

            var loaded = storage.Load();

            Assert.AreEqual(SaveFile.CurrentVersion, loaded.Version);
            var copies = storage.FailedCopies();
            Assert.AreEqual(1, copies.Length);
            Assert.AreEqual(json, File.ReadAllText(copies[0]));
        }

        [Test]
        public void Load_OfAGoodFile_LeavesNoCopies()
        {
            var storage = new JsonSaveStorage(_dir);
            storage.Save(new SaveFile());

            storage.Load();

            Assert.AreEqual(0, storage.FailedCopies().Length);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsThroughJson()
        {
            var storage = new JsonSaveStorage(_dir);
            var save = new SaveFile();
            save.Wallet.OilDrops = 777;
            save.Progress.EndlessRecord = 4321;
            save.Profile.Nick = "Тест";
            storage.Save(save);

            var loaded = storage.Load();

            Assert.AreEqual(777, loaded.Wallet.OilDrops);
            Assert.AreEqual(4321, loaded.Progress.EndlessRecord);
            Assert.AreEqual("Тест", loaded.Profile.Nick);
        }
    }
}
