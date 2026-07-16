using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Частки вибуху (Unity built-in Particle System): 8-12 часток кольору клітинки,
    /// що розлітаються і згасають за ~300ms. Живе в EffectPool — жодних Instantiate/Destroy.
    /// Сам ParticleSystem конфігурує бутстрап при створенні префаба.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BurstEffect : MonoBehaviour, IPoolable
    {
        [SerializeField] private ParticleSystem particles;

        /// <summary>Скільки тримати ефект до повернення в пул.</summary>
        public float TotalDuration
        {
            get
            {
                var main = particles.main;
                return main.duration + main.startLifetime.constantMax;
            }
        }

        public void Play(Color color)
        {
            var main = particles.main;
            main.startColor = color;
            particles.Clear();
            particles.Play();
        }

        public void OnGetFromPool() { }

        public void OnReleaseToPool() =>
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
