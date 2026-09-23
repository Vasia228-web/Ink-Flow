using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Мішок фігур (документ §8): «не чистий рандом — гра дивиться на форму вільного
    /// місця й підвищує ймовірність фігур, які туди поміщаються. Вона не підказує,
    /// куди класти, але не підсовує завідомо неможливий набір».
    ///
    /// Три правила, кожне під тестом:
    ///  (A) вага форми росте з кількістю її позицій на полі (<see cref="BalanceData.BagBias"/>);
    ///  (B) набір, у якому не влазить жодна фігура, перегенеровується — спершу як є,
    ///      потім із меншими фігурами (прототип v3: 60 спроб, після 40 — стеля 3,
    ///      далі 20 спроб двоклітинковими);
    ///  (C) якщо не влазить навіть двоклітинкова — місця справді немає, і це чесний
    ///      програш, а не вина мішка. Скільки разів довелось зменшувати фігури —
    ///      метрика якості мішка, а не гри.
    ///
    /// Кольори: серія одного пігменту з імовірністю за рівнем складності (§8: далі
    /// серії рідшають), інакше — перевага пігменту, якого на полі менше. Перша фігура
    /// може отримати заданий пігмент (крок 4: колір, потрібний активній зоні).
    /// </summary>
    public sealed class TrayGenerator
    {
        private readonly PieceCatalogData _catalog;
        private readonly BalanceData _balance;
        private readonly int[] _fits;
        private readonly float[] _weights;
        private readonly int[] _pigmentCounts = new int[Pigments.Count];
        private Pigment _lastPigment = Pigment.None;

        public TrayGenerator(PieceCatalogData catalog, BalanceData balance)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _fits = new int[catalog.Count];
            _weights = new float[catalog.Count];
        }

        /// <summary>Скільки лотків видано за партію — це і є раунд для прогресії.</summary>
        public int TraysIssued { get; private set; }

        /// <summary>Скільки разів довелось зменшувати фігури, щоб хоч одна влізла.</summary>
        public int RescuesUsed { get; private set; }

        public void Reset()
        {
            TraysIssued = 0;
            RescuesUsed = 0;
            _lastPigment = Pigment.None;
        }

        /// <summary>
        /// Наповнює лоток. Повертає true, якщо довелось зменшувати фігури (діагностика).
        /// Якщо не влазить нічого навіть після рятувальних спроб — лоток лишається
        /// останнім згенерованим, і сесія чесно фіксує програш.
        /// </summary>
        public bool Fill(PieceDef[] tray, Board board, IRandomSource random, Pigment wantedFirst = Pigment.None)
        {
            if (tray is null) throw new ArgumentNullException(nameof(tray));
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (random is null) throw new ArgumentNullException(nameof(random));

            TraysIssued++;
            var tier = _balance.TierFor(TraysIssued);
            CountFits(board);
            CountPigments(board);

            var cap = _balance.SizeCapFor(tier);
            for (var attempt = 0; attempt < _balance.MaxTrayAttempts; attempt++)
            {
                var sizeCap = attempt < _balance.TrayShrinkAfterAttempts ? cap : Math.Min(cap, 3);
                Build(tray, sizeCap, tier, wantedFirst, random);
                if (PlacementRules.AnyPieceFits(board, tray))
                    return false;
            }

            RescuesUsed++;
            var rescueCap = Math.Min(cap, _balance.MinPieceSize);
            for (var attempt = 0; attempt < _balance.TrayRescueAttempts; attempt++)
            {
                Build(tray, rescueCap, tier, wantedFirst, random);
                if (PlacementRules.AnyPieceFits(board, tray))
                    return true;
            }

            // Останній рубіж, і він детермінований: перша найменша форма, що влазить,
            // стає першою фігурою. Випадковість тут була б брехнею — «не влазить нічого»
            // мусить означати, що не влазить нічого, а не що не пощастило двадцять разів.
            for (var i = 0; i < _catalog.Count; i++)
            {
                var shape = _catalog[i];
                if (shape.Size > rescueCap || !PlacementRules.AnyFit(board, shape))
                    continue;
                tray[0] = new PieceDef(shape, tray[0].IsEmpty ? PickPigment(tier, random) : tray[0].Pigment);
                return true;
            }

            return true;
        }

        private void Build(PieceDef[] tray, int sizeCap, int tier, Pigment wantedFirst, IRandomSource random)
        {
            for (var k = 0; k < tray.Length; k++)
            {
                var shape = PickShape(sizeCap, tier, random);
                var pigment = k == 0 && wantedFirst != Pigment.None ? wantedFirst : PickPigment(tier, random);
                _lastPigment = pigment;
                tray[k] = new PieceDef(shape, pigment);
            }

            // Тасування Фішера — Йетса: без нього «потрібний» колір завжди лежав би
            // першим, і гравець читав би лоток за позицією, а не за кольором.
            for (var i = tray.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (tray[i], tray[j]) = (tray[j], tray[i]);
            }
        }

        private void CountFits(Board board)
        {
            for (var i = 0; i < _catalog.Count; i++)
                _fits[i] = PlacementRules.CountFits(board, _catalog[i]);
        }

        private void CountPigments(Board board)
        {
            for (var i = 0; i < Pigments.Count; i++)
                _pigmentCounts[i] = board.CountOf(Pigments.FromIndex(i));
        }

        private PieceShape PickShape(int sizeCap, int tier, IRandomSource random)
        {
            var total = 0f;
            for (var i = 0; i < _catalog.Count; i++)
            {
                var shape = _catalog[i];
                var w = 0f;
                if (shape.Size <= sizeCap)
                {
                    w = _balance.SizeWeight(shape.Size, tier) * shape.Weight;
                    if (w > 0f && _balance.BagBias > 0f)
                        w *= (float)Math.Pow(_balance.BagFitOffset + _fits[i], _balance.BagBias);
                }

                _weights[i] = w;
                total += w;
            }

            if (total <= 0f)
                return _catalog.Smallest;

            // Ціле джерело випадковості → дріб у [0, 1): так само, як робить бот.
            var roll = random.Next(1 << 20) / (float)(1 << 20) * total;
            for (var i = 0; i < _catalog.Count; i++)
            {
                roll -= _weights[i];
                if (roll < 0f && _weights[i] > 0f)
                    return _catalog[i];
            }

            for (var i = _catalog.Count - 1; i >= 0; i--)
                if (_weights[i] > 0f)
                    return _catalog[i];
            return _catalog.Smallest;
        }

        private Pigment PickPigment(int tier, IRandomSource random)
        {
            if (_lastPigment != Pigment.None)
            {
                var streak = _balance.StreakChance(tier);
                if (streak > 0f && random.Next(1000) < streak * 1000f)
                    return _lastPigment;
            }

            // Перевага пігменту, якого на полі менше: поле само себе вирівнює,
            // і «застрягти без синього» стає неможливо, не вимикаючи випадковості.
            var max = 0;
            for (var i = 0; i < Pigments.Count; i++)
                if (_pigmentCounts[i] > max)
                    max = _pigmentCounts[i];

            var total = 0f;
            for (var i = 0; i < Pigments.Count; i++)
                total += 1f + (max - _pigmentCounts[i]) * _balance.ColorScarcityWeight;

            var roll = random.Next(1 << 20) / (float)(1 << 20) * total;
            for (var i = 0; i < Pigments.Count; i++)
            {
                roll -= 1f + (max - _pigmentCounts[i]) * _balance.ColorScarcityWeight;
                if (roll < 0f)
                    return Pigments.FromIndex(i);
            }

            return Pigments.FromIndex(Pigments.Count - 1);
        }
    }
}
