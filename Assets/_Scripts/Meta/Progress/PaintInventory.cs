using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Міст між запасом фарб у пам'яті (<see cref="PaintStock"/>) і збереженням
    /// (<see cref="PaintsData"/>).
    ///
    /// Окремим типом, бо перетворення несиметричне: у файлі лежать пари
    /// «ідентифікатор → літри», і зайвий чи невідомий ідентифікатор не має
    /// ронити гру. Файл могли зробити старішою версією, а могли й зіпсувати.
    /// </summary>
    public static class PaintInventory
    {
        /// <summary>
        /// Ідентифікатор фарби у файлі. Пишемо НАЗВУ, а не число: якщо колись
        /// вставимо фарбу в середину переліку, збережені числа поїхали б і в
        /// гравця мовчки змінився б колір палітри.
        /// </summary>
        public static string IdOf(PaintKind kind) => kind.ToString();

        public static bool TryParse(string? id, out PaintKind kind)
        {
            kind = default;
            if (id is null || id.Length == 0)
                return false;

            for (var i = 0; i < PaintKinds.Count; i++)
            {
                var candidate = (PaintKind)i;
                if (string.Equals(candidate.ToString(), id, System.StringComparison.Ordinal))
                {
                    kind = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Читає запас із файла. Невідомі ідентифікатори тихо пропускаються.</summary>
        public static PaintStock Load(PaintsData? data)
        {
            var stock = new PaintStock();
            if (data?.Stacks == null)
                return stock;

            for (var i = 0; i < data.Stacks.Count; i++)
            {
                var stack = data.Stacks[i];
                if (!TryParse(stack.PaintId, out var kind))
                    continue;
                if (stack.Liters > 0f)
                    stock.Set(kind, stack.Liters);
            }

            return stock;
        }

        /// <summary>
        /// Записує запас у файл. Нульові залишки НЕ зберігаються — інакше файл
        /// розпухав би вісьмома записами в кожного гравця, більшість із яких порожні.
        /// </summary>
        public static void Save(PaintStock stock, PaintsData data)
        {
            if (stock == null || data == null)
                return;

            data.Stacks.Clear();
            for (var i = 0; i < PaintKinds.Count; i++)
            {
                var kind = (PaintKind)i;
                var liters = stock[kind];
                if (liters <= 0f)
                    continue;
                data.Stacks.Add(new PaintStack { PaintId = IdOf(kind), Liters = liters });
            }
        }

        /// <summary>Скільки літрів усього — для підпису в профілі.</summary>
        public static float TotalLiters(PaintStock stock)
        {
            if (stock == null)
                return 0f;
            var sum = 0f;
            for (var i = 0; i < PaintKinds.Count; i++)
                sum += stock[(PaintKind)i];
            return sum;
        }

        /// <summary>Скільки різних фарб має гравець — потрібне досягненням.</summary>
        public static int DistinctPaints(PaintStock stock)
        {
            if (stock == null)
                return 0;
            var n = 0;
            for (var i = 0; i < PaintKinds.Count; i++)
                if (stock[(PaintKind)i] > 0f)
                    n++;
            return n;
        }
    }
}
