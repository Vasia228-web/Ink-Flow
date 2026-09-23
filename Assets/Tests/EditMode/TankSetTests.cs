using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class TankSetTests
    {
        [Test]
        public void Pour_AddsToTheRightTankOnly()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Blue, 12);

            Assert.AreEqual(12, tanks[Pigment.Blue]);
            Assert.AreEqual(0, tanks[Pigment.Red]);
            Assert.AreEqual(0, tanks[Pigment.Yellow]);
            Assert.AreEqual(12, tanks.Total);
            Assert.AreEqual(12, tanks.TotalReceived);
            Assert.IsFalse(tanks.IsEmpty);
        }

        [Test]
        public void Levels_FollowThePigmentOrder()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Yellow, 3);
            tanks.Pour(Pigment.Blue, 1);
            tanks.Pour(Pigment.Red, 2);

            Assert.AreEqual(1, tanks.Levels[Pigments.IndexOf(Pigment.Blue)]);
            Assert.AreEqual(2, tanks.Levels[Pigments.IndexOf(Pigment.Red)]);
            Assert.AreEqual(3, tanks.Levels[Pigments.IndexOf(Pigment.Yellow)]);
        }

        [Test]
        public void Take_RemovesButNeverBelowZero()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Blue, 10);
            tanks.Take(Pigment.Blue, 4);
            Assert.AreEqual(6, tanks[Pigment.Blue]);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => tanks.Take(Pigment.Blue, 7));
        }

        [Test]
        public void Reset_EmptiesEverything()
        {
            var tanks = new TankSet();
            tanks.Pour(Pigment.Blue, 30);
            tanks.Reset();
            Assert.IsTrue(tanks.IsEmpty);
            Assert.AreEqual(0, tanks.TotalReceived);
        }

        [Test]
        public void Pour_RejectsNonePigment()
        {
            var tanks = new TankSet();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => tanks.Pour(Pigment.None, 1));
        }
    }
}
