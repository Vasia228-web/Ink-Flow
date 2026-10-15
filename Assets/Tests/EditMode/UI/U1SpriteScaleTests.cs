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
        public void Showcase_HasNoOverflowingTexts_AndNoSmallTaps_OnBothCheckpointDevices()
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
    }
}
