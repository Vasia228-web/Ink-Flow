using UnityEngine;
using UnityEngine.Pool;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Обгортка над UnityEngine.Pool.ObjectPool&lt;CellView&gt;.
    /// Єдине місце в грі, якому дозволено Instantiate/Destroy клітинок:
    /// на мобільних GC-паузи від Instantiate/Destroy під час chain-бурстів
    /// відчуваються як лаги, тому в'ю бере клітинки тільки через Get()/Release().
    /// </summary>
    public sealed class CellPool : MonoBehaviour
    {
        [SerializeField] private CellView cellPrefab;

        [Tooltip("Батьківський transform для клітинок з пулу (зазвичай GridRoot).")]
        [SerializeField] private Transform contentRoot;

        [Tooltip("Скільки клітинок створити наперед: сітка 7×7 = 49, з запасом.")]
        [SerializeField] private int defaultCapacity = 64;

        [SerializeField] private int maxSize = 128;

        private ObjectPool<CellView> _pool;

        private void Awake()
        {
            if (cellPrefab == null || contentRoot == null)
            {
                Debug.LogError(
                    "[InkFlow] CellPool: не призначено Cell Prefab або Content Root. " +
                    "Запусти меню Ink Flow → Setup → Bootstrap Phase 1, щоб перебудувати сцену.",
                    this);
                enabled = false;
                return;
            }

            _pool = new ObjectPool<CellView>(
                createFunc: CreatePooledCell,
                actionOnGet: view =>
                {
                    view.gameObject.SetActive(true);
                    view.OnGetFromPool();
                },
                actionOnRelease: view =>
                {
                    view.OnReleaseToPool();
                    view.gameObject.SetActive(false);
                },
                actionOnDestroy: view => Destroy(view.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public CellView Get()
        {
            EnsureInitialized();
            return _pool.Get();
        }

        public void Release(CellView view)
        {
            EnsureInitialized();
            _pool.Release(view);
        }

        private void EnsureInitialized()
        {
            if (_pool == null)
                throw new System.InvalidOperationException(
                    "CellPool не ініціалізовано (cellPrefab/contentRoot порожні) — див. помилку в консолі.");
        }

        private CellView CreatePooledCell()
        {
            var view = Instantiate(cellPrefab, contentRoot);
            view.gameObject.SetActive(false);
            return view;
        }

        private void OnDestroy() => _pool?.Dispose();
    }
}
