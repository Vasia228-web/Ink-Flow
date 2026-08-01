using UnityEngine;
using UnityEngine.Pool;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Пул клітинок. Єдине місце в грі, якому дозволено Instantiate/Destroy крапель:
    /// на мобільних GC-спайк під час chain-вибухів = помітний фриз (§8, §18 інваріант 9).
    /// </summary>
    public sealed class CellPool : MonoBehaviour
    {
        [SerializeField] private CellView cellPrefab;

        [Tooltip("Батьківський transform для клітинок з пулу (зазвичай GridRoot).")]
        [SerializeField] private Transform contentRoot;

        [Tooltip("Створити наперед: сітка 7×7 = 49, з запасом.")]
        [SerializeField] private int defaultCapacity = 64;

        [SerializeField] private int maxSize = 128;

        private ObjectPool<CellView> _pool;

        /// <summary>Скільки клітинок зараз видано — PlayMode-тест перевіряє, що пул не тече.</summary>
        public int ActiveCount { get; private set; }

        private void Awake()
        {
            if (cellPrefab == null || contentRoot == null)
            {
                Debug.LogError(
                    "[InkFlow] CellPool: не призначено Cell Prefab або Content Root. " +
                    "Запусти меню Ink Flow → Setup → Bootstrap Scene, щоб перебудувати сцену.", this);
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
            EnsureReady();
            ActiveCount++;
            return _pool.Get();
        }

        public void Release(CellView view)
        {
            EnsureReady();
            ActiveCount--;
            _pool.Release(view);
        }

        private void EnsureReady()
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
