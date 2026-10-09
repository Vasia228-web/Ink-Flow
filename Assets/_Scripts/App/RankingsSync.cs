using System;
using System.Collections.Generic;
using System.Text;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Platform;

namespace InkFlow.App
{
    /// <summary>
    /// Міст між локальним станом і хмарою (майстер-док §16): локальний файл — правда, у хмару йде лише
    /// публічна вітрина та значення двох метрик за два періоди. Без Unity — перевіряється headless.
    ///
    /// Події стану (слоти, профіль, перемикачі) лише ставлять прапорець; <see cref="Flush"/> (раз на кадр
    /// із бутстрапа) будує вітрину й порівнює з останньою опублікованою: без змін — жодного запиту.
    /// Так дванадцять картинок на планеті — одна публікація, а перемикач звуку — жодної. Вхід у хмару
    /// повторюється, доки не вдасться (офлайн-старт не вбиває сесію).
    /// </summary>
    public sealed class RankingsSync : IDisposable
    {
        private readonly PlayerState _state;
        private readonly IIdentityService _identity;
        private readonly ILeaderboardService _leaderboards;
        private readonly IShowcaseService _showcases;
        private readonly Func<DateTime> _clock;
        private readonly Dictionary<(RankMetric, RankPeriod), long> _sent = new Dictionary<(RankMetric, RankPeriod), long>();
        private string? _publishedSignature;
        private bool _dirty;
        private bool _signingIn;
        private bool _disposed;

        public RankingsSync(PlayerState state, IIdentityService identity, ILeaderboardService leaderboards, IShowcaseService showcases,
            Func<DateTime>? clock = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _leaderboards = leaderboards ?? throw new ArgumentNullException(nameof(leaderboards));
            _showcases = showcases ?? throw new ArgumentNullException(nameof(showcases));
            _clock = clock ?? (() => DateTime.UtcNow);

            _state.GalaxyChanged += MarkDirty;
            _state.ProfileChanged += MarkDirty;
            _state.SettingsChanged += MarkDirty;
        }

        /// <summary>Хмарний id гравця; порожньо, поки не ввійшов.</summary>
        public string PlayerId => _identity.IsSignedIn ? _identity.PlayerId : string.Empty;

        /// <summary>Скільки разів вітрину справді публікували (дев-панелі й тестам).</summary>
        public int Publications { get; private set; }

        /// <summary>Скільки значень справді надіслано в таблиці.</summary>
        public int Submissions { get; private set; }

        /// <summary>Є зміни, які ще не пішли в хмару.</summary>
        public bool IsDirty => _dirty;

        /// <summary>Увійти (якщо ще ні) і показати себе світу. Повторний виклик безпечний — і саме він лікує офлайн-старт.</summary>
        public void Start()
        {
            _dirty = true;
            Flush();
        }

        /// <summary>Щось змінилось — у хмару піде на наступному <see cref="Flush"/>.</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>
        /// Раз на кадр: якщо є зміни — вітрину й числа в хмару, але лише те, що справді змінилось від
        /// останньої вдалої відправки. Без входу — спроба ввійти, зміни чекають.
        /// </summary>
        public void Flush()
        {
            if (_disposed || !_dirty)
                return;
            if (!_identity.IsSignedIn)
            {
                SignIn();
                return;
            }
            _dirty = false;
            var now = _clock();

            if (_showcases.IsAvailable)
            {
                var showcase = _state.BuildShowcase(_identity.PlayerId, now);
                var signature = SignatureOf(showcase);
                if (!string.Equals(signature, _publishedSignature, StringComparison.Ordinal))
                    _showcases.Publish(showcase, ok =>
                    {
                        if (ok) { _publishedSignature = signature; Publications++; }
                        else _dirty = true;
                    });
            }

            if (_leaderboards.IsAvailable)
                foreach (var metric in new[] { RankMetric.Planets, RankMetric.Galaxies })
                    foreach (var period in new[] { RankPeriod.Week, RankPeriod.AllTime })
                        SubmitIfChanged(metric, period, _state.RankValue(metric, period, now));
        }

        private void SubmitIfChanged(RankMetric metric, RankPeriod period, long value)
        {
            if (_sent.TryGetValue((metric, period), out var last) && last == value)
                return;
            _leaderboards.Submit(metric, period, value, ok =>
            {
                if (ok) { _sent[(metric, period)] = value; Submissions++; }
                else _dirty = true;
            });
        }

        private void SignIn()
        {
            if (_signingIn)
                return;
            _signingIn = true;
            _identity.SignIn(ok =>
            {
                _signingIn = false;
                if (ok && !_disposed)
                    Flush();
            });
        }

        /// <summary>Підпис вітрини — усе, що бачать інші; без дати, бо вона міняється сама.</summary>
        private static string SignatureOf(PublicShowcase showcase)
        {
            var sb = new StringBuilder(64 + showcase.Slots.Count * 24);
            sb.Append(showcase.Incognito ? 'h' : 'v').Append('|').Append(showcase.Nick).Append('|').Append(showcase.AvatarId)
              .Append('|').Append(showcase.ShowcasePictureId ?? string.Empty).Append('|').Append(showcase.PlanetsDone)
              .Append('|').Append(showcase.GalaxiesDone).Append('|').Append(showcase.Galaxy);
            foreach (var slot in showcase.Slots)
                sb.Append('|').Append(slot.PlanetId).Append(':').Append(slot.Slot).Append(':').Append(slot.PictureId);
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _state.GalaxyChanged -= MarkDirty;
            _state.ProfileChanged -= MarkDirty;
            _state.SettingsChanged -= MarkDirty;
        }
    }
}
