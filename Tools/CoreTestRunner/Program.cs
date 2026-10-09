// Рефлексійний runner: знаходить [TestFixture]-класи з [Test]-методами
// (ті самі файли, що ганяє Unity Test Runner) і виконує їх без Unity.
// Запуск: ./Tools/run-core-tests.sh

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

var failures = 0;
var passed = 0;
var failed = new System.Collections.Generic.List<string>();

// Клас із [Test]-методами вважаємо фікстурою навіть без [TestFixture] — саме так
// поводиться справжній NUnit. Вимагати атрибут означало б МОВЧКИ пропускати тести,
// які в редакторі виконуються: рівно той хибний зелений, від якого цей раннер і є.
var fixtures = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => t.GetCustomAttribute<TestFixtureAttribute>() != null
                || t.GetMethods().Any(m => m.GetCustomAttribute<TestAttribute>() != null))
    .OrderBy(t => t.Name);

foreach (var fixture in fixtures)
{
    Console.WriteLine($"── {fixture.Name}");
    var setUps = fixture.GetMethods()
        .Where(m => m.GetCustomAttribute<SetUpAttribute>() != null)
        .ToArray();

    foreach (var test in fixture.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
    {
        var instance = Activator.CreateInstance(fixture);
        try
        {
            foreach (var setUp in setUps)
                setUp.Invoke(instance, null);
            test.Invoke(instance, null);
            passed++;
            Console.WriteLine($"   ✔ {test.Name}");
        }
        catch (TargetInvocationException e)
        {
            failures++;
            var inner = e.InnerException ?? e;
            var kind = inner is AssertionException ? "FAIL" : "ERROR";
            failed.Add($"{fixture.Name}.{test.Name} [{kind}]: {inner.Message.Split('\n').FirstOrDefault()?.Trim()}");
            Console.WriteLine($"   ✘ {test.Name} [{kind}]");
            Console.WriteLine($"     {inner.Message}");
            if (inner is not AssertionException)
                Console.WriteLine($"     {inner.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
        }
    }
}

Console.WriteLine();
// Невдачі — ще раз у підсумку: серед трьох сотень рядків «✘» губиться, а ім'я тесту потрібне наступному прогону.
foreach (var name in failed)
    Console.WriteLine($"   ✘ {name}");
Console.WriteLine(failures == 0
    ? $"OK — {passed} tests passed."
    : $"FAILED — {failures} failed, {passed} passed.");
return failures == 0 ? 0 : 1;
