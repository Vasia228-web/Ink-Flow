using System.Collections.Generic;
using InkFlow.App;
using InkFlow.Gameplay;
using InkFlow.Style;
using InkFlow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static InkFlow.Editor.UiBuilder;

namespace InkFlow.Editor
{
    /// <summary>
    /// Складає ЗАСТОСУНОК: один канвас, один <see cref="NavigationStack"/>,
    /// дев'ять екранів із префабів і <see cref="AppRouter"/> між ними.
    /// Меню: Ink Flow → Setup → Build Main Scene.
    ///
    /// Це ТОЧКА ВХОДУ гри — саме `Main.unity` стоїть у Build Settings, і саме тут
    /// живе `GameBootstrap` із сервісами й збереженням. Окремі сцени екранів
    /// (`Hub.unity`, `Shop.unity`…) лишаються майстернею: на них зручно правити
    /// один екран, не піднімаючи весь застосунок.
    ///
    /// Розкладки тут немає жодної — усе приходить префабами з Build*Screen.
    /// Тому перед цією командою треба зібрати всі дев'ять екранів.
    ///
    /// У збереженій сцені активний лише хаб: решта вісім лежать згаслими. Інакше
    /// дев'ять розтягнутих на весь канвас екранів накладаються один на одного
    /// й у Scene, і в Game.
    /// </summary>
    public static class BuildMainScene
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string BalancePath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";
        private const string EconomyPath = "Assets/_ScriptableObjects/Balance/EconomyConfig.asset";

        /// <summary>Імена префабів у порядку, в якому вони лягають у сцену.</summary>
        private static readonly string[] ScreenNames =
        {
            "HubScreen", "LevelMapScreen", "LevelScreen", "EndlessScreen",
            "GalaxyScreen", "PaintScreen", "ShopScreen", "RankingsScreen", "ProfileScreen"
        };

        [MenuItem("Ink Flow/Setup/Build Main Scene")]
        public static void Build()
        {
            if (!InkFlowBootstrap.EnsureEditMode())
                return;

            StyleRefresh.Suspended = true;
            try { BuildScene(); }
            finally { StyleRefresh.Suspended = false; }
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (balance == null) missing.Add(BalancePath);
            if (economy == null) missing.Add(EconomyPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var name in ScreenNames)
            {
                var path = $"{ScreenPrefabFolder}/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    missing.Add(path);
                else
                    prefabs[name] = prefab;
            }

            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Головну сцену НЕ зібрано — спершу збери всі екрани " +
                               "(Ink Flow → Setup → Build …). Не знайдено:\n  " +
                               string.Join("\n  ", missing));
                return;
            }

            CreateCamera(design!);

            var canvasGo = new GameObject("UI Root");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Фон один на весь застосунок: він не належить жодному екрану й не
            // має перебудовуватись при кожному переході.
            PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);

            var safe = Child(canvasGo, "SafeArea");
            Stretch(safe);
            var safeBinder = safe.AddComponent<SafeAreaBinder>();
            var navigation = safe.AddComponent<NavigationStack>();

            var uiRoot = canvasGo.AddComponent<UIRoot>();
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler),
                ("navigation", navigation), ("safeArea", safeBinder));

            var screens = new Dictionary<string, ScreenBase>();
            foreach (var name in ScreenNames)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name], safe.transform);
                instance.name = name;
                Stretch(instance);

                var screen = instance.GetComponent<ScreenBase>();
                if (screen == null)
                {
                    Debug.LogError($"[InkFlow] У префабі {name} немає ScreenBase.");
                    return;
                }

                // У сцену кладемо ЗГАСЛИМИ, крім хаба. Дев'ять розтягнутих на весь
                // канвас екранів інакше лежать один на одному — і в Scene, і в Game
                // видно кашу, з якою неможливо працювати. AppRouter.HideAll()
                // лишається як запобіжник у рантаймі, але сцена мусить зберігатись
                // у притомному стані, а не покладатись на Play Mode.
                instance.SetActive(name == "HubScreen");

                screens[name] = screen;
            }

            var routerGo = Child(safe, "AppRouter");
            var router = routerGo.AddComponent<AppRouter>();
            Wire(router,
                ("navigation", navigation),
                ("hub", screens["HubScreen"]),
                ("levelMap", screens["LevelMapScreen"]),
                ("level", screens["LevelScreen"]),
                ("endless", screens["EndlessScreen"]),
                ("galaxy", screens["GalaxyScreen"]),
                ("paint", screens["PaintScreen"]),
                ("shop", screens["ShopScreen"]),
                ("rankings", screens["RankingsScreen"]),
                ("profile", screens["ProfileScreen"]));

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            var catalog = bootstrapGo.AddComponent<LevelCatalog>();
            Wire(bootstrap,
                ("balanceConfig", balance!),
                ("economyConfig", economy!),
                ("levelCatalog", catalog),
                ("router", router),
                ("levelScreen", screens["LevelScreen"]),
                ("endlessScreen", screens["EndlessScreen"]));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Дев-панель живе поруч із бутстрапом і в релізний білд не потрапляє:
            // весь її файл під #if.
            var devPanel = bootstrapGo.AddComponent<DevPanel>();
            Wire(devPanel, ("bootstrap", bootstrap));
#endif

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            uiRoot.ApplyScaler();

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log($"[InkFlow] Головну сцену зібрано: {ScenePath} — " +
                      $"{ScreenNames.Length} екранів під одним NavigationStack.");
        }

        /// <summary>
        /// Ставить Main.unity ЄДИНОЮ сценою збірки. Сцени окремих екранів у
        /// білд не йдуть: вони існують лише для роботи в редакторі.
        /// </summary>
        private static void AddToBuildSettings()
        {
            var guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (string.IsNullOrEmpty(guid))
                return;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
