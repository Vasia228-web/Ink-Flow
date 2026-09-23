using System;
using System.Collections.Generic;

namespace InkFlow.Meta
{
    /// <summary>
    /// Колекція зібраних картинок (документ §5, §8, §10): скільки разів яку зібрано
    /// і коли вперше. Рекорд колекції — кількість РІЗНИХ картинок (§8). Ідентифікатор —
    /// назва картинки з Core, не індекс: колода росте темами, і індекси поїхали б.
    /// </summary>
    public sealed class PictureCollection
    {
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        public sealed class Entry
        {
            public Entry(string id, int count, DateTime firstUtc)
            {
                Id = id;
                Count = count;
                FirstUtc = firstUtc;
            }

            public string Id { get; }
            public int Count { get; internal set; }
            public DateTime FirstUtc { get; }
        }

        /// <summary>Скільки різних картинок зібрано — рекорд колекції (§8).</summary>
        public int Distinct => _order.Count;

        /// <summary>Скільки всього завершень, з повторами.</summary>
        public int Total
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _order.Count; i++)
                    n += _entries[_order[i]].Count;
                return n;
            }
        }

        /// <summary>Ідентифікатори в порядку першого збирання.</summary>
        public IReadOnlyList<string> Ids => _order;

        public int CountOf(string id) => _entries.TryGetValue(id, out var entry) ? entry.Count : 0;

        public bool Has(string id) => _entries.ContainsKey(id);

        public Entry? Get(string id) => _entries.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>Додає завершення. Повертає true, якщо картинка в колекції нова.</summary>
        public bool Add(string id, DateTime utcNow)
        {
            if (id is null || id.Length == 0)
                throw new ArgumentException("Порожній id картинки.", nameof(id));

            if (_entries.TryGetValue(id, out var entry))
            {
                entry.Count++;
                return false;
            }

            _entries[id] = new Entry(id, 1, utcNow);
            _order.Add(id);
            return true;
        }

        public static PictureCollection Load(CollectionData? data)
        {
            var collection = new PictureCollection();
            if (data?.Pictures is null)
                return collection;

            foreach (var record in data.Pictures)
            {
                if (record.PictureId is null || record.PictureId.Length == 0 || record.Count <= 0)
                    continue;
                if (collection._entries.ContainsKey(record.PictureId))
                    continue;
                var first = DateTime.TryParse(record.FirstUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed)
                    ? parsed
                    : DateTime.MinValue;
                collection._entries[record.PictureId] = new Entry(record.PictureId, record.Count, first);
                collection._order.Add(record.PictureId);
            }

            return collection;
        }

        public static void Save(PictureCollection collection, CollectionData data)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            if (data is null) throw new ArgumentNullException(nameof(data));

            data.Pictures.Clear();
            for (var i = 0; i < collection._order.Count; i++)
            {
                var entry = collection._entries[collection._order[i]];
                data.Pictures.Add(new CollectedPicture
                {
                    PictureId = entry.Id,
                    Count = entry.Count,
                    FirstUtc = entry.FirstUtc.ToString("o")
                });
            }
        }
    }
}
