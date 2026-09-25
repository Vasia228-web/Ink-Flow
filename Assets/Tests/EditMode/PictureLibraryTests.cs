using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §6–7 про СПРАВЖНЮ бібліотеку Assets/_Pictures: 80+ картинок, кожна в межах
    /// своєї рідкості. Тест читає ті самі файли, що й гра, — тож зламана картинка не
    /// дійде до гравця.
    /// </summary>
    public sealed class PictureLibraryTests
    {
        [Test]
        public void Library_HasAtLeastEightyPicturesAndEveryRarity()
        {
            var library = TestBoard.RealLibrary;
            Assert.GreaterOrEqual(library.Count, 80, "§7: бібліотека з 80+ картинок");
            foreach (var rarity in Rarities.All)
                Assert.Greater(library.CountOf(rarity), 0, $"немає жодної картинки рідкості {rarity}");
            Assert.GreaterOrEqual(library.CountOf(Rarity.Common), library.CountOf(Rarity.Rare), "звичайних більше, ніж рідкісних");
            Assert.GreaterOrEqual(library.Themes.Count, 8, "тем має бути багато — колекція живе темами");
        }

        [Test]
        public void EveryPicture_FitsItsRarity()
        {
            var library = TestBoard.RealLibrary;
            var balance = BalanceData.Default;
            foreach (var picture in library.Pictures)
            {
                var max = balance.GridSizeFor(picture.Rarity);
                Assert.LessOrEqual(picture.Width, max, $"«{picture.Id}»: ширина {picture.Width} понад сітку {max} рідкості {picture.Rarity}");
                Assert.LessOrEqual(picture.Height, max, $"«{picture.Id}»: висота {picture.Height} понад сітку {max}");
                Assert.GreaterOrEqual(picture.Size, 6, $"«{picture.Id}»: замала — не читається");
                var colors = picture.FillColors.Count;
                Assert.GreaterOrEqual(colors, balance.MinColorsFor(picture.Rarity), $"«{picture.Id}»: {colors} кольорів — замало для {picture.Rarity}");
                Assert.LessOrEqual(colors, balance.MaxColorsFor(picture.Rarity), $"«{picture.Id}»: {colors} кольорів — забагато для {picture.Rarity}");
                foreach (var c in picture.FillColors)
                    Assert.IsTrue(MasterPalette.IsFill(c), $"«{picture.Id}»: колір {c} не ігровий");
                Assert.IsFalse(MasterPalette.IsFill(picture.Outline), $"«{picture.Id}»: контур мусить бути контурним кольором (1–3)");
                Assert.Greater(picture.OutlineCount, 0, $"«{picture.Id}»: без контуру форму не видно до заливки");
                Assert.IsFalse(string.IsNullOrWhiteSpace(picture.Name), $"«{picture.Id}»: без назви");
                Assert.AreNotEqual(picture.ThemeId, ThemeNames.Of(picture.ThemeId), $"«{picture.Id}»: тема «{picture.ThemeId}» без людської назви");
            }
        }

        [Test]
        public void EveryPicture_IsReadableAtAGlance()
        {
            // Головний колір не має бути контуром, а заливки має бути більше, ніж контуру,
            // інакше на екрані — чорна пляма з крапками.
            foreach (var picture in TestBoard.RealLibrary.Pictures)
                Assert.Greater(picture.FillCount, picture.OutlineCount / 2,
                    $"«{picture.Id}»: контуру ({picture.OutlineCount}) значно більше за заливку ({picture.FillCount})");
        }

        [Test]
        public void Ids_AreUniqueAndLookupWorks()
        {
            var library = TestBoard.RealLibrary;
            var seen = new HashSet<string>();
            for (var i = 0; i < library.Count; i++)
            {
                Assert.IsTrue(seen.Add(library[i].Id), $"«{library[i].Id}» двічі");
                Assert.AreEqual(i, library.IndexOf(library[i].Id));
            }
            Assert.AreEqual(-1, library.IndexOf("немає_такої"));
            Assert.IsNull(library.Find("немає_такої"));
            Assert.IsNotNull(library.Find("ghost"));
            Assert.AreEqual("ghosts", library.Find("ghost")!.ThemeId);
        }

        [Test]
        public void Library_RejectsDuplicatesAndEmptiness()
        {
            var a = TestBoard.Picture("a", Rarity.Common, "kbk");
            Assert.Throws<System.ArgumentException>(() => new PictureLibrary(new[] { a, a }));
            Assert.Throws<System.ArgumentException>(() => new PictureLibrary(new PixelPicture[0]));
        }

        [Test]
        public void Parse_SortsByIdSoIndicesAreStable()
        {
            var library = PictureLibrary.Parse(new[]
            {
                "id: zebra\nname: З\ntheme: test\nrarity: common\ncolors: k=1 b=16\noutline: k\ngrid:\nkbk",
                "id: apple\nname: Я\ntheme: test\nrarity: rare\ncolors: k=1 r=8\noutline: k\ngrid:\nkrk"
            });
            Assert.AreEqual("apple", library[0].Id);
            Assert.AreEqual("zebra", library[1].Id);
            Assert.AreEqual(1, library.CountOf(Rarity.Rare));
            Assert.AreEqual(2, library.CountOfTheme("test"));
        }

        [Test]
        public void Fallback_IsASinglePlayableHeart()
        {
            var fallback = PictureLibrary.Fallback;
            Assert.AreEqual(1, fallback.Count);
            Assert.Greater(fallback[0].FillCount, 0);
            Assert.AreEqual("Тест", ThemeNames.Of(fallback[0].ThemeId));
        }
    }
}
