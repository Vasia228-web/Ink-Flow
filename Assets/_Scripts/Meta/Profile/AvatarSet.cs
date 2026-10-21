using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Набір готових аватарів (майстер-док §14, U1Glaze): 8 спрайтів avatar_1…8. У файлі лежить номер
    /// (<c>ProfileData.AvatarId</c>, 0…7); номер поза набором (старий файл) читається як перший, а не падає.
    /// Колір чорнила аватара (<see cref="InkOf"/>) — лише для старих градієнтних куль, циклом по палітрі.
    /// </summary>
    public static class AvatarSet
    {
        /// <summary>Скільки аватарів у наборі (спрайти avatar_1…8).</summary>
        public const int Count = 8;

        public static IReadOnlyList<InkColor> Inks => InkColors.All;

        public static int Clamp(int id) => id >= 0 && id < Count ? id : 0;

        /// <summary>Колір чорнила для старих градієнтних куль: номер циклом по палітрі.</summary>
        public static InkColor InkOf(int id) => InkColors.All[Clamp(id) % InkColors.All.Length];

        /// <summary>Номер аватара за кольором; невідомий колір — перший.</summary>
        public static int IdOf(InkColor ink)
        {
            for (var i = 0; i < InkColors.All.Length; i++)
                if (InkColors.All[i] == ink)
                    return i;
            return 0;
        }
    }
}
