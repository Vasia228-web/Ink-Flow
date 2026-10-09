using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Набір готових аватарів у стилі краплі (майстер-док §14): по одному на кожен колір чорнила
    /// з палітри (<see cref="InkColors.All"/>). У файлі лежить номер (<c>ProfileData.AvatarId</c>);
    /// номер поза набором (старий файл, менша палітра) читається як перший, а не падає.
    /// </summary>
    public static class AvatarSet
    {
        public static IReadOnlyList<InkColor> Inks => InkColors.All;

        public static int Count => InkColors.All.Length;

        public static int Clamp(int id) => id >= 0 && id < Count ? id : 0;

        public static InkColor InkOf(int id) => InkColors.All[Clamp(id)];

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
