// Адаптери Unity Gaming Services (майстер-док §16): Authentication (анонімний вхід), Leaderboards
// (чотири таблиці: планети/галактики × тиждень/увесь час), Cloud Save (публічна вітрина).
//
// Компілюється лише з define INKFLOW_UGS і встановленими пакетами com.unity.services.core,
// com.unity.services.authentication, com.unity.services.leaderboards, com.unity.services.cloudsave.
// Без них гра збирається з Null/Fake-реалізаціями й працює повністю (§16: офлайн-варіант обов'язковий).
// Ручні кроки для автора — у CLAUDE.md і звіті Фази 7.
#if INKFLOW_UGS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InkFlow.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;
using CoreEntry = InkFlow.Core.LeaderboardEntry;

namespace InkFlow.Platform.Ugs
{
    /// <summary>Спільна ініціалізація UGS: один раз на застосунок, помилка не валить гру.</summary>
    public static class UgsCore
    {
        private static Task? _init;

        public static Task EnsureInitialized()
        {
            if (_init == null)
                _init = UnityServices.InitializeAsync();
            return _init;
        }

        public static bool Online => Application.internetReachability != NetworkReachability.NotReachable;

        /// <summary>Leaderboards і Cloud Save вимагають автентифікованого гравця: перед кожним викликом — вхід, якщо його ще немає.</summary>
        public static async Task EnsureSignedIn()
        {
            await EnsureInitialized();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public static LeaderboardStatus StatusFor(Exception error) =>
            !Online ? LeaderboardStatus.NoConnection : LeaderboardStatus.Failed;
    }

    public sealed class UgsIdentity : IIdentityService
    {
        public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized
                                  && AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        public async void SignIn(Action<bool> done)
        {
            try
            {
                await UgsCore.EnsureSignedIn();
                done?.Invoke(true);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[InkFlow] UGS: вхід не вдався — {error.Message}");
                done?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Таблиці лідерів UGS. Ідентифікатори чотирьох таблиць — з конфігу (AppConfig); тижневі таблиці
    /// скидаються на сервері (reset schedule — щопонеділка 00:00 UTC, як і локальна база тижня).
    /// Нік, аватар і прапорець інкогніто — у метаданих запису (у дашборді ввімкнути Metadata).
    /// </summary>
    public sealed class UgsLeaderboards : ILeaderboardService
    {
        [Serializable]
        private sealed class EntryMetadata
        {
            public string nick = string.Empty;
            public int avatar;
            public bool incognito;
        }

        private readonly string _planetsWeek, _planetsAll, _galaxiesWeek, _galaxiesAll;
        private readonly Func<EntryMetadata> _metadata;

        public UgsLeaderboards(string planetsWeek, string planetsAll, string galaxiesWeek, string galaxiesAll,
            Func<(string nick, int avatar, bool incognito)> metadata)
        {
            _planetsWeek = planetsWeek;
            _planetsAll = planetsAll;
            _galaxiesWeek = galaxiesWeek;
            _galaxiesAll = galaxiesAll;
            _metadata = () =>
            {
                var (nick, avatar, incognito) = metadata();
                return new EntryMetadata { nick = nick, avatar = avatar, incognito = incognito };
            };
        }

        public bool IsAvailable => !string.IsNullOrEmpty(_planetsWeek) && !string.IsNullOrEmpty(_planetsAll)
                                   && !string.IsNullOrEmpty(_galaxiesWeek) && !string.IsNullOrEmpty(_galaxiesAll);

        private string IdOf(RankMetric metric, RankPeriod period) => metric switch
        {
            RankMetric.Planets => period == RankPeriod.Week ? _planetsWeek : _planetsAll,
            _ => period == RankPeriod.Week ? _galaxiesWeek : _galaxiesAll
        };

        public async void Fetch(RankMetric metric, RankPeriod period, int limit, Action<LeaderboardPage> done)
        {
            if (!IsAvailable)
            {
                done?.Invoke(LeaderboardPage.Unavailable(metric, period, LeaderboardStatus.NotConfigured));
                return;
            }
            if (!UgsCore.Online)
            {
                done?.Invoke(LeaderboardPage.Unavailable(metric, period, LeaderboardStatus.NoConnection));
                return;
            }
            try
            {
                await UgsCore.EnsureSignedIn();
                var id = IdOf(metric, period);
                var scores = await LeaderboardsService.Instance.GetScoresAsync(id,
                    new GetScoresOptions { Limit = Math.Max(1, limit), IncludeMetadata = true });
                var entries = new List<CoreEntry>(scores.Results.Count);
                foreach (var row in scores.Results)
                {
                    var meta = ParseMetadata(row.Metadata);
                    entries.Add(new CoreEntry(row.PlayerId, string.IsNullOrEmpty(meta.nick) ? row.PlayerName : meta.nick,
                        meta.avatar, meta.incognito, (long)row.Score, row.Rank + 1));
                }

                var yourRank = 0;
                long yourValue = 0;
                try
                {
                    var mine = await LeaderboardsService.Instance.GetPlayerScoreAsync(id);
                    yourRank = mine.Rank + 1;
                    yourValue = (long)mine.Score;
                }
                catch (Exception)
                {
                    // Гравець ще нічого не надсилав у цю таблицю — місця немає, це не помилка.
                }
                done?.Invoke(new LeaderboardPage(metric, period, LeaderboardStatus.Ok, entries, yourRank, yourValue));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[InkFlow] UGS: таблиця не прочиталась — {error.Message}");
                done?.Invoke(LeaderboardPage.Unavailable(metric, period, UgsCore.StatusFor(error), error.Message));
            }
        }

        private static EntryMetadata ParseMetadata(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return new EntryMetadata();
            try { return JsonUtility.FromJson<EntryMetadata>(json) ?? new EntryMetadata(); }
            catch (Exception) { return new EntryMetadata(); }
        }

        public async void Submit(RankMetric metric, RankPeriod period, long value, Action<bool>? done = null)
        {
            if (!IsAvailable || !UgsCore.Online)
            {
                done?.Invoke(false);
                return;
            }
            try
            {
                await UgsCore.EnsureSignedIn();
                var options = new AddPlayerScoreOptions { Metadata = _metadata() };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(IdOf(metric, period), value, options);
                done?.Invoke(true);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[InkFlow] UGS: значення не надіслалось — {error.Message}");
                done?.Invoke(false);
            }
        }
    }

    /// <summary>Публічна вітрина в Cloud Save: один ключ із публічним доступом; чужу читаємо за PlayerId.</summary>
    public sealed class UgsShowcase : IShowcaseService
    {
        [Serializable]
        private sealed class SlotDto { public string planet = string.Empty; public int slot; public string picture = string.Empty; }

        [Serializable]
        private sealed class ShowcaseDto
        {
            public string nick = string.Empty;
            public int avatar;
            public bool incognito;
            public string showcase = string.Empty;
            public int planetsDone;
            public int galaxiesDone;
            public int galaxy;
            public List<SlotDto> slots = new List<SlotDto>();
            public string updatedUtc = string.Empty;
        }

        private readonly string _key;

        public UgsShowcase(string key) => _key = key;

        public bool IsAvailable => !string.IsNullOrEmpty(_key);

        public async void Publish(PublicShowcase showcase, Action<bool>? done = null)
        {
            if (!IsAvailable || !UgsCore.Online || showcase == null)
            {
                done?.Invoke(false);
                return;
            }
            try
            {
                await UgsCore.EnsureSignedIn();
                var dto = new ShowcaseDto
                {
                    nick = showcase.Nick, avatar = showcase.AvatarId, incognito = showcase.Incognito,
                    showcase = showcase.ShowcasePictureId ?? string.Empty, planetsDone = showcase.PlanetsDone,
                    galaxiesDone = showcase.GalaxiesDone, galaxy = showcase.Galaxy, updatedUtc = showcase.UpdatedUtc
                };
                foreach (var slot in showcase.Slots)
                    dto.slots.Add(new SlotDto { planet = slot.PlanetId, slot = slot.Slot, picture = slot.PictureId });
                var data = new Dictionary<string, object> { [_key] = JsonUtility.ToJson(dto) };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data, new SaveOptions(new PublicWriteAccessClassOptions()));
                done?.Invoke(true);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[InkFlow] UGS: вітрина не опублікувалась — {error.Message}");
                done?.Invoke(false);
            }
        }

        public async void Fetch(string playerId, Action<PublicShowcase?> done)
        {
            if (!IsAvailable || !UgsCore.Online || string.IsNullOrEmpty(playerId))
            {
                done?.Invoke(null);
                return;
            }
            try
            {
                await UgsCore.EnsureSignedIn();
                var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { _key },
                    new LoadOptions(new PublicReadAccessClassOptions(playerId)));
                if (!loaded.TryGetValue(_key, out var item))
                {
                    done?.Invoke(null);
                    return;
                }
                var dto = JsonUtility.FromJson<ShowcaseDto>(item.Value.GetAs<string>());
                if (dto == null)
                {
                    done?.Invoke(null);
                    return;
                }
                var slots = new List<ShowcaseSlot>(dto.slots.Count);
                foreach (var slot in dto.slots)
                    slots.Add(new ShowcaseSlot(slot.planet, slot.slot, slot.picture));
                done?.Invoke(new PublicShowcase(playerId, dto.nick, dto.avatar, dto.incognito, dto.showcase,
                    dto.planetsDone, dto.galaxiesDone, dto.galaxy, slots, dto.updatedUtc));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[InkFlow] UGS: вітрина не прочиталась — {error.Message}");
                done?.Invoke(null);
            }
        }
    }
}
#endif
