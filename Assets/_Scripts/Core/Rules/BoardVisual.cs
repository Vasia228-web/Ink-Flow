using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Видимий стан поля — чиста модель для в'ю (K1Candy: окремі блоки, без злиття).
    /// На кожну клітинку: чи видно блок, його родина й масштаб анімації; окремо —
    /// привид фігури й підсвітка ліній. В'ю лише дзеркалить це в спрайти.
    ///
    /// Навіщо окремо від MonoBehaviour: після постановки фігур і зривів у ПОРОЖНІХ клітинках
    /// не має лишатись жодного активного блока, а пул привида — повертатись до вихідного
    /// стану. Це тримає headless-тест, який ганяє справжню сесію; MonoBehaviour у тесті не
    /// запустиш, а саме така «забута» графіка й дає порізи на полі.
    /// </summary>
    public sealed class BoardVisual
    {
        public enum Anim : byte { None, Land, Clear }

        public struct BlockState
        {
            /// <summary>Блок видно (активний об'єкт у в'ю).</summary>
            public bool Active;

            /// <summary>Родина (індекс палітри); 0 — порожньо.</summary>
            public byte Color;

            /// <summary>Масштаб блока: 1 — спокій; менше — приземлення чи зрив.</summary>
            public float Scale;

            public Anim Anim;
            public float Time;
        }

        private readonly BlockState[] _cells;
        private readonly byte[] _highlights;      // 0 — немає; інакше родина підсвітки
        private readonly float[] _highlightAlpha;
        private readonly int[] _ghostCells;
        private readonly int _width;
        private readonly int _height;
        private int _ghostCount;
        private int _animating;

        public BoardVisual(int width, int height, int ghostCapacity = 5)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            _width = width;
            _height = height;
            _cells = new BlockState[width * height];
            _highlights = new byte[width * height];
            _highlightAlpha = new float[width * height];
            _ghostCells = new int[ghostCapacity];
            _ghostCount = 0;
        }

        public int Width => _width;
        public int Height => _height;
        public int CellCount => _cells.Length;
        public BlockState this[int index] => _cells[index];
        public BlockState At(int x, int y) => _cells[y * _width + x];

        /// <summary>Скільки клітинок зараз анімується.</summary>
        public int Animating => _animating;

        /// <summary>Скільки блоків активні (видимі).</summary>
        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _cells.Length; i++)
                    if (_cells[i].Active) n++;
                return n;
            }
        }

        public int IndexOf(GridPos p) => p.Y * _width + p.X;

        // ── Синхронізація з моделлю ──

        /// <summary>Повний знімок поля: блоки рівно там, де клітинки моделі; анімації скасовано.</summary>
        public void Sync(Board board)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                {
                    var i = y * _width + x;
                    var color = board.Contains(new GridPos(x, y)) ? board[x, y] : Board.Empty;
                    _cells[i].Color = color;
                    _cells[i].Active = color != Board.Empty;
                    _cells[i].Scale = 1f;
                    _cells[i].Anim = Anim.None;
                    _cells[i].Time = 0f;
                }
            _animating = 0;
        }

        /// <summary>Поле вже в нових кольорах, а перефарбування ще не зіграло: у цих клітинках показуємо старі (§8).</summary>
        public void Override(int index, byte color)
        {
            if (index < 0 || index >= _cells.Length) return;
            _cells[index].Color = color;
            _cells[index].Active = color != Board.Empty;
        }

        /// <summary>Фігура лягла: блок з'являється з 0.6 і пружно росте до 1.</summary>
        public void Place(int index, byte color)
        {
            if (index < 0 || index >= _cells.Length) return;
            _cells[index].Color = color;
            _cells[index].Active = true;
            _cells[index].Scale = 0.6f;
            Start(index, Anim.Land);
        }

        /// <summary>Клітинку зриває: блок коротко розширюється й схлопується в нуль, потім гасне.</summary>
        public void Clear(int index)
        {
            if (index < 0 || index >= _cells.Length || !_cells[index].Active) return;
            Start(index, Anim.Clear);
        }

        /// <summary>Перефарбування хвилею: той самий поп, що й приземлення, у новому кольорі.</summary>
        public void Recolor(int index, byte color) => Place(index, color);

        private void Start(int index, Anim anim)
        {
            if (_cells[index].Anim == Anim.None)
                _animating++;
            _cells[index].Anim = anim;
            _cells[index].Time = 0f;
        }

        /// <summary>
        /// Крок анімацій. Приземлення — <paramref name="landCurve"/> від 0.6 до 1; зрив —
        /// 1 → 1.2 за першу чверть, далі до 0, і блок вимикається. Повертає true, якщо щось змінилось.
        /// </summary>
        public bool Advance(float dt, float landDuration, float clearDuration, Func<float, float> landCurve)
        {
            if (_animating == 0)
                return false;
            landDuration = Math.Max(landDuration, 0.01f);
            clearDuration = Math.Max(clearDuration, 0.01f);
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].Anim == Anim.None)
                    continue;
                _cells[i].Time += dt;
                if (_cells[i].Anim == Anim.Land)
                {
                    var k = Math.Min(1f, _cells[i].Time / landDuration);
                    _cells[i].Scale = 0.6f + 0.4f * landCurve(k);
                    if (k >= 1f)
                    {
                        _cells[i].Anim = Anim.None;
                        _cells[i].Scale = 1f;
                        _animating--;
                    }
                }
                else
                {
                    var k = Math.Min(1f, _cells[i].Time / clearDuration);
                    _cells[i].Scale = k < 0.25f ? 1f + k * 0.8f : 1.2f * (1f - (k - 0.25f) / 0.75f);
                    if (k >= 1f)
                    {
                        _cells[i].Anim = Anim.None;
                        _cells[i].Scale = 0f;
                        _cells[i].Active = false;
                        _cells[i].Color = Board.Empty;
                        _animating--;
                    }
                }
            }
            return true;
        }

        // ── Підсвітка ліній ──

        public byte HighlightColor(int index) => _highlights[index];
        public float HighlightAlpha(int index) => _highlightAlpha[index];

        public void SetHighlight(int index, byte color, float alpha)
        {
            if (index < 0 || index >= _highlights.Length) return;
            _highlights[index] = color;
            _highlightAlpha[index] = alpha;
        }

        public void ClearHighlights()
        {
            Array.Clear(_highlights, 0, _highlights.Length);
            Array.Clear(_highlightAlpha, 0, _highlightAlpha.Length);
        }

        public int HighlightCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _highlights.Length; i++)
                    if (_highlights[i] != 0) n++;
                return n;
            }
        }

        // ── Привид ──

        public int GhostCapacity => _ghostCells.Length;
        public int GhostCount => _ghostCount;
        public int GhostCell(int k) => _ghostCells[k];
        public byte GhostColor { get; private set; }
        public bool GhostValid { get; private set; }

        /// <summary>Привид фігури: клітинки в межах поля; поза межами — не показуються.</summary>
        public void ShowGhost(PieceShape shape, GridPos anchor, byte color, bool valid)
        {
            if (shape is null) throw new ArgumentNullException(nameof(shape));
            _ghostCount = 0;
            GhostColor = color;
            GhostValid = valid;
            for (var i = 0; i < shape.Cells.Length && _ghostCount < _ghostCells.Length; i++)
            {
                var x = anchor.X + shape.Cells[i].X;
                var y = anchor.Y + shape.Cells[i].Y;
                if (x < 0 || y < 0 || x >= _width || y >= _height)
                    continue;
                _ghostCells[_ghostCount++] = y * _width + x;
            }
        }

        public void HideGhost()
        {
            _ghostCount = 0;
            ClearHighlights();
        }

        /// <summary>Чи поле «чисте»: у порожніх клітинках моделі немає активних блоків, привид і підсвітка сховані.</summary>
        public bool IsCleanAgainst(Board board, out string why)
        {
            if (board is null) throw new ArgumentNullException(nameof(board));
            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                {
                    var i = y * _width + x;
                    var empty = board[x, y] == Board.Empty;
                    if (empty && _cells[i].Active)
                    {
                        why = $"клітинка ({x},{y}) порожня, а блок активний";
                        return false;
                    }
                    if (!empty && (!_cells[i].Active || _cells[i].Color != board[x, y]))
                    {
                        why = $"клітинка ({x},{y}) має колір {board[x, y]}, а блок {(_cells[i].Active ? _cells[i].Color.ToString() : "неактивний")}";
                        return false;
                    }
                }
            if (_ghostCount != 0) { why = "привид не схований"; return false; }
            if (HighlightCount != 0) { why = "підсвітка не знята"; return false; }
            if (_animating != 0) { why = "анімації не завершені"; return false; }
            why = string.Empty;
            return true;
        }
    }
}
