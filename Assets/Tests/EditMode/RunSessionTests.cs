using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class RunSessionTests
    {
        [Test]
        public void NewRun_StartsWithAnEmptyBoardAndAFullTray()
        {
            var session = TestBoard.NewSession();

            Assert.AreEqual(64, session.Board.CountEmpty(), "документ §2: поле порожнє на старті");
            Assert.AreEqual(3, session.Tray.Count);
            foreach (var piece in session.Tray)
                Assert.IsFalse(piece.IsEmpty);
            Assert.AreEqual(1, session.Round);
            Assert.AreEqual(GameState.Playing, session.State);
            Assert.AreEqual(0, session.Score);
        }

        [Test]
        public void RejectedPlacement_ChangesNothing()
        {
            var session = TestBoard.NewSession();
            TestBoard.SetTray(session, TestBoard.Piece("square", Pigment.Blue));
            var before = session.Board.StateHash();

            var result = session.TryPlace(0, new GridPos(7, 7));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.Events.Count);
            Assert.AreEqual(before, session.Board.StateHash());
            Assert.IsFalse(session.Tray[0].IsEmpty, "фігура лишилась у лотку");
            Assert.AreEqual(0, session.PlacementCount);
        }

        [Test]
        public void EmptySlot_CannotBePlaced()
        {
            var session = TestBoard.NewSession();
            TestBoard.SetTray(session, TestBoard.Piece("2h", Pigment.Red));
            Assert.IsFalse(session.TryPlace(1, new GridPos(0, 0)).Accepted);
            Assert.IsFalse(session.TryPlace(-1, new GridPos(0, 0)).Accepted);
        }

        [Test]
        public void AcceptedPlacement_FillsCellsRemovesPieceAndScores()
        {
            var session = TestBoard.NewSession();
            TestBoard.SetTray(session, TestBoard.Piece("square", Pigment.Yellow), TestBoard.Piece("2h", Pigment.Red));

            var result = session.TryPlace(0, new GridPos(2, 3));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(Pigment.Yellow, session.Board[2, 3]);
            Assert.AreEqual(Pigment.Yellow, session.Board[3, 4]);
            Assert.IsTrue(session.Tray[0].IsEmpty);
            Assert.IsFalse(session.Tray[1].IsEmpty, "лоток не поповнюється, поки не порожній");
            Assert.AreEqual(1, session.PlacementCount);
            Assert.AreEqual(4 * BalanceData.Default.ScorePerPlacedCell, session.Score);

            var placed = result.Events[0];
            Assert.AreEqual(GameEventType.PiecePlaced, placed.Type);
            Assert.AreEqual(0, placed.Extra);
            Assert.AreEqual(4, placed.CellCount);
            Assert.AreEqual(Pigment.Yellow, placed.Pigment);
        }

        [Test]
        public void PureRow_IsClearedAndPaysThePureYield()
        {
            var session = TestBoard.NewSession();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.LinesCleared);
            Assert.AreEqual(1, result.PureLinesCleared);
            Assert.AreEqual(12, result.PaintYielded);
            for (var x = 0; x < 8; x++)
                Assert.AreEqual(Pigment.None, session.Board[x, 0], $"клітинка ({x},0) мала очиститись");
            Assert.AreEqual(Pigment.Blue, session.Board[7, 1], "друга клітинка фігури лишається");

            var cleared = result.Events[1];
            Assert.AreEqual(GameEventType.LineCleared, cleared.Type);
            Assert.AreEqual(LineKind.Row, cleared.Kind);
            Assert.AreEqual(0, cleared.Extra);
            Assert.IsTrue(cleared.IsPure);
            Assert.AreEqual(12, cleared.Value);
            Assert.AreEqual(8, cleared.CellCount);
            Assert.AreEqual(new GridPos(0, 0), result.Cell(cleared, 0));

            var expected = 2 * 10 + 100 * 2;
            Assert.AreEqual(expected, session.Score, "2 клітинки + чиста лінія ×2");
        }

        [Test]
        public void MixedRow_PaysTheDominantPigmentOnly()
        {
            var session = TestBoard.NewSession();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Red));

            var result = session.TryPlace(0, new GridPos(7, 0));

            var cleared = result.Events[1];
            Assert.AreEqual(GameEventType.LineCleared, cleared.Type);
            Assert.IsFalse(cleared.IsPure);
            Assert.AreEqual(Pigment.Blue, cleared.Pigment);
            Assert.AreEqual(3, cleared.Value);
            Assert.AreEqual(2 * 10 + 100, session.Score);
        }

        [Test]
        public void RowAndColumn_ClearTogetherWithComboAndSharedCellOnce()
        {
            var session = TestBoard.NewSession();
            // Рядок 0 без (3,0); стовпець 3 без (3,0) і (3,1).
            TestBoard.FillRow(session.Board, 0, "bbb.bbbb");
            for (var y = 2; y < 8; y++)
                session.Board[3, y] = Pigment.Blue;
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));

            var result = session.TryPlace(0, new GridPos(3, 0));

            Assert.AreEqual(2, result.LinesCleared);
            Assert.AreEqual(2, session.BestChain);
            var combo = result.Events[3];
            Assert.AreEqual(GameEventType.ComboApplied, combo.Type);
            Assert.AreEqual(2, combo.Extra);
            Assert.AreEqual(1.5f, combo.Multiplier, 1e-4);

            for (var x = 0; x < 8; x++)
                Assert.AreEqual(Pigment.None, session.Board[x, 0]);
            for (var y = 0; y < 8; y++)
                Assert.AreEqual(Pigment.None, session.Board[3, y]);
            Assert.AreEqual(64, session.Board.CountEmpty(), "перетин очищено один раз, поле знову порожнє");

            // Дві чисті лінії по 12, кожна ×1.5 → 18 + 18.
            Assert.AreEqual(36, result.PaintYielded);
        }

        [Test]
        public void PureRow_PoursItsPaintIntoTheMatchingTankAndFiresTheMixer()
        {
            var session = TestBoard.NewSession();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            var poured = FindEvent(result, GameEventType.PaintPoured);
            Assert.AreEqual(Pigment.Blue, poured.Pigment, "чиста синя лінія → синій бак");
            Assert.AreEqual(12, poured.Value);
            Assert.AreEqual(12, poured.Extra, "Extra — рівень бака після наливання");

            // 12 ≥ 8 → змішувач спрацював один раз, забрав 8 синього; лишилось 4.
            Assert.AreEqual(1, result.Splashes);
            Assert.AreEqual(1, session.Splashes);
            Assert.AreEqual(Hue.Blue, result.LastSplashHue);
            Assert.AreEqual(4, session.Tanks[Pigment.Blue]);
            Assert.AreEqual(0, session.Tanks[Pigment.Red]);
            var drained = FindEvent(result, GameEventType.TankDrained);
            Assert.AreEqual(Pigment.Blue, drained.Pigment);
            Assert.AreEqual(8, drained.Value);
            Assert.AreEqual(4, drained.Extra, "Extra — рівень бака після зливу");
            var fired = FindEvent(result, GameEventType.MixerFired);
            Assert.AreEqual(8, fired.Value);
            Assert.AreEqual((int)Hue.Blue, fired.Extra);
            Assert.AreEqual(1, session.SplashesByHue[(int)Hue.Blue]);
        }

        [Test]
        public void Events_PourBeforeDrainBeforeSplash()
        {
            var session = TestBoard.NewSession();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            var cleared = IndexOf(result, GameEventType.LineCleared);
            var poured = IndexOf(result, GameEventType.PaintPoured);
            var drained = IndexOf(result, GameEventType.TankDrained);
            var fired = IndexOf(result, GameEventType.MixerFired);
            Assert.Less(cleared, poured, "спершу лінія зривається, потім фарба ллється");
            Assert.Less(poured, drained, "потім три струмені в змішувач");
            Assert.Less(drained, fired, "і аж тоді виплеск");
        }

        [Test]
        public void BigYield_FiresTheMixerSeveralTimes()
        {
            var session = TestBoard.NewSession();
            session.Tanks.Pour(Pigment.Yellow, 6);
            TestBoard.FillRow(session.Board, 0, "yyyyyyy.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Yellow));

            var result = session.TryPlace(0, new GridPos(7, 0));

            // 6 + 12 = 18 → два виплески по 8, лишилось 2.
            Assert.AreEqual(2, result.Splashes);
            Assert.AreEqual(2, result.CountEvents(GameEventType.MixerFired));
            Assert.AreEqual(2, session.Tanks.Total);
        }

        [Test]
        public void MixedTanks_GiveASecondaryHue()
        {
            var session = TestBoard.NewSession();
            session.Tanks.Pour(Pigment.Blue, 4);
            session.Tanks.Pour(Pigment.Yellow, 3);
            TestBoard.FillRow(session.Board, 0, "bbbbrrr.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Red));

            var result = session.TryPlace(0, new GridPos(7, 0));

            // Мішана 4:4 → нічия → червоний (його в баках менше) → +2. Разом 4 / 2 / 3 = 9 ≥ 8:
            // синій 44 % не домінує, червоний 22 % — нижче порогу помітності → синій + жовтий.
            Assert.AreEqual(1, result.Splashes);
            Assert.AreEqual(Hue.Green, result.LastSplashHue, "синій + жовтий = зелений, червоного замало");
        }

        [Test]
        public void TieInAMixedLine_GoesToTheEmptierTank()
        {
            var session = TestBoard.NewSession();
            session.Tanks.Pour(Pigment.Blue, 3);
            TestBoard.FillRow(session.Board, 0, "bbbbrrr.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Red));

            var result = session.TryPlace(0, new GridPos(7, 0));

            var cleared = FindEvent(result, GameEventType.LineCleared);
            Assert.AreEqual(Pigment.Red, cleared.Pigment, "4:4 — червоного в баках менше");
            Assert.AreEqual(2, session.Tanks[Pigment.Red]);
            Assert.AreEqual(3, session.Tanks[Pigment.Blue]);
            Assert.AreEqual(0, result.Splashes, "5 < 8 — змішувач мовчить");
        }

        [Test]
        public void Restart_EmptiesTanksAndSplashes()
        {
            var session = TestBoard.NewSession();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));
            session.TryPlace(0, new GridPos(7, 0));
            Assert.AreEqual(1, session.Splashes);

            session.Restart();

            Assert.IsTrue(session.Tanks.IsEmpty);
            Assert.AreEqual(0, session.Splashes);
            Assert.AreEqual(Hue.None, session.LastSplashHue);
            Assert.AreEqual(0, session.SplashesByHue[(int)Hue.Blue]);
        }

        [Test]
        public void Splash_LandsOnThePictureZoneOfItsHue()
        {
            var one = PictureCatalogData.Picture("one", "ОДНА", Rarity.Common,
                new[] { "AAAAAAAA", "AAAAAAAA", "BBBB...." },
                PictureCatalogData.Zone('A', Hue.Blue, "небо"),
                PictureCatalogData.Zone('B', Hue.Red, "земля"));
            var session = new RunSession(BalanceData.Default, PieceCatalogData.Default,
                new XorShiftRandom(7u), new PictureCatalogData(new[] { one }));
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            // Виплеск 8 синього → зона «небо» (16 клітинок, стеля 8) залита повністю.
            var filled = FindEvent(result, GameEventType.ZoneFilled);
            Assert.AreEqual(0, filled.Value);
            Assert.AreEqual(8, filled.CellCount, "скільки лягло");
            Assert.AreEqual(8, filled.Extra, "рівень зони після");
            Assert.IsTrue(filled.IsPure, "зону закінчено");
            Assert.IsTrue(session.Picture.IsZoneComplete(0));
            Assert.AreEqual(1, session.Picture.ActiveZone);
            Assert.AreEqual(Hue.Red, session.Picture.WantedHue);
            Assert.AreEqual(0, result.PaintMissed);
            Assert.IsFalse(result.Has(GameEventType.PictureCompleted));
        }

        [Test]
        public void Splash_OfAnUnneededHueIsMissed()
        {
            var one = PictureCatalogData.Picture("one", "ОДНА", Rarity.Common,
                new[] { "AAAA" }, PictureCatalogData.Zone('A', Hue.Red, "усе"));
            var session = new RunSession(BalanceData.Default, PieceCatalogData.Default,
                new XorShiftRandom(7u), new PictureCatalogData(new[] { one }));
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            var missed = FindEvent(result, GameEventType.SplashMissed);
            Assert.AreEqual(8, missed.Value);
            Assert.AreEqual((int)Hue.Blue, missed.Extra);
            Assert.AreEqual(8, session.PaintMissed);
            Assert.AreEqual(0, session.Picture.TotalFilled);
        }

        [Test]
        public void CompletedPicture_IsReplacedByAnotherOneImmediately()
        {
            var small = PictureCatalogData.Picture("small", "МАЛА", Rarity.Common,
                new[] { "AA" }, PictureCatalogData.Zone('A', Hue.Blue, "усе"));
            var other = PictureCatalogData.Picture("other", "ІНША", Rarity.Common,
                new[] { "BB" }, PictureCatalogData.Zone('B', Hue.Red, "усе"));
            var catalog = new PictureCatalogData(new[] { small, other });
            var session = new RunSession(BalanceData.Default, PieceCatalogData.Default,
                new XorShiftRandom(3u), catalog);
            var wanted = session.Picture.CatalogIndex == 0 ? Pigment.Blue : Pigment.Red;
            var row = wanted == Pigment.Blue ? "bbbbbbb." : "rrrrrrr.";
            TestBoard.FillRow(session.Board, 0, row);
            TestBoard.SetTray(session, TestBoard.Piece("2v", wanted));
            var before = session.Picture.CatalogIndex;

            var result = session.TryPlace(0, new GridPos(7, 0));

            Assert.AreEqual(1, result.PicturesCompleted);
            Assert.AreEqual(1, session.PicturesCompleted);
            Assert.AreEqual(before, FindEvent(result, GameEventType.PictureCompleted).Value);
            var started = FindEvent(result, GameEventType.PictureStarted);
            Assert.AreNotEqual(before, started.Value, "та сама не приходить двічі поспіль");
            Assert.AreEqual(started.Value, session.Picture.CatalogIndex);
            Assert.AreEqual(0, session.Picture.TotalFilled, "нова картинка чиста");
            Assert.AreEqual(0, result.PaintMissed, "стеля зони — рівно виплеск, нічого не пропало");
            Assert.Less(IndexOf(result, GameEventType.ZoneFilled), IndexOf(result, GameEventType.PictureCompleted));
            Assert.Less(IndexOf(result, GameEventType.PictureCompleted), IndexOf(result, GameEventType.PictureStarted));
        }

        [Test]
        public void Restart_DrawsAFreshPicture()
        {
            var session = TestBoard.NewSession();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", Pigment.Blue));
            session.TryPlace(0, new GridPos(7, 0));

            session.Restart();

            Assert.AreEqual(0, session.Picture.TotalFilled);
            Assert.AreEqual(0, session.PicturesCompleted);
            Assert.AreEqual(0, session.PaintMissed);
        }

        [Test]
        public void WantedPigment_FollowsTheActiveZoneAndTheTanks()
        {
            var green = PictureCatalogData.Picture("g", "З", Rarity.Common,
                new[] { "AA" }, PictureCatalogData.Zone('A', Hue.Green, "листя"));
            var session = new RunSession(BalanceData.Default, PieceCatalogData.Default,
                new XorShiftRandom(7u), new PictureCatalogData(new[] { green }));

            session.WantedPigments(out var first, out var second);
            Assert.AreEqual(Pigment.Blue, first, "порівну — перший за порядком");
            Assert.AreEqual(Pigment.Yellow, second);

            session.Tanks.Pour(Pigment.Blue, 5);
            Assert.AreEqual(Pigment.Yellow, session.WantedPigment, "синього вже досить — тягнемо жовтий");

            var red = PictureCatalogData.Picture("r", "Ч", Rarity.Common,
                new[] { "AA" }, PictureCatalogData.Zone('A', Hue.Red, "усе"));
            var pure = new RunSession(BalanceData.Default, PieceCatalogData.Default,
                new XorShiftRandom(7u), new PictureCatalogData(new[] { red }));
            Assert.AreEqual(Pigment.Red, pure.WantedPigment);
        }

        private static int IndexOf(MoveResult result, GameEventType type)
        {
            for (var i = 0; i < result.Events.Count; i++)
                if (result.Events[i].Type == type)
                    return i;
            Assert.Fail($"події {type} немає");
            return -1;
        }

        private static GameEvent FindEvent(MoveResult result, GameEventType type)
        {
            for (var i = 0; i < result.Events.Count; i++)
                if (result.Events[i].Type == type)
                    return result.Events[i];
            Assert.Fail($"події {type} немає");
            return default;
        }

        [Test]
        public void Tray_RefillsOnlyWhenAllThreeArePlaced()
        {
            var session = TestBoard.NewSession();
            TestBoard.SetTray(session,
                TestBoard.Piece("2h", Pigment.Blue), TestBoard.Piece("2h", Pigment.Red), TestBoard.Piece("2h", Pigment.Yellow));

            Assert.IsFalse(session.TryPlace(0, new GridPos(0, 7)).Has(GameEventType.TrayRefilled));
            Assert.IsFalse(session.TryPlace(1, new GridPos(0, 5)).Has(GameEventType.TrayRefilled));
            var last = session.TryPlace(2, new GridPos(0, 3));

            Assert.IsTrue(last.Has(GameEventType.TrayRefilled));
            Assert.AreEqual(2, session.Round);
            foreach (var piece in session.Tray)
                Assert.IsFalse(piece.IsEmpty);
        }

        [Test]
        public void Run_IsLostWhenNoPieceFitsAfterAMove()
        {
            var session = TestBoard.NewSession();
            // Діагональ порожня + пара сусідніх дірок у верхньому ряду. Жодна лінія
            // не зривається (у кожному рядку й стовпці лишається дірка), а після
            // ходу лишаються лише несуміжні дірки.
            TestBoard.Load(session.Board,
                "..bbbbb.",
                "b.bbbbbb",
                "bb.bbbbb",
                "bbb.bbbb",
                "bbbb.bbb",
                "bbbbb.bb",
                "bbbbbb.b",
                ".bbbbbb.");
            TestBoard.SetTray(session,
                TestBoard.Piece("2h", Pigment.Blue), TestBoard.Piece("2h", Pigment.Red), TestBoard.Piece("2v", Pigment.Yellow));

            var result = session.TryPlace(0, new GridPos(0, 7));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(GameState.Lost, session.State);
            Assert.IsTrue(session.IsOver);
            Assert.IsTrue(result.Has(GameEventType.GameLost));
            Assert.IsFalse(session.TryPlace(1, new GridPos(0, 0)).Accepted, "після програшу ходів немає");
        }

        [Test]
        public void Restart_ResetsEverythingInPlace()
        {
            var session = TestBoard.NewSession();
            TestBoard.SetTray(session, TestBoard.Piece("square", Pigment.Blue));
            session.TryPlace(0, new GridPos(0, 0));
            Assert.Greater(session.Score, 0);

            session.Restart();

            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.PlacementCount);
            Assert.AreEqual(64, session.Board.CountEmpty());
            Assert.AreEqual(1, session.Round);
            Assert.AreEqual(GameState.Playing, session.State);
            foreach (var piece in session.Tray)
                Assert.IsFalse(piece.IsEmpty);
        }

        [Test]
        public void Replay_ReproducesTheRunByteForByte()
        {
            const uint seed = 4242u;
            var original = TestBoard.NewSession(seed);
            var replay = new RunReplay(seed);
            var bot = new RunBot();

            while (!original.IsOver && bot.TryChooseMove(original, out var index, out var anchor))
            {
                replay.Record(index, anchor);
                original.TryPlace(index, anchor);
            }

            Assert.Greater(replay.Steps.Count, 5);

            var copy = TestBoard.NewSession(seed);
            replay.Replay(copy);

            Assert.AreEqual(original.Board.StateHash(), copy.Board.StateHash());
            Assert.AreEqual(original.Score, copy.Score);
            Assert.AreEqual(original.State, copy.State);
            Assert.AreEqual(original.Round, copy.Round);
        }

        [Test]
        public void Hint_PointsToAValidPlacement()
        {
            var session = TestBoard.NewSession();
            Assert.IsTrue(session.TryFindHint(out var index, out var anchor));
            Assert.IsTrue(PlacementRules.CanPlace(session.Board, session.Tray[index].Shape!, anchor));
        }

        [Test]
        public void HaloWarning_TurnsOnWhenTheBoardIsNearlyFull()
        {
            var session = TestBoard.NewSession();
            Assert.IsFalse(session.HaloWarning);
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                    if ((x + y) % 7 != 0)
                        session.Board[x, y] = Pigment.Red;
            Assert.IsTrue(session.HaloWarning);
        }

        [Test]
        public void HaloWarning_TurnsOnWhenAPieceInHandIsStuck()
        {
            var session = TestBoard.NewSession();
            // Багато вільного місця, але воно діряве: квадрат 2×2 нікуди не влазить.
            TestBoard.Load(session.Board,
                "b.b.b.b.",
                ".b.b.b.b",
                "b.b.b.b.",
                ".b.b.b.b",
                "b.b.b.b.",
                ".b.b.b.b",
                "b.b.b.b.",
                ".b.b.b.b");
            TestBoard.SetTray(session, TestBoard.Piece("square", Pigment.Yellow), TestBoard.Piece("2h", Pigment.Blue));

            Assert.AreEqual(32, session.Board.CountEmpty(), "місця вдосталь — тиск не через кількість");
            Assert.IsTrue(session.AnyPieceStuck());
            Assert.IsTrue(session.HaloWarning);
        }
    }
}
