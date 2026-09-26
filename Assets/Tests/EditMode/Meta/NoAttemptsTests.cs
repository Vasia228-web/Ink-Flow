using System;
using System.Collections.Generic;
using System.Reflection;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Документ §9: системи спроб немає — ані лічильників, ані збереження незавершених.
    /// Доведено відсутністю сутностей у Core і Meta, а не переглядом коду.
    /// </summary>
    public sealed class NoAttemptsTests
    {
        private static RunSession NewSession(uint seed) =>
            new RunSession(BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(seed), TestLibrary.Real);

        private static readonly string[] ForbiddenTypes = { "UnfinishedPicture", "UnfinishedStore", "UnfinishedData", "PictureStart" };

        [Test]
        public void NoUnfinishedTypesInCoreOrMeta()
        {
            var forbidden = new HashSet<string>(ForbiddenTypes, StringComparer.Ordinal);
            var offenders = new List<string>();
            foreach (var assembly in new[] { typeof(RunSession).Assembly, typeof(PlayerState).Assembly })
                foreach (var type in assembly.GetTypes())
                    if (forbidden.Contains(type.Name))
                        offenders.Add(type.FullName ?? type.Name);
            Assert.AreEqual(0, offenders.Count, "сутності спроб лишились: " + string.Join(", ", offenders));
        }

        [Test]
        public void NoAttemptCountersInBalanceSessionOrSave()
        {
            foreach (var type in new[] { typeof(BalanceData), typeof(RunSession), typeof(RunSnapshot), typeof(SaveFile), typeof(PictureProgress) })
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var name = member.Name;
                    Assert.IsFalse(name.IndexOf("Unfinished", StringComparison.OrdinalIgnoreCase) >= 0
                                   || name.IndexOf("Attempt", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("Tray", StringComparison.OrdinalIgnoreCase) < 0,
                        $"{type.Name}.{name} — слід системи спроб");
                }
        }

        [Test]
        public void LostRun_LosesThePictureAndTheNextRunDrawsAFreshOne()
        {
            // Програш = картинка втрачена: наступна сесія починає нову картинку з нуля.
            var lost = NewSession(21u);
            var bot = new RunBot();
            var guard = 0;
            while (!lost.IsOver && guard++ < 5000 && bot.TryChooseMove(lost, out var index, out var anchor))
                lost.TryPlace(index, anchor);
            Assert.IsTrue(lost.IsOver);

            var next = NewSession(22u);
            Assert.AreEqual(0, next.Picture.FilledCount, "нова картинка чиста — нічого не переноситься");
        }

        [Test]
        public void Interruption_IsNotLoss_TheSnapshotKeepsThePicture()
        {
            var session = NewSession(4242u);
            var bot = new RunBot();
            for (var i = 0; i < 15 && !session.IsOver && bot.TryChooseMove(session, out var index, out var anchor); i++)
                session.TryPlace(index, anchor);
            var snapshot = new RunSnapshot();
            session.Capture(snapshot);
            var restored = new RunSession(BalanceData.Default, PieceCatalogData.Default, TestLibrary.Real, snapshot);
            Assert.AreEqual(session.Picture.Picture.Id, restored.Picture.Picture.Id);
            Assert.AreEqual(session.Picture.FilledCount, restored.Picture.FilledCount, "перерваний забіг зберігає картинку з кроками");
        }
    }
}
