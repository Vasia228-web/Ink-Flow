using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class MixerTests
    {
        private static readonly BalanceData Balance = BalanceData.Default;

        [Test]
        public void Resolve_OneDominantColourGivesItself()
        {
            Assert.AreEqual(Hue.Blue, Mixer.Resolve(8, 0, 0, Balance));
            Assert.AreEqual(Hue.Blue, Mixer.Resolve(6, 1, 1, Balance), "6 з 8 = 75 % — домінує");
            Assert.AreEqual(Hue.Red, Mixer.Resolve(1, 5, 2, Balance), "5 з 8 = 62.5 % — домінує");
            Assert.AreEqual(Hue.Yellow, Mixer.Resolve(0, 3, 5, Balance));
        }

        [Test]
        public void Resolve_TwoColoursMixPhysically()
        {
            Assert.AreEqual(Hue.Green, Mixer.Resolve(4, 0, 4, Balance), "синій + жовтий = зелений");
            Assert.AreEqual(Hue.Orange, Mixer.Resolve(0, 4, 4, Balance), "червоний + жовтий = помаранчевий");
            Assert.AreEqual(Hue.Purple, Mixer.Resolve(4, 4, 0, Balance), "синій + червоний = фіолетовий");
            Assert.AreEqual(Hue.Green, Mixer.Resolve(4, 1, 3, Balance), "червоного 12.5 % — не помічається");
        }

        [Test]
        public void Resolve_ThreeNotableColoursGiveBrown()
        {
            Assert.AreEqual(Hue.Brown, Mixer.Resolve(3, 3, 2, Balance));
            Assert.AreEqual(Hue.Brown, Mixer.Resolve(4, 2, 2, Balance), "50 % не домінує, а решта по 25 % помітні");
        }

        [Test]
        public void Resolve_EmptyIsNone()
        {
            Assert.AreEqual(Hue.None, Mixer.Resolve(0, 0, 0, Balance));
        }

        [Test]
        public void Fire_TakesTheSplashProportionallyAndKeepsTheRatio()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Blue, 12);
            tanks.Pour(Pigment.Yellow, 4);
            var mixer = new Mixer(Balance);
            Assert.IsTrue(mixer.CanFire(tanks));
            Assert.AreEqual(Hue.Blue, mixer.Preview(tanks), "3:1 — синій домінує (75 %)");

            var taken = new int[3];
            var splash = mixer.Fire(tanks, taken);

            Assert.AreEqual(8, splash.Amount);
            Assert.AreEqual(Hue.Blue, splash.Hue);
            Assert.AreEqual(6, taken[0], "12 × 8 / 16");
            Assert.AreEqual(0, taken[1]);
            Assert.AreEqual(2, taken[2], "4 × 8 / 16");
            Assert.AreEqual(6, tanks[Pigment.Blue]);
            Assert.AreEqual(2, tanks[Pigment.Yellow]);
            Assert.AreEqual(8, tanks.Total, "залишок — рівно один виплеск тієї ж пропорції");
            Assert.AreEqual(Hue.Blue, mixer.Preview(tanks));
        }

        [Test]
        public void Fire_HueComesFromTheProportionThePlayerSaw()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Blue, 9);
            tanks.Pour(Pigment.Yellow, 7);
            var mixer = new Mixer(Balance);
            Assert.AreEqual(Hue.Green, mixer.Preview(tanks), "56 % синього не домінує, жовтий помітний");

            var taken = new int[3];
            var splash = mixer.Fire(tanks, taken);

            Assert.AreEqual(5, taken[0], "4.5 → остача, нічия з жовтим → перший за порядком");
            Assert.AreEqual(3, taken[2]);
            Assert.AreEqual(Hue.Green, splash.Hue, "хоч у взятому 5:3 = 62.5 % синього — відтінок той, що показувало прев'ю");
        }

        [Test]
        public void Fire_RoundsByLargestRemainderAndNeverOverdraws()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Blue, 5);
            tanks.Pour(Pigment.Red, 3);
            tanks.Pour(Pigment.Yellow, 1);
            var mixer = new Mixer(Balance);
            var taken = new int[3];

            mixer.Fire(tanks, taken); // 9 → 8: частки 4.44 / 2.67 / 0.89

            Assert.AreEqual(8, taken[0] + taken[1] + taken[2]);
            Assert.AreEqual(4, taken[0]);
            Assert.AreEqual(3, taken[1], "остача червоного найбільша — йому +1");
            Assert.AreEqual(1, taken[2], "остача жовтого друга — йому +1");
            Assert.AreEqual(1, tanks.Total);
            Assert.IsFalse(mixer.CanFire(tanks));
        }

        [Test]
        public void Fire_RefusesBelowTheThreshold()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Red, 7);
            var mixer = new Mixer(Balance);
            Assert.IsFalse(mixer.CanFire(tanks));
            Assert.AreEqual(0.875f, mixer.Fill(tanks), 1e-6);
            Assert.Throws<System.InvalidOperationException>(() => mixer.Fire(tanks, new int[3]));
        }

        [Test]
        public void Hues_SecondaryPairsAreSymmetric()
        {
            Assert.AreEqual(Hue.Green, Hues.Secondary(Pigment.Blue, Pigment.Yellow));
            Assert.AreEqual(Hue.Green, Hues.Secondary(Pigment.Yellow, Pigment.Blue));
            Assert.AreEqual(Hue.Purple, Hues.Secondary(Pigment.Red, Pigment.Blue));
            Assert.AreEqual(Hue.Orange, Hues.Secondary(Pigment.Yellow, Pigment.Red));
            Assert.AreEqual(Hue.Red, Hues.Secondary(Pigment.Red, Pigment.Red));
            Assert.AreEqual(Hue.Blue, Hues.Of(Pigment.Blue));
            Assert.IsTrue(Hues.IsBase(Hue.Yellow));
            Assert.IsTrue(Hues.IsSecondary(Hue.Purple));
            Assert.IsFalse(Hues.IsSecondary(Hue.Brown));
        }
    }
}
