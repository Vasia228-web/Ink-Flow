using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Зона картинки: символ у кресленні, потрібний відтінок, підпис.</summary>
    public readonly struct ZoneDef
    {
        public ZoneDef(char key, Hue hue, string label)
        {
            if (key == '.' || char.IsWhiteSpace(key))
                throw new ArgumentOutOfRangeException(nameof(key), "Крапка й пробіл — порожнє місце креслення.");
            if (hue == Hue.None)
                throw new ArgumentOutOfRangeException(nameof(hue));
            Key = key;
            Hue = hue;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public char Key { get; }
        public Hue Hue { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Картинка (документ §5): силует із зонами заливки. Core бачить лише креслення —
    /// рядки символів, де кожен символ — зона, крапка — порожнє місце. Пікселі, маски
    /// й спрайти робить редактор із цього ж креслення, тож розійтись вони не можуть.
    ///
    /// Порядок зон у <see cref="Zones"/> — порядок заливки: перша незалита зона —
    /// «активна», її відтінок гравець бачить як ціль.
    /// </summary>
    public sealed class PictureDef
    {
        private readonly GridPos[][] _cells;

        public PictureDef(string id, string name, Rarity rarity, string[] rows, ZoneDef[] zones)
        {
            if (id is null || id.Length == 0) throw new ArgumentException("Порожній id.", nameof(id));
            if (name is null || name.Length == 0) throw new ArgumentException("Порожня назва.", nameof(name));
            if (rows is null || rows.Length == 0) throw new ArgumentException("Порожнє креслення.", nameof(rows));
            if (zones is null || zones.Length == 0) throw new ArgumentException("Картинка без зон.", nameof(zones));

            Id = id;
            Name = name;
            Rarity = rarity;
            Rows = rows;
            Zones = zones;
            Width = rows[0].Length;
            Height = rows.Length;

            for (var r = 0; r < rows.Length; r++)
                if (rows[r].Length != Width)
                    throw new ArgumentException($"Рядок {r} креслення «{id}» має іншу ширину.", nameof(rows));

            for (var i = 0; i < zones.Length; i++)
                for (var j = i + 1; j < zones.Length; j++)
                    if (zones[i].Key == zones[j].Key)
                        throw new ArgumentException($"Зона '{zones[i].Key}' у «{id}» описана двічі.", nameof(zones));

            var buckets = new List<GridPos>[zones.Length];
            for (var i = 0; i < zones.Length; i++)
                buckets[i] = new List<GridPos>();

            // Y — від верхнього рядка креслення вниз: так само читається текст.
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    var c = rows[y][x];
                    if (c == '.')
                        continue;
                    var zone = IndexOfKey(c);
                    if (zone < 0)
                        throw new ArgumentException($"Символ '{c}' у кресленні «{id}» не є зоною.", nameof(rows));
                    buckets[zone].Add(new GridPos(x, y));
                }

            _cells = new GridPos[zones.Length][];
            TotalCells = 0;
            for (var i = 0; i < zones.Length; i++)
            {
                if (buckets[i].Count == 0)
                    throw new ArgumentException($"Зона '{zones[i].Key}' у «{id}» не має жодної клітинки.", nameof(zones));
                _cells[i] = buckets[i].ToArray();
                TotalCells += _cells[i].Length;
            }
        }

        public string Id { get; }
        public string Name { get; }
        public Rarity Rarity { get; }

        /// <summary>Креслення: символ — зона, крапка — порожньо. Для генератора масок і тестів.</summary>
        public IReadOnlyList<string> Rows { get; }

        public IReadOnlyList<ZoneDef> Zones { get; }
        public int ZoneCount => Zones.Count;
        public int Width { get; }
        public int Height { get; }
        public int TotalCells { get; }

        /// <summary>Клітинки зони (X — стовпець, Y — рядок згори).</summary>
        public IReadOnlyList<GridPos> CellsOf(int zone) => _cells[zone];

        public int IndexOfKey(char key)
        {
            for (var i = 0; i < Zones.Count; i++)
                if (Zones[i].Key == key)
                    return i;
            return -1;
        }

        /// <summary>Чи потрібен картинці цей відтінок узагалі — для підбору картинки під тему.</summary>
        public bool Needs(Hue hue)
        {
            for (var i = 0; i < Zones.Count; i++)
                if (Zones[i].Hue == hue)
                    return true;
            return false;
        }
    }
}
