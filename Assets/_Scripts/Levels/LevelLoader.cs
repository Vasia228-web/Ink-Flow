using System;
using InkFlow.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace InkFlow.Levels
{
    /// <summary>
    /// Завантажує LevelConfig-асет через Addressables (група "Levels") і віддає
    /// POCO-знімок LevelData. Addressables — щоб рівні/арти можна було оновлювати
    /// окремо від білду (live-ops), а не заради складності: локально це той самий асет.
    /// </summary>
    public sealed class LevelLoader : MonoBehaviour
    {
        private AsyncOperationHandle<LevelConfig> _handle;

        /// <summary>Асинхронно завантажує рівень за адресою (напр. "Levels/Level_001").</summary>
        public void Load(string address, Action<LevelData> onLoaded, Action<string> onFailed = null)
        {
            ReleaseHandle();
            _handle = Addressables.LoadAssetAsync<LevelConfig>(address);
            _handle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    onLoaded?.Invoke(handle.Result.ToLevelData());
                else
                    onFailed?.Invoke(
                        $"Не вдалося завантажити рівень '{address}': {handle.OperationException?.Message}");
            };
        }

        private void ReleaseHandle()
        {
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }

        private void OnDestroy() => ReleaseHandle();
    }
}
