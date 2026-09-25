using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Мішок фігур (документ §2): «не чистий рандом — гра дивиться на форму вільного
    /// місця й підвищує ймовірність фігур, які туди поміщаються. Вона не підказує,
    /// куди класти, але не підсовує завідомо неможливий набір».
    ///
    /// Три правила, кожне під тестом:
    ///  (A) вага форми росте з кількістю її позицій на полі (<see cref="BalanceData.BagBias"/>);
    ///  (B) набір, у якому не влазить жодна фігура, перегенеровується — спершу як є,
    ///      потім із меншими фігурами (60 спроб, після 40 — стеля 3, далі 20 спроб двоклітинковими);
    ///  (C) якщо не влазить навіть двоклітинкова — місця справді немає, і це чесний
    ///      програш, а не вина мішка.
    ///
    /// Кольори (§5): лише кольори заливки поточної картинки, з вагою за тим, скільки
    /// пікселів кожного кольору ще лишилось. Прогресії кольорів немає — їх диктує картинка.
    /// </summary>
    public sealed class TrayGenerator
    {
        private readonly PieceCatalogData _catalog;
        private readonly BalanceData _balance;
        private readonly int[] _fits;
        private readonly float[] _weights;
        private IReadOnlyList<byte> _colors = Array.Empty<byte>();
        private IReadOnlyList<int> _colorWeights = Array.Empty<int>();

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
        }

        /// <summary>
        /// Наповнює лоток. <paramref name="colors"/> — кольори, які ще потрібні картинці,
        /// <paramref name="colorWeights"/> — скільки пікселів кожного лишилось (вага).
        /// Повертає true, якщо довелось зменшувати фігури (діагностика). Якщо не влазить
        /// нічого навіть після рятувальних спроб — лоток лишається останнім згенерованим,
        /// і сесія чесно фіксує програш.
        /// </summary>
        public bool Fill(PieceDef[] tray, Board board, IRandomSource random,
            IReadOnlyList<byte> colors, IReadOnlyList<int> colorWeights)
        {
            if (tray is null) throw new ArgumentNullException(nameof(tray));
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (random is null) throw new ArgumentNullException(nameof(random));
            if (colors is null || colors.Count == 0) throw new ArgumentException("Лоток без кольорів.", nameof(colors));
            if (colorWeights is null || colorWeights.Count != colors.Count) throw new ArgumentException("Ваги не збігаються з кольорами.", nameof(colorWeights));

            _colors = colors;
            _colorWeights = colorWeights;

            TraysIssued++;
            var tier = _balance.TierFor(TraysIssued);
            CountFits(board);

            var cap = _balance.SizeCapFor(tier);
            for (var attempt = 0; attempt < _balance.MaxTrayAttempts; attempt++)
            {
                var sizeCap = attempt < _balance.TrayShrinkAfterAttempts ? cap : Math.Min(cap, 3);
                Build(tray, sizeCap, tier, random);
                if (PlacementRules.AnyPieceFits(board, tray))
                    return false;
            }

            RescuesUsed++;
            var rescueCap = Math.Min(cap, _balance.MinPieceSize);
            for (var attempt = 0; attempt < _balance.TrayRescueAttempts; attempt++)
            {
                Build(tray, rescueCap, tier, random);
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
                tray[0] = new PieceDef(shape, tray[0].IsEmpty ? PickColor(random) : tray[0].Color);
                return true;
            }

            return true;
        }

        private void Build(PieceDef[] tray, int sizeCap, int tier, IRandomSource random)
        {
            for (var k = 0; k < tray.Length; k++)
                tray[k] = new PieceDef(PickShape(sizeCap, tier, random), PickColor(random));

            // Тасування Фішера — Йетса: без нього перша комірка завжди мала б «свою»
            // роль, і гравець читав би лоток за позицією.
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

        /// <summary>Колір — зважено за пікселями, що лишились; нульові ваги не випадають, якщо є ненульові.</summary>
        private byte PickColor(IRandomSource random)
        {
            var total = 0L;
            for (var i = 0; i < _colors.Count; i++)
                total += Math.Max(0, _colorWeights[i]);

            if (total <= 0)
                return _colors[random.Next(_colors.Count)];

            var roll = (long)(random.Next(1 << 20) / (double)(1 << 20) * total);
            for (var i = 0; i < _colors.Count; i++)
            {
                roll -= Math.Max(0, _colorWeights[i]);
                if (roll < 0 && _colorWeights[i] > 0)
                    return _colors[i];
            }

            for (var i = _colors.Count - 1; i >= 0; i--)
                if (_colorWeights[i] > 0)
                    return _colors[i];
            return _colors[0];
        }
    }
}
