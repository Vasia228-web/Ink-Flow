using System;
using System.IO;
using InkFlow.Core;

namespace InkFlow.Tests.Meta
{
    /// <summary>Справжня бібліотека картинок для Meta-тестів: збереження говорить назвами, тож потрібні справжні назви.</summary>
    public static class TestLibrary
    {
        private static PictureLibrary? _real;

        public static PictureLibrary Real => _real ??= PictureLibrary.LoadFromDirectory(FindPictures());

        private static string FindPictures()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "_Pictures");
                    if (Directory.Exists(candidate))
                        return candidate;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Не знайшов Assets/_Pictures.");
        }
    }
}
