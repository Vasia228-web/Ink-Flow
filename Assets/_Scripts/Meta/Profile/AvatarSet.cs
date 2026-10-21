namespace InkFlow.Meta
{
    /// <summary>
    /// Набір готових аватарів (майстер-док §14, U1Glaze): 8 спрайтів avatar_1…8. У файлі лежить номер
    /// (<c>ProfileData.AvatarId</c>, 0…7); номер поза набором (старий файл) читається як перший, а не падає.
    /// </summary>
    public static class AvatarSet
    {
        /// <summary>Скільки аватарів у наборі (спрайти avatar_1…8).</summary>
        public const int Count = 8;

        public static int Clamp(int id) => id >= 0 && id < Count ? id : 0;
    }
}
