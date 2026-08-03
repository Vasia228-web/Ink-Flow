using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Прив'язує RectTransform до безпечної зони екрана (§9).
    /// ОБОВ'ЯЗКОВО на корені кожного екрана: без цього UI ріжеться на iPhone з Dynamic Island
    /// і на Android з жестовою навігацією. Перераховується при зміні орієнтації/роздільності.
    /// </summary>
    /// <remarks>
    /// ExecuteAlways: без нього прив'язка застосовується лише в Play Mode, і в
    /// Scene view / Device Simulator верстка виглядає так, ніби safe area немає —
    /// шапка заходить під Dynamic Island.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaBinder : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void OnEnable()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == _lastSafeArea && resolution == _lastResolution)
                return;
            Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var min = _lastSafeArea.position;
            var max = min + _lastSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
