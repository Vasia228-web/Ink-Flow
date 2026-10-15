using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace InkFlow.Editor
{
    /// <summary>
    /// Генерує TMP Font Asset з Nunito, покриваючи Latin + Cyrillic.
    /// Меню: Ink Flow → Setup → Generate Font Assets.
    ///
    /// Чому Nunito, хоча макет намальовано в Baloo 2: Baloo 2 не має кирилиці,
    /// а весь інтерфейс гри українською. Nunito — найближча за характером
    /// (округла, геометрична, важкі накреслення) і має повний кириличний набір.
    ///
    /// ★ і ↺ у шрифті свідомо ВІДСУТНІ — вони згенеровані як спрайти
    /// (див. GenerateUISprites), бо жоден OFL-шрифт Google їх не покриває,
    /// та й у макеті вони намальовані фігурами, а не набрані текстом.
    /// </summary>
    public static class GenerateFontAsset
    {
        private const string FontFolder = "Assets/_Fonts";

        /// <summary>Розмір семплювання SDF. 90 — компроміс якості й розміру атласа.</summary>
        private const int SamplingPointSize = 90;

        private const int AtlasPadding = 9;

        /// <summary>
        /// Одна сторінка 2048×1024 на шрифт (Alpha8, 2 МБ — стільки ж, скільки дві сторінки 1024², що були).
        /// 303 гліфи при семплюванні 90 у 1024² не вміщались і розлітались на ДВІ сторінки, і це ламало два
        /// місця: кожен напис із гліфами з обох сторінок малювався двома мешами (зайвий виклик на кириличних
        /// написах), а еліпсис TMP малюється спецсимволом матеріалом першої сторінки — якщо «…» опинявся на
        /// другій, обрізаний нік лишався без крапок. Тест <c>FontAssets_FitOnOneAtlasPage</c>.
        /// </summary>
        private const int AtlasWidth = 2048;
        private const int AtlasHeight = 1024;

        private static readonly (string file, string label)[] Weights =
        {
            ("Nunito-Regular", "Nunito Regular SDF"),
            ("Nunito-Bold", "Nunito Bold SDF"),
            ("Nunito-ExtraBold", "Nunito ExtraBold SDF"),
            // U1Glaze: 900 — заголовки, числа, кнопки.
            ("Nunito-Black", "Nunito Black SDF")
        };

        public const string BlackPath = FontFolder + "/Nunito Black SDF.asset";
        public const string BodyPath = FontFolder + "/Nunito Bold SDF.asset";

        /// <summary>Пресет матеріалу з тінню тексту для коралової кнопки (SPEC: 0 −2 #6E0A28 35 %).</summary>
        public const string BlackShadowMaterialPath = FontFolder + "/Nunito Black SDF - Shadow.mat";

        [MenuItem("Ink Flow/Setup/Generate Font Assets")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            var characters = BuildCharacterSet();
            Debug.Log($"[InkFlow] Набір гліфів: {characters.Length} символів.");

            foreach (var (file, label) in Weights)
                BuildOne(file, label, characters);

            EnsureShadowMaterial();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Матеріал «Nunito Black SDF - Shadow»: копія матеріалу атласа з увімкненим Underlay —
        /// білий текст із тінню на кораловій кнопці. Окремий матеріал — окремий батч, тож
        /// його носить лише коралова кнопка (одна на екран). Числа тіні — токени DesignSystem.
        /// </summary>
        public static void EnsureShadowMaterial()
        {
            var black = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BlackPath);
            if (black == null || black.material == null)
            {
                Debug.LogWarning($"[InkFlow] Немає {BlackPath} — пресет тіні тексту не створено.");
                return;
            }
            var design = AssetDatabase.LoadAssetAtPath<InkFlow.Style.DesignSystem>("Assets/_ScriptableObjects/Style/DesignSystem.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>(BlackShadowMaterialPath);
            if (material == null)
            {
                material = new Material(black.material) { name = "Nunito Black SDF - Shadow" };
                AssetDatabase.CreateAsset(material, BlackShadowMaterialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(black.material);
            }
            material.EnableKeyword("UNDERLAY_ON");
            var shadow = design != null ? design.ButtonTextShadow : new Color(0.43f, 0.04f, 0.16f, 0.35f);
            material.SetColor("_UnderlayColor", shadow);
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", design != null ? design.ButtonTextShadowOffsetY : -0.35f);
            material.SetFloat("_UnderlayDilate", 0f);
            material.SetFloat("_UnderlaySoftness", design != null ? design.ButtonTextShadowSoftness : 0.2f);
            EditorUtility.SetDirty(material);
        }

        private static void BuildOne(string file, string label, string characters)
        {
            var sourcePath = $"{FontFolder}/{file}.ttf";
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[InkFlow] Не знайдено {sourcePath}. Шрифти качаються скриптом Tools/fetch-fonts.sh");
                return;
            }

            var assetPath = $"{FontFolder}/{label}.asset";
            AssetDatabase.DeleteAsset(assetPath);

            // Dynamic на час генерації: у цьому режимі TMP може допікати гліфи в атлас.
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError($"[InkFlow] TMP не зміг створити асет із {sourcePath}.");
                return;
            }

            fontAsset.name = label;
            fontAsset.TryAddCharacters(characters, out var missing);

            // Static: атлас заморожено, у білд іде лише текстура, а не TTF.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            var pages = 0;
            foreach (var texture in fontAsset.atlasTextures)
                if (texture != null && texture.width > 1)
                    pages++;
            if (pages > 1)
                Debug.LogError($"[InkFlow] {label}: гліфи розлетілись на {pages} сторінки атласа {AtlasWidth}×{AtlasHeight} — " +
                               "еліпсис («…» нікa) зникне, а кириличні написи малюватимуться двома мешами. Збільш атлас.");

            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // Матеріал і текстури атласа мусять стати під-асетами, інакше після
            // перезапуску редактора вони «загубляться» і шрифт стане рожевим.
            for (var i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                if (fontAsset.atlasTextures[i] == null)
                    continue;
                fontAsset.atlasTextures[i].name = $"{label} Atlas {i}";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{label} Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssetIfDirty(fontAsset);

            var glyphs = fontAsset.characterTable?.Count ?? 0;
            if (string.IsNullOrEmpty(missing))
                Debug.Log($"[InkFlow] {label}: {glyphs} гліфів, усі символи покрито ✔");
            else
                Debug.LogWarning($"[InkFlow] {label}: {glyphs} гліфів, БРАКУЄ {missing.Length}: " +
                                 $"{Describe(missing)}");
        }

        /// <summary>
        /// Діапазони, які реально використовує гра. Свідомо НЕ беремо все підряд:
        /// кожен зайвий гліф — це місце в атласі 1024×1024, а на мобільних це пам'ять.
        /// </summary>
        private static string BuildCharacterSet()
        {
            var sb = new StringBuilder();

            AddRange(sb, 0x0020, 0x007E); // ASCII: латиниця, цифри, пунктуація
            AddRange(sb, 0x00A0, 0x00FF); // Latin-1: ° × · « » тощо
            AddRange(sb, 0x0400, 0x045F); // Кирилиця (основна)
            AddRange(sb, 0x0490, 0x0491); // Ґ ґ — обов'язкові для української

            // Типографіка й символи, що реально трапляються в текстах гри.
            sb.Append('–'); // –
            sb.Append('—'); // —
            sb.Append('‘').Append('’'); // ' '
            sb.Append('“').Append('”'); // " "
            sb.Append('„'); // „
            sb.Append('…'); // …
            sb.Append('∞'); // ∞ — «Нескінченний» режим
            // → свідомо НЕ додаємо: у Nunito його немає, а в текстах гри він не трапляється.
            sb.Append('•'); // •

            // Ці чотири вже трапляються в написах, тож мусять бути в атласі.
            // Межа проста: те, що є ТЕКСТОМ (пунктуація, знаки), беремо гліфом;
            // те, що є КАРТИНКОЮ (★ ✓ ↺ ✎), малюємо спрайтом — саме тому їх тут немає.
            sb.Append('‹').Append('›'); // шеврони кнопок «назад» і карток
            sb.Append('№');             // «ще +2 до №213» у рейтингах
            sb.Append('−');             // справжній мінус у «−5%», «−45%»

            return sb.ToString();
        }

        private static void AddRange(StringBuilder sb, int from, int to)
        {
            for (var c = from; c <= to; c++)
                sb.Append((char)c);
        }

        private static string Describe(string missing)
        {
            var sb = new StringBuilder();
            foreach (var c in missing)
                sb.Append($"U+{(int)c:X4} ");
            return sb.ToString().Trim();
        }
    }
}
