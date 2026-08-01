using InkFlow.Platform;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Звук + гаптика ланцюга. Кожна наступна ланка звучить вище і б'є сильніше —
    /// саме це робить ланцюг «найкайфовішим моментом гри» (майстер-док §3).
    /// Клип генерується процедурно: плейсхолдер без бінарних асетів у репозиторії.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ChainFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private float basePitch = 1f;
        [SerializeField, Range(0f, 0.5f)] private float pitchStepPerLink = 0.08f;
        [SerializeField, Range(1f, 3f)] private float maxPitch = 2.2f;

        private AudioClip _popClip;
        private IHapticService _haptics = new NullHaptics();

        /// <summary>Гаптику підставляє композиційний корінь (§11) — геймплей не знає платформи.</summary>
        public void SetHaptics(IHapticService haptics) => _haptics = haptics ?? new NullHaptics();

        private void Awake() => _popClip = CreatePopClip();

        public void PlayMerge()
        {
            _haptics.Light();
        }

        /// <summary>chainIndex — 0-базований номер вибуху в ланцюгу.</summary>
        public void PlayBurst(int chainIndex)
        {
            if (_popClip != null && source != null)
            {
                source.pitch = Mathf.Min(maxPitch, basePitch + pitchStepPerLink * chainIndex);
                source.PlayOneShot(_popClip);
            }

            if (chainIndex == 0)
                _haptics.Medium();
            else
                _haptics.Chain(chainIndex + 1);
        }

        /// <summary>Синусовий «плоп» ~150 мс: частота спадає 600→450 Гц, експонентне згасання.</summary>
        private static AudioClip CreatePopClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.15f;
            var samples = new float[(int)(sampleRate * duration)];

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var phase = 2f * Mathf.PI * (600f * t - 500f * t * t);
                var envelope = Mathf.Exp(-t * 28f);
                samples[i] = Mathf.Sin(phase) * envelope * 0.5f;
            }

            var clip = AudioClip.Create("PopPlaceholder", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
