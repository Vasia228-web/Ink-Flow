using System;
using System.Collections.Generic;
using System.IO;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Покупка нафти за реальні гроші — У ЗАПАСІ з 2026-10-15 (рішення автора: «поки будемо без неї; коли
    /// гра масштабується — інтегруємо»). У грі немає магазину, пакетів, стору й «+» на капсулі; заробляти й
    /// витрачати нафту в грі (забіг, картинки, «продовжити», «домалювати») — лишається, це тримає
    /// <see cref="OilSpendingTests"/>. Файли лежать у <c>Reserve/OilShop</c> з тими самими шляхами —
    /// цей тест перевіряє і відсутність у грі, і те, що запас не загубився. На «інтегруємо» тест змінюється
    /// разом із поверненням (README запасу).
    /// </summary>
    public sealed class OilPurchaseAbsenceTests
    {
        private static readonly string[] ForbiddenTypes = { "OilPack", "OilPackCard", "ShopScreen", "IIapService", "StoreProduct", "PurchaseResult", "NullIap", "FakeIap" };

        /// <summary>Імена типів, членів і даних покупки — не згадуються ніде в коді гри.</summary>
        private static readonly string[] ForbiddenWordsInScripts =
        {
            "ShopScreen", "OilPack", "IIapService", "StoreProduct", "PurchaseResult", "NullIap", "FakeIap", "IapEnabled",
            "GrantPurchasedOil", "FindOilPack", "SmallestPackInRuns", "RewardSource.Purchase", "OilDropSize",
            // icon_plus не забороняємо: це загальна іконка набору, вона знадобиться не лише магазину.
            "PlusClicked", "SetPlusVisible", "capsule_plus", "icon-shop", "BuildShopScreen",
            "oil_1000", "\"shop\""
        };

        private static readonly string[] ReservedFiles =
        {
            "Assets/_Scripts/UI/Shop/ShopScreen.cs",
            "Assets/_Scripts/UI/Shop/OilPackCard.cs",
            "Assets/_Scripts/Meta/Shop/OilPack.cs",
            "Assets/_Scripts/Editor/BuildShopScreen.cs",
            "Assets/_Scripts/Platform/IapServices.cs",
            "Assets/Tests/EditMode/Meta/OilShopTests.cs",
            "Assets/Tests/EditMode/UI/ShopScreenTests.cs",
            "Assets/_Sprites/U1Glaze/oil/oil_pack_1.png",
            "Assets/_Sprites/U1Glaze/oil/oil_pack_2.png",
            "Assets/_Sprites/U1Glaze/oil/oil_pack_3.png",
            "Assets/_Sprites/U1Glaze/oil/oil_pack_4.png",
            "Assets/_Sprites/U1Glaze/controls/capsule_plus.png",
            "Assets/_Sprites/U1Glaze/icons/icon_restore.png"
        };

        /// <summary>Спрайти лише магазину: атлас U1Glaze пакує всю теку, тож у грі їх не має бути й у маніфесті.</summary>
        private static readonly string[] ReservedSprites = { "oil_pack_1", "oil_pack_2", "oil_pack_3", "oil_pack_4", "capsule_plus", "icon_restore" };

        private static readonly string[] GeneratedFiles =
        {
            "Assets/Scenes/Shop.unity",
            "Assets/_Prefabs/Screens/ShopScreen.prefab",
            "Assets/_Sprites/UI/icon-shop.png"
        };

        [Test]
        public void CoreAndMeta_HaveNoPurchaseTypes()
        {
            var forbidden = new HashSet<string>(ForbiddenTypes, StringComparer.Ordinal);
            var offenders = new List<string>();
            foreach (var assembly in new[] { typeof(InkFlow.Core.RunSession).Assembly, typeof(PlayerState).Assembly })
                foreach (var type in assembly.GetTypes())
                {
                    // Headless-раннер компілює тести в ту саму збірку — самі тести не рахуємо.
                    if (type.Namespace != null && type.Namespace.Contains("Tests"))
                        continue;
                    if (forbidden.Contains(type.Name))
                        offenders.Add(type.FullName ?? type.Name);
                }

            Assert.AreEqual(0, offenders.Count, "типи покупки нафти досі в Core/Meta: " + string.Join(", ", offenders));
        }

        [Test]
        public void StateAndEconomy_HaveNoPurchaseMembers()
        {
            var offenders = new List<string>();
            foreach (var type in new[] { typeof(PlayerState), typeof(EconomyData), typeof(EconomyReport), typeof(Wallet) })
                foreach (var member in type.GetMembers())
                    foreach (var word in new[] { "Purchase", "OilPack", "SmallestPack" })
                        if (member.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                            offenders.Add($"{type.Name}.{member.Name}");

            Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
            Assert.IsFalse(Array.IndexOf(Enum.GetNames(typeof(RewardSource)), "Purchase") >= 0,
                "джерела нафти «Покупка» в грі немає — купити нафту нема де");
        }

        [Test]
        public void GameScripts_DoNotMentionThePurchase()
        {
            // Жодних мертвих посилань і «вкладок у нікуди»: ні маршруту "shop", ні «+» на капсулі, ні стору.
            var scripts = Path.Combine(FindRepoRoot(), "Assets", "_Scripts");
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var word in ForbiddenWordsInScripts)
                    if (text.Contains(word))
                        offenders.Add($"{Path.GetFileName(file)}: {word}");
            }

            Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
        }

        [Test]
        public void ShopFiles_AreInReserve_NotInTheGame()
        {
            var root = FindRepoRoot();
            foreach (var relative in ReservedFiles)
            {
                Assert.IsFalse(File.Exists(Path.Combine(root, relative)), $"{relative} має бути в запасі, а не в грі");
                Assert.IsTrue(File.Exists(Path.Combine(root, "Reserve", "OilShop", relative)), $"{relative} має лежати в Reserve/OilShop");
            }
            foreach (var relative in GeneratedFiles)
                Assert.IsFalse(File.Exists(Path.Combine(root, relative)), $"{relative} збирає збирач із запасу — у грі його бути не повинно");

            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Assets", "_Scripts", "UI", "Shop")), "тека UI/Shop — у запасі");
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Assets", "_Scripts", "Meta", "Shop")), "тека Meta/Shop — у запасі");
            Assert.IsTrue(File.Exists(Path.Combine(root, "Reserve", "OilShop", "README.md")), "Reserve/OilShop/README.md — як повернути");
            Assert.IsTrue(File.Exists(Path.Combine(root, "Reserve", "OilShop", "wiring.patch")), "Reserve/OilShop/wiring.patch — що прибрано зі спільних файлів");

            // Атлас U1Glaze збирає всю теку: спрайт магазину в маніфесті гри поїхав би в збірку невидимим.
            var manifest = File.ReadAllText(Path.Combine(root, "Assets", "_Sprites", "U1Glaze", "sprites.json"));
            var reserved = File.ReadAllText(Path.Combine(root, "Reserve", "OilShop", "sprites-entries.json"));
            foreach (var name in ReservedSprites)
            {
                Assert.IsFalse(manifest.Contains($"\"{name}\""), $"спрайт {name} — у запасі, не в маніфесті гри");
                Assert.IsTrue(reserved.Contains($"\"{name}\""), $"запис маніфесту {name} має лежати в Reserve/OilShop/sprites-entries.json");
            }
        }

        private static string FindRepoRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Scripts")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Не знайшов корінь проєкту з Assets/_Scripts.");
        }
    }
}
