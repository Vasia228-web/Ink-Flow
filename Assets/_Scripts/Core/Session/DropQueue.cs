using System;

namespace InkFlow.Core
{
    /// <summary>Крапля, яка чекає в черзі на долив.</summary>
    public readonly struct QueuedDrop
    {
        public QueuedDrop(InkColor color, int density)
        {
            Color = color;
            Density = density;
        }

        public InkColor Color { get; }
        public int Density { get; }

        public bool IsEmpty => Color == InkColor.None;
    }

    /// <summary>
    /// Черга наступних крапель (майстер-док §5).
    ///
    /// Це не прев'ю збоку від рефілу, а сам його ДЖЕРЕЛО: долив бере краплі
    /// звідси по одній. Інакше показане й насипане розійшлись би — а черга
    /// цінна саме тим, що з неї можна планувати хід.
    ///
    /// Кільцевий буфер сталої довжини: за партію відбувається тисячі доливів,
    /// і жоден із них не має алокувати.
    /// </summary>
    public sealed class DropQueue
    {
        /// <summary>Скільки крапель видно гравцеві.</summary>
        public const int PreviewCount = 5;

        private readonly QueuedDrop[] _items = new QueuedDrop[PreviewCount];
        private int _head;

        /// <summary>Скільки крапель зараз у черзі (у грі — завжди PreviewCount).</summary>
        public int Count { get; private set; }

        /// <summary>
        /// Крапля за номером наперед: 0 — та, що впаде найближчою.
        /// Саме її екран малює більшою і з кольоровим кільцем.
        /// </summary>
        public QueuedDrop Peek(int ahead)
        {
            if (ahead < 0 || ahead >= Count)
                throw new ArgumentOutOfRangeException(nameof(ahead));
            return _items[(_head + ahead) % PreviewCount];
        }

        public void Enqueue(QueuedDrop drop)
        {
            if (Count >= PreviewCount)
                throw new InvalidOperationException("Черга повна — спершу Dequeue.");
            _items[(_head + Count) % PreviewCount] = drop;
            Count++;
        }

        public QueuedDrop Dequeue()
        {
            if (Count == 0)
                throw new InvalidOperationException("Черга порожня.");
            var drop = _items[_head];
            _head = (_head + 1) % PreviewCount;
            Count--;
            return drop;
        }

        public void Clear()
        {
            _head = 0;
            Count = 0;
            Array.Clear(_items, 0, _items.Length);
        }
    }
}
