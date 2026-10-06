using InkFlow.Platform;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Звук + гаптика поля: фігура лягла, лінія зірвалась, ланцюг. Кожна наступна
    /// лінія одного ходу звучить вище і б'є сильніше — саме це робить ланцюг подією.
    /// Клип генерується процедурно: плейсхолдер без бінарних асетів у репозиторії.
    ///
    /// Саме заради цього компонента збірка UI має посилання на Platform — гаптику
    /// підставляє композиційний корінь.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class BoardFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private float basePitch = 1f;
        [SerializeField, Range(0f, 0.5f)] private float pitchStepPerLine = 0.1f;
        [SerializeField, Range(1f, 3f)] private float maxPitch = 2.2f;

        private AudioClip _popClip;
        private IHapticService _haptics = new NullHaptics();
        private GameAudio? _audio;

        /// <summary>Гаптику підставляє композиційний корінь — геймплей не знає платформи (за перемикачем «Вібрація» — обгортка).</summary>
        public void SetHaptics(IHapticService haptics) => _haptics = haptics ?? new NullHaptics();

        /// <summary>Перемикач «Звук» (§15) живе в <see cref="GameAudio"/>: без нього поле звучить завжди (майстерня).</summary>
        public void SetAudio(GameAudio? audio) => _audio = audio;

        private bool SoundOn => _audio == null || _audio.SoundOn;

        private void Awake() => _popClip = CreatePopClip();

        public void PlayPlace() => _haptics.Light();

        /// <summary>lineIndex — 0-базований номер лінії в межах ходу.</summary>
        public void PlayLineClear(int lineIndex, bool pure)
        {
            if (_popClip != null && source != null && SoundOn)
            {
                source.pitch = Mathf.Min(maxPitch, basePitch + pitchStepPerLine * lineIndex + (pure ? 0.15f : 0f));
                source.PlayOneShot(_popClip, pure ? 1f : 0.7f);
            }

            if (lineIndex == 0)
                _haptics.Medium();
            else
                _haptics.Chain(lineIndex + 1);
        }

        public void PlayCombo(int lines) => _haptics.Chain(lines);

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
