using InkFlow.Core;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Картинка з колекції, поставлена на планету (майстер-док §10): накладка, яку
    /// <see cref="PlanetStage"/> проєктує щокадру так само, як зони, — зсув, стиснення
    /// й згасання на лімбі. Прозорість — через CanvasGroup: вона не перебудовує графіку.
    /// </summary>
    public sealed class PlacementMarker : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private PictureView picture;

        private RectTransform? _rect;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public bool IsBound { get; private set; }
        public float Longitude { get; private set; }
        public float Latitude { get; private set; }

        /// <summary>Базовий розмір без ракурсу: стиснення робить localScale.</summary>
        public float BaseSize => Rect.sizeDelta.x;

        public void Bind(PixelPicture picture, float longitude, float latitude)
        {
            Longitude = longitude;
            Latitude = latitude;
            IsBound = true;
            gameObject.SetActive(true);
            this.picture?.ShowCompleted(picture, string.Empty);
        }

        public void Release()
        {
            IsBound = false;
            gameObject.SetActive(false);
        }

        public void Project(Vector2 localPosition, float scale, float alpha)
        {
            Rect.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            Rect.localScale = new Vector3(scale, scale, 1f);
            if (group != null)
                group.alpha = alpha;
        }

        public void Apply() => picture?.Apply();
    }
}
