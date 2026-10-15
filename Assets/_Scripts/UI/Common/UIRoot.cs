using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Корінь інтерфейсу (§9): Canvas + CanvasScaler під портрет + стек навігації.
    ///
    /// Reference 1080×1920. Match залежить від співвідношення сторін екрана
    /// (<see cref="MatchFor(float, float)"/>): на телефонах (9:16 і вужчих) — 0, тобто
    /// прив'язка до ширини: 1 px макета 402×874 = 1080/402 одиниці на будь-якому телефоні,
    /// і кнопка 56 px макета має ті самі 56 px на екрані 402 завширшки; на вужчих 20:9 зайва
    /// висота йде гнучким блокам (заставка, планета, список) — саме так намальовано S12.
    /// На планшетах (ширше за 9:16) match росте до 1 на 3:4, щоб по висоті вміщалось
    /// щонайменше 1920 одиниць — інакше на iPad відрізало б низ екрана.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UIRoot : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>Співвідношення ширина/висота, до якого (включно) працює чиста прив'язка до ширини.</summary>
        public const float PhoneAspect = 9f / 16f;

        /// <summary>Співвідношення, на якому прив'язка стає повністю до висоти (iPad 3:4).</summary>
        public const float TabletAspect = 3f / 4f;

        /// <summary>
        /// Match для CanvasScaler за розміром екрана: 0 на телефонах, лінійно до 1 між 9:16 і 3:4.
        /// Одна функція для гри і для стендів знімків — інакше знімки брехали б про розкладку.
        /// </summary>
        public static float MatchFor(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
                return 0f;
            var aspect = screenWidth / screenHeight;
            return Mathf.Clamp01((aspect - PhoneAspect) / (TabletAspect - PhoneAspect));
        }

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private NavigationStack navigation;
        [SerializeField] private SafeAreaBinder safeArea;

        public NavigationStack Navigation => navigation;
        public SafeAreaBinder SafeArea => safeArea;
        public Canvas Canvas => canvas;

        private void Awake() => ApplyScaler();

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, ApplyScaler);
#endif

        /// <summary>
        /// Публічний, бо бутстрап мусить викликати його синхронно перед SaveScene:
        /// правка з OnValidate відкладена на наступний кадр і в збережену сцену
        /// вже не потрапила б — CanvasScaler лишився б дефолтним.
        /// </summary>
        public void ApplyScaler()
        {
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchFor(Screen.width, Screen.height);
        }
    }
}
