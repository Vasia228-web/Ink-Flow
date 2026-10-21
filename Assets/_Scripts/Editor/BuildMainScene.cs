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
    /// вісім екранів із префабів і <see cref="AppRouter"/> між ними.
    /// Меню: Ink Flow → Setup → Build Main Scene.
    ///
    /// Це ТОЧКА ВХОДУ гри — саме `Main.unity` стоїть у Build Settings, і саме тут
    /// живе `GameBootstrap` із сервісами й збереженням. Окремі сцени екранів
    /// (`Hub.unity`, `Galaxy.unity`…) лишаються майстернею: на них зручно правити
    /// один екран, не піднімаючи весь застосунок.
    ///
    /// Розкладки тут немає жодної — усе приходить префабами з Build*Screen.
    /// Тому перед цією командою треба зібрати всі вісім екранів.
    ///
    /// У збереженій сцені активний лише хаб: решта сім лежать згаслими. Інакше
    /// вісім розтягнутих на весь канвас екранів накладаються один на одного
    /// й у Scene, і в Game.
    /// </summary>
    public static class BuildMainScene
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string BalancePath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";
        private const string EconomyPath = "Assets/_ScriptableObjects/Balance/EconomyConfig.asset";
        private const string PicturesPath = RefreshPictureLibrary.AssetPath;

        /// <summary>Імена префабів у порядку, в якому вони лягають у сцену.</summary>
        private static readonly string[] ScreenNames =
        {
            "HubScreen", "EndlessScreen",
            "GalaxyScreen", "PlanetScreen", "CollectionScreen", "RankingsScreen", "ProfileScreen",
            "SettingsScreen"
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
            // Розкладка галактики з дефолтами створюється тут же: вимагати окремого кроку заради асета,
            // який і так дорівнює коду, означало б ще одну причину «не зібралось».
            var galaxyConfig = InkFlowBootstrap.EnsureGalaxyConfig();
            var appConfig = InkFlowBootstrap.EnsureAppConfig();
            var pictures = AssetDatabase.LoadAssetAtPath<PictureLibraryAsset>(PicturesPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (balance == null) missing.Add(BalancePath);
            if (economy == null) missing.Add(EconomyPath);
            if (pictures == null) missing.Add($"{PicturesPath} (Ink Flow → Setup → Refresh Picture Library)");
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
            var confirm = BuildConfirmPrompt(safe, design!);

            var routerGo = Child(safe, "AppRouter");
            var router = routerGo.AddComponent<AppRouter>();
            Wire(router,
                ("navigation", navigation),
                ("hub", screens["HubScreen"]),
                ("endless", screens["EndlessScreen"]),
                ("galaxy", screens["GalaxyScreen"]),
                ("planet", screens["PlanetScreen"]),
                ("collection", screens["CollectionScreen"]),
                ("rankings", screens["RankingsScreen"]),
                ("profile", screens["ProfileScreen"]),
                ("settings", screens["SettingsScreen"]),
                ("nickPrompt", prompt),
                ("confirmPrompt", confirm));

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            // Музика (§15) — свій об'єкт з AudioSource поруч із бутстрапом; клип генерується в рантаймі.
            var audioGo = new GameObject("GameAudio");
            audioGo.transform.SetParent(bootstrapGo.transform, false);
            var audioSource = audioGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            var gameAudio = audioGo.AddComponent<GameAudio>();
            Wire(gameAudio, ("music", audioSource));
            // Гаптика поля підставляється сюди: композиційний корінь — єдиний, хто
            // знає реалізацію Platform, а звук/гаптика поля живуть у префабі екрана.
            var feedback = screens["EndlessScreen"].GetComponentInChildren<BoardFeedback>(true);
            Wire(bootstrap,
                ("balanceConfig", balance!),
                ("economyConfig", economy!),
                ("galaxyConfig", galaxyConfig),
                ("appConfig", appConfig),
                ("pictureLibrary", pictures!),
                ("router", router),
                ("endlessScreen", screens["EndlessScreen"]),
                ("boardFeedback", feedback),
                ("gameAudio", gameAudio));

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
        /// Діалог ніка U1Glaze (S10Profile_02…04) — публічна фабрика: UI-тест збирає його так само, як головна сцена.
        /// <see cref="U1.Dialog"/> (затемнення 78 % + непрозора панель 362): «Твій нік» h1 ліворуч, поле <see cref="U1.Field"/>
        /// з <c>TMP_InputField</c>, рядок «підказка / помилка» ліворуч і лічильник «N / M» праворуч, бірюзова «Зберегти»
        /// 56 і текстова «Скасувати». Лежить вимкненим.
        /// </summary>
        public static NickPrompt BuildNickPrompt(GameObject parent, DesignSystem design)
        {
            var dialog = U1.Dialog(parent, "NickPrompt", design, U1.DialogWidthPx, out var panel, U1.DialogPaddingPx);
            var title = U1.Text(panel, "Title", "Твій нік", design, U1.Role.H1, design.InkText, TextAlignmentOptions.Left);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var (fieldBody, ring, text) = U1.Field(panel, "Field", string.Empty, design, error: false, focused: true);
            U1.Size(fieldBody.gameObject, 0f, 58f);
            // Поле вводу: TMP_InputField поверх спрайта поля; текст — роль h3 900, як на макеті.
            var input = fieldBody.gameObject.AddComponent<TMP_InputField>();
            var viewportGo = Child(fieldBody.gameObject, "TextArea");
            Stretch(viewportGo);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.offsetMin = new Vector2(U1.Px(18f), U1.Px(4f));
            viewportRect.offsetMax = new Vector2(-U1.Px(18f), -U1.Px(4f));
            viewportGo.AddComponent<RectMask2D>();
            text.transform.SetParent(viewportGo.transform, false);
            Stretch(text.gameObject);
            text.margin = Vector4.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = viewportRect;
            input.textComponent = text;
            input.targetGraphic = fieldBody;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 16;
            input.caretColor = design.Teal;
            input.customCaretColor = true;
            input.caretWidth = 3;
            input.selectionColor = DesignSystem.WithAlpha(design.Teal, 0.35f);
            input.onFocusSelectAll = false;

            var infoRow = Child(panel, "Info");
            var info = infoRow.AddComponent<HorizontalLayoutGroup>();
            info.spacing = U1.Px(8f);
            info.childAlignment = TextAnchor.MiddleLeft;
            info.childControlWidth = info.childControlHeight = true;
            info.childForceExpandWidth = info.childForceExpandHeight = false;
            U1.Size(infoRow, 0f, 24f).flexibleHeight = 0f;
            var hint = U1.Text(infoRow, "Hint", NickPrompt.HintText, design, U1.Role.Body, design.InkMuted, TextAlignmentOptions.Left);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            hint.enableAutoSizing = true;
            hint.fontSizeMax = design.UiBody;
            hint.fontSizeMin = design.UiBody * 0.8f;
            var hintSize = hint.gameObject.AddComponent<LayoutElement>();
            hintSize.preferredWidth = 0f;
            hintSize.minWidth = 0f;
            hintSize.flexibleWidth = 1f;
            var counter = U1.Text(infoRow, "Counter", "0 / 16", design, U1.Role.Body, design.InkMuted, TextAlignmentOptions.Right);
            counter.font = design.FontBlack;
            counter.textWrappingMode = TextWrappingModes.NoWrap;

            U1.Gap(panel, 6f);
            var save = U1.Button(panel, "Save", "Зберегти", design, CandyButton.Tone.Teal);
            U1.Size(save.gameObject, 0f, 56f);
            var cancel = U1.TextButton(panel, "Cancel", "Скасувати", design, design.InkMuted);
            U1.Size(cancel.gameObject, 0f, 44f);

            var prompt = dialog.gameObject.AddComponent<NickPrompt>();
            Wire(prompt, ("design", design), ("root", dialog.GetComponent<RectTransform>()), ("dialog", dialog),
                ("field", input), ("fieldBody", fieldBody), ("fieldRing", ring), ("hintLabel", hint), ("counterLabel", counter),
                ("confirmButton", save), ("cancelButton", cancel),
                ("fieldSprite", U1.S("field")), ("fieldErrorSprite", U1.S("field_error")));
            dialog.gameObject.SetActive(false);
            return prompt;
        }

        /// <summary>«Вийти додому?» (§15: «Додому» з забігу) — діалог U1Glaze, та сама фабрика, що в стенді знімків забігу.</summary>
        private static ConfirmPrompt BuildConfirmPrompt(GameObject parent, DesignSystem design) => U1.ConfirmPrompt(parent, design);

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
