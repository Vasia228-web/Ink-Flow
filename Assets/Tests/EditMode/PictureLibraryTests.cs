using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §4, §6–7 про СПРАВЖНЮ бібліотеку Assets/_Pictures: 80+ картинок, кожна ~32 px
    /// з 10–16 тонами й 4–6 родинами, кроків — стільки, скільки просить рідкість. Тест читає ті
    /// самі файли, що й гра, — тож зламана картинка не дійде до гравця.
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
                Assert.GreaterOrEqual(picture.Size, 24, $"«{picture.Id}»: замала — §4 просить ~32 px");
                Assert.GreaterOrEqual(System.Math.Min(picture.Width, picture.Height), 12, $"«{picture.Id}»: менша сторона менша за 12");

                var tones = picture.ToneCount;
                Assert.GreaterOrEqual(tones, balance.MinColorsFor(picture.Rarity), $"«{picture.Id}»: {tones} тонів — замало для {picture.Rarity}");
                Assert.LessOrEqual(tones, balance.MaxColorsFor(picture.Rarity), $"«{picture.Id}»: {tones} тонів — забагато для {picture.Rarity}");

                var families = picture.FillColors.Count;
                Assert.GreaterOrEqual(families, balance.MinFamilies, $"«{picture.Id}»: {families} родин — замало");
                Assert.LessOrEqual(families, balance.MaxFamilies, $"«{picture.Id}»: {families} родин — забагато");
                foreach (var c in picture.FillColors)
                    Assert.IsTrue(MasterPalette.IsFill(c), $"«{picture.Id}»: родина {c} не ігровий колір");

                var target = balance.StepTargetFor(picture.Rarity);
                Assert.GreaterOrEqual(picture.FillCount, target * (1f - balance.StepTolerance), $"«{picture.Id}»: {picture.FillCount} кроків — замало для {picture.Rarity} (ціль {target})");
                Assert.LessOrEqual(picture.FillCount, target * (1f + balance.StepTolerance), $"«{picture.Id}»: {picture.FillCount} кроків — забагато для {picture.Rarity} (ціль {target})");

                Assert.IsFalse(MasterPalette.IsFill(picture.Outline), $"«{picture.Id}»: контур мусить бути контурним кольором (1–3)");
                Assert.Greater(picture.OutlineCount, 0, $"«{picture.Id}»: без контуру форму не видно до заливки");
                Assert.IsFalse(string.IsNullOrWhiteSpace(picture.Name), $"«{picture.Id}»: без назви");
                Assert.AreNotEqual(picture.ThemeId, ThemeNames.Of(picture.ThemeId), $"«{picture.Id}»: тема «{picture.ThemeId}» без людської назви");
            }
        }

        [Test]
        public void EveryStep_IsOneFamilyAndOfSensibleSize()
        {
            // Крок — осмислена частина малюнка однієї родини, не шум: кожен крок одноколірний за
            // родиною, і крихітних кроків (менше половини розміру) — не більше чверті.
            foreach (var picture in TestBoard.RealLibrary.Pictures)
            {
                var tiny = 0;
                for (var s = 0; s < picture.FillCount; s++)
                {
                    var color = picture.StepColor(s);
                    var n = picture.StepLength(s);
                    Assert.Greater(n, 0, $"«{picture.Id}»: порожній крок {s}");
                    for (var k = 0; k < n; k++)
                        Assert.AreEqual(color, picture.FamilyAt(picture.StepPixel(s, k)), $"«{picture.Id}»: крок {s} змішує родини");
                    if (n * 2 < picture.Step)
                        tiny++;
                }
                Assert.LessOrEqual(tiny * 4, picture.FillCount, $"«{picture.Id}»: {tiny} крихітних кроків із {picture.FillCount}");
            }
        }

        [Test]
        public void RarerPictures_AreRicher()
        {
            // §6: рідкість — складність. У середньому в рідкіснішої більше тонів і кроків.
            var library = TestBoard.RealLibrary;
            float MeanTones(Rarity r) { var n = 0; var sum = 0f; foreach (var p in library.Pictures) if (p.Rarity == r) { n++; sum += p.ToneCount; } return n == 0 ? 0f : sum / n; }
            float MeanSteps(Rarity r) { var n = 0; var sum = 0f; foreach (var p in library.Pictures) if (p.Rarity == r) { n++; sum += p.FillCount; } return n == 0 ? 0f : sum / n; }
            Assert.Greater(MeanSteps(Rarity.Cosmic), MeanSteps(Rarity.Legendary));
            Assert.Greater(MeanSteps(Rarity.Legendary), MeanSteps(Rarity.Epic));
            Assert.Greater(MeanSteps(Rarity.Epic), MeanSteps(Rarity.Common));
            Assert.GreaterOrEqual(MeanTones(Rarity.Legendary), MeanTones(Rarity.Common));
        }

        [Test]
        public void EveryPicture_IsReadableAtAGlance()
        {
            // Заливки має бути більше, ніж контуру, інакше на екрані — чорна пляма з крапками.
            foreach (var picture in TestBoard.RealLibrary.Pictures)
                Assert.Greater(picture.FillPixelCount, picture.OutlineCount / 2,
                    $"«{picture.Id}»: контуру ({picture.OutlineCount}) значно більше за заливку ({picture.FillPixelCount})");
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
