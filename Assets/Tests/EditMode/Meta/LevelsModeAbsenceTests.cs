using System;
using System.Collections.Generic;
using System.IO;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Прибирання 2026-10-10: «велике прибирання сиріт режиму «Рівні»» — доведено тестом, як і
    /// відсутність старого ядра та економіки фарби. У збірках Core і Meta немає типів карти рівнів,
    /// прогресу рівнів і підсумку рівня; у стані, економіці й файлі — членів про рівні, босів і віхи;
    /// у коді гри — файлів екрана, збирача, сцени й префаба; у джерелах нафти — записів старого режиму.
    /// Картка «Рівні» в хабі лишається й веде на «Скоро» — це єдине, що від режиму є в грі, і маршрут
    /// у роутері тримає окремий тест (check-navigation перевіряє лише, що подія підписана, не куди).
    /// </summary>
    public sealed class LevelsModeAbsenceTests
    {
        private static readonly string[] ForbiddenTypes =
        {
            "LevelMap", "LevelNode", "LevelNodeKind", "LevelProgress", "LevelRecord", "GameResult"
        };

        private static readonly string[] ForbiddenMemberWords = { "Level", "Boss", "Milestone" };

        private static readonly string[] DeletedFiles =
        {
            "Assets/_Scripts/Meta/Levels/LevelMap.cs",
            "Assets/_Scripts/Meta/Progress/LevelProgress.cs",
            "Assets/_Scripts/UI/Levels/LevelMapScreen.cs",
            "Assets/_Scripts/UI/Levels/LevelNodeView.cs",
            "Assets/_Scripts/Editor/BuildLevelMapScreen.cs",
            "Assets/Scenes/LevelMap.unity",
            "Assets/_Prefabs/Screens/LevelMapScreen.prefab",
            "Assets/AddressableAssetsData/AssetGroups/Levels.asset",
            "Assets/AddressableAssetsData/AssetGroups/Schemas/Levels_BundledAssetGroupSchema.asset",
            "Assets/AddressableAssetsData/AssetGroups/Schemas/Levels_ContentUpdateGroupSchema.asset",
            "Assets/Tests/EditMode/Meta/LevelMapTests.cs",
            "Assets/Tests/EditMode/Meta/LevelProgressTests.cs"
        };

        /// <summary>Імена типів і членів, яких більше немає, — не згадуються ніде в коді гри.</summary>
        private static readonly string[] ForbiddenWordsInScripts =
        {
            "LevelMapScreen", "LevelNodeView", "LevelMap.", "LevelNodeKind", "LevelProgress", "LevelRecord",
            "CompleteLevel", "ForLevel", "ForEndlessRecord", "ForMilestones",
            "BaseLevelReward", "BossMultiplier", "EndlessMilestones", "LevelClear", "BossClear",
            "FontSizeLevelNode", "LevelMapFocus", "BossSheetFrom", "TrailPassed", "StarPipEmpty"
        };

        [Test]
        public void CoreAndMeta_HaveNoLevelsModeTypes()
        {
            var forbidden = new HashSet<string>(ForbiddenTypes, StringComparer.Ordinal);
            var offenders = new List<string>();
            foreach (var assembly in new[] { typeof(RunSession).Assembly, typeof(PlayerState).Assembly })
                foreach (var type in assembly.GetTypes())
                {
                    // Headless-раннер компілює тести в ту саму збірку — самі тести не рахуємо.
                    if (type.Namespace != null && type.Namespace.Contains("Tests"))
                        continue;
                    if (forbidden.Contains(type.Name))
                        offenders.Add(type.FullName ?? type.Name);
                }

            Assert.AreEqual(0, offenders.Count, "типи режиму «Рівні» досі в Core/Meta: " + string.Join(", ", offenders));
        }

        [Test]
        public void StateEconomyAndSave_HaveNoLevelMembers()
        {
            var offenders = new List<string>();
            foreach (var type in new[] { typeof(PlayerState), typeof(EconomyData), typeof(RewardCalculator), typeof(SaveFile), typeof(ProgressData), typeof(XorShiftRandom) })
                foreach (var member in type.GetMembers())
                    foreach (var word in ForbiddenMemberWords)
                        if (member.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                            offenders.Add($"{type.Name}.{member.Name}");

            Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
        }

        [Test]
        public void RewardSources_HaveNoLevelsModeEntries()
        {
            var names = Enum.GetNames(typeof(RewardSource));
            foreach (var old in new[] { "LevelClear", "BossClear", "EndlessRecord", "EndlessMilestone" })
                Assert.IsFalse(Array.IndexOf(names, old) >= 0, $"джерело нафти старого режиму {old} досі в RewardSource");
        }

        [Test]
        public void LevelsModeFiles_AreGone()
        {
            var root = FindRepoRoot();
            foreach (var relative in DeletedFiles)
            {
                Assert.IsFalse(File.Exists(Path.Combine(root, relative)), $"{relative} має бути видалено");
                Assert.IsFalse(File.Exists(Path.Combine(root, relative + ".meta")), $"{relative}.meta має бути видалено разом із файлом");
            }

            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Assets", "_Scripts", "Meta", "Levels")), "тека Meta/Levels має зникнути");
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Assets", "_Scripts", "UI", "Levels")), "тека UI/Levels має зникнути");
        }

        [Test]
        public void HubLevelsCard_StillLeadsToComingSoon()
        {
            // Граф навігації живе лише в AppRouter (CLAUDE.md), тож маршрут картки перевіряється по його тексту:
            // підписка є (це бачить і check-navigation) і веде саме на заглушку «Скоро», а не на інший екран.
            var router = File.ReadAllText(Path.Combine(FindRepoRoot(), "Assets", "_Scripts", "UI", "Common", "AppRouter.cs"));
            Assert.IsTrue(router.Contains("hub.LevelsRequested += () => Push(comingSoon);"),
                "картка «Рівні» в хабі має вести на ComingSoonScreen, поки режиму немає");
        }

        [Test]
        public void GameScripts_DoNotMentionLevelsModeMembers()
        {
            // Жодних «буде замінено» в коментарях і мертвих посилань: імена зниклих типів і членів
            // не згадуються ніде в коді гри. Legacy-міграція тут ні до чого — рівні в неї не переносились.
            var root = FindRepoRoot();
            var scripts = Path.Combine(root, "Assets", "_Scripts");
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
