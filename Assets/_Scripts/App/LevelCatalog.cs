using System;
using InkFlow.Core;
using InkFlow.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace InkFlow.App
{
    /// <summary>
    /// Завантаження рівнів через Addressables (§7): рівні можна додавати й правити
    /// окремо від білду, без ре-публікації в сторах.
    /// Адреса рівня: "Levels/Level_001".
    /// </summary>
    public sealed class LevelCatalog : MonoBehaviour
    {
        public const string AddressPrefix = "Levels/";

        private AsyncOperationHandle<LevelDefinition> _handle;

        public static string AddressFor(int levelId) => $"{AddressPrefix}Level_{levelId:D3}";

        public void Load(int levelId, Action<LevelData> onLoaded, Action<string>? onFailed = null)
        {
            var address = AddressFor(levelId);
            Release();

            _handle = Addressables.LoadAssetAsync<LevelDefinition>(address);
            _handle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    onLoaded?.Invoke(handle.Result.ToLevelData());
                else
                    onFailed?.Invoke(
                        $"Не вдалося завантажити рівень '{address}': {handle.OperationException?.Message}");
            };
        }

        private void Release()
        {
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }

        private void OnDestroy() => Release();
    }
}
