using InkFlow.Editor;
using InkFlow.Style;
using InkFlow.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.Tests.UI
{
    /// <summary>
    /// Уточнення автора п.2: спрайти U1Glaze (3×) при канвасі 1080×1920 мають давати на екрані
    /// рівно розмір макета. Перевіряється на одному спрайті (button_coral, 56 px макета) на
    /// пристрої 402×874 @3×: у reference-одиницях — 56 × 2.687, у пікселях кадру — 56 × 3 = 168.
    /// </summary>
    public sealed class U1SpriteScaleTests
    {
        [Test]
        public void ButtonCoral_OnTheMockupDevice_IsExactlyTheMockupSize()
        {
            Assert.IsTrue(MetaScreenRig<ScreenBase>.Available("ComponentsShowcase"), "спершу Build Components Showcase");
            using var rig = MetaScreenRig<ComponentsShowcase>.Create(ScreenRigBase.Mockup, "ComponentsShowcase");
            rig.Enter(null, ScreenArgs.Empty);
            rig.Screen.PreviewPage(0);
            rig.Render();

            var body = rig.Safe.Find("ComponentsShowcase/Scroll/Viewport/Content/КНОПКИ/Button_Coral/Body")?.GetComponent<Image>();
            Assert.IsNotNull(body, "тіло коралової кнопки на вітрині");
            var sprite = body!.sprite;
            Assert.AreEqual(U1GlazeSprites.PixelsPerUnit, sprite.pixelsPerUnit, 0.01f, "PPU спрайта з імпортера");

            var rect = body.rectTransform;
            var expectedUnits = 56f * DesignSystem.MockupToReference;
            Assert.AreEqual(expectedUnits, rect.rect.height, 0.6f, "висота кнопки в reference-одиницях = 56 px макета");

            // У пікселях кадру 1206×2622 (@3×) ті самі 56 px макета = 168 px.
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var top = rig.ToPixel(corners[1]).y;
            var bottom = rig.ToPixel(corners[0]).y;
            Assert.AreEqual(56f * 3f, top - bottom, 1.5f, "висота кнопки в пікселях екрана 402×874 @3×");

            // Native-розмір самого спрайта теж збігається з макетом (288×168 px текстури → 96×56 макета).
            var native = new Vector2(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit * 100f;
            Assert.AreEqual(96f * DesignSystem.MockupToReference, native.x, 0.6f);
            Assert.AreEqual(56f * DesignSystem.MockupToReference, native.y, 0.6f);
        }

        [Test]
        public void Showcase_HasNoOverflowingTexts_NoSmallTaps_AndNoOverlaps_OnBothCheckpointDevices()
        {
            Assert.IsTrue(MetaScreenRig<ScreenBase>.Available("ComponentsShowcase"), "спершу Build Components Showcase");
            foreach (var device in new[] { ScreenRigBase.Mockup, ScreenRigBase.Small })
            {
                using var rig = MetaScreenRig<ComponentsShowcase>.Create(device, "ComponentsShowcase");
                rig.Enter(null, ScreenArgs.Empty);
                rig.Render();
                var problems = UiAudit.All(rig.Safe);
                Assert.AreEqual(0, problems.Count, $"{device.Name}:\n" + string.Join("\n", problems));
            }
        }

        [Test]
        public void WidthAudit_CatchesAWideSingleLine_ButNotTrailingLetterSpacing()
        {
            // isTextOverflowing — лише по вертикалі; однорядковий NoWrap-напис, ширший за прямокутник, ловить
            // окрема міра ширини. Розрядка після останньої літери (ярлики .16em) переповненням не вважається.
            var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Fonts/Nunito Bold SDF.asset");
            Assert.IsNotNull(font, "Nunito Bold SDF — спершу Generate Font Assets");
            var root = new GameObject("AuditCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var go = new GameObject("Label", typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                var label = go.AddComponent<TMPro.TextMeshProUGUI>();
                label.font = font;
                label.fontSize = 40f;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                label.overflowMode = TMPro.TextOverflowModes.Overflow;
                label.characterSpacing = 16f;
                label.text = "НЕЗВИЧАЙНА";

                label.rectTransform.sizeDelta = new Vector2(label.GetPreferredValues().x, 60f);
                Assert.AreEqual(0, UiAudit.OverflowingTexts(root.transform).Count, "напис рівно у свій preferred-розмір — не переповнення");

                label.rectTransform.sizeDelta = new Vector2(100f, 60f);
                var problems = UiAudit.OverflowingTexts(root.transform);
                Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
                StringAssert.Contains("ширина тексту", problems[0]);

                // Бічний виступ гліфа («ніжка» «Д» у Nunito Black) — не переповнення: напис у свій preferred-розмір чистий.
                var black = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Fonts/Nunito Black SDF.asset");
                Assert.IsNotNull(black, "Nunito Black SDF — спершу Generate Font Assets");
                label.font = black;
                label.characterSpacing = 0f;
                label.fontSize = 54f;
                foreach (var word in new[] { "Далі", "Летіти" })
                {
                    label.text = word;
                    label.rectTransform.sizeDelta = new Vector2(label.GetPreferredValues().x, 80f);
                    var clean = UiAudit.OverflowingTexts(root.transform);
                    Assert.AreEqual(0, clean.Count, $"«{word}» рівно у свій preferred-розмір:\n" + string.Join("\n", clean));
                    label.rectTransform.sizeDelta = new Vector2(label.GetPreferredValues().x * 0.8f, 80f);
                    Assert.AreEqual(1, UiAudit.OverflowingTexts(root.transform).Count, $"«{word}» у 80 % ширини — переповнення");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OverlapAudit_CatchesSiblingsThatRunIntoEachOther_AndPassesANormalRow()
        {
            // Регресія до перевірки: на вітрині кроку 0 три бейджі рідкості в ряд на 402 налізали один на одного,
            // а переповнення тексту цього не бачило. Справний ряд — чисто; зсунутий сусід — вада.
            var root = new GameObject("AuditCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(root.transform, false);
                var rowRect = (RectTransform)row.transform;
                rowRect.sizeDelta = new Vector2(1000f, 100f);
                var group = row.GetComponent<HorizontalLayoutGroup>();
                group.spacing = 20f;
                group.childControlWidth = group.childControlHeight = false;
                group.childForceExpandWidth = group.childForceExpandHeight = false;
                RectTransform Item(string name)
                {
                    var go = new GameObject(name, typeof(RectTransform));
                    go.transform.SetParent(row.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.sizeDelta = new Vector2(300f, 80f);
                    return rect;
                }
                var a = Item("A");
                var b = Item("B");
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
                Assert.AreEqual(0, UiAudit.OverlappingSiblings(root.transform).Count, "ряд із проміжком — без накладань");

                // B на 40 одиниць лізе на A (≈15 px макета) — як бейдж, розтягнутий фітером ширше за свій слот.
                b.localPosition = a.localPosition + new Vector3(a.rect.width - 40f, 0f, 0f);
                var problems = UiAudit.OverlappingSiblings(root.transform);
                Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
                StringAssert.Contains("налазить на «A»", problems[0]);

                // Ряд, вужчий за дітей: без накладань, але діти виїжджають за край — теж вада.
                b.localPosition = a.localPosition + new Vector3(a.rect.width + 20f, 0f, 0f);
                rowRect.sizeDelta = new Vector2(500f, 100f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
                var tooNarrow = UiAudit.OverlappingSiblings(root.transform);
                Assert.AreEqual(1, tooNarrow.Count, string.Join("\n", tooNarrow));
                StringAssert.Contains("ряд ширший", tooNarrow[0]);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ButtonAudit_CatchesAButtonThatGrewWithTheColumn_AndPassesAPinnedRow()
        {
            // Регресія кроку 3: ряд «Відкрити» з childForceExpandHeight став гнучким по висоті, і стовпчик віддав
            // кнопці пів залишку — 56 px макета виросли до двохсот. Ні переповнення, ні накладання цього не бачать.
            var design = ScriptableObject.CreateInstance<DesignSystem>();
            var root = new GameObject("AuditCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var column = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
                column.transform.SetParent(root.transform, false);
                var columnRect = (RectTransform)column.transform;
                columnRect.sizeDelta = new Vector2(1000f, 2000f);
                var col = column.GetComponent<VerticalLayoutGroup>();
                col.childControlWidth = col.childControlHeight = true;
                col.childForceExpandWidth = true;
                col.childForceExpandHeight = false;

                var area = new GameObject("Carousel", typeof(RectTransform), typeof(LayoutElement));
                area.transform.SetParent(column.transform, false);
                area.GetComponent<LayoutElement>().flexibleHeight = 1f;

                var row = new GameObject("OpenRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(column.transform, false);
                var rowGroup = row.GetComponent<HorizontalLayoutGroup>();
                rowGroup.childControlWidth = rowGroup.childControlHeight = true;
                rowGroup.childForceExpandWidth = rowGroup.childForceExpandHeight = true;
                var rowSize = row.GetComponent<LayoutElement>();
                rowSize.preferredHeight = design.ButtonHeight;

                var buttonGo = new GameObject("Open", typeof(RectTransform), typeof(LayoutElement));
                buttonGo.transform.SetParent(row.transform, false);
                buttonGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                var button = buttonGo.AddComponent<CandyButton>();
                var so = new UnityEditor.SerializedObject(button);
                so.FindProperty("design").objectReferenceValue = design;
                so.ApplyModifiedPropertiesWithoutUndo();

                LayoutRebuilder.ForceRebuildLayoutImmediate(columnRect);
                var grown = UiAudit.OversizedButtons(root.transform);
                Assert.AreEqual(1, grown.Count, "ряд без flexibleHeight 0 ділить залишок із каруселлю");
                StringAssert.Contains("кнопка", grown[0]);

                rowSize.flexibleHeight = 0f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(columnRect);
                Assert.AreEqual(0, UiAudit.OversizedButtons(root.transform).Count, "прибитий ряд — кнопка висоти макета");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(design);
            }
        }
    }
}
