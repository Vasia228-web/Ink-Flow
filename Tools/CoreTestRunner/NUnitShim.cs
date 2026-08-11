// Мінімальний шим NUnit API для headless-прогону Edit Mode тестів без Unity.
// В Unity ті самі тестові файли компілюються проти справжнього NUnit
// (com.unity.ext.nunit); цей шим існує ТІЛЬКИ в Tools/CoreTestRunner і
// покриває лише API, використані в Assets/_Tests/EditMode.

using System;
using System.Collections.Generic;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition)
                throw new AssertionException(message ?? "Expected: true, but was: false");
        }

        public static void IsFalse(bool condition, string message = null)
        {
            if (condition)
                throw new AssertionException(message ?? "Expected: false, but was: true");
        }

        public static void AreEqual(object? expected, object? actual, string message = null)
        {
            if (!EqualityComparer<object>.Default.Equals(Normalize(expected), Normalize(actual)))
                throw new AssertionException(
                    message ?? $"Expected: {expected ?? "null"}, but was: {actual ?? "null"}");
        }

        public static void AreNotEqual(object? expected, object? actual, string message = null)
        {
            if (EqualityComparer<object>.Default.Equals(Normalize(expected), Normalize(actual)))
                throw new AssertionException(message ?? $"Expected NOT: {expected ?? "null"}");
        }

        // Порівняння величин. Шим реалізує лише те, чим справді користуються тести:
        // додавати сюди метод варто тоді, коли він знадобився, а не «про запас».
        //
        // Числа зводяться до спільного типу перед порівнянням: справжній NUnit
        // порівнює int з long без питань, а CompareTo — кидає
        // «Object must be of type Int64». Без цього тест, зелений у редакторі,
        // падав би тільки headless.
        public static void LessOrEqual(IComparable actual, IComparable limit, string message = null)
        {
            if (Compare(actual, limit) > 0)
                throw new AssertionException(message ?? $"Expected: <= {limit}, but was: {actual}");
        }

        public static void GreaterOrEqual(IComparable actual, IComparable limit, string message = null)
        {
            if (Compare(actual, limit) < 0)
                throw new AssertionException(message ?? $"Expected: >= {limit}, but was: {actual}");
        }

        public static void Less(IComparable actual, IComparable limit, string message = null)
        {
            if (Compare(actual, limit) >= 0)
                throw new AssertionException(message ?? $"Expected: < {limit}, but was: {actual}");
        }

        public static void Greater(IComparable actual, IComparable limit, string message = null)
        {
            if (Compare(actual, limit) <= 0)
                throw new AssertionException(message ?? $"Expected: > {limit}, but was: {actual}");
        }

        private static int Compare(IComparable actual, IComparable limit)
        {
            if (IsNumeric(actual) && IsNumeric(limit))
            {
                // Через decimal, а не double: він точний на цілих аж до 10^28,
                // тож порівняння long не з'їде на великих значеннях.
                var a = Convert.ToDecimal(actual);
                var b = Convert.ToDecimal(limit);
                return a.CompareTo(b);
            }

            return actual.CompareTo(limit);
        }

        private static bool IsNumeric(object value) => value switch
        {
            sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal => true,
            _ => false
        };

        // Порівняння ЗА ПОСИЛАННЯМ. Не те саме, що AreEqual: у магазині важливо,
        // що банер тижня показує той самий об'єкт, а не копію з тими ж полями.
        public static void AreSame(object? expected, object? actual, string message = null)
        {
            if (!ReferenceEquals(expected, actual))
                throw new AssertionException(message ?? "Expected the same instance, but they differ.");
        }

        public static void AreNotSame(object? expected, object? actual, string message = null)
        {
            if (ReferenceEquals(expected, actual))
                throw new AssertionException(message ?? "Expected different instances, but got the same.");
        }

        // Порівняння з допуском. Без нього тест на float або падає на 1e-7,
        // або пишеться через Math.Abs — і тоді повідомлення про падіння
        // не показує, які саме числа розійшлися.
        public static void AreEqual(double expected, double actual, double tolerance,
            string message = null)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new AssertionException(
                    message ?? $"Expected: {expected} ± {tolerance}, but was: {actual}");
        }

        public static void IsNull(object? value, string message = null)
        {
            if (value != null)
                throw new AssertionException(message ?? $"Expected: null, but was: {value}");
        }

        public static void IsNotNull(object? value, string message = null)
        {
            if (value == null)
                throw new AssertionException(message ?? "Expected: not null, but was: null");
        }

        // NUnit має обидві назви — і IsNotNull, і NotNull. Тести пишуться проти
        // справжнього NUnit, тож шим мусить приймати ту саму пару.
        public static void NotNull(object? value, string message = null) => IsNotNull(value, message);

        public static void Null(object? value, string message = null) => IsNull(value, message);

        public static void Fail(string message = null) =>
            throw new AssertionException(message ?? "Assert.Fail");

        public static void DoesNotThrow(Action action, string message = null)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                throw new AssertionException(
                    message ?? $"Expected no exception, but got {e.GetType().Name}: {e.Message}");
            }
        }

        public static TException Throws<TException>(Action action, string message = null)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new AssertionException(
                    message ?? $"Expected {typeof(TException).Name}, but was {other.GetType().Name}: {other.Message}");
            }

            throw new AssertionException(message ?? $"Expected {typeof(TException).Name}, but no exception was thrown");
        }

        // NUnit порівнює числові типи за значенням (AreEqual(5, cell.Density) де Density — int).
        // Приводимо цілі числа до long, щоб уникнути хибних падінь через боксинг різних типів.
        private static object Normalize(object value) => value switch
        {
            sbyte or byte or short or ushort or int or uint or long => Convert.ToInt64(value),
            _ => value
        };
    }
}
