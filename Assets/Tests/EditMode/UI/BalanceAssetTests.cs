using System.Collections;
using System.Reflection;
using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.Meta;
using NUnit.Framework;
using UnityEditor;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Асети балансу й економіки — ті самі числа, що дефолти коду. Прогонник (`Tools/InkFlow.Sim`) і
    /// симуляція економіки ганяють дефолти; розійдуться з асетом — бот міряє не ту гру, що на телефоні.
    /// Посунув повзунок — перенеси число в дефолт <see cref="BalanceData"/> / <see cref="EconomyData"/> (і навпаки).
    /// </summary>
    public sealed class BalanceAssetTests
    {
        [Test]
        public void BalanceAsset_MatchesTheDefaultsTheBotRuns()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/_ScriptableObjects/Balance/BalanceConfig.asset");
            Assert.IsNotNull(asset);
            AssertSame(BalanceData.Default, asset.ToBalanceData());
        }

        [Test]
        public void EconomyAsset_MatchesTheDefaultsTheSimulationRuns()
        {
            var asset = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_ScriptableObjects/Balance/EconomyConfig.asset");
            Assert.IsNotNull(asset);
            AssertSame(EconomyData.Default, asset.ToEconomyData());
        }

        private static void AssertSame(object expected, object actual)
        {
            foreach (var property in expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;
                var a = property.GetValue(expected);
                var b = property.GetValue(actual);
                if (a is IEnumerable left && b is IEnumerable right && !(a is string))
                    CollectionAssertEqual(left, right, property.Name);
                else
                    Assert.AreEqual(a, b, $"{property.Name}: асет ≠ дефолт коду");
            }
        }

        private static void CollectionAssertEqual(IEnumerable left, IEnumerable right, string name)
        {
            var l = new System.Collections.Generic.List<object>();
            var r = new System.Collections.Generic.List<object>();
            foreach (var x in left) l.Add(x);
            foreach (var x in right) r.Add(x);
            Assert.AreEqual(string.Join(",", l), string.Join(",", r), $"{name}: асет ≠ дефолт коду");
        }
    }
}
