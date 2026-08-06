using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Одна зона на поверхні планети. Об'єкт із пулу: створюється раз під час
    /// збирання екрана, далі лише перепризначається.
    ///
    /// Заливка йде маскою в шейдері (<c>_Fill</c>), тож проміжних станів рівно
    /// стільки, скільки кадрів. Зміна властивості матеріалу графіку не бруднить —
    /// на відміну від зміни кольору самого Image.
    /// </summary>
    public sealed class ZoneMarker : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader zoneShader;
        [SerializeField] private Image image;

        private static readonly int PaintId = Shader.PropertyToID("_Paint");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int SelectedId = Shader.PropertyToID("_Selected");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private Material? _material;
        private RectTransform _rect = null!;
        private CanvasRenderer _renderer = null!;
        private Coroutine? _fill;

        /// <summary>Зона, яку показує цей маркер. null — маркер вільний.</summary>
        public PlanetZone? Zone { get; private set; }

        /// <summary>Базовий діаметр без урахування ракурсу: щокадрове стиснення
        /// робить localScale, бо sizeDelta просив би перебудову графіки.</summary>
        public float BaseSize { get; private set; }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _renderer = GetComponent<CanvasRenderer>();
        }

        public void Bind(PlanetZone zone, float baseSize)
        {
            Zone = zone;
            BaseSize = baseSize;

            EnsureMaterial();
            _rect.sizeDelta = new Vector2(baseSize, baseSize);

            if (_material == null || design == null)
                return;

            // Форма плями стала для конкретної зони: сід від id, тож «материк»
            // не перестрибує з кадру в кадр і не змінюється між запусками.
            _material.SetFloat(SeedId, Mathf.Abs(zone.Id.GetHashCode() % 617) * 0.017f);
            _material.SetColor(PaintId, PaintColor(zone));
            _material.SetFloat(FillId, zone.IsPainted ? 1f : 0f);
            _material.SetFloat(SelectedId, 0f);
        }

        private Color PaintColor(PlanetZone zone) =>
            zone.Painted.HasValue ? design.Paint(zone.Painted.Value) : design.AccentBlue;

        public void SetSelected(bool selected)
        {
            if (_material != null)
                _material.SetFloat(SelectedId, selected ? 1f : 0f);
        }

        /// <summary>Ставить ракурс: зсув, стиснення й прозорість на краю кулі.</summary>
        public void Project(Vector2 localPosition, float scale, float alpha)
        {
            _rect.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            _rect.localScale = new Vector3(scale, scale, 1f);
            _renderer.SetAlpha(alpha);
        }

        /// <summary>
        /// Розтікання фарби від точки дотику. <paramref name="originUv"/> — у частках
        /// прямокутника зони; поза межами теж припустимо, фронт просто прийде збоку.
        /// </summary>
        public void PlayFill(PaintKindColor paint, Vector2 originUv, float duration)
        {
            if (_material == null)
                return;

            _material.SetColor(PaintId, paint.Color);
            _material.SetVector(OriginId, new Vector4(originUv.x, originUv.y, 0f, 0f));

            if (!isActiveAndEnabled)
            {
                _material.SetFloat(FillId, 1f);
                return;
            }

            if (_fill != null)
                StopCoroutine(_fill);
            _fill = StartCoroutine(FillRoutine(duration));
        }

        private System.Collections.IEnumerator FillRoutine(float duration)
        {
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                // Швидко на початку, м'яко наприкінці — так рідина «наздоганяє» край.
                var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 2f);
                _material!.SetFloat(FillId, k);
                yield return null;
            }

            _material!.SetFloat(FillId, 1f);
            _fill = null;
        }

        public void Release()
        {
            Zone = null;
            gameObject.SetActive(false);
        }

        private void EnsureMaterial()
        {
            if (_material != null || zoneShader == null || image == null)
                return;
            _material = new Material(zoneShader) { name = "Zone" };
            image.material = _material;
        }

        private void OnDestroy()
        {
            if (_material == null)
                return;
            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
        }
    }

    /// <summary>Фарба у вигляді, потрібному в'юхам: вид і колір разом.</summary>
    public readonly struct PaintKindColor
    {
        public PaintKindColor(InkFlow.Core.PaintKind kind, Color color)
        {
            Kind = kind;
            Color = color;
        }

        public InkFlow.Core.PaintKind Kind { get; }
        public Color Color { get; }
    }
}
