using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Tests
{
    /// <summary>Множина в написах: перша планета має 4 слоти, і «Усі 4 слотів» на ній — помилка, яку бачить кожен гравець.</summary>
    public sealed class PluralTests
    {
        [Test]
        public void Of_PicksTheUkrainianForm()
        {
            var cases = new (int n, string expected)[]
            {
                (1, "слот"), (2, "слоти"), (3, "слоти"), (4, "слоти"), (5, "слотів"),
                (11, "слотів"), (12, "слотів"), (14, "слотів"),
                (21, "слот"), (22, "слоти"), (24, "слоти"), (25, "слотів"),
                (111, "слотів"), (0, "слотів")
            };
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, Plural.Of(n, "слот", "слоти", "слотів"), $"n = {n}");
        }

        [Test]
        public void Count_JoinsNumberAndWord()
        {
            Assert.AreEqual("4 слоти", Plural.Count(4, "слот", "слоти", "слотів"));
            Assert.AreEqual("9 планет", Plural.Count(9, "планета", "планети", "планет"));
            Assert.AreEqual("1 планета", Plural.Count(1, "планета", "планети", "планет"));
        }
    }
}
