using System;
using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Промт Сесії 2: «жодна сутність старого ядра не викликається під час забігу» —
    /// доведено тим, що їх не існує в збірці Core. Список — з таблиці звіту Сесії 1;
    /// Сесія 3 додала сутності економіки фарби (баки, змішувач, зони).
    /// </summary>
    public sealed class OldCoreAbsenceTests
    {
        private static readonly string[] ForbiddenTypes =
        {
            "GridModel", "Cell", "CellFlags", "CellSeed", "BurstResolver", "MergeRules", "MoveValidator",
            "DeadlockDetector", "BossModel", "BossAction", "BossActionType", "BossSession",
            "GameSession", "PuzzleSession", "EndlessSession", "DropQueue", "QueuedDrop",
            "SwipeGesture", "GestureResult", "GestureOutcome", "Direction", "BoardJitter",
            "ScoreCalculator", "StarCalculator", "LevelSolver", "SolveReport",
            "LevelData", "EndlessData", "StarterLevels", "PuzzleGoal", "ReplayMove",
            // Сесія 3: баки, змішувач і зони пішли разом із фарбою.
            "TankSet", "Mixer", "Hue", "Hues", "Pigment", "Pigments", "PictureDef", "ZoneDef", "ThemeDef",
            "PictureCatalogData", "MixResult"
        };

        private static readonly string[] ForbiddenEvents =
        {
            "Merge", "Burst", "Paint", "Grow", "Blur", "Repaint", "Splash", "Thaw", "BlotCleared",
            "OutOfBounds", "BossHit", "BossSegmentPainted", "BossSegmentRepainted", "BossAction",
            "BossTelegraph", "Refill", "ChainTruncated", "Deadlock",
            "PaintPoured", "TankDrained", "MixerFired", "ZoneFilled", "SplashMissed"
        };

        [Test]
        public void CoreAssembly_HasNoOldCoreTypes()
        {
            var forbidden = new HashSet<string>(ForbiddenTypes, StringComparer.Ordinal);
            var offenders = new List<string>();
            foreach (var type in typeof(RunSession).Assembly.GetTypes())
                if (type.Namespace == "InkFlow.Core" && forbidden.Contains(type.Name))
                    offenders.Add(type.Name);

            Assert.AreEqual(0, offenders.Count, "у Core лишились типи старого ядра: " + string.Join(", ", offenders));
        }

        [Test]
        public void GameEventType_HasNoOldCoreEvents()
        {
            var names = new HashSet<string>(Enum.GetNames(typeof(GameEventType)), StringComparer.Ordinal);
            foreach (var old in ForbiddenEvents)
                Assert.IsFalse(names.Contains(old), $"подія старого ядра {old} досі в GameEventType");
        }

        [Test]
        public void GameEvent_CarriesPaletteIndicesNotPigments()
        {
            Assert.AreEqual(typeof(byte), typeof(GameEvent).GetProperty("Color")!.PropertyType);
            Assert.AreEqual(typeof(byte), typeof(PieceDef).GetProperty("Color")!.PropertyType);
        }

        [Test]
        public void InkColor_IsUntouched()
        {
            // Палітра інтерфейсу лишається як була — заміна її на пігменти зламала б Style/UI.
            var names = Enum.GetNames(typeof(InkColor));
            Assert.AreEqual(7, names.Length);
            Assert.IsTrue(Array.IndexOf(names, "Magenta") >= 0);
            Assert.IsTrue(Array.IndexOf(names, "Rose") >= 0);
        }
    }
}
