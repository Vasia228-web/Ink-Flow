using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Колода (документ §7): спершу віддає картинки, яких ще немає в колекції, з вагою
    /// рідкості; повтори — лише коли невидані закінчились. Щойно закінчена не приходить
    /// двічі поспіль. Якщо картинок кинутої рідкості немає — крок до звичайнішої, далі до
    /// рідкіснішої.
    /// </summary>
    public sealed class PictureDeck
    {
        private readonly PictureLibrary _library;
        private readonly BalanceData _balance;
        private readonly List<int> _pool = new List<int>(32);
        private readonly int[] _weights = new int[Rarities.Count];

        public PictureDeck(PictureLibrary library, BalanceData balance)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>
        /// Індекс наступної картинки. <paramref name="isCollected"/> — чи картинка вже в
        /// колекції (за id); null — колекції немає, усе вважається невиданим.
        /// </summary>
        public int Draw(IRandomSource random, int exclude, Func<string, bool>? isCollected)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (_library.Count == 1)
                return 0;

            // Рідкість — завжди за звичайними вагами (§6), а вже в її межах невидані першими. Раніше невидані
            // вирішували й рідкість: коли звичайні зібрано, лишались самі невидані рідкі, кожна картинка ставала
            // епічною чи легендарною (8–13 хв, домалювати — рідко), і галактика 3 тяглась уп'ятеро довше за 2-гу.
            var rarity = RollRarity(random, exclude, null, unseenOnly: false) ?? Rarity.Common;
            Collect(rarity, exclude, isCollected, unseenOnly: true);
            if (_pool.Count > 0)
                return _pool[random.Next(_pool.Count)];
            return DrawOf(rarity, random, exclude);
        }

        /// <summary>
        /// Картинка щонайменше рідкості <paramref name="min"/> — обіцянка нагороди галактики. Рідкість — за звичайними
        /// вагами серед дозволених (епічна частіше за космічну), невидані — першими, як у <see cref="Draw"/>.
        /// </summary>
        public int DrawAtLeast(Rarity min, IRandomSource random, int exclude, Func<string, bool>? isCollected)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            for (var pass = 0; pass < 2; pass++)
            {
                var unseenOnly = pass == 0;
                var total = 0;
                for (var r = 0; r < Rarities.Count; r++)
                {
                    _weights[r] = 0;
                    if (r < (int)min)
                        continue;
                    Collect((Rarity)r, exclude, isCollected, unseenOnly);
                    _weights[r] = _pool.Count > 0 ? Math.Max(1, _balance.RarityWeights[r]) : 0;
                    total += _weights[r];
                }
                if (total <= 0)
                    continue;
                var roll = random.Next(total);
                for (var r = 0; r < Rarities.Count; r++)
                {
                    roll -= _weights[r];
                    if (roll >= 0 || _weights[r] == 0)
                        continue;
                    Collect((Rarity)r, exclude, isCollected, unseenOnly);
                    return _pool[random.Next(_pool.Count)];
                }
            }
            // Такої рідкості в бібліотеці немає — найближча, як у DrawOf.
            return DrawOf(min, random, exclude);
        }

        /// <summary>Картинка саме цієї рідкості (з відкатом), без огляду на колекцію.</summary>
        public int DrawOf(Rarity rarity, IRandomSource random, int exclude)
        {
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (_library.Count == 1)
                return 0;

            var start = (int)rarity;
            for (var step = 0; step < Rarities.Count; step++)
            {
                // Звичайніші спершу (вниз), потім рідкісніші (вгору).
                var candidate = step <= start ? start - step : step;
                if (candidate < 0 || candidate >= Rarities.Count)
                    continue;
                Collect((Rarity)candidate, exclude, null, unseenOnly: false);
                if (_pool.Count > 0)
                    return _pool[random.Next(_pool.Count)];
            }

            _pool.Clear();
            for (var i = 0; i < _library.Count; i++)
                if (i != exclude)
                    _pool.Add(i);
            return _pool.Count > 0 ? _pool[random.Next(_pool.Count)] : 0;
        }

        private Rarity? RollRarity(IRandomSource random, int exclude, Func<string, bool>? isCollected, bool unseenOnly)
        {
            var total = 0;
            for (var r = 0; r < Rarities.Count; r++)
            {
                Collect((Rarity)r, exclude, isCollected, unseenOnly);
                _weights[r] = _pool.Count > 0 ? _balance.RarityWeights[r] : 0;
                total += _weights[r];
            }

            if (total <= 0)
                return null;

            var roll = random.Next(total);
            for (var r = 0; r < Rarities.Count; r++)
            {
                roll -= _weights[r];
                if (roll < 0 && _weights[r] > 0)
                    return (Rarity)r;
            }
            return null;
        }

        private void Collect(Rarity rarity, int exclude, Func<string, bool>? isCollected, bool unseenOnly)
        {
            _pool.Clear();
            for (var i = 0; i < _library.Count; i++)
            {
                if (i == exclude || _library[i].Rarity != rarity)
                    continue;
                if (unseenOnly && isCollected != null && isCollected(_library[i].Id))
                    continue;
                _pool.Add(i);
            }
        }
    }
}
