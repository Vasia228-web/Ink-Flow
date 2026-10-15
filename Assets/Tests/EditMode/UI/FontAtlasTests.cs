using NUnit.Framework;
using TMPro;
using UnityEditor;

namespace InkFlow.Tests.UI
{
    /// <summary>
    /// Шрифти гри (крок 1 U1Glaze): кожен SDF-асет — ОДНА сторінка атласа. Коли гліфи розліталися на дві,
    /// кириличний напис малювався двома мешами, а еліпсис TMP (спецсимвол, матеріал першої сторінки) брав
    /// порожнє місце: довгий нік на головному обрізався без «…».
    /// </summary>
    public sealed class FontAtlasTests
    {
        [Test]
        public void FontAssets_FitOnOneAtlasPage_AndHaveTheEllipsis()
        {
            foreach (var name in new[] { "Nunito Regular SDF", "Nunito Bold SDF", "Nunito ExtraBold SDF", "Nunito Black SDF" })
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/_Fonts/{name}.asset");
                Assert.IsNotNull(font, $"{name} — спершу Generate Font Assets (Build UI Kit)");
                var pages = 0;
                foreach (var texture in font.atlasTextures)
                    if (texture != null && texture.width > 1)
                        pages++;
                Assert.AreEqual(1, pages, $"{name}: сторінок атласа {pages}");
                Assert.IsTrue(font.characterLookupTable.TryGetValue(0x2026, out var ellipsis) && ellipsis.glyph.atlasIndex == 0,
                    $"{name}: «…» має лежати на першій сторінці — її бере еліпсис TMP");
            }
        }
    }
}
