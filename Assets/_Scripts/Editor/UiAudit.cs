using System.Collections.Generic;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.Editor
{
    /// <summary>
    /// Автоперевірка екрана в стенді (промт U1Glaze, «Самоперевірка»): жоден напис без Ellipsis
    /// не переповнений (TMP <c>isTextOverflowing</c>), жодна кнопка не менша за 44 px макета і жоден
    /// сусід у розкладці не налазить на інший (ряд, що не вміщається, група стискає до мінімуму —
    /// написи налізають один на одного, а переповнення тексту цього не бачить).
    /// Міряє в одиницях канваса стенда — тих самих, що й порівняння з еталоном.
    /// </summary>
    public static class UiAudit
    {
        /// <summary>Напис, що не вміщається, без «…» — його обріже маска або він налізе на сусіда.</summary>
        public static List<string> OverflowingTexts(Transform root)
        {
            var problems = new List<string>();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!text.enabled || string.IsNullOrWhiteSpace(text.text))
                    continue;
                if (text.overflowMode == TextOverflowModes.Ellipsis || text.overflowMode == TextOverflowModes.Truncate)
                    continue;
                text.ForceMeshUpdate();
                if (text.isTextOverflowing)
                    problems.Add($"{Path(text.transform)}: «{Shorten(text.text)}» переповнює {text.rectTransform.rect.width:0}×{text.rectTransform.rect.height:0}");
            }
            return problems;
        }

        /// <summary>Кнопки з зоною тапу меншою за <paramref name="minPx"/> px макета по будь-якій стороні.</summary>
        public static List<string> SmallTapTargets(Transform root, float minPx = 44f)
        {
            var problems = new List<string>();
            var canvas = root.GetComponentInParent<Canvas>();
            var canvasScale = canvas != null ? canvas.transform.lossyScale.x : 1f;
            var min = minPx * DesignSystem.MockupToReference - 0.5f;
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(false))
            {
                if (!selectable.interactable || !selectable.gameObject.activeInHierarchy)
                    continue;
                var rect = (RectTransform)selectable.transform;
                var scale = canvasScale > 0f ? rect.lossyScale.x / canvasScale : 1f;
                var w = rect.rect.width * scale;
                var h = rect.rect.height * scale;
                if (w < min || h < min)
                    problems.Add($"{Path(rect)}: зона тапу {w / DesignSystem.MockupToReference:0}×{h / DesignSystem.MockupToReference:0} px макета (< {minPx})");
            }
            return problems;
        }

        /// <summary>
        /// Сусіди в горизонтальній чи вертикальній групі, що налазять один на одного більше ніж на пів px
        /// макета. Порядок — за положенням, не за індексом (групи бувають з reverseArrangement); групи з
        /// від'ємним проміжком (накладання навмисне) і елементи поза розкладкою (тіні, гало) не рахуються.
        /// </summary>
        public static List<string> OverlappingSiblings(Transform root)
        {
            var problems = new List<string>();
            var eps = 0.5f * DesignSystem.MockupToReference;
            foreach (var group in root.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(false))
            {
                if (!group.enabled || group.spacing < 0f)
                    continue;
                var horizontal = group is HorizontalLayoutGroup;
                var space = (RectTransform)group.transform;
                var items = new List<(RectTransform rect, Rect box)>();
                foreach (Transform child in space)
                {
                    if (!child.gameObject.activeInHierarchy || !(child is RectTransform rect))
                        continue;
                    var element = child.GetComponent<ILayoutIgnorer>();
                    if (element != null && element.ignoreLayout)
                        continue;
                    var min = space.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
                    var max = space.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
                    items.Add((rect, Rect.MinMaxRect(min.x, min.y, max.x, max.y)));
                }
                items.Sort((a, b) => horizontal ? a.box.xMin.CompareTo(b.box.xMin) : b.box.yMax.CompareTo(a.box.yMax));
                for (var i = 1; i < items.Count; i++)
                {
                    var overlap = horizontal ? items[i - 1].box.xMax - items[i].box.xMin : items[i].box.yMax - items[i - 1].box.yMin;
                    if (overlap > eps)
                        problems.Add($"{Path(items[i].rect)}: налазить на «{items[i - 1].rect.name}» на {overlap / DesignSystem.MockupToReference:0} px макета");
                }
            }
            return problems;
        }

        /// <summary>Усі перевірки одним списком.</summary>
        public static List<string> All(Transform root)
        {
            var problems = OverflowingTexts(root);
            problems.AddRange(SmallTapTargets(root));
            problems.AddRange(OverlappingSiblings(root));
            return problems;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && parts.Count < 5; c = c.parent)
                parts.Insert(0, c.name);
            return string.Join("/", parts);
        }

        private static string Shorten(string text) => text.Length <= 32 ? text : text.Substring(0, 29) + "…";
    }
}
