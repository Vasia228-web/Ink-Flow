using System;
using System.Collections.Generic;
using System.IO;

namespace InkFlow.Core
{
    /// <summary>Назви тем для написів. Нова тема без назви показується своїм id — не падає.</summary>
    public static class ThemeNames
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["animals"] = "Тварини",
            ["plants"] = "Рослини",
            ["dinos"] = "Динозаврики",
            ["ghosts"] = "Привиди",
            ["food"] = "Їжа",
            ["sea"] = "Морські мешканці",
            ["space"] = "Космос",
            ["monsters"] = "Монстрики",
            ["weather"] = "Погода",
            ["things"] = "Предмети",
            ["sweets"] = "Солодощі",
            ["bugs"] = "Комашки",
            ["test"] = "Тест"
        };

        public static string Of(string themeId) => Names.TryGetValue(themeId, out var name) ? name : themeId;
    }

    /// <summary>
    /// Бібліотека картинок (документ §7): усі картинки гри, кожна — з окремого текстового
    /// файлу. У Unity тексти приходять із каталогу-асета, у тестах і прогоннику — з диска.
    /// Ідентифікатори у збереженні — назви картинок, не індекси: бібліотека росте темами.
    /// </summary>
    public sealed class PictureLibrary
    {
        private readonly PixelPicture[] _pictures;
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string[] _themes;

        public PictureLibrary(IReadOnlyList<PixelPicture> pictures)
        {
            if (pictures is null || pictures.Count == 0)
                throw new ArgumentException("Порожня бібліотека.", nameof(pictures));
            _pictures = new PixelPicture[pictures.Count];
            var themes = new List<string>();
            for (var i = 0; i < pictures.Count; i++)
            {
                var p = pictures[i] ?? throw new ArgumentNullException(nameof(pictures));
                if (_index.ContainsKey(p.Id))
                    throw new ArgumentException($"Картинка «{p.Id}» у бібліотеці двічі.", nameof(pictures));
                _index[p.Id] = i;
                _pictures[i] = p;
                if (!themes.Contains(p.ThemeId))
                    themes.Add(p.ThemeId);
            }
            _themes = themes.ToArray();
        }

        public int Count => _pictures.Length;
        public PixelPicture this[int index] => _pictures[index];
        public IReadOnlyList<PixelPicture> Pictures => _pictures;

        /// <summary>Теми в порядку першої появи.</summary>
        public IReadOnlyList<string> Themes => _themes;

        public int IndexOf(string id) => id != null && _index.TryGetValue(id, out var i) ? i : -1;

        public PixelPicture? Find(string id)
        {
            var i = IndexOf(id);
            return i < 0 ? null : _pictures[i];
        }

        public int CountOf(Rarity rarity)
        {
            var n = 0;
            for (var i = 0; i < _pictures.Length; i++)
                if (_pictures[i].Rarity == rarity)
                    n++;
            return n;
        }

        public int CountOfTheme(string themeId)
        {
            var n = 0;
            for (var i = 0; i < _pictures.Length; i++)
                if (_pictures[i].ThemeId == themeId)
                    n++;
            return n;
        }

        /// <summary>Розбирає тексти файлів; id береться з файлу, а без нього — з підказки.</summary>
        public static PictureLibrary Parse(IReadOnlyList<string> texts, IReadOnlyList<string>? fallbackIds = null)
        {
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            var list = new List<PixelPicture>(texts.Count);
            for (var i = 0; i < texts.Count; i++)
                list.Add(PixelPicture.Parse(texts[i], fallbackIds != null && i < fallbackIds.Count ? fallbackIds[i] : null));
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return new PictureLibrary(list);
        }

        /// <summary>Читає всі *.txt із теки (рекурсивно). Для тестів і прогонника; Unity читає асети.</summary>
        public static PictureLibrary LoadFromDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Немає теки картинок: {directory}");
            var files = Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            var texts = new List<string>(files.Length);
            var ids = new List<string>(files.Length);
            foreach (var file in files)
            {
                texts.Add(File.ReadAllText(file));
                ids.Add(Path.GetFileNameWithoutExtension(file));
            }
            return Parse(texts, ids);
        }

        /// <summary>
        /// Запасна бібліотека з однієї вбудованої картинки — щоб екран партії лишався
        /// запускним окремою сценою без асетів. У грі її ніколи не видно.
        /// </summary>
        public static PictureLibrary Fallback { get; } = new PictureLibrary(new[]
        {
            PixelPicture.Parse(
                "id: fallback_heart\nname: СЕРЦЕ\ntheme: test\nrarity: common\ncolors: K=1 R=8 P=20\noutline: K\ngrid:\n" +
                "..KKK..KKK..\n.KRRRKKRRRK.\nKRRPRRRRPRRK\nKRRRRRRRRRRK\nKRRRRRRRRRRK\n.KRRRRRRRRK.\n..KRRRRRRK..\n...KRRRRK...\n....KRRK....\n.....KK.....")
        });
    }
}
