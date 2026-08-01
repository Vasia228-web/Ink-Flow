using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace InkFlow.Editor
{
    /// <summary>
    /// Налаштування проєкту під мобільний реліз (§12-13). Ідемпотентно, запускається з меню:
    /// Ink Flow → Setup → Apply Mobile Project Settings.
    ///
    /// Що тут НЕ робиться свідомо (це кроки Фази 5, і вони потребують секретів/акаунтів):
    ///  • keystore Android — живе поза репозиторієм, шлях через змінні середовища;
    ///  • PrivacyInfo.xcprivacy та ATT-текст — додаються разом із реальними SDK;
    ///  • Target API level Play — звіряється з вимогами Google на дату релізу, не «прибивається» зараз.
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Ink Flow/Setup/Apply Mobile Project Settings")]
        public static void ApplyMobileSettings()
        {
            ApplyCommon();
            ApplyAndroid();
            ApplyIos();
            ApplyEditorSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] Мобільні налаштування застосовано: portrait-only, IL2CPP, .NET Standard 2.1, " +
                      "ARM64, ASTC, Medium stripping.");
        }

        private static void ApplyCommon()
        {
            // Гра портретна: автоповорот на телефоні лише псує компоновку поля (§13).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.companyName = string.IsNullOrEmpty(PlayerSettings.companyName)
                ? "Ink Flow" : PlayerSettings.companyName;
            PlayerSettings.productName = "Ink Flow";

            // .NET Standard 2.1 — менший рантайм і суворіші межі API (§1).
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);

            // IL2CPP обов'язковий: Mono під ARM64 у сторах не приймається.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            // Medium stripping: помітно менший білд. Типи, що серіалізуються, тримає link.xml.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
        }

        private static void ApplyAndroid()
        {
            // ARM64 only: ARMv7 більше не потрібен і подвоює розмір білда (§13).
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // AAB для Play; APK лишається для тестів на пристрої через Build Settings.
            EditorUserBuildSettings.buildAppBundle = true;

            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        }

        private static void ApplyIos()
        {
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
        }

        private static void ApplyEditorSettings()
        {
            // Force Text — без цього мерджити сцени неможливо (§16).
            // Visible Meta Files у Unity 6 діють автоматично для проєктів поза VCS-інтеграцією.
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        /// <summary>
        /// Монотонний build number (§13). Викликається з CI/білд-скрипта, не з меню:
        /// версія, що зменшилась, ламає завантаження і в Play, і в App Store.
        /// </summary>
        public static void IncrementBuildNumber()
        {
            PlayerSettings.Android.bundleVersionCode++;

            if (int.TryParse(PlayerSettings.iOS.buildNumber, out var iosBuild))
                PlayerSettings.iOS.buildNumber = (iosBuild + 1).ToString();
            else
                PlayerSettings.iOS.buildNumber = "1";

            Debug.Log($"[InkFlow] Build number: Android {PlayerSettings.Android.bundleVersionCode}, " +
                      $"iOS {PlayerSettings.iOS.buildNumber}");
        }
    }
}
