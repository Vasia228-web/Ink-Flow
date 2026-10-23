using InkFlow.Platform;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Звук + гаптика поля: фігура лягла, лінія зірвалась, ланцюг, комбо-серія, чисте поле.
    /// Кожна наступна лінія одного ходу звучить вище і б'є сильніше — саме це робить ланцюг
    /// подією; так само дзвіночок серії піднімається з кожним ходом серії.
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
        [Tooltip("Дзвіночок серії: на скільки вище звучить кожен наступний хід серії.")]
        [SerializeField, Range(0f, 0.3f)] private float streakPitchStep = 0.06f;
        [SerializeField, Range(0f, 1f)] private float streakVolume = 0.55f;

        private AudioClip _popClip;
        private AudioClip _chimeClip;
        private IHapticService _haptics = new NullHaptics();
        private GameAudio? _audio;

        /// <summary>Гаптику підставляє композиційний корінь — геймплей не знає платформи (за перемикачем «Вібрація» — обгортка).</summary>
        public void SetHaptics(IHapticService haptics) => _haptics = haptics ?? new NullHaptics();

        /// <summary>Перемикач «Звук» (§15) живе в <see cref="GameAudio"/>: без нього поле звучить завжди (майстерня).</summary>
        public void SetAudio(GameAudio? audio) => _audio = audio;

        private bool SoundOn => _audio == null || _audio.SoundOn;

        private void Awake()
        {
            _popClip = CreatePopClip();
            _chimeClip = CreateChimeClip();
        }

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

        /// <summary>streak — довжина серії (≥ 2): дзвіночок над «плопом» лінії, щоразу вище.</summary>
        public void PlayStreak(int streak)
        {
            if (_chimeClip != null && source != null && SoundOn)
            {
                source.pitch = Mathf.Min(maxPitch, basePitch + streakPitchStep * (streak - 1));
                source.PlayOneShot(_chimeClip, streakVolume);
            }
        }

        /// <summary>Чисте поле: повний дзвіночок і найсильніший удар.</summary>
        public void PlayBoardClear()
        {
            if (_chimeClip != null && source != null && SoundOn)
            {
                source.pitch = basePitch;
                source.PlayOneShot(_chimeClip, 1f);
            }
            _haptics.Heavy();
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

        /// <summary>Дзвіночок ~350 мс: два тони (квінта 880 + 1320 Гц), друге вступає на 60 мс пізніше.</summary>
        private static AudioClip CreateChimeClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.35f;
            var samples = new float[(int)(sampleRate * duration)];

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / sampleRate;
                var low = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 9f);
                var high = t < 0.06f ? 0f : Mathf.Sin(2f * Mathf.PI * 1320f * (t - 0.06f)) * Mathf.Exp(-(t - 0.06f) * 9f);
                samples[i] = (low + high) * 0.25f;
            }

            var clip = AudioClip.Create("ChimePlaceholder", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
