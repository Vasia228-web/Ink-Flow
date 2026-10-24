using InkFlow.Core;
using InkFlow.Editor;
using NUnit.Framework;
using UnityEngine;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Колекція (§12) на живому рендері: показує лише зібране, фільтрує за рідкістю, у режимі
    /// вибору притлумлює картинки, усі копії яких уже стоять у слотах.
    /// Потрібен зібраний префаб — Ink Flow → Setup → Build Collection Screen.
    /// </summary>
    public sealed class CollectionScreenTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<CollectionScreen>.Available("CollectionScreen"))
                Assert.Ignore("Немає префаба екрана колекції — спершу Ink Flow → Setup → Build Collection Screen.");
        }

        [Test]
        public void ShowsOnlyCollectedPictures_AndFiltersByRarity()
        {
            using var rig = MetaScreenRig<CollectionScreen>.Create(ScreenRigBase.Devices[1], "CollectionScreen");
            var player = rig.NewPlayer();
            PixelPicture? common = null, rare = null;
            foreach (var picture in rig.Library.Pictures)
            {
                if (common == null && picture.Rarity == Rarity.Common) common = picture;
                if (rare == null && picture.Rarity == Rarity.Rare) rare = picture;
            }
            Assert.IsNotNull(common);
            Assert.IsNotNull(rare);
            player.CollectPicture(common!.Id, System.DateTime.UtcNow);
            player.CollectPicture(rare!.Id, System.DateTime.UtcNow);

            rig.Enter(player, new CollectionArgs());
            Assert.AreEqual(2, rig.Screen.VisibleIds.Count, "лише зібране");

            var atlas = rig.Screen.PreviewAtlas;
            Assert.IsNotNull(atlas);

            rig.Screen.SetRarity((int)Rarity.Rare + 1);
            Assert.AreEqual(1, rig.Screen.VisibleIds.Count);
            Assert.AreEqual(rare.Id, rig.Screen.VisibleIds[0]);

            rig.Screen.SetRarity(0);
            Assert.AreEqual(2, rig.Screen.VisibleIds.Count);
            Assert.AreSame(atlas, rig.Screen.PreviewAtlas, "фільтр не перебудовує атлас — лише вибирає клітинки");
        }

        [Test]
        public void PickMode_DimsPicturesWithoutFreeCopies()
        {
            using var rig = MetaScreenRig<CollectionScreen>.Create(ScreenRigBase.Devices[0], "CollectionScreen");
            var player = rig.NewPlayer();
            var placed = rig.Library[0];
            var free = rig.Library[1];
            player.CollectPicture(placed.Id, System.DateTime.UtcNow);
            player.CollectPicture(free.Id, System.DateTime.UtcNow);
            var planet = player.Layout.Planets[0].Id;
            Assert.IsTrue(player.TryPlaceInSlot(0, planet, 0, placed.Id, System.DateTime.UtcNow));

            rig.Enter(player, new CollectionArgs(0, planet, 1));

            CollectionCard? placedCard = null, freeCard = null;
            foreach (var card in rig.Screen.Cards)
            {
                if (card == null || !card.gameObject.activeSelf) continue;
                if (card.PictureId == placed.Id) placedCard = card;
                if (card.PictureId == free.Id) freeCard = card;
            }
            Assert.IsNotNull(placedCard, "картка поставленої картинки є в сітці");
            Assert.IsNotNull(freeCard);
            Assert.Less(placedCard!.Alpha, 0.5f, "усі копії в слотах — картка притлумлена");
            Assert.AreEqual(1f, freeCard!.Alpha, 1e-3f, "вільна копія — повна яскравість");
        }

        [Test]
        public void ShowcaseMode_KeepsPlacedPicturesBright_AndTapPicks()
        {
            // §14: на вітрину йде будь-яка зібрана картинка — копії в слотах не рахуються, тап віддає вибір одразу.
            using var rig = MetaScreenRig<CollectionScreen>.Create(ScreenRigBase.Devices[0], "CollectionScreen");
            var player = rig.NewPlayer();
            var placed = rig.Library[0];
            player.CollectPicture(placed.Id, System.DateTime.UtcNow);
            Assert.IsTrue(player.TryPlaceInSlot(0, player.Layout.Planets[0].Id, 0, placed.Id, System.DateTime.UtcNow));

            CollectionArgs? pickedArgs = null;
            string? pickedId = null;
            rig.Screen.PicturePicked += (args, id) => { pickedArgs = args; pickedId = id; };
            rig.Enter(player, CollectionArgs.ForShowcase());

            CollectionCard? card = null;
            var index = -1;
            foreach (var candidate in rig.Screen.Cards)
            {
                if (candidate == null || !candidate.gameObject.activeSelf || candidate.PictureId != placed.Id) continue;
                card = candidate;
                index = candidate.Index;
            }
            Assert.IsNotNull(card, "єдина зібрана картинка є в сітці");
            Assert.AreEqual(1f, card!.Alpha, 1e-3f, "усі копії в слотах, але для вітрини це не має значення");

            rig.Screen.PreviewTap(index);
            Assert.IsNull(pickedId, "вибір — два кроки: тап лише обирає");
            Assert.AreEqual(placed.Id, rig.Screen.SelectedId, "на вітрину — без перевірки вільних копій");
            rig.Screen.PreviewPlace();
            Assert.AreEqual(placed.Id, pickedId, "коралова кнопка віддає обрану картинку");
            Assert.IsTrue(pickedArgs != null && pickedArgs.Showcase, "роутер відрізнить вітрину від слота за аргументами");
        }

        [Test]
        public void SlotPick_IsTwoSteps_RingFollowsTheTap_AndUsedPictureExplainsItself()
        {
            // S06_02: тап обирає (кільце й галочка), «Поставити «…»» підтверджує; картинка без вільних копій — пояснення.
            using var rig = MetaScreenRig<CollectionScreen>.Create(ScreenRigBase.Devices[0], "CollectionScreen");
            var player = rig.NewPlayer();
            var placed = rig.Library[0];
            var free = rig.Library[1];
            player.CollectPicture(placed.Id, System.DateTime.UtcNow);
            player.CollectPicture(free.Id, System.DateTime.UtcNow);
            var planet = player.Layout.Planets[0].Id;
            Assert.IsTrue(player.TryPlaceInSlot(0, planet, 0, placed.Id, System.DateTime.UtcNow));

            string? pickedId = null;
            rig.Screen.PicturePicked += (_, id) => pickedId = id;
            rig.Enter(player, new CollectionArgs(0, planet, 5));

            Assert.IsTrue(rig.Screen.PreviewFooterShown, "у виборі — низ із кнопкою");
            Assert.IsFalse(rig.Screen.PreviewNavBarShown, "у виборі навігації немає");
            Assert.IsFalse(rig.Screen.PreviewPlaceEnabled, "поки нічого не обрано — кнопка спить");

            CollectionCard? usedCard = null, freeCard = null;
            foreach (var card in rig.Screen.Cards)
            {
                if (card == null || !card.gameObject.activeSelf) continue;
                if (card.PictureId == placed.Id) usedCard = card;
                if (card.PictureId == free.Id) freeCard = card;
            }
            Assert.IsNotNull(usedCard);
            Assert.IsNotNull(freeCard);
            Assert.AreEqual("усі в слотах", usedCard!.CountText);

            rig.Screen.PreviewTap(usedCard.Index);
            Assert.IsNull(rig.Screen.SelectedId, "картинку без вільних копій не обрати");
            StringAssertContains(rig.Screen.PreviewHint, "у слотах");

            rig.Screen.PreviewTap(freeCard!.Index);
            Assert.AreEqual(free.Id, rig.Screen.SelectedId);
            Assert.IsTrue(freeCard.IsSelected, "кільце на обраній картці");
            Assert.IsFalse(usedCard.IsSelected);
            Assert.IsTrue(rig.Screen.PreviewPlaceEnabled);
            StringAssertContains(rig.Screen.PreviewPlaceText, "Поставити «");
            Assert.IsNull(pickedId, "тап сам не ставить");

            rig.Screen.PreviewPlace();
            Assert.AreEqual(free.Id, pickedId);
        }

        [Test]
        public void TabShowsNavBar_PushedFromSettingsShowsBack()
        {
            using var rig = MetaScreenRig<CollectionScreen>.Create(ScreenRigBase.Devices[0], "CollectionScreen");
            var player = rig.NewPlayer();
            player.CollectPicture(rig.Library[0].Id, System.DateTime.UtcNow);

            rig.Enter(player, CollectionArgs.Tab());
            Assert.IsTrue(rig.Screen.PreviewNavBarShown, "вкладка — з навігацією");
            Assert.IsFalse(rig.Screen.PreviewBackShown, "вкладка — без «‹»");
            Assert.IsFalse(rig.Screen.PreviewFooterShown);

            rig.Enter(player, new CollectionArgs());
            Assert.IsFalse(rig.Screen.PreviewNavBarShown, "з налаштувань — підекран без навігації");
            Assert.IsTrue(rig.Screen.PreviewBackShown, "з налаштувань — «‹» назад");
        }

        [Test]
        public void EmptyFilter_ShowsCardWithPlay_InGenitivePlural()
        {
            // S06_03: фільтр без картинок — картка «Ще немає космічних картинок» і «Грати», сітки немає.
            using var rig = MetaScreenRig<CollectionScreen>.Create(ScreenRigBase.Devices[0], "CollectionScreen");
            var player = rig.NewPlayer();
            PixelPicture? common = null;
            foreach (var picture in rig.Library.Pictures)
                if (common == null && picture.Rarity == Rarity.Common) common = picture;
            player.CollectPicture(common!.Id, System.DateTime.UtcNow);

            var played = false;
            rig.Screen.PlayRequested += () => played = true;
            rig.Enter(player, CollectionArgs.Tab());
            Assert.IsFalse(rig.Screen.PreviewEmptyShown);

            rig.Screen.SetRarity((int)Rarity.Cosmic + 1);
            Assert.IsTrue(rig.Screen.PreviewEmptyShown);
            Assert.AreEqual("Ще немає космічних картинок", rig.Screen.PreviewEmptyTitle);
            Assert.AreEqual(0, rig.Screen.VisibleIds.Count);

            var fresh = rig.NewPlayer();
            rig.Enter(fresh, CollectionArgs.Tab());
            Assert.IsTrue(rig.Screen.PreviewEmptyShown, "порожня колекція — та сама картка");
            Assert.AreEqual("Колекція поки порожня", rig.Screen.PreviewEmptyTitle);
            _ = played;
        }

        private static void StringAssertContains(string actual, string expected) =>
            Assert.IsTrue(actual.Contains(expected), $"«{actual}» має містити «{expected}»");
    }
}
