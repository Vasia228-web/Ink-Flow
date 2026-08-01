using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace InkFlow.Gameplay
{
    /// <summary>Частинки вибуху/бризки. Конфігурує префаб бутстрап; ефект сам вертається в пул.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BurstEffect : MonoBehaviour, IPoolable
    {
        [SerializeField] private ParticleSystem particles;

        public float TotalDuration
        {
            get
            {
                var main = particles.main;
                return main.duration + main.startLifetime.constantMax;
            }
        }

        public void Play(Color color, float scale = 1f)
        {
            var main = particles.main;
            main.startColor = color;
            transform.localScale = Vector3.one * scale;
            particles.Clear();
            particles.Play();
        }

        public void OnGetFromPool() { }

        public void OnReleaseToPool() =>
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Пул частинок — той самий принцип, що CellPool: під час партії нічого не інстанціюється (§8).
    /// </summary>
    public sealed class ParticlePool : MonoBehaviour
    {
        [SerializeField] private BurstEffect effectPrefab;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private int defaultCapacity = 8;
        [SerializeField] private int maxSize = 32;

        private ObjectPool<BurstEffect> _pool;

        private void Awake()
        {
            if (effectPrefab == null || contentRoot == null)
            {
                Debug.LogError(
                    "[InkFlow] ParticlePool: не призначено Effect Prefab або Content Root. " +
                    "Запусти меню Ink Flow → Setup → Bootstrap Scene.", this);
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

        /// <summary>Вибух: сила керує розміром сплеску — важкий вибух виглядає важчим.</summary>
        public void PlayBurst(Vector3 position, Color color, float scale = 1f)
        {
            if (_pool == null)
                return;

            var fx = _pool.Get();
            fx.transform.position = position;
            fx.Play(color, scale);
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
