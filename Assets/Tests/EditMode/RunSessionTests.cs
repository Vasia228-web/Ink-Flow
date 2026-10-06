using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class RunSessionTests
    {
        /// <summary>Картинка з трьома смугами по 24 пікселі: синя, червона, жовта — щоб лоток мав усі три кольори.</summary>
        private static PictureLibrary ThreeStripes() => TestBoard.Library(TestBoard.Picture("stripes", Rarity.Common,
            "kkkkkkkkkk",
            "kbbbbbbbbk",
            "kbbbbbbbbk",
            "kbbbbbbbbk",
            "krrrrrrrrk",
            "krrrrrrrrk",
            "krrrrrrrrk",
            "kyyyyyyyyk",
            "kyyyyyyyyk",
            "kyyyyyyyyk",
            "kkkkkkkkkk"));

        private static RunSession Stripes(uint seed = 7u) => TestBoard.NewSession(seed, null, ThreeStripes());

        [Test]
        public void NewRun_StartsWithAnEmptyBoardAndAFullTrayInPictureColors()
        {
            var session = TestBoard.NewSession();

            Assert.AreEqual(64, session.Board.CountEmpty(), "документ §2: поле порожнє на старті");
            Assert.AreEqual(3, session.Tray.Count);
            foreach (var piece in session.Tray)
            {
                Assert.IsFalse(piece.IsEmpty);
                Assert.IsTrue(session.Picture.Picture.UsesColor(piece.Color), "§5: фігури — у кольорах картинки");
            }
            Assert.AreEqual(1, session.Round);
            Assert.AreEqual(GameState.Playing, session.State);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.Picture.FilledCount);
            Assert.AreSame(TestBoard.RealLibrary, session.Library);
        }

        [Test]
        public void RejectedPlacement_ChangesNothing()
        {
            var session = Stripes();
            TestBoard.SetTray(session, TestBoard.Piece("square", TestBoard.Blue));
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
            var session = Stripes();
            TestBoard.SetTray(session, TestBoard.Piece("2h", TestBoard.Red));
            Assert.IsFalse(session.TryPlace(1, new GridPos(0, 0)).Accepted);
            Assert.IsFalse(session.TryPlace(-1, new GridPos(0, 0)).Accepted);
        }

        [Test]
        public void AcceptedPlacement_FillsCellsRemovesPieceAndScores()
        {
            var session = Stripes();
            TestBoard.SetTray(session, TestBoard.Piece("square", TestBoard.Yellow), TestBoard.Piece("2h", TestBoard.Red));

            var result = session.TryPlace(0, new GridPos(2, 3));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(TestBoard.Yellow, session.Board[2, 3]);
            Assert.AreEqual(TestBoard.Yellow, session.Board[3, 4]);
            Assert.IsTrue(session.Tray[0].IsEmpty);
            Assert.IsFalse(session.Tray[1].IsEmpty, "лоток не поповнюється, поки не порожній");
            Assert.AreEqual(1, session.PlacementCount);
            Assert.AreEqual(4 * BalanceData.Default.ScorePerPlacedCell, session.Score);

            var placed = result.Events[0];
            Assert.AreEqual(GameEventType.PiecePlaced, placed.Type);
            Assert.AreEqual(0, placed.Value);
            Assert.AreEqual(4, placed.CellCount);
            Assert.AreEqual(TestBoard.Yellow, placed.Color);
            Assert.AreEqual(0, result.PixelsFilled, "без лінії — жодного пікселя");
        }

        [Test]
        public void PureRow_IsClearedAndFillsThreePixelsPerCell()
        {
            var session = Stripes();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.LinesCleared);
            Assert.AreEqual(1, result.PureLinesCleared);
            Assert.AreEqual(24, result.PixelsFilled, "§5: 8 клітинок × 3");
            Assert.AreEqual(24, session.PixelsFilled);
            Assert.AreEqual(0, result.PixelsWasted);
            Assert.AreEqual(24, session.Picture.FilledCount);
            Assert.AreEqual(0, session.Picture.Remaining(TestBoard.Blue), "сині пікселі закінчились");
            for (var x = 0; x < 8; x++)
                Assert.AreEqual(Board.Empty, session.Board[x, 0], $"клітинка ({x},0) мала очиститись");

            var cleared = result.Events[1];
            Assert.AreEqual(GameEventType.LineCleared, cleared.Type);
            Assert.AreEqual(LineKind.Row, cleared.Kind);
            Assert.AreEqual(0, cleared.Extra);
            Assert.IsTrue(cleared.IsPure);
            Assert.AreEqual(TestBoard.Blue, cleared.Color);
            Assert.AreEqual(24, cleared.Value, "Value лінії — скільки пікселів вона дала");
            Assert.AreEqual(8, cleared.CellCount);
            Assert.AreEqual(new GridPos(0, 0), result.Cell(cleared, 0));

            Assert.AreEqual(24, result.CountEvents(GameEventType.PixelFilled));
            var pixel = result.Events[2];
            Assert.AreEqual(GameEventType.PixelFilled, pixel.Type);
            Assert.AreEqual(TestBoard.Blue, pixel.Color);
            Assert.IsTrue(pixel.IsPure);
            var firstBlue = -1;
            for (var s = 0; s < session.Picture.Picture.FillCount; s++)
                if (session.Picture.Picture.StepColor(s) == TestBoard.Blue) { firstBlue = s; break; }
            Assert.AreEqual(firstBlue, pixel.Value, "Value — індекс першого синього кроку в порядку проявлення");
            var firstBluePixel = -1;
            foreach (var i in session.Picture.Picture.RevealOrder)
                if (session.Picture.Picture.FamilyAt(i) == TestBoard.Blue) { firstBluePixel = i; break; }
            Assert.AreEqual(firstBluePixel, session.Picture.Picture.StepPixel(pixel.Value, 0),
                "перший синій крок починається з першого синього пікселя в порядку проявлення");
            Assert.AreEqual(1, pixel.CellCount);
            Assert.AreEqual(new GridPos(0, 0), result.Cell(pixel, 0), "клітинка-джерело першої краплі — перша клітинка лінії");
            Assert.AreEqual(new GridPos(1, 0), result.Cell(result.Events[5], 0), "четверта крапля — з другої клітинки");

            var expected = 2 * 10 + 100 * 2;
            Assert.AreEqual(expected, session.Score, "2 клітинки + чиста лінія ×2");
        }

        [Test]
        public void MixedRow_GivesOnePixelPerCellInEachCellsColor()
        {
            var session = Stripes();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Red));

            var result = session.TryPlace(0, new GridPos(7, 0));

            var cleared = result.Events[1];
            Assert.AreEqual(GameEventType.LineCleared, cleared.Type);
            Assert.IsFalse(cleared.IsPure);
            Assert.AreEqual(TestBoard.Blue, cleared.Color, "головний колір — той, кого більше");
            Assert.AreEqual(8, cleared.Value);
            Assert.AreEqual(8, result.PixelsFilled);
            Assert.AreEqual(7, 24 - session.Picture.Remaining(TestBoard.Blue));
            Assert.AreEqual(1, 24 - session.Picture.Remaining(TestBoard.Red), "червона клітинка дала червоний піксель");
            Assert.AreEqual(TestBoard.Red, result.Events[9].Color, "восьма крапля — червона, з восьмої клітинки");
            Assert.IsFalse(result.Events[9].IsPure);
            Assert.AreEqual(2 * 10 + 100, session.Score);
        }

        [Test]
        public void Events_LineClearedComesBeforeItsPixelsAndScoreAfterAll()
        {
            var session = Stripes();
            TestBoard.FillRow(session.Board, 0, "bbb.bbbb");
            for (var y = 2; y < 8; y++)
                session.Board[3, y] = TestBoard.Blue;
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Blue));

            var result = session.TryPlace(0, new GridPos(3, 0));

            var types = new List<GameEventType>();
            foreach (var e in result.Events)
                types.Add(e.Type);
            Assert.AreEqual(GameEventType.PiecePlaced, types[0]);
            Assert.AreEqual(GameEventType.LineCleared, types[1]);
            var secondLine = types.IndexOf(GameEventType.LineCleared, 2);
            Assert.Greater(secondLine, 2, "між двома лініями — пікселі першої");
            for (var i = 2; i < secondLine; i++)
                Assert.AreEqual(GameEventType.PixelFilled, types[i]);
            Assert.Less(secondLine, types.IndexOf(GameEventType.ComboApplied));
            Assert.Less(types.IndexOf(GameEventType.ComboApplied), types.IndexOf(GameEventType.ScoreGained));
        }

        [Test]
        public void RowAndColumn_ClearTogetherWithComboAndSharedCellOnce()
        {
            var session = Stripes();
            TestBoard.FillRow(session.Board, 0, "bbb.bbbb");
            for (var y = 2; y < 8; y++)
                session.Board[3, y] = TestBoard.Blue;
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Blue));

            var result = session.TryPlace(0, new GridPos(3, 0));

            Assert.AreEqual(2, result.LinesCleared);
            Assert.AreEqual(2, session.BestChain);
            var combo = TestBoard.Find(result, GameEventType.ComboApplied);
            Assert.AreEqual(2, combo.Value);
            Assert.AreEqual(1.5f, combo.Multiplier, 1e-4);

            for (var x = 0; x < 8; x++)
                Assert.AreEqual(Board.Empty, session.Board[x, 0]);
            for (var y = 0; y < 8; y++)
                Assert.AreEqual(Board.Empty, session.Board[3, y]);
            Assert.AreEqual(64, session.Board.CountEmpty(), "перетин очищено один раз, поле знову порожнє");

            // Дві чисті лінії по 8 × 3 = 48 пікселів, але синіх лише 24: решта згорає.
            Assert.AreEqual(24, result.PixelsFilled);
            Assert.AreEqual(24, result.PixelsWasted, "§5: лишок понад потребу згорає");
            Assert.AreEqual(24, session.PixelsWasted);
            Assert.AreEqual(2 * 10 + (int)(200 * 1.5f) * 2, session.Score, "множник ланцюга — до очок");
        }

        [Test]
        public void WastedPixels_BurnWhenTheColorIsNotNeeded()
        {
            var session = TestBoard.NewSession(7u, null, TestBoard.Library(TestBoard.Picture("one", Rarity.Common, "kbk", "kbk")));
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Blue));

            var result = session.TryPlace(0, new GridPos(7, 0));

            Assert.AreEqual(2, result.PixelsFilled);
            Assert.AreEqual(22, result.PixelsWasted);
            Assert.AreEqual(1, result.PicturesCompleted);
        }

        [Test]
        public void CompletedPicture_IsReplacedAndTheBoardIsRecoloredToTheNextOne()
        {
            var library = TestBoard.Library(
                TestBoard.Picture("blue", Rarity.Common, "kbbk"),
                TestBoard.Picture("red", Rarity.Common, "krrk"));
            var session = TestBoard.NewSession(3u, null, library);
            var before = session.Picture.LibraryIndex;
            var wanted = before == 0 ? TestBoard.Blue : TestBoard.Red;
            var other = before == 0 ? TestBoard.Red : TestBoard.Blue;
            TestBoard.FillRow(session.Board, 0, before == 0 ? "bbbbbbb." : "rrrrrrr.");
            session.Board[0, 5] = wanted;
            TestBoard.SetTray(session, TestBoard.Piece("2v", wanted), TestBoard.Piece("2h", wanted));

            var result = session.TryPlace(0, new GridPos(7, 0));

            Assert.AreEqual(1, result.PicturesCompleted);
            Assert.AreEqual(1, session.PicturesCompleted);
            Assert.AreEqual(before, TestBoard.Find(result, GameEventType.PictureCompleted).Value);
            var started = TestBoard.Find(result, GameEventType.PictureStarted);
            Assert.AreEqual(1 - before, started.Value, "та сама не приходить двічі поспіль");
            Assert.AreEqual(started.Value, session.Picture.LibraryIndex);
            Assert.AreEqual(0, session.Picture.FilledCount, "нова картинка чиста");
            Assert.AreEqual(before, session.PicturesCollected[0]);

            // §5: усе на полі й у лотку — у кольори нової картинки.
            Assert.AreEqual(other, session.Board[7, 1]);
            Assert.AreEqual(other, session.Board[0, 5]);
            var recolored = TestBoard.Find(result, GameEventType.BoardRecolored);
            Assert.AreEqual(wanted, recolored.Value);
            Assert.AreEqual(other, recolored.Extra);
            Assert.AreEqual(2, recolored.CellCount);
            var tray = TestBoard.Find(result, GameEventType.TrayRecolored);
            Assert.AreEqual(1, tray.Value);
            Assert.AreEqual(other, tray.Extra);
            Assert.AreEqual(other, session.Tray[1].Color);

            Assert.Less(TestBoard.IndexOf(result, GameEventType.PixelFilled), TestBoard.IndexOf(result, GameEventType.PictureCompleted));
            Assert.Less(TestBoard.IndexOf(result, GameEventType.PictureCompleted), TestBoard.IndexOf(result, GameEventType.PictureStarted));
            Assert.Less(TestBoard.IndexOf(result, GameEventType.PictureStarted), TestBoard.IndexOf(result, GameEventType.BoardRecolored));
        }

        [Test]
        public void ExhaustedColor_IsRecoloredToTheMostNeededOne()
        {
            var session = Stripes();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            session.Board[0, 5] = TestBoard.Blue;
            session.Board[1, 5] = TestBoard.Blue;
            session.Board[2, 5] = TestBoard.Yellow;
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Blue), TestBoard.Piece("2h", TestBoard.Blue), TestBoard.Piece("2h", TestBoard.Yellow));

            var result = session.TryPlace(0, new GridPos(7, 0));

            Assert.AreEqual(0, session.Picture.Remaining(TestBoard.Blue));
            Assert.IsFalse(result.Has(GameEventType.PictureCompleted));
            var recolored = TestBoard.Find(result, GameEventType.BoardRecolored);
            Assert.AreEqual(TestBoard.Blue, recolored.Value);
            Assert.AreEqual(TestBoard.Red, recolored.Extra, "червоного й жовтого порівну — нижчий індекс палітри");
            Assert.AreEqual(3, recolored.CellCount, "(0,5), (1,5) і хвіст фігури (7,1)");
            Assert.AreEqual(TestBoard.Red, session.Board[7, 1]);
            Assert.AreEqual(TestBoard.Yellow, session.Board[2, 5], "жовтий ще потрібен — лишається");
            var tray = TestBoard.Find(result, GameEventType.TrayRecolored);
            Assert.AreEqual(1, tray.Value);
            Assert.AreEqual(TestBoard.Red, session.Tray[1].Color);
            Assert.AreEqual(TestBoard.Yellow, session.Tray[2].Color);
            Assert.AreEqual(0, session.Board.CountOf(TestBoard.Blue));
        }

        [Test]
        public void TrayColors_AlwaysComeFromTheRemainingPixels()
        {
            var session = Stripes(5u);
            var bot = new RunBot();
            var guard = 0;
            while (!session.IsOver && guard++ < 400 && bot.TryChooseMove(session, out var index, out var anchor))
            {
                session.TryPlace(index, anchor);
                foreach (var piece in session.Tray)
                    if (!piece.IsEmpty)
                        Assert.Greater(session.Picture.Remaining(piece.Color), 0,
                            $"хід {guard}: у лотку колір {MasterPalette.NameOf(piece.Color)}, якого картинці вже не треба");
                foreach (var color in new[] { TestBoard.Blue, TestBoard.Red, TestBoard.Yellow })
                    if (session.Picture.Remaining(color) == 0)
                        Assert.AreEqual(0, session.Board.CountOf(color), $"хід {guard}: вичерпаний колір лишився на полі");
            }
        }

        [Test]
        public void ContinueAfterLoss_AllowsContinuesPerRunThenRefuses()
        {
            // §10/§17: два продовження за забіг — перше за ролик, друге за нафту. Число — з балансу.
            Assert.AreEqual(2, BalanceData.Default.ContinuesPerRun, "стеля продовжень за замовчуванням");

            var session = LostSession(out var scoreAtLoss);
            Assert.IsTrue(session.IsOver);
            Assert.IsTrue(session.CanContinue);
            var pictureAtLoss = session.Picture.FilledCount;

            var result = session.ContinueAfterLoss();

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(result.Has(GameEventType.RunContinued));
            Assert.IsTrue(result.Has(GameEventType.TrayRefilled), "лоток новий");
            Assert.IsFalse(session.IsOver);
            Assert.AreEqual(64, session.Board.CountEmpty(), "поле чисте");
            Assert.AreEqual(scoreAtLoss, session.Score, "рахунок лишається");
            Assert.AreEqual(pictureAtLoss, session.Picture.FilledCount, "картинка лишається");
            Assert.AreEqual(1, session.ContinuesUsed);
            Assert.IsFalse(session.CanContinue, "живий забіг не продовжують");
            Assert.IsFalse(session.ContinueAfterLoss().Accepted);

            Lose(session);
            Assert.IsTrue(session.CanContinue, "друге продовження ще в межах стелі");
            Assert.IsTrue(session.ContinueAfterLoss().Accepted);
            Assert.AreEqual(2, session.ContinuesUsed);

            Lose(session);
            Assert.IsFalse(session.CanContinue, "стеля: рівно ContinuesPerRun продовжень за забіг");
            Assert.IsFalse(session.ContinueAfterLoss().Accepted);
            Assert.AreEqual(2, session.ContinuesUsed);

            session.Restart();
            Assert.AreEqual(0, session.ContinuesUsed);
        }

        [Test]
        public void CompletePictureNow_FillsEverythingAndMovesOn()
        {
            var session = TestBoard.NewSession();
            var before = session.Picture.LibraryIndex;
            var total = session.Picture.Total;

            var result = session.CompletePictureNow();

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(total, result.CountEvents(GameEventType.PixelFilled));
            Assert.AreEqual(total, result.PixelsFilled);
            Assert.AreEqual(1, result.PicturesCompleted);
            Assert.AreEqual(before, TestBoard.Find(result, GameEventType.PictureCompleted).Value);
            Assert.AreNotEqual(before, session.Picture.LibraryIndex, "прийшла наступна");
            Assert.AreEqual(1, session.PicturesCompleted);
            Assert.AreEqual(before, session.PicturesCollected[0]);
            Assert.AreEqual(0, session.PlacementCount, "донат не чіпає поле й рахунок");
            Assert.AreEqual(0, session.Score);
            Assert.IsFalse(session.CompletePictureNow().Accepted == false && session.Picture.IsComplete, "нова картинка не повна");
        }

        [Test]
        public void Restart_DrawsAFreshPicture()
        {
            var session = Stripes();
            TestBoard.FillRow(session.Board, 0, "bbbbbbb.");
            TestBoard.SetTray(session, TestBoard.Piece("2v", TestBoard.Blue));
            session.TryPlace(0, new GridPos(7, 0));
            Assert.Greater(session.PixelsFilled, 0);

            session.Restart();

            Assert.AreEqual(0, session.Picture.FilledCount);
            Assert.AreEqual(0, session.PicturesCompleted);
            Assert.AreEqual(0, session.PixelsFilled);
            Assert.AreEqual(0, session.PixelsWasted);
            Assert.AreEqual(0, session.PicturesCollected.Count);
        }

        /// <summary>Сесія, доведена до програшу: шахове поле з двома дірками й лоток, який швидко застрягає.</summary>
        private static RunSession LostSession(out int score)
        {
            var session = Stripes();
            Lose(session);
            score = session.Score;
            return session;
        }

        /// <summary>Доводить живу сесію до програшу: забиває поле й ходить за підказкою, доки є куди.</summary>
        private static void Lose(RunSession session)
        {
            for (var y = 0; y < 7; y++)
                TestBoard.FillRow(session.Board, y, "brbrbrbr");
            TestBoard.FillRow(session.Board, 7, "brbrbr..");
            TestBoard.SetTray(session, TestBoard.Piece("2h", TestBoard.Yellow), TestBoard.Piece("5h", TestBoard.Red), TestBoard.Piece("plus", TestBoard.Blue));
            var result = session.TryPlace(0, new GridPos(6, 7));
            Assert.IsTrue(result.Accepted);
            var guard = 0;
            while (!session.IsOver && guard++ < 40)
            {
                if (!PlacementRules.TryFindHint(session.Board, session.TrayPieces, out var index, out var anchor))
                    break;
                session.TryPlace(index, anchor);
            }
            Assert.IsTrue(session.IsOver, "сесія мала програти");
        }

        [Test]
        public void Tray_RefillsOnlyWhenAllThreeArePlaced()
        {
            var session = Stripes();
            TestBoard.SetTray(session,
                TestBoard.Piece("2h", TestBoard.Blue), TestBoard.Piece("2h", TestBoard.Red), TestBoard.Piece("2h", TestBoard.Yellow));

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
            var session = Stripes();
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
                TestBoard.Piece("2h", TestBoard.Blue), TestBoard.Piece("2h", TestBoard.Red), TestBoard.Piece("2v", TestBoard.Yellow));

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
            var session = Stripes();
            TestBoard.SetTray(session, TestBoard.Piece("square", TestBoard.Blue));
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
        public void SameSeed_ReproducesTheRunByteForByte()
        {
            const uint seed = 4242u;
            var original = TestBoard.NewSession(seed);
            var copy = TestBoard.NewSession(seed);
            var bot = new RunBot();
            var moves = 0;

            while (!original.IsOver && bot.TryChooseMove(original, out var index, out var anchor))
            {
                original.TryPlace(index, anchor);
                copy.TryPlace(index, anchor);
                moves++;
            }

            Assert.Greater(moves, 5);
            Assert.AreEqual(original.Board.StateHash(), copy.Board.StateHash());
            Assert.AreEqual(original.Score, copy.Score);
            Assert.AreEqual(original.State, copy.State);
            Assert.AreEqual(original.Picture.LibraryIndex, copy.Picture.LibraryIndex);
            Assert.AreEqual(original.Picture.FilledCount, copy.Picture.FilledCount);
        }

        [Test]
        public void Hint_PointsToAValidPlacement()
        {
            var session = TestBoard.NewSession();
            Assert.IsTrue(session.TryFindHint(out var index, out var anchor));
            Assert.IsTrue(PlacementRules.CanPlace(session.Board, session.Tray[index].Shape!, anchor));
        }

        [Test]
        public void AnyPieceStuck_SeesAPieceWithoutRoom()
        {
            var session = Stripes();
            TestBoard.Load(session.Board,
                "b.b.b.b.",
                ".b.b.b.b",
                "b.b.b.b.",
                ".b.b.b.b",
                "b.b.b.b.",
                ".b.b.b.b",
                "b.b.b.b.",
                ".b.b.b.b");
            TestBoard.SetTray(session, TestBoard.Piece("square", TestBoard.Yellow), TestBoard.Piece("2h", TestBoard.Blue));

            Assert.AreEqual(32, session.Board.CountEmpty(), "місця вдосталь — тиск не через кількість");
            Assert.IsTrue(session.AnyPieceStuck());
            Assert.IsTrue(session.NoPieceFits(), "на шахівниці не влазить і двійка — це вже програш");
            session.Board[1, 0] = Board.Empty;
            Assert.IsFalse(session.NoPieceFits(), "з'явилась пара сусідніх дірок — двійка влазить");
            Assert.IsTrue(session.AnyPieceStuck(), "а квадрат — досі ні");
        }

        [Test]
        public void Events_PixelsFollowTheirLineImmediatelyAndNoneComeAfterCompletion()
        {
            // В'ю спирається на ці два порядки: краплі лінії запускаються на її LineCleared,
            // а після PictureCompleted пікселів у стрічці вже немає (вони — старої картинки).
            var library = TestBoard.Library(
                TestBoard.Picture("blue", Rarity.Common, "kbbbbk", "kbbbbk"),
                TestBoard.Picture("red", Rarity.Common, "krrrrk", "krrrrk"));
            var session = TestBoard.NewSession(9u, null, library);
            var wanted = session.Picture.LibraryIndex == 0 ? TestBoard.Blue : TestBoard.Red;
            TestBoard.FillRow(session.Board, 0, wanted == TestBoard.Blue ? "bbb.bbbb" : "rrr.rrrr");
            for (var y = 2; y < 8; y++)
                session.Board[3, y] = wanted;
            session.Board[0, 5] = wanted; // лишиться після зриву — саме її перефарбують
            TestBoard.SetTray(session, TestBoard.Piece("2v", wanted));

            var result = session.TryPlace(0, new GridPos(3, 0));
            Assert.AreEqual(1, result.PicturesCompleted);

            var completedAt = TestBoard.IndexOf(result, GameEventType.PictureCompleted);
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type == GameEventType.PixelFilled)
                {
                    Assert.Less(i, completedAt, "піксель після завершення — це піксель нової картинки, якого бути не може");
                    var previous = result.Events[i - 1].Type;
                    Assert.IsTrue(previous == GameEventType.LineCleared || previous == GameEventType.PixelFilled,
                        $"піксель на позиції {i} відірвано від своєї лінії ({previous})");
                    Assert.AreEqual(1, e.CellCount, "клітинка-джерело — рівно одна");
                }
            }
            Assert.Greater(TestBoard.IndexOf(result, GameEventType.BoardRecolored), completedAt, "перефарбування — після завершення");
        }

        [Test]
        public void IsCollected_SteersTheDeckTowardUnseenPictures()
        {
            var library = TestBoard.Library(
                TestBoard.Picture("a", Rarity.Common, "kbk"),
                TestBoard.Picture("b", Rarity.Common, "krk"),
                TestBoard.Picture("c", Rarity.Common, "kyk"));
            for (uint seed = 1; seed < 30; seed++)
            {
                var session = new RunSession(BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(seed), library,
                    id => id != "c");
                Assert.AreEqual("c", session.Picture.Picture.Id, "§7: спершу те, чого немає в колекції");
            }
        }
    }
}
