using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §4: картинка — сітка індексів палітри; контур видно одразу, заливка проявляється сусід за сусідом.</summary>
    public sealed class PixelPictureTests
    {
        [Test]
        public void Parse_ReadsHeaderAndGrid()
        {
            var picture = PixelPicture.Parse(
                "# коментар\nid: dot\nname: КРАПКА\ntheme: test\nrarity: rare\ncolors: k=1 b=16 r=8\noutline: k\ngrid:\n" +
                ".kk.\nkbbk\nkbrk\n.kk.\n");

            Assert.AreEqual("dot", picture.Id);
            Assert.AreEqual("КРАПКА", picture.Name);
            Assert.AreEqual("test", picture.ThemeId);
            Assert.AreEqual(Rarity.Rare, picture.Rarity);
            Assert.AreEqual(4, picture.Width);
            Assert.AreEqual(4, picture.Height);
            Assert.AreEqual(1, picture.Outline);
            Assert.AreEqual(8, picture.OutlineCount);
            Assert.AreEqual(4, picture.FillCount);
            Assert.AreEqual(TestBoard.Blue, picture[1, 1], "X — стовпець, Y — рядок згори");
            Assert.AreEqual(TestBoard.Red, picture[2, 2]);
            Assert.AreEqual(Board.Empty, picture[0, 0]);
            Assert.AreEqual(2, picture.FillColors.Count);
            Assert.AreEqual(TestBoard.Blue, picture.FillColors[0], "кольори заливки — за спаданням кількості");
            Assert.AreEqual(3, picture.CountOf(TestBoard.Blue));
            Assert.AreEqual(1, picture.CountOf(TestBoard.Red));
            Assert.IsTrue(picture.UsesColor(TestBoard.Red));
            Assert.IsFalse(picture.UsesColor(TestBoard.Green));
        }

        [Test]
        public void Parse_TakesTheFallbackIdFromTheFileName()
        {
            var picture = PixelPicture.Parse("name: X\ntheme: test\nrarity: common\ncolors: k=1 b=16\noutline: k\ngrid:\nkbk", "from_file");
            Assert.AreEqual("from_file", picture.Id);
        }

        [Test]
        public void Parse_RejectsBrokenFiles()
        {
            const string head = "id: x\nname: X\ntheme: test\nrarity: common\ncolors: k=1 b=16 n=3\noutline: k\ngrid:\n";
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(head + "kbq"), "символ не оголошено");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(head + "kbb\nkb"), "рвані рядки");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(head + "kkk"), "немає заливки");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(head + "knb"), "контурний колір як заливка");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse("id: x\nname: X\ntheme: test\nrarity: common\ncolors: k=1 b=16\noutline: z\ngrid:\nkb"), "контур поза colors");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse("id: x\nname: X\ntheme: test\nrarity: mythic\ncolors: k=1 b=16\noutline: k\ngrid:\nkb"), "невідома рідкість");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse("id: x\nname: X\ntheme: test\nrarity: common\ncolors: k=1 b=16\noutline: k\nsize: 3\ngrid:\nkb"), "невідомий ключ");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse("id: x\ntheme: test\nrarity: common\ncolors: k=1 b=16\noutline: k\ngrid:\nkb"), "без назви");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse("id: x\nname: X\ntheme: test\nrarity: common\ncolors: k=1 b=99\noutline: k\ngrid:\nkb"), "індекс поза палітрою");
        }

        [Test]
        public void RevealOrder_StartsAtTheBottomCenterAndGrowsByNeighbours()
        {
            var picture = TestBoard.Picture("blob", Rarity.Common,
                ".kkkk.",
                "kbbbbk",
                "kbbbbk",
                "kbbbbk",
                ".kkkk.");
            var order = picture.RevealOrder;
            Assert.AreEqual(picture.FillCount, order.Count);

            var first = order[0];
            Assert.AreEqual(3, first / picture.Width, "перший піксель — з нижнього ряду заливки");
            Assert.IsTrue(first % picture.Width == 2 || first % picture.Width == 3, "і ближче до центру");

            var seen = new HashSet<int> { first };
            for (var k = 1; k < order.Count; k++)
            {
                var i = order[k];
                Assert.IsTrue(picture.IsFillPixel(i));
                Assert.IsTrue(seen.Add(i), "кожен піксель рівно раз");
                var x = i % picture.Width;
                var y = i / picture.Width;
                var adjacent = seen.Contains(i - picture.Width) || seen.Contains(i + picture.Width)
                    || (x > 0 && seen.Contains(i - 1)) || (x < picture.Width - 1 && seen.Contains(i + 1));
                Assert.IsTrue(adjacent, $"§5: піксель ({x},{y}) не сусід уже проявлених — форма мала б рости, а не з'являтись шумом");
            }
        }

        [Test]
        public void RevealOrder_CrossesAnOutlineGapToTheNearestRegion()
        {
            // Око, відділене контуром від тіла: спершу все тіло, потім око.
            var picture = TestBoard.Picture("eye", Rarity.Common,
                "kkkkk",
                "kbkbk",
                "kkkkk",
                "kbbbk",
                "kkkkk");
            var order = picture.RevealOrder;
            Assert.AreEqual(5, order.Count);
            for (var k = 0; k < 3; k++)
                Assert.AreEqual(3, order[k] / picture.Width, "спершу нижня зв'язна область цілком");
            Assert.AreEqual(1, order[3] / picture.Width);
            Assert.AreEqual(1, order[4] / picture.Width);
        }

        [Test]
        public void NonSquarePictures_AreAllowed()
        {
            var wide = TestBoard.Picture("wide", Rarity.Common, "kbbbbbbk");
            Assert.AreEqual(8, wide.Width);
            Assert.AreEqual(1, wide.Height);
            Assert.AreEqual(8, wide.Size, "розмір — більша сторона");
        }
    }
}
