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
    /// Ширше за 9:16 (планшети, розкладні, split-screen) — 1, прив'язка до висоти: по висоті рівно
    /// 1920 одиниць, зайва ширина — бічним полям. Разом це те саме, що <c>ScreenMatchMode.Expand</c>:
    /// канвас ніколи не менший за 1080×1920 ні по ширині, ні по висоті, а на межі 9:16 обидва
    /// варіанти дають той самий масштаб — стрибка немає. (Лінійна рампа 0→1 між 9:16 і 3:4, як було
    /// спершу, давала на проміжних екранах висоту менше 1920: iPad mini — 1778, і низ відрізало б.)
    /// Розмір вікна на Android може змінитись на ходу (split-screen, розкладні) — match перераховується.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UIRoot : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>Співвідношення ширина/висота, до якого (включно) працює прив'язка до ширини.</summary>
        public const float PhoneAspect = 9f / 16f;

        /// <summary>
        /// Match для CanvasScaler за розміром екрана: 0 на 9:16 і вужчих, 1 — на ширших.
        /// Одна функція для гри і для стендів знімків — інакше знімки брехали б про розкладку.
        /// </summary>
        public static float MatchFor(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
                return 0f;
            return screenWidth / screenHeight <= PhoneAspect + 1e-4f ? 0f : 1f;
        }

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private NavigationStack navigation;
        [SerializeField] private SafeAreaBinder safeArea;

        public NavigationStack Navigation => navigation;
        public SafeAreaBinder SafeArea => safeArea;
        public Canvas Canvas => canvas;

        private Vector2Int _appliedFor;

        private void Awake() => ApplyScaler();

        /// <summary>Вікно змінило розмір (split-screen, розкладний) — той самий match, що й на старті, але для нового розміру.</summary>
        private void Update()
        {
            if (Screen.width != _appliedFor.x || Screen.height != _appliedFor.y)
                ApplyScaler();
        }

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
            _appliedFor = new Vector2Int(Screen.width, Screen.height);
            scaler.matchWidthOrHeight = MatchFor(Screen.width, Screen.height);
        }
    }
}
