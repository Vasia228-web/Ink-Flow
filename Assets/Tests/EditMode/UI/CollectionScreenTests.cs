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
    }
}
