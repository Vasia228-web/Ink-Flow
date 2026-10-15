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
                var rect = text.rectTransform.rect;
                // isTextOverflowing — лише по вертикалі (TMP рахує рядки, що не влізли у висоту); однорядковий
                // NoWrap-напис, ширший за свій прямокутник, він не бачить — тому окремо міряємо ширину.
                // Міряємо видимі гліфи, а не textBounds: у межі TMP входить і розрядка після останньої літери
                // (ярлики з розрядкою .16em «виходили» на 5 одиниць, хоча за край не вилазить жоден піксель).
                var available = rect.width - text.margin.x - text.margin.z;
                var inkWidth = VisibleWidth(text);
                var tooWide = inkWidth > available + 0.5f * DesignSystem.MockupToReference;
                if (text.isTextOverflowing || tooWide)
                    problems.Add($"{Path(text.transform)}: «{Shorten(text.text)}» переповнює {rect.width:0}×{rect.height:0}" +
                                 (tooWide ? $" (ширина тексту {inkWidth:0})" : string.Empty));
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
        /// макета, і горизонтальний ряд, чий вміст ширший за сам ряд (діти виїжджають за край — так ряд із
        /// шести рамок рідкості вилазив за екран). Порядок — за положенням, не за індексом (групи бувають з
        /// reverseArrangement); групи з від'ємним проміжком (накладання навмисне) і елементи поза
        /// розкладкою (тіні, гало) не рахуються.
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
                if (horizontal && items.Count > 0)
                {
                    var bounds = space.rect;
                    var outside = Mathf.Max(bounds.xMin - items[0].box.xMin, items[items.Count - 1].box.xMax - bounds.xMax);
                    if (outside > eps)
                        problems.Add($"{Path(space)}: ряд ширший за себе — діти виходять за край на {outside / DesignSystem.MockupToReference:0} px макета");
                }
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

        /// <summary>Ширина від лівого краю першого до правого краю останнього видимого гліфа, в одиницях напису.</summary>
        private static float VisibleWidth(TMP_Text text)
        {
            var info = text.textInfo;
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible)
                    continue;
                min = Mathf.Min(min, c.bottomLeft.x);
                max = Mathf.Max(max, c.topRight.x);
            }
            return max > min ? max - min : 0f;
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
