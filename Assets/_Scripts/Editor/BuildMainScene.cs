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
using TMPro;
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
            "HubScreen", "LevelMapScreen", "ComingSoonScreen", "EndlessScreen",
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

            // Діалог ніка лежить ПОВЕРХ усіх екранів: він не бере участі в
            // навігації й не має ховатись разом з екраном, з якого відкритий.
            var prompt = BuildNickPrompt(safe, design!);

            var routerGo = Child(safe, "AppRouter");
            var router = routerGo.AddComponent<AppRouter>();
            Wire(router,
                ("navigation", navigation),
                ("hub", screens["HubScreen"]),
                ("levelMap", screens["LevelMapScreen"]),
                ("comingSoon", screens["ComingSoonScreen"]),
                ("endless", screens["EndlessScreen"]),
                ("galaxy", screens["GalaxyScreen"]),
                ("paint", screens["PaintScreen"]),
                ("shop", screens["ShopScreen"]),
                ("rankings", screens["RankingsScreen"]),
                ("profile", screens["ProfileScreen"]),
                ("nickPrompt", prompt));

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            // Гаптика поля підставляється сюди: композиційний корінь — єдиний, хто
            // знає реалізацію Platform, а звук/гаптика поля живуть у префабі екрана.
            var feedback = screens["EndlessScreen"].GetComponentInChildren<BoardFeedback>(true);
            Wire(bootstrap,
                ("balanceConfig", balance!),
                ("economyConfig", economy!),
                ("router", router),
                ("endlessScreen", screens["EndlessScreen"]),
                ("boardFeedback", feedback));

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
        /// Діалог зміни ніка. Розкладка проста настільки, що окремий збирач
        /// їй не потрібен — усе поміщається тут.
        /// </summary>
        private static NickPrompt BuildNickPrompt(GameObject parent, DesignSystem design)
        {
            const float k = 1080f / 390f;
            var go = Child(parent, "NickPrompt");
            Stretch(go);

            var scrim = go.AddComponent<Image>();
            scrim.color = design.OverScrim;

            var panelGo = Child(go, "Panel");
            var panel = panelGo.AddComponent<GradientImage>();
            panel.sprite = LoadSprite("rounded-rect");
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(Mathf.Round(28f * k));
            panel.SetGradient(design.OverCardFrom, design.OverCardTo);
            Place(panel, Vector2.zero, new Vector2(Mathf.Round(300f * k), Mathf.Round(190f * k)),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var title = Label(panelGo, "Title", "Як тебе звати?", design, design.Font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Place(title, new Vector2(0f, -Mathf.Round(26f * k)),
                new Vector2(Mathf.Round(260f * k), Mathf.Round(30f * k)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var fieldGo = Child(panelGo, "Field");
            var fieldBg = fieldGo.AddComponent<Image>();
            fieldBg.sprite = LoadSprite("rounded-rect");
            fieldBg.type = Image.Type.Sliced;
            fieldBg.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(Mathf.Round(16f * k));
            fieldBg.color = design.GlassFill;
            Place(fieldBg, new Vector2(0f, -Mathf.Round(76f * k)),
                new Vector2(Mathf.Round(252f * k), Mathf.Round(46f * k)),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

            var textGo = Child(fieldGo, "Text");
            Stretch(textGo, Mathf.Round(12f * k));
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = design.FontSizeSubtitle;
            text.color = design.TextPrimary;
            text.alignment = TextAlignmentOptions.Left;
            if (design.Font != null) text.font = design.Font;

            var input = fieldGo.AddComponent<TMP_InputField>();
            input.textViewport = (RectTransform)textGo.transform;
            input.textComponent = text;
            input.characterLimit = NickPrompt.MaxLength;

            var okGo = Child(panelGo, "Confirm");
            var okFill = okGo.AddComponent<GradientImage>();
            okFill.sprite = LoadSprite("rounded-rect");
            okFill.type = Image.Type.Sliced;
            okFill.pixelsPerUnitMultiplier = GlassPanel.PixelsPerUnitFor(Mathf.Round(22f * k));
            okFill.SetGradient(design.AccentTeal, design.AccentBlue);
            Place(okFill, new Vector2(Mathf.Round(62f * k), Mathf.Round(20f * k)),
                new Vector2(Mathf.Round(120f * k), Mathf.Round(46f * k)),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var okLabel = Label(okGo, "Label", "Готово", design, design.Font,
                design.FontSizeSubtitle, design.TextPrimary, TextAlignmentOptions.Center);
            Stretch(okLabel.gameObject);
            var ok = okGo.AddComponent<Button>();
            ok.targetGraphic = okFill;

            var cancelGo = Child(panelGo, "Cancel");
            var cancelLabel = Label(cancelGo, "Label", "Скасувати", design, design.Font,
                design.FontSizeShopCard, design.TextMuted, TextAlignmentOptions.Center);
            cancelLabel.raycastTarget = true;
            Stretch(cancelLabel.gameObject);
            Place(cancelLabel, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var cancelRect = cancelGo.GetComponent<RectTransform>();
            cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(-Mathf.Round(70f * k), Mathf.Round(20f * k));
            cancelRect.sizeDelta = new Vector2(Mathf.Round(110f * k), Mathf.Round(46f * k));
            var cancel = cancelGo.AddComponent<Button>();
            cancel.targetGraphic = cancelLabel;

            var prompt = go.AddComponent<NickPrompt>();
            Wire(prompt, ("root", go.GetComponent<RectTransform>()), ("field", input),
                ("confirmButton", ok), ("cancelButton", cancel));

            go.SetActive(false);
            return prompt;
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
