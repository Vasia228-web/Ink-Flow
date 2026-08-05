using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Зорі космічного фону: один меш із квадратиків, що мерехтять із різними періодами.
    ///
    /// Макет має два шари з `background-size` 236 px і 340 px та мерехтінням 7 s / 11 s.
    /// Тут це один Graphic замість двох шарів частинок: 90 зір — це 90 квадів
    /// в одному draw call, а ParticleSystem коштував би окремого матеріалу й апдейта.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StarField : MaskableGraphic
    {
        [SerializeField] private DesignSystem design;

        [Tooltip("Сід розкладки зір: та сама розкладка при кожному запуску.")]
        [SerializeField] private int seed = 20260803;

        [Tooltip("Розмір зорі в reference-одиницях (1-1.5 px макета).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(2.7f, 4f);

        private float[] _phases = System.Array.Empty<float>();
        private float[] _speeds = System.Array.Empty<float>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (design == null || design.StarCount <= 0)
                return;

            var rect = rectTransform.rect;
            var random = new System.Random(seed);
            var count = design.StarCount;

            if (_phases.Length != count)
            {
                _phases = new float[count];
                _speeds = new float[count];
            }

            for (var i = 0; i < count; i++)
            {
                var x = rect.xMin + (float)random.NextDouble() * rect.width;
                var y = rect.yMin + (float)random.NextDouble() * rect.height;
                // Кубічний розподіл: дрібних зір багато, великих одиниці —
                // саме так виглядає справжнє небо.
                var roll = (float)random.NextDouble();
                var size = Mathf.Lerp(sizeRange.x, sizeRange.y, roll * roll * roll);

                _phases[i] = (float)random.NextDouble() * Mathf.PI * 2f;
                // Мерехтить лише частина зір: коли блимає все небо, воно читається
                // як шум, а не як глибина. Решта світить рівно (швидкість 0).
                _speeds[i] = (float)random.NextDouble() < design.StarTwinkleFraction
                    ? (i % 2 == 0 ? design.StarTwinkleSlow : design.StarTwinkleFast)
                    : 0f;

                // Яскравість корелює з розміром — інакше дрібні зорі «кричать».
                var alpha = Mathf.Lerp(0.28f, 0.95f, roll * 0.6f + (float)random.NextDouble() * 0.4f);
                var tint = i % 3 == 0
                    ? new Color(0.82f, 0.9f, 1f, alpha)   // холодні зорі макета (#CFE6FF)
                    : new Color(1f, 1f, 1f, alpha);

                AddQuad(vh, new Vector2(x, y), size, tint);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 pos, float size, Color color)
        {
            var index = vh.currentVertCount;
            var h = size * 0.5f;
            vh.AddVert(new Vector3(pos.x - h, pos.y - h), color, Vector2.zero);
            vh.AddVert(new Vector3(pos.x - h, pos.y + h), color, Vector2.up);
            vh.AddVert(new Vector3(pos.x + h, pos.y + h), color, Vector2.one);
            vh.AddVert(new Vector3(pos.x + h, pos.y - h), color, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private void LateUpdate()
        {
            if (design == null || _phases.Length == 0)
                return;

            // Мерехтіння як дихання прозорості шару: перебудовувати меш щокадру
            // заради 90 квадів — марна трата, а різниця на око невідчутна.
            // Два періоди накладаються, тож ритм не читається як синусоїда.
            var slow = Mathf.Sin(Time.time / design.StarTwinkleSlow * Mathf.PI * 2f);
            var fast = Mathf.Sin(Time.time / design.StarTwinkleFast * Mathf.PI * 2f + _phases[0]);
            var t = 0.5f + 0.25f * (slow + fast);
            canvasRenderer.SetAlpha(Mathf.Lerp(0.72f, 1f, t));
        }
    }
}
