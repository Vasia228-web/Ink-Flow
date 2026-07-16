using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Плейсхолдер-звук вибуху: короткий процедурний «плоп» (генерується в Awake,
    /// без бінарних асетів). За ТЗ кожна наступна ланка ланцюга звучить вище —
    /// AudioSource.pitch зростає з chainIndex.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ChainAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        [SerializeField] private float basePitch = 1f;

        [Tooltip("Приріст pitch на кожну наступну ланку ланцюга.")]
        [SerializeField, Range(0f, 0.5f)] private float pitchStepPerLink = 0.08f;

        [SerializeField, Range(1f, 3f)] private float maxPitch = 2.2f;

        private AudioClip _popClip;

        private void Awake() => _popClip = CreatePopClip();

        /// <summary>Звук ланки ланцюга; chainIndex — 0-базований номер вибуху.</summary>
        public void PlayChainPop(int chainIndex)
        {
            if (_popClip == null || source == null)
                return;

            source.pitch = Mathf.Min(maxPitch, basePitch + pitchStepPerLink * chainIndex);
            source.PlayOneShot(_popClip);
        }

        /// <summary>Синусовий «плоп» ~150ms: частота спадає 600→450 Гц, експонентне згасання.</summary>
        private static AudioClip CreatePopClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.15f;
            var samples = new float[(int)(sampleRate * duration)];

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var phase = 2f * Mathf.PI * (600f * t - 500f * t * t); // 600 Гц → ~450 Гц
                var envelope = Mathf.Exp(-t * 28f);
                samples[i] = Mathf.Sin(phase) * envelope * 0.5f;
            }

            var clip = AudioClip.Create("PopPlaceholder", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
