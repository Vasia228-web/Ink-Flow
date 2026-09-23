using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Витягує наступну картинку (документ §6): спершу кидається рідкість за вагами
    /// 70 / 25 / 5, потім рівноймовірно серед картинок цієї рідкості. Щойно закінчена
    /// не приходить двічі поспіль. Якщо картинок такої рідкості немає — крок до
    /// звичайнішої, далі до рідкіснішої; колода з однієї картинки віддає її завжди.
    ///
    /// Гарантоване випадіння незавершеної (§7) — крок 6, поверх цього.
    /// </summary>
    public sealed class PictureDeck
    {
        private readonly PictureCatalogData _catalog;
        private readonly BalanceData _balance;
        private readonly List<int> _pool = new List<int>(16);

        public PictureDeck(PictureCatalogData catalog, BalanceData balance)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>Індекс наступної картинки в колоді.</summary>
        public int Draw(IRandomSource random, int exclude)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (_catalog.Count == 1)
                return 0;

            var rarity = _balance.RarityFor(random.Next(_balance.RarityWeightTotal));
            return DrawOf(rarity, random, exclude);
        }

        /// <summary>Картинка саме цієї рідкості (з відкатом, якщо таких немає).</summary>
        public int DrawOf(Rarity rarity, IRandomSource random, int exclude)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (_catalog.Count == 1)
                return 0;

            // Спершу ця рідкість, далі — звичайніші, потім рідкісніші.
            var order = rarity switch
            {
                Rarity.Legendary => new[] { Rarity.Legendary, Rarity.Rare, Rarity.Common },
                Rarity.Rare => new[] { Rarity.Rare, Rarity.Common, Rarity.Legendary },
                _ => new[] { Rarity.Common, Rarity.Rare, Rarity.Legendary }
            };

            for (var step = 0; step < order.Length; step++)
            {
                Collect(order[step], exclude);
                if (_pool.Count > 0)
                    return _pool[random.Next(_pool.Count)];
            }

            // Усе, крім виключеної, — лише коли колода геть перекошена.
            _pool.Clear();
            for (var i = 0; i < _catalog.Count; i++)
                if (i != exclude)
                    _pool.Add(i);
            return _pool.Count > 0 ? _pool[random.Next(_pool.Count)] : 0;
        }

        private void Collect(Rarity rarity, int exclude)
        {
            _pool.Clear();
            for (var i = 0; i < _catalog.Count; i++)
                if (i != exclude && _catalog[i].Rarity == rarity)
                    _pool.Add(i);
        }
    }
}
