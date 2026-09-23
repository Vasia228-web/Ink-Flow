using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Тема (документ §6): набір картинок одного світу — космос, природа… — з однією
    /// легендарною. Додати контент = намалювати одну тему, а не сто картинок.
    /// </summary>
    public sealed class ThemeDef
    {
        public ThemeDef(string id, string name, PictureDef[] pictures)
        {
            if (id is null || id.Length == 0) throw new ArgumentException("Порожній id теми.", nameof(id));
            if (name is null || name.Length == 0) throw new ArgumentException("Порожня назва теми.", nameof(name));
            if (pictures is null || pictures.Length == 0) throw new ArgumentException($"Тема «{id}» без картинок.", nameof(pictures));
            for (var i = 0; i < pictures.Length; i++)
                if (pictures[i].ThemeId != id)
                    throw new ArgumentException($"Картинка «{pictures[i].Id}» належить темі «{pictures[i].ThemeId}», а не «{id}».", nameof(pictures));

            Id = id;
            Name = name;
            Pictures = pictures;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<PictureDef> Pictures { get; }

        public int CountOf(Rarity rarity)
        {
            var n = 0;
            for (var i = 0; i < Pictures.Count; i++)
                if (Pictures[i].Rarity == rarity)
                    n++;
            return n;
        }
    }
}
