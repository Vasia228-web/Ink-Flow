using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Частинки вибуху/бризки. Конфігурує префаб бутстрап; ефект сам вертається в пул.
    ///
    /// УВАГА: кожен MonoBehaviour мусить лежати у файлі зі своєю назвою — інакше Unity
    /// не створює для нього MonoScript, і компонент серіалізується з порожнім m_Script
    /// («The referenced script on this Behaviour is missing»), хоча код компілюється.
    /// </summary>
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
}
