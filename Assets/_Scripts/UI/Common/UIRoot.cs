using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Корінь інтерфейсу (§9): Canvas + CanvasScaler під портрет + стек навігації.
    ///
    /// Reference 1080×1920, match = 0.5. Саме 0.5, а не 0 чи 1: на вузьких 20:9
    /// прив'язка лише до ширини роздула б інтерфейс по висоті, а лише до висоти —
    /// обрізала б по боках. Половина тримає компроміс на всьому діапазоні
    /// від 4:3 (планшети) до 20:9.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UIRoot : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float MatchWidthOrHeight = 0.5f;

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private NavigationStack navigation;
        [SerializeField] private SafeAreaBinder safeArea;

        public NavigationStack Navigation => navigation;
        public SafeAreaBinder SafeArea => safeArea;
        public Canvas Canvas => canvas;

        private void Awake() => ApplyScaler();

#if UNITY_EDITOR
        private void OnValidate() => ApplyScaler();
#endif

        private void ApplyScaler()
        {
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
        }
    }
}
