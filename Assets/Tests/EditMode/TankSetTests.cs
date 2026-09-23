using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class TankSetTests
    {
        [Test]
        public void Pour_AddsToTheRightTankOnly()
        {
            var tanks = new TankSet(40);
            Assert.AreEqual(0, tanks.Pour(Pigment.Blue, 12));

            Assert.AreEqual(12, tanks[Pigment.Blue]);
            Assert.AreEqual(0, tanks[Pigment.Red]);
            Assert.AreEqual(0, tanks[Pigment.Yellow]);
            Assert.AreEqual(12, tanks.Total);
            Assert.AreEqual(12, tanks.TotalReceived);
        }

        [Test]
        public void Pour_OverTheCapacityIsWasted()
        {
            var tanks = new TankSet(40);
            tanks.Pour(Pigment.Red, 35);
            var wasted = tanks.Pour(Pigment.Red, 12);

            Assert.AreEqual(7, wasted);
            Assert.AreEqual(40, tanks[Pigment.Red]);
            Assert.IsTrue(tanks.IsFull(Pigment.Red));
            Assert.AreEqual(7, tanks.TotalWasted);
            Assert.AreEqual(47, tanks.TotalReceived, "лічильник рахує все, що видали лінії");
        }

        [Test]
        public void Levels_FollowThePigmentOrder()
        {
            var tanks = new TankSet(40);
            tanks.Pour(Pigment.Yellow, 3);
            tanks.Pour(Pigment.Blue, 1);
            tanks.Pour(Pigment.Red, 2);

            Assert.AreEqual(1, tanks.Levels[Pigments.IndexOf(Pigment.Blue)]);
            Assert.AreEqual(2, tanks.Levels[Pigments.IndexOf(Pigment.Red)]);
            Assert.AreEqual(3, tanks.Levels[Pigments.IndexOf(Pigment.Yellow)]);
            Assert.AreEqual(0.075f, tanks.Fraction(Pigment.Yellow), 1e-4);
        }

        [Test]
        public void Take_RemovesButNeverBelowZero()
        {
            var tanks = new TankSet(40);
            tanks.Pour(Pigment.Blue, 10);
            tanks.Take(Pigment.Blue, 4);
            Assert.AreEqual(6, tanks[Pigment.Blue]);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => tanks.Take(Pigment.Blue, 7));
        }

        [Test]
        public void Reset_EmptiesEverything()
        {
            var tanks = new TankSet(10);
            tanks.Pour(Pigment.Blue, 30);
            tanks.Reset();
            Assert.AreEqual(0, tanks.Total);
            Assert.AreEqual(0, tanks.TotalWasted);
            Assert.AreEqual(0, tanks.TotalReceived);
        }

        [Test]
        public void Pour_RejectsNonePigment()
        {
            var tanks = new TankSet(10);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => tanks.Pour(Pigment.None, 1));
        }
    }
}
