using System;
using System.Collections.Generic;
using System.IO;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Промт Фази 4: «після цієї фази від економіки фарби в грі не лишилось нічого — доведи тестом».
    /// Доводиться так само, як відсутність старого ядра: у збірках Core і Meta немає жодного типу чи
    /// члена про фарбу, літри, мензурки, набори й «фарбу тижня», у файлі збереження — старих полів,
    /// у коді гри — файлів магазину фарб. Єдиний дозволений виняток — <c>InkFlow.Meta.Legacy</c>:
    /// міграція v7→v8 мусить читати старі файли, старі кроки не видаляються ніколи.
    /// </summary>
    public sealed class PaintEconomyAbsenceTests
    {
        private static readonly string[] ForbiddenWords = { "Paint", "Liter", "Beaker", "Bundle", "Weekly", "ShopCatalog" };

        private static readonly string[] DeletedFiles =
        {
            "Assets/_Scripts/Core/Galaxy/PaintKind.cs",
            "Assets/_Scripts/Meta/Shop/ShopCatalog.cs",
            "Assets/_Scripts/UI/Shop/PaintCard.cs",
            "Assets/_Scripts/UI/Shop/BundleCard.cs",
            "Assets/_Scripts/UI/Common/BeakerGauge.cs",
            "Assets/Tests/EditMode/Meta/ShopCatalogTests.cs"
        };

        private static bool Forbidden(string name)
        {
            foreach (var word in ForbiddenWords)
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        [Test]
        public void CoreAndMeta_HaveNoPaintEconomyTypes()
        {
            var offenders = new List<string>();
            foreach (var assembly in new[] { typeof(RunSession).Assembly, typeof(PlayerState).Assembly })
                foreach (var type in assembly.GetTypes())
                {
                    // Headless-раннер компілює тести в ту саму збірку — самі тести не рахуємо.
                    if (type.Namespace == "InkFlow.Meta.Legacy" ||
                        (type.Namespace != null && type.Namespace.Contains("Tests")))
                        continue;
                    if (Forbidden(type.Name))
                        offenders.Add(type.FullName ?? type.Name);
                }

            Assert.AreEqual(0, offenders.Count, "економіка фарби досі в Core/Meta: " + string.Join(", ", offenders));
        }

        [Test]
        public void StateEconomyAndSave_HaveNoPaintMembers()
        {
            var offenders = new List<string>();
            foreach (var type in new[] { typeof(PlayerState), typeof(EconomyData), typeof(SaveFile), typeof(GalaxyData), typeof(Wallet), typeof(RewardCalculator) })
                foreach (var member in type.GetMembers())
                    if (Forbidden(member.Name) || member.Name == "Placements" || member.Name == "PaintedZones")
                        offenders.Add($"{type.Name}.{member.Name}");

            Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
        }

        [Test]
        public void EconomyRewardSources_HaveNoPaintEntries()
        {
            foreach (var name in Enum.GetNames(typeof(RewardSource)))
                Assert.IsFalse(Forbidden(name), name);
        }

        [Test]
        public void PaintShopFiles_AreGone()
        {
            var root = FindRepoRoot();
            foreach (var relative in DeletedFiles)
            {
                Assert.IsFalse(File.Exists(Path.Combine(root, relative)), $"{relative} має бути видалено");
                Assert.IsFalse(File.Exists(Path.Combine(root, relative + ".meta")), $"{relative}.meta має бути видалено разом із файлом");
            }
        }

        [Test]
        public void GameScripts_DoNotMentionPaintEconomyTypes()
        {
            // Імена типів, яких більше немає, не мають згадуватись ніде в коді гри — ні в коментарях
            // «буде замінено», ні в мертвих посиланнях. Legacy-міграція — виняток за призначенням.
            var root = FindRepoRoot();
            var scripts = Path.Combine(root, "Assets", "_Scripts");
            var forbidden = new[] { "PaintKind", "ShopCatalog", "PaintProduct", "PaintSection", "BeakerGauge", "PaintCard", "BundleCard", "WeeklyOffer", "ShopBundle", "PaintsData", "PaintStack", "PaintedZone", "PicturePlacement" };
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (file.EndsWith("LegacySave.cs", StringComparison.Ordinal))
                    continue;
                var text = File.ReadAllText(file);
                foreach (var word in forbidden)
                    if (text.Contains(word))
                        offenders.Add($"{Path.GetFileName(file)}: {word}");
            }

            Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
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
