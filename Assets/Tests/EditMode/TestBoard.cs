using System;
using System.Collections.Generic;
using System.IO;
using InkFlow.Core;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Хелпери для тестів ядра. Поле задається текстом: рядки ЗВЕРХУ ВНИЗ, '.' — порожньо,
    /// літера — колір майстер-палітри (w білий, r червоний, y жовтий, g зелений, b синій,
    /// p фіолетовий; k — контурне чорнило для картинок). Тест, у якому поле видно очима,
    /// ловить помилку швидше за тест, який його збирає викликами.
    /// </summary>
    public static class TestBoard
    {
        public const byte Ink = 1;
        public const byte White = 4;
        public const byte Red = 8;
        public const byte Yellow = 11;
        public const byte Green = 13;
        public const byte Blue = 16;
        public const byte Violet = 18;

        public static byte ColorOf(char c) => c switch
        {
            'k' => Ink,
            'w' => White,
            'r' => Red,
            'y' => Yellow,
            'g' => Green,
            'b' => Blue,
            'p' => Violet,
            _ => Board.Empty
        };

        public static char CharOf(byte color) => color switch
        {
            Ink => 'k',
            White => 'w',
            Red => 'r',
            Yellow => 'y',
            Green => 'g',
            Blue => 'b',
            Violet => 'p',
            _ => '.'
        };

        public static Board Parse(params string[] rows)
        {
            var height = rows.Length;
            var width = rows[0].Length;
            var board = new Board(width, height);
            for (var r = 0; r < height; r++)
                for (var x = 0; x < width; x++)
                    board[x, height - 1 - r] = ColorOf(rows[r][x]);
            return board;
        }

        /// <summary>Копіює розкладку в поле сесії (розміри мають збігатися).</summary>
        public static void Load(Board target, params string[] rows)
        {
            var source = Parse(rows);
            target.CopyFrom(source);
        }

        /// <summary>Заповнює рядок кольорами з тексту — щоб зібрати конкретну лінію під зрив.</summary>
        public static void FillRow(Board board, int y, string colors)
        {
            for (var x = 0; x < colors.Length; x++)
                board[x, y] = ColorOf(colors[x]);
        }

        public static string Dump(Board board)
        {
            var sb = new System.Text.StringBuilder();
            for (var y = board.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < board.Width; x++)
                    sb.Append(CharOf(board[x, y]));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static PieceShape ShapeById(string id)
        {
            var catalog = PieceCatalogData.Default;
            for (var i = 0; i < catalog.Count; i++)
                if (catalog[i].Id == id)
                    return catalog[i];
            throw new ArgumentException($"Немає форми {id}.");
        }

        /// <summary>Підміняє лоток сесії заданими фігурами — щоб перевірити конкретний хід.</summary>
        public static void SetTray(RunSession session, params PieceDef[] pieces)
        {
            for (var i = 0; i < session.TrayPieces.Length; i++)
                session.TrayPieces[i] = i < pieces.Length ? pieces[i] : PieceDef.None;
        }

        public static PieceDef Piece(string shapeId, byte color) => new PieceDef(ShapeById(shapeId), color);

        // ── Картинки ──

        /// <summary>
        /// Картинка з рядків тими самими літерами, що й поле (k — контур). Рядки — згори вниз,
        /// як у файлі. Тема «test».
        /// </summary>
        public static PixelPicture Picture(string id, Rarity rarity, params string[] rows)
        {
            var text = $"id: {id}\nname: {id.ToUpperInvariant()}\ntheme: test\nrarity: {Rarities.IdOf(rarity)}\n" +
                       "colors: k=1 w=4 r=8 y=11 g=13 b=16 p=18\noutline: k\ngrid:\n" + string.Join("\n", rows);
            return PixelPicture.Parse(text);
        }

        public static PictureLibrary Library(params PixelPicture[] pictures) => new PictureLibrary(pictures);

        /// <summary>Одна картинка «bb / bb» — щоб сесія мала лише синій колір.</summary>
        public static PictureLibrary BlueSquare(int side = 2)
        {
            var row = new string('b', side);
            var rows = new string[side];
            for (var i = 0; i < side; i++)
                rows[i] = row;
            return Library(Picture("blue_square", Rarity.Common, rows));
        }

        private static string? _picturesDirectory;

        /// <summary>Тека Assets/_Pictures — шукається вгору від робочої теки й від збірки тестів.</summary>
        public static string PicturesDirectory
        {
            get
            {
                if (_picturesDirectory != null)
                    return _picturesDirectory;
                foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                {
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        var candidate = Path.Combine(dir.FullName, "Assets", "_Pictures");
                        if (Directory.Exists(candidate))
                            return _picturesDirectory = candidate;
                        dir = dir.Parent;
                    }
                }
                throw new DirectoryNotFoundException("Не знайшов Assets/_Pictures — тести бібліотеки потребують справжніх картинок.");
            }
        }

        private static PictureLibrary? _real;

        /// <summary>Справжня бібліотека гри з Assets/_Pictures.</summary>
        public static PictureLibrary RealLibrary => _real ??= PictureLibrary.LoadFromDirectory(PicturesDirectory);

        public static RunSession NewSession(uint seed = 7u, BalanceData? balance = null, PictureLibrary? library = null) =>
            new RunSession(balance ?? BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(seed), library ?? RealLibrary);

        public static int IndexOf(MoveResult result, GameEventType type)
        {
            for (var i = 0; i < result.Events.Count; i++)
                if (result.Events[i].Type == type)
                    return i;
            return -1;
        }

        public static GameEvent Find(MoveResult result, GameEventType type)
        {
            var i = IndexOf(result, type);
            if (i < 0)
                throw new InvalidOperationException($"події {type} немає");
            return result.Events[i];
        }
    }
}
