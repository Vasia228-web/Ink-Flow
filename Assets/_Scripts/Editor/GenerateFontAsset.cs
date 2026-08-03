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
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        private static readonly (string file, string label)[] Weights =
        {
            ("Nunito-Regular", "Nunito Regular SDF"),
            ("Nunito-Bold", "Nunito Bold SDF"),
            ("Nunito-ExtraBold", "Nunito ExtraBold SDF")
        };

        [MenuItem("Ink Flow/Setup/Generate Font Assets")]
        public static void Generate()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            var characters = BuildCharacterSet();
            Debug.Log($"[InkFlow] Набір гліфів: {characters.Length} символів.");

            foreach (var (file, label) in Weights)
                BuildOne(file, label, characters);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            sb.Append('→'); // →
            sb.Append('∞'); // ∞ — «Нескінченний» режим
            sb.Append('•'); // •

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
