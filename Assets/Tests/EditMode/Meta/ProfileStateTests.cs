using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>§14: профіль — аватар із набору, нік за правилами, вітринна картинка з колекції.</summary>
    public sealed class ProfileStateTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

        private static PlayerState Fresh() => PlayerState.NewPlayer(EconomyData.Default);

        private static PlayerState Stored(out MemoryStorage storage)
        {
            storage = new MemoryStorage();
            return PlayerState.NewPlayer(EconomyData.Default, storage);
        }

        [Test]
        public void AvatarSet_IsOneDropPerInkColour()
        {
            Assert.AreEqual(InkColors.All.Length, AvatarSet.Count);
            Assert.AreEqual(InkColor.Magenta, AvatarSet.InkOf(0));
            Assert.AreEqual(InkColor.Magenta, AvatarSet.InkOf(-1), "поза набором — перший");
            Assert.AreEqual(InkColor.Magenta, AvatarSet.InkOf(AvatarSet.Count + 5));
            for (var i = 0; i < AvatarSet.Count; i++)
                Assert.AreEqual(i, AvatarSet.IdOf(AvatarSet.InkOf(i)));
        }

        [Test]
        public void SetAvatar_ClampsAndNotifiesOnlyOnChange()
        {
            var state = Stored(out var storage);
            var changes = 0;
            state.ProfileChanged += () => changes++;

            state.SetAvatar(3);
            Assert.AreEqual(3, state.AvatarId);
            Assert.AreEqual(3, storage.Written!.Profile.AvatarId, "у сховище одразу — убитий застосунок не відбере аватар");
            Assert.AreEqual(1, changes);

            state.SetAvatar(3);
            Assert.AreEqual(1, changes, "те саме значення — без події");

            state.SetAvatar(99);
            Assert.AreEqual(0, state.AvatarId, "поза набором — перший");

            state.File.Profile.AvatarId = 42;
            Assert.AreEqual(0, state.AvatarId, "старий файл із більшим набором читається як перший, не падає");
        }

        [Test]
        public void SetNick_AppliesTheRules_AndKeepsTheOldNickOnRefusal()
        {
            var state = Stored(out var storage);
            var changes = 0;
            state.ProfileChanged += () => changes++;

            Assert.IsTrue(state.SetNick("  Нова   Зоря ", NickRules.Default, out var verdict));
            Assert.AreEqual(NickVerdict.Ok, verdict);
            Assert.AreEqual("Нова Зоря", state.Nick, "нормалізований");
            Assert.AreEqual("Нова Зоря", storage.Written!.Profile.Nick, "у сховище одразу");
            Assert.AreEqual(1, changes);

            Assert.IsFalse(state.SetNick("Ян", NickRules.Default, out verdict));
            Assert.AreEqual(NickVerdict.TooShort, verdict);
            Assert.AreEqual("Нова Зоря", state.Nick, "відмова лишає старий нік");

            Assert.IsFalse(state.SetNick("xyйло", NickRules.Default, out verdict));
            Assert.AreEqual(NickVerdict.Offensive, verdict);
            Assert.AreEqual("Нова Зоря", state.Nick);
            Assert.AreEqual(1, changes, "відмови без події");

            Assert.IsTrue(state.SetNick("Нова Зоря", NickRules.Default, out verdict), "той самий нік — успіх");
            Assert.AreEqual(1, changes, "але без запису й події");
        }

        [Test]
        public void EnforceNickRules_ResetsAnOldNickThatBreaksTheRules()
        {
            // Старий діалог приймав будь-який нік від одного символу — у файлі може лежати «Ян» або лайка.
            var state = Stored(out var storage);
            state.Nick = "Ян";
            Assert.IsTrue(state.EnforceNickRules(NickRules.Default));
            Assert.AreEqual(ProfileData.DefaultNick, state.Nick);
            Assert.AreEqual(ProfileData.DefaultNick, storage.Written!.Profile.Nick, "у сховище одразу");

            state.Nick = "хуйло";
            Assert.IsTrue(state.EnforceNickRules(NickRules.Default));
            Assert.AreEqual(ProfileData.DefaultNick, state.Nick);

            state.Nick = "Марічка";
            Assert.IsFalse(state.EnforceNickRules(NickRules.Default), "добрий нік не чіпаємо");
            Assert.AreEqual("Марічка", state.Nick);
        }

        [Test]
        public void Showcase_SkipsPicturesTheLibraryDoesNotKnow()
        {
            // Колекція пам'ятає назви; бібліотека росте й міняється — зниклу картинку вітрина пропускає.
            var library = TestLibrary.Real;
            var state = PlayerState.NewPlayer(EconomyData.Default, null, library);
            var known = library[0].Id;
            var alsoKnown = library[1].Id;
            state.CollectPicture(known, Today);
            state.CollectPicture("no-such-picture", Today);
            Assert.AreEqual(known, state.ShowcasePictureId, "остання зібрана невідома бібліотеці — беремо попередню");

            Assert.IsTrue(state.SetShowcasePicture("no-such-picture"), "у колекції вона є — файл її пам'ятає");
            Assert.AreEqual(known, state.ShowcasePictureId, "але показуємо лише те, що бібліотека вміє намалювати");
            Assert.IsFalse(state.IsShowcaseChosen, "показана не та, що обрана");

            state.CollectPicture(alsoKnown, Today);
            Assert.IsTrue(state.SetShowcasePicture(alsoKnown));
            Assert.IsTrue(state.IsShowcaseChosen);
        }

        [Test]
        public void Showcase_FallsBackToTheLastCollectedPicture()
        {
            var library = TestLibrary.Real;
            var whale = library[0].Id;
            var comet = library[1].Id;
            var owl = library[2].Id;
            var state = PlayerState.NewPlayer(EconomyData.Default, null, library);
            Assert.IsNull(state.ShowcasePictureId, "колекція порожня — вітрини немає");
            Assert.IsFalse(state.SetShowcasePicture(whale), "незібрану не поставиш");
            Assert.IsFalse(state.SetShowcasePicture(null));

            state.CollectPicture(whale, Today);
            state.CollectPicture(comet, Today);
            state.CollectPicture(whale, Today);
            Assert.AreEqual(comet, state.ShowcasePictureId, "без вибору — остання зібрана (за першим зібранням)");
            Assert.IsFalse(state.IsShowcaseChosen, "підставлена грою, не обрана");

            var changes = 0;
            state.ProfileChanged += () => changes++;
            Assert.IsTrue(state.SetShowcasePicture(whale));
            Assert.AreEqual(whale, state.ShowcasePictureId);
            Assert.IsTrue(state.IsShowcaseChosen);
            Assert.AreEqual(whale, state.File.Profile.ShowcasePictureId, "у файл одразу");
            Assert.AreEqual(1, changes);
            Assert.IsTrue(state.SetShowcasePicture(whale));
            Assert.AreEqual(1, changes, "те саме — без події");

            state.CollectPicture(owl, Today);
            Assert.AreEqual(whale, state.ShowcasePictureId, "обрана тримається, нові зібрання її не збивають");

            state.Collection.Clear();
            state.CollectPicture(owl, Today);
            Assert.AreEqual(owl, state.ShowcasePictureId, "обраної більше немає в колекції — вітрина показує останню, не порожнє місце");
        }

        [Test]
        public void Showcase_SurvivesAReload()
        {
            var library = TestLibrary.Real;
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage, library);
            state.CollectPicture(library[0].Id, Today);
            state.CollectPicture(library[1].Id, Today);
            state.SetShowcasePicture(library[0].Id);
            state.SetAvatar(2);
            Assert.AreEqual(library[0].Id, storage.Written!.Profile.ShowcasePictureId, "у сховище одразу");

            var reloaded = new PlayerState(storage.Load(), EconomyData.Default, storage, library);
            Assert.AreEqual(library[0].Id, reloaded.ShowcasePictureId);
            Assert.AreEqual(2, reloaded.AvatarId);
        }
    }
}
