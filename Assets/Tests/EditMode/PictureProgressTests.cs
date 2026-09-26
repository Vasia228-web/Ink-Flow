using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §5: піксель кольору X заповнюється лише кольором X, у порядку проявлення; лишок згорає.</summary>
    public sealed class PictureProgressTests
    {
        private static PixelPicture TwoColors() => TestBoard.Picture("two", Rarity.Common,
            "kkkk",
            "krrk",
            "kbbk",
            "kbbk",
            "kkkk");

        [Test]
        public void FillOne_FollowsTheRevealOrderOfItsColor()
        {
            var picture = TwoColors();
            var progress = new PictureProgress(picture, 0);
            Assert.AreEqual(6, progress.Total);
            Assert.AreEqual(4, progress.Remaining(TestBoard.Blue));
            Assert.AreEqual(2, progress.Remaining(TestBoard.Red));
            Assert.AreEqual(0f, progress.FilledFraction, 1e-6);

            var first = progress.FillOne(TestBoard.Blue);
            Assert.AreEqual(0, first, "перший синій — перший крок у порядку проявлення");
            Assert.AreEqual(picture.RevealOrder[0], picture.StepPixel(first, 0));
            Assert.IsTrue(progress.IsFilled(first));
            Assert.IsTrue(progress.IsPixelFilled(picture.RevealOrder[0]));
            Assert.AreEqual(3, progress.Remaining(TestBoard.Blue));

            var red = progress.FillOne(TestBoard.Red);
            Assert.AreEqual(TestBoard.Red, picture.StepColor(red), "червоний іде лише в червоний крок");
            Assert.AreEqual(2, progress.FilledCount);
            Assert.AreEqual(2f / 6f, progress.FilledFraction, 1e-6);
        }

        [Test]
        public void FillOne_ReturnsMinusOneWhenTheColorIsNotNeeded()
        {
            var progress = new PictureProgress(TwoColors(), 0);
            Assert.AreEqual(-1, progress.FillOne(TestBoard.Green), "кольору немає в картинці — згорає");
            progress.FillOne(TestBoard.Red);
            progress.FillOne(TestBoard.Red);
            Assert.AreEqual(-1, progress.FillOne(TestBoard.Red), "червоні закінчились");
            Assert.AreEqual(0, progress.Remaining(TestBoard.Red));
            Assert.IsFalse(progress.IsComplete);
        }

        [Test]
        public void MostNeeded_AndRemainingColors_TrackTheLeftovers()
        {
            var progress = new PictureProgress(TwoColors(), 0);
            Assert.AreEqual(TestBoard.Blue, progress.MostNeededColor());
            var colors = new List<byte>();
            var weights = new List<int>();
            progress.RemainingColors(colors, weights);
            Assert.AreEqual(2, colors.Count);
            Assert.AreEqual(TestBoard.Blue, colors[0]);
            Assert.AreEqual(TestBoard.Red, colors[1]);
            Assert.AreEqual(4, weights[0]);
            Assert.AreEqual(2, weights[1]);

            for (var i = 0; i < 3; i++)
                progress.FillOne(TestBoard.Blue);
            Assert.AreEqual(TestBoard.Red, progress.MostNeededColor(), "лишилось 1 синій, 2 червоні");
            progress.FillOne(TestBoard.Blue);
            progress.RemainingColors(colors, weights);
            Assert.AreEqual(1, colors.Count, "вичерпаний колір зникає з лотка (§5)");
            Assert.AreEqual(TestBoard.Red, colors[0]);
        }

        [Test]
        public void FillAll_CompletesInRevealOrder()
        {
            var picture = TwoColors();
            var progress = new PictureProgress(picture, 0);
            progress.FillOne(TestBoard.Blue);
            var now = new List<int>();
            progress.FillAll(now);
            Assert.AreEqual(5, now.Count, "лише ті, яких ще не було");
            Assert.IsTrue(progress.IsComplete);
            Assert.AreEqual(Board.Empty, progress.MostNeededColor());
            Assert.AreEqual(1f, progress.FilledFraction, 1e-6);
        }

        [Test]
        public void FilledIndices_RoundTripThroughRestore()
        {
            var picture = TwoColors();
            var progress = new PictureProgress(picture, 3);
            progress.FillOne(TestBoard.Blue);
            progress.FillOne(TestBoard.Red);
            var saved = new List<int>();
            progress.FilledIndices(saved);
            Assert.AreEqual(2, saved.Count);

            var restored = new PictureProgress(picture, 3);
            restored.Restore(saved);
            Assert.AreEqual(2, restored.FilledCount);
            Assert.AreEqual(3, restored.Remaining(TestBoard.Blue));
            Assert.AreEqual(1, restored.Remaining(TestBoard.Red));
            Assert.AreEqual(3, restored.LibraryIndex);
            foreach (var i in saved)
                Assert.IsTrue(restored.IsFilled(i));

            // Наступний синій після відновлення — не той, що вже є.
            var next = restored.FillOne(TestBoard.Blue);
            Assert.IsFalse(saved.Contains(next));
        }

        [Test]
        public void Restore_IgnoresGarbage()
        {
            var progress = new PictureProgress(TwoColors(), 0);
            progress.Restore(new List<int> { -1, 999, 0, 0, 5, 5 }); // кроків 6: 0..5; сміття й дублікати — геть
            Assert.AreEqual(2, progress.FilledCount, "дублікати й сміття пропущено");
            Assert.AreEqual(6 - 2, progress.Remaining(TestBoard.Blue) + progress.Remaining(TestBoard.Red));
        }
    }
}
