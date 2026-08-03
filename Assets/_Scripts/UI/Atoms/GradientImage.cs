using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Image з лінійним градієнтом по вершинах — дзеркало `linear-gradient(135deg, …)` з макета.
    ///
    /// Через вершинні кольори, а не шейдер: градієнт лягає на ті самі вершини, що вже
    /// генерує Image, тож це нуль додаткових draw call і нуль нових матеріалів —
    /// а бюджет ≤35 draw call на екран (§12) інакше з'їдається дуже швидко.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GradientImage : Image
    {
        [SerializeField] private Color colorFrom = Color.white;
        [SerializeField] private Color colorTo = Color.white;

        [Tooltip("Кут градієнта в градусах: 135 = зліва-вгору → вправо-вниз, як у макеті.")]
        [SerializeField, Range(0f, 360f)] private float angle = 135f;

        public void SetGradient(Color from, Color to)
        {
            colorFrom = from;
            colorTo = to;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            // Напрямок градієнта в локальних координатах прямокутника.
            var rad = Mathf.Deg2Rad * angle;
            var dir = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));

            // Проєкція кутів прямокутника на напрямок дає діапазон нормалізації.
            var corners = new[]
            {
                new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax)
            };

            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var corner in corners)
            {
                var d = Vector2.Dot(corner, dir);
                if (d < min) min = d;
                if (d > max) max = d;
            }

            var range = Mathf.Max(0.0001f, max - min);
            var vertex = new UIVertex();

            for (var i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                var t = (Vector2.Dot(vertex.position, dir) - min) / range;
                // Множимо, а не замінюємо: так Image.color і CanvasGroup.alpha
                // продовжують працювати як завжди.
                vertex.color *= Color.Lerp(colorFrom, colorTo, t);
                vh.SetUIVertex(vertex, i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
