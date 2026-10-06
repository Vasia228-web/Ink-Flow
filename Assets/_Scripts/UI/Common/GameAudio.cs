using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Музика й загальний вимикач звуку (§15). Живе в головній сцені поруч із бутстрапом і читає
    /// перемикачі зі стану гравця: «Музика» вмикає й вимикає фоновий цикл, «Звук» — усі ефекти
    /// (поле питає <see cref="SoundOn"/> перед кожним «попом»).
    ///
    /// Треків у проєкті ще немає, тож цикл генерується: м'який акорд із трьох синусів із повільним
    /// диханням, безшовно зациклений (усі огинаючі — періодичні на довжині циклу). Це плейсхолдер,
    /// доки автор не дасть музику; перемикач уже справжній.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource music;
        [Tooltip("Гучність згенерованого циклу: тло, не солістка.")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.22f;

        private PlayerState? _state;
        private AudioClip? _loop;

        /// <summary>Чи грати ефекти зараз (перемикач «Звук»).</summary>
        public bool SoundOn => _state == null || _state.Settings.Sound;

        /// <summary>Чи грає музика зараз (тестам і дев-панелі).</summary>
        public bool MusicPlaying => music != null && music.isPlaying && !music.mute;

        /// <summary>Стан гравця підставляє композиційний корінь; перемикачі застосовуються одразу й на кожну зміну.</summary>
        public void Bind(PlayerState? state)
        {
            if (_state != null)
                _state.SettingsChanged -= Apply;
            _state = state;
            if (_state != null)
                _state.SettingsChanged += Apply;
            Apply();
        }

        private void Awake() => EnsureClip();

        /// <summary>
        /// Кліп і джерело готуються тут, а не лише в Awake: бутстрап кличе <see cref="Bind"/> зі свого
        /// Awake, і порядок Awake двох об'єктів Unity не гарантує — Play() на джерелі без кліпа мовчав би
        /// до першого перемикача.
        /// </summary>
        private bool EnsureClip()
        {
            if (music == null)
                music = GetComponent<AudioSource>();
            if (music == null)
                return false;
            if (music.clip == null)
            {
                _loop ??= CreateAmbientLoop();
                music.clip = _loop;
            }
            music.loop = true;
            music.playOnAwake = false;
            music.volume = musicVolume;
            return true;
        }

        private void OnDestroy()
        {
            if (_state != null)
                _state.SettingsChanged -= Apply;
            if (_loop != null)
            {
                if (Application.isPlaying) Destroy(_loop);
                else DestroyImmediate(_loop);
                _loop = null;
            }
        }

        /// <summary>Застосувати перемикачі: музика грає лише коли ввімкнена і застосунок у Play Mode.</summary>
        public void Apply()
        {
            if (!EnsureClip())
                return;
            var on = (_state == null || _state.Settings.Music) && Application.isPlaying;
            music.mute = !on;
            if (on && !music.isPlaying)
                music.UnPause();
            if (on && !music.isPlaying)
                music.Play();
            else if (!on && music.isPlaying)
                music.Pause();
        }

        /// <summary>
        /// Восьмисекундний цикл: акорд A3–E4–C#5 на синусах, кожна нота дихає власним періодом, кратним
        /// довжині циклу, тож стик нечутний; легкий п'ятий обертон додає «скла».
        /// </summary>
        private static AudioClip CreateAmbientLoop()
        {
            const int sampleRate = 44100;
            const float seconds = 8f;
            var length = (int)(sampleRate * seconds);
            var samples = new float[length];
            var notes = new[] { 220f, 329.63f, 554.37f };
            var breaths = new[] { 1f, 2f, 4f }; // дихань на цикл — періодичні, щоб стик був безшовним

            for (var i = 0; i < length; i++)
            {
                var t = i / (float)sampleRate;
                var u = t / seconds;
                var value = 0f;
                for (var n = 0; n < notes.Length; n++)
                {
                    var breath = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * breaths[n] * u + n * 1.7f);
                    var phase = 2f * Mathf.PI * notes[n] * t;
                    value += (Mathf.Sin(phase) + 0.08f * Mathf.Sin(5f * phase)) * breath / notes.Length;
                }
                samples[i] = value * 0.6f;
            }

            var clip = AudioClip.Create("AmbientPlaceholder", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
