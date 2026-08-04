using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Спільний фон усіх екранів: радіальний градієнт «космос» + два шари зір.
    ///
    /// Макет: `radial-gradient(130% 90% at 50% -12%, #2A1E56, #1A1338 34%, #0C0820 68%, #070512)`.
    /// Зорі — два шари з мерехтінням 7 s і 11 s (reverse).
    ///
    /// Градієнт малюємо мешем на CanvasRenderer, а не текстурою: чотири зупинки
    /// на весь екран у вигляді PNG дали б або бандинг, або зайві мегабайти.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CosmicBackground : MaskableGraphic
    {
        [SerializeField] private DesignSystem design;

        [Tooltip("Кількість сегментів радіального градієнта. 24 достатньо, щоб не було кілець.")]
        [SerializeField, Range(8, 64)] private int segments = 28;

        [Tooltip("Центр градієнта в частках від розміру: макет — 50% / -12%.")]
        [SerializeField] private Vector2 center = new Vector2(0.5f, 1.12f);

        [Tooltip("Радіус градієнта в частках ширини/висоти: макет — 130% / 90%.")]
        [SerializeField] private Vector2 radius = new Vector2(1.3f, 0.9f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (design == null)
                return;

            var rect = rectTransform.rect;
            var origin = new Vector2(
                rect.xMin + rect.width * center.x,
                rect.yMin + rect.height * center.y);

            // Прямокутник фону не круглий, тож радіус беремо з запасом по діагоналі —
            // інакше в кутах лишиться незамальована зона.
            var rx = rect.width * radius.x;
            var ry = rect.height * radius.y;
            var reach = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) /
                        Mathf.Max(0.0001f, Mathf.Min(rx, ry));

            var stops = design.BackgroundStops;
            // Шість зупинок замість чотирьох: між кожною парою макета вставляємо
            // проміжну. Чотири зупинки на весь екран давали видимий бандинг —
            // око ловить межі там, де градієнт міняє нахил.
            var colors = new[]
            {
                design.BackgroundInner,
                Color.Lerp(design.BackgroundInner, design.BackgroundMid, 0.55f),
                design.BackgroundMid,
                Color.Lerp(design.BackgroundMid, design.BackgroundOuter, 0.5f),
                design.BackgroundOuter,
                design.BackgroundEdge
            };
            var positions = new[]
            {
                stops.x,
                Mathf.Lerp(stops.x, stops.y, 0.55f),
                stops.y,
                Mathf.Lerp(stops.y, stops.z, 0.5f),
                stops.z,
                Mathf.Max(stops.w, reach)
            };

            // Центральна вершина.
            vh.AddVert(origin, colors[0], Vector2.zero);

            var ringStart = 1;
            for (var ring = 0; ring < positions.Length; ring++)
            {
                var t = positions[ring];
                for (var s = 0; s <= segments; s++)
                {
                    var angle = Mathf.PI * 2f * s / segments;
                    var p = origin + new Vector2(Mathf.Cos(angle) * rx * t, Mathf.Sin(angle) * ry * t);
                    vh.AddVert(p, colors[ring], Vector2.zero);
                }
            }

            var perRing = segments + 1;

            // Внутрішній «віяло»-трикутник від центру до першого кільця.
            for (var s = 0; s < segments; s++)
                vh.AddTriangle(0, ringStart + s, ringStart + s + 1);

            // Кільця між зупинками.
            for (var ring = 0; ring < positions.Length - 1; ring++)
            {
                var a = ringStart + ring * perRing;
                var b = a + perRing;
                for (var s = 0; s < segments; s++)
                {
                    vh.AddTriangle(a + s, b + s, b + s + 1);
                    vh.AddTriangle(a + s, b + s + 1, a + s + 1);
                }
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
