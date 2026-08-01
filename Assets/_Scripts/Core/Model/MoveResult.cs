using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Повний опис того, що сталося за хід, у вигляді впорядкованої стрічки подій (§4).
    ///
    /// Це найважливіше архітектурне рішення проєкту: Core не анімує — він повертає події,
    /// а GridView програє їх із таймінгами. Завдяки цьому логіка тестується без жодного
    /// кадру рендеру, анімації можна прискорити чи вимкнути, не чіпаючи правила, а симулятор
    /// Фази 4 ганяє мільйони ходів за секунди.
    ///
    /// ВАЖЛИВО: екземпляр переиспользується сесією між ходами (нуль алокацій, §12).
    /// Хто хоче зберегти події довше ніж до наступного ходу — копіює їх собі.
    /// </summary>
    public sealed class MoveResult
    {
        private readonly List<GameEvent> _events = new List<GameEvent>(64);

        /// <summary>false — хід відхилено (різні кольори, перешкода): ходи НЕ витрачаються (§5.1).</summary>
        public bool Accepted { get; private set; }

        public IReadOnlyList<GameEvent> Events => _events;

        /// <summary>Скільки вибухів сталося за цей хід — від цього залежать комбо-множник і feel.</summary>
        public int ChainDepth { get; private set; }

        public int ScoreGained { get; private set; }

        /// <summary>Ланцюг обірвано запобіжником — стан поля валідний, але не «доведений до кінця».</summary>
        public bool ChainWasTruncated { get; private set; }

        internal void Reset()
        {
            _events.Clear();
            Accepted = false;
            ChainDepth = 0;
            ScoreGained = 0;
            ChainWasTruncated = false;
        }

        internal void MarkAccepted() => Accepted = true;

        internal void Add(in GameEvent gameEvent)
        {
            _events.Add(gameEvent);
            switch (gameEvent.Type)
            {
                case GameEventType.Burst:
                    ChainDepth++;
                    break;
                case GameEventType.ChainTruncated:
                    ChainWasTruncated = true;
                    break;
            }
        }

        internal void AddScore(int points) => ScoreGained += points;

        /// <summary>Знімок подій — для тестів, реплеїв і логів (алокує, у грі не використовується).</summary>
        public GameEvent[] SnapshotEvents() => _events.ToArray();

        public int CountEvents(GameEventType type)
        {
            var count = 0;
            for (var i = 0; i < _events.Count; i++)
                if (_events[i].Type == type)
                    count++;
            return count;
        }
    }
}
