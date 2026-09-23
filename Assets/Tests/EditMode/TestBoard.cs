using InkFlow.Core;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Хелпери для тестів ядра. Поле задається текстом: рядки ЗВЕРХУ ВНИЗ,
    /// '.' — порожньо, 'b'/'r'/'y' — пігменти. Тест, у якому поле видно очима,
    /// ловить помилку швидше за тест, який його збирає викликами.
    /// </summary>
    public static class TestBoard
    {
        public static Pigment PigmentOf(char c) => c switch
        {
            'b' => Pigment.Blue,
            'r' => Pigment.Red,
            'y' => Pigment.Yellow,
            _ => Pigment.None
        };

        public static char CharOf(Pigment p) => p switch
        {
            Pigment.Blue => 'b',
            Pigment.Red => 'r',
            Pigment.Yellow => 'y',
            _ => '.'
        };

        public static Board Parse(params string[] rows)
        {
            var height = rows.Length;
            var width = rows[0].Length;
            var board = new Board(width, height);
            for (var r = 0; r < height; r++)
                for (var x = 0; x < width; x++)
                    board[x, height - 1 - r] = PigmentOf(rows[r][x]);
            return board;
        }

        /// <summary>Копіює розкладку в поле сесії (розміри мають збігатися).</summary>
        public static void Load(Board target, params string[] rows)
        {
            var source = Parse(rows);
            target.CopyFrom(source);
        }

        /// <summary>Заповнює рядок пігментами з тексту — щоб зібрати конкретну лінію під зрив.</summary>
        public static void FillRow(Board board, int y, string colors)
        {
            for (var x = 0; x < colors.Length; x++)
                board[x, y] = PigmentOf(colors[x]);
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
            throw new System.ArgumentException($"Немає форми {id}.");
        }

        /// <summary>Підміняє лоток сесії заданими фігурами — щоб перевірити конкретний хід.</summary>
        public static void SetTray(RunSession session, params PieceDef[] pieces)
        {
            for (var i = 0; i < session.TrayPieces.Length; i++)
                session.TrayPieces[i] = i < pieces.Length ? pieces[i] : PieceDef.None;
        }

        public static PieceDef Piece(string shapeId, Pigment pigment) => new PieceDef(ShapeById(shapeId), pigment);

        public static RunSession NewSession(uint seed = 7u, BalanceData? balance = null) =>
            new RunSession(balance ?? BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(seed));
    }
}
