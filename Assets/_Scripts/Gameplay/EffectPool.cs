using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Пул burst-ефектів — той самий підхід, що CellPool: на мобільних
    /// Instantiate/Destroy часток під час chain-реакцій дає відчутні GC-паузи.
    /// Ефект повертається в пул автоматично після відльоту часток.
    /// </summary>
    public sealed class EffectPool : MonoBehaviour
    {
        [SerializeField] private BurstEffect effectPrefab;

        [Tooltip("Батьківський transform для ефектів з пулу.")]
        [SerializeField] private Transform contentRoot;

        [SerializeField] private int defaultCapacity = 8;
        [SerializeField] private int maxSize = 32;

        private ObjectPool<BurstEffect> _pool;

        private void Awake()
        {
            if (effectPrefab == null || contentRoot == null)
            {
                Debug.LogError(
                    "[InkFlow] EffectPool: не призначено Effect Prefab або Content Root. " +
                    "Запусти меню Ink Flow → Setup → Bootstrap Phase 1, щоб перебудувати сцену.",
                    this);
                enabled = false;
                return;
            }

            _pool = new ObjectPool<BurstEffect>(
                createFunc: CreatePooledEffect,
                actionOnGet: fx =>
                {
                    fx.gameObject.SetActive(true);
                    fx.OnGetFromPool();
                },
                actionOnRelease: fx =>
                {
                    fx.OnReleaseToPool();
                    fx.gameObject.SetActive(false);
                },
                actionOnDestroy: fx => Destroy(fx.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        /// <summary>Запускає частки вибуху в позиції position кольором color.</summary>
        public void PlayBurst(Vector3 position, Color color)
        {
            if (_pool == null)
                return;

            var fx = _pool.Get();
            fx.transform.position = position;
            fx.Play(color);
            StartCoroutine(ReleaseWhenDone(fx));
        }

        private IEnumerator ReleaseWhenDone(BurstEffect fx)
        {
            yield return new WaitForSeconds(fx.TotalDuration);
            _pool.Release(fx);
        }

        private BurstEffect CreatePooledEffect()
        {
            var fx = Instantiate(effectPrefab, contentRoot);
            fx.gameObject.SetActive(false);
            return fx;
        }

        private void OnDestroy() => _pool?.Dispose();
    }
}
