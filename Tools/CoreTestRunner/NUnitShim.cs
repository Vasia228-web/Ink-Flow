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

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!EqualityComparer<object>.Default.Equals(Normalize(expected), Normalize(actual)))
                throw new AssertionException(
                    message ?? $"Expected: {expected ?? "null"}, but was: {actual ?? "null"}");
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
