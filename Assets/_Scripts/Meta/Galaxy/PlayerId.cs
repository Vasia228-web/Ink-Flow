using System;

namespace InkFlow.Meta
{
    /// <summary>
    /// Ідентифікатор гравця. Потрібен уже зараз, бо екран галактики приймає
    /// власника: той самий екран показує і власну галактику, і чужу з Рейтингів.
    ///
    /// Обгортка над рядком, а не сам рядок: інакше «id гравця» й «id планети»
    /// стають взаємозамінними в сигнатурах, і компілятор про це мовчить.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <summary>Сам гравець. Порожній id теж означає «я» — це стан за замовчуванням.</summary>
        public static readonly PlayerId Self = default;

        private readonly string? _value;

        public PlayerId(string value) => _value = value;

        public string Value => _value ?? string.Empty;

        public bool IsSelf => Value.Length == 0;

        public bool Equals(PlayerId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => IsSelf ? "self" : Value;

        public static bool operator ==(PlayerId a, PlayerId b) => a.Equals(b);

        public static bool operator !=(PlayerId a, PlayerId b) => !a.Equals(b);
    }
}
