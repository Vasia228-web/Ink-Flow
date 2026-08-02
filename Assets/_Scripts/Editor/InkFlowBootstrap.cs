using System.IO;
using InkFlow.App;
using InkFlow.Core;
using InkFlow.Gameplay;
using InkFlow.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace InkFlow.Editor
{
    /// <summary>
    /// Ідемпотентний бутстрап проєкту: генерує спрайт, префаби, конфіги, рівні,
    /// Addressables-групу і сцену Game з повним ригом.
    /// Меню: Ink Flow → Setup → Bootstrap Scene. Batch:
    ///   Unity -batchmode -quit -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.InkFlowBootstrap.BootstrapScene
    ///
    /// ВАЖЛИВО про порядок: усі асети завантажуються ЗАНОВО безпосередньо перед підв'язкою.
    /// NewScene() і будь-який AssetDatabase.Refresh вивантажують незакорінені асети, і
    /// референс, узятий раніше, тихо перетворюється на «fake null», який записався б у сцену
    /// порожнім полем.
    /// </summary>
    public static class InkFlowBootstrap
    {
        private const string SpritePath = "Assets/_Sprites/Square.png";
        private const string CellPrefabPath = "Assets/_Prefabs/Cell.prefab";
        private const string BurstFxPrefabPath = "Assets/_Prefabs/BurstFx.prefab";
        private const string BossSegmentPrefabPath = "Assets/_Prefabs/BossSegment.prefab";
        private const string BalanceConfigPath = "Assets/_ScriptableObjects/Balance/BalanceConfig.asset";
        private const string FeelConfigPath = "Assets/_ScriptableObjects/Balance/FeelConfig.asset";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string InputActionsPath = "Assets/_Scripts/Gameplay/InkFlowControls.inputactions";
        private const string UnlitSpriteMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string ParticleMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        [MenuItem("Ink Flow/Setup/Bootstrap Scene")]
        public static void BootstrapScene()
        {
            if (!EnsureTmpEssentials())
            {
                Debug.LogWarning("[InkFlow] TMP Essential Resources щойно імпортовано — " +
                                 "запусти Bootstrap Scene ще раз, щоб добудувати сцену.");
                return;
            }

            EnsureSquareSprite();
            EnsureCellPrefab();
            EnsureBurstFxPrefab();
            EnsureBossSegmentPrefab();
            EnsureConfigs();
            LevelAuthoring.CreateStarterLevels();
            EnsureAddressableLevels();
            BuildGameScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] Bootstrap завершено: Assets/Scenes/Game.unity готова до Play Mode.");
        }

        // ---------- TMP ----------

        private static bool EnsureTmpEssentials()
        {
            if (TMP_Settings.instance != null)
                return true;

            var packagePaths = new[]
            {
                "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
                "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage"
            };

            foreach (var path in packagePaths)
            {
                if (!File.Exists(Path.GetFullPath(path)))
                    continue;
                AssetDatabase.ImportPackage(path, false);
                AssetDatabase.Refresh();
                return TMP_Settings.instance != null;
            }

            Debug.LogError("[InkFlow] Не знайдено TMP Essential Resources — імпортуй вручну: " +
                           "Window → TextMeshPro → Import TMP Essential Resources.");
            return false;
        }

        // ---------- Асети ----------

        private static void EnsureSquareSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath) != null)
                return;

            EnsureFolder("Assets/_Sprites");

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(SpritePath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(SpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size; // 1 world unit = 1 клітинка
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void EnsureCellPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<CellView>(CellPrefabPath) != null)
                return;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            EnsureFolder("Assets/_Prefabs");

            var root = new GameObject("Cell");
            try
            {
                var body = root.AddComponent<SpriteRenderer>();
                body.sprite = sprite;
                body.sortingOrder = 1;
                if (unlit != null)
                    body.sharedMaterial = unlit;

                var overlayGo = new GameObject("Overlay");
                overlayGo.transform.SetParent(root.transform, false);
                overlayGo.transform.localPosition = new Vector3(0f, 0f, -0.005f);
                var overlay = overlayGo.AddComponent<SpriteRenderer>();
                overlay.sprite = sprite;
                overlay.sortingOrder = 2;
                overlay.enabled = false;
                if (unlit != null)
                    overlay.sharedMaterial = unlit;

                var labelGo = new GameObject("DensityLabel");
                labelGo.transform.SetParent(root.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                var label = labelGo.AddComponent<TextMeshPro>();
                label.text = "0";
                label.fontSize = 7f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.rectTransform.sizeDelta = Vector2.one;
                label.GetComponent<MeshRenderer>().sortingOrder = 3;

                var view = root.AddComponent<CellView>();
                Wire(view,
                    ("spriteRenderer", body),
                    ("densityLabel", label),
                    ("overlayRenderer", overlay));

                PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureBurstFxPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<BurstEffect>(BurstFxPrefabPath) != null)
                return;

            EnsureFolder("Assets/_Prefabs");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);

            var root = new GameObject("BurstFx");
            try
            {
                var ps = root.AddComponent<ParticleSystem>();

                var main = ps.main;
                main.duration = 0.1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = 0.3f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
                main.gravityModifier = 0f;
                main.maxParticles = 16; // мобільний бюджет: частинок мало і без Collision-модуля

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12, 1, 0.01f) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.08f;

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

                var sizeOverLifetime = ps.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.sortingOrder = 5;

                var fx = root.AddComponent<BurstEffect>();
                Wire(fx, ("particles", ps));

                PrefabUtility.SaveAsPrefabAsset(root, BurstFxPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureBossSegmentPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<SpriteRenderer>(BossSegmentPrefabPath) != null)
                return;

            EnsureFolder("Assets/_Prefabs");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);

            var root = new GameObject("BossSegment");
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(0.12f, 0.12f, 0.16f, 1f);
                renderer.sortingOrder = 1;
                if (unlit != null)
                    renderer.sharedMaterial = unlit;
                root.transform.localScale = new Vector3(1f, 0.7f, 1f);

                PrefabUtility.SaveAsPrefabAsset(root, BossSegmentPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureConfigs()
        {
            EnsureFolder("Assets/_ScriptableObjects/Balance");
            EnsureAsset<BalanceConfig>(BalanceConfigPath);
            EnsureAsset<FeelConfig>(FeelConfigPath);
        }

        private static void EnsureAsset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        // ---------- Addressables ----------

        private static void EnsureAddressableLevels()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("Levels") ?? settings.CreateGroup(
                "Levels", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            RemoveDanglingEntries(settings, group);

            foreach (var path in LevelAuthoring.LevelAssetPaths())
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    Debug.LogError($"[InkFlow] Рівень не знайдено: {path}");
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = LevelCatalog.AddressPrefix + Path.GetFileNameWithoutExtension(path);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        }

        /// <summary>
        /// Прибирає записи на асети, яких уже немає. Addressables зберігають GUID, а не шлях,
        /// тож перейменований чи видалений рівень лишає «висячий» запис, який ламає
        /// збірку контенту — і робить це мовчки, аж до білда.
        /// </summary>
        private static void RemoveDanglingEntries(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var stale = new System.Collections.Generic.List<AddressableAssetEntry>();
            foreach (var entry in group.entries)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                    stale.Add(entry);
            }

            foreach (var entry in stale)
            {
                Debug.Log($"[InkFlow] Прибрано висячий Addressables-запис '{entry.address}' (асет видалено).");
                settings.RemoveAssetEntry(entry.guid, false);
            }
        }

        // ---------- Сцена ----------

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Асети вантажимо СТРОГО після NewScene — див. коментар у шапці класу.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var cellPrefab = AssetDatabase.LoadAssetAtPath<CellView>(CellPrefabPath);
            var burstFx = AssetDatabase.LoadAssetAtPath<BurstEffect>(BurstFxPrefabPath);
            var bossSegment = AssetDatabase.LoadAssetAtPath<SpriteRenderer>(BossSegmentPrefabPath);
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceConfigPath);
            var feel = AssetDatabase.LoadAssetAtPath<FeelConfig>(FeelConfigPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (sprite == null || cellPrefab == null || burstFx == null || bossSegment == null ||
                balance == null || feel == null || actions == null)
            {
                Debug.LogError("[InkFlow] Не всі асети знайдено — сцена не збудована. " +
                               "Запусти Bootstrap Scene ще раз.");
                return;
            }

            var camera = CreateCamera();
            CreateGlobalLight();

            var rig = CreateGridRig(camera, cellPrefab, burstFx, bossSegment, feel, actions);
            CreateAppRoot(rig.presenter, rig.feedback, balance);
            CreateHud(sprite);
            CreateEventSystem();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 25, 38, 255);
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<ShakeController>();
            return camera;
        }

        private static void CreateGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static (GamePresenter presenter, ChainFeedback feedback) CreateGridRig(
            Camera camera, CellView cellPrefab, BurstEffect burstFx, SpriteRenderer bossSegment,
            FeelConfig feel, InputActionAsset actions)
        {
            var gridRoot = new GameObject("GridRoot");

            var cellPool = gridRoot.AddComponent<CellPool>();
            var particlePool = gridRoot.AddComponent<ParticlePool>();
            var gridView = gridRoot.AddComponent<GridView>();
            var swipeInput = gridRoot.AddComponent<SwipeInput>();
            var presenter = gridRoot.AddComponent<GamePresenter>();

            var audioSource = gridRoot.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            var feedback = gridRoot.AddComponent<ChainFeedback>();

            var bossGo = new GameObject("Boss");
            bossGo.transform.SetParent(gridRoot.transform, false);
            var bossView = bossGo.AddComponent<BossView>();

            Wire(cellPool, ("cellPrefab", cellPrefab), ("contentRoot", gridRoot.transform));
            Wire(particlePool, ("effectPrefab", burstFx), ("contentRoot", gridRoot.transform));
            Wire(gridView,
                ("cellPool", cellPool), ("particlePool", particlePool), ("feel", feel),
                ("shaker", camera.GetComponent<ShakeController>()), ("feedback", feedback));
            Wire(swipeInput, ("actionsAsset", actions), ("gridView", gridView), ("worldCamera", camera));
            Wire(feedback, ("source", audioSource));
            Wire(bossView, ("gridView", gridView), ("segmentPrefab", bossSegment));
            Wire(presenter, ("gridView", gridView), ("swipeInput", swipeInput), ("bossView", bossView));

            bossGo.SetActive(false); // вмикається лише на бос-рівні
            return (presenter, feedback);
        }

        private static void CreateAppRoot(GamePresenter presenter, ChainFeedback feedback, BalanceConfig balance)
        {
            var go = new GameObject("App");
            var catalog = go.AddComponent<LevelCatalog>();
            var bootstrap = go.AddComponent<GameBootstrap>();
            Wire(bootstrap,
                ("balanceConfig", balance),
                ("levelCatalog", catalog),
                ("presenter", presenter),
                ("chainFeedback", feedback));
        }

        private static void CreateHud(Sprite sprite)
        {
            var canvasGo = new GameObject("HUD Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Safe area — обов'язково, інакше UI ріжеться на iPhone з Dynamic Island (§9).
            var safeGo = new GameObject("SafeArea");
            safeGo.transform.SetParent(canvasGo.transform, false);
            var safeRect = safeGo.AddComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeGo.AddComponent<SafeAreaBinder>();

            var moves = CreateLabel(safeGo.transform, "MovesLabel", "Ходи: —",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(0f, 1f),
                52f, TextAlignmentOptions.Left);
            var goal = CreateLabel(safeGo.transform, "GoalLabel", "Ціль: —",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(1f, 1f),
                44f, TextAlignmentOptions.Right);
            var score = CreateLabel(safeGo.transform, "ScoreLabel", "Очки: 0",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(0f, 1f),
                44f, TextAlignmentOptions.Left);

            var retry = CreateRetryButton(safeGo.transform, sprite);

            var winPanel = CreateBanner(safeGo.transform, sprite, "WinPanel", new Color32(0, 150, 90, 235),
                out var winLabel, out var starsLabel);
            var losePanel = CreateBanner(safeGo.transform, sprite, "LosePanel", new Color32(160, 40, 60, 235),
                out var loseLabel, out _);
            _ = loseLabel;

            var hud = safeGo.AddComponent<HudScreen>();
            Wire(hud,
                ("movesLabel", moves), ("goalLabel", goal), ("scoreLabel", score),
                ("retryButton", retry), ("winPanel", winPanel), ("losePanel", losePanel),
                ("outcomeLabel", winLabel), ("starsLabel", starsLabel));
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 pivot,
            float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            var rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(560f, 80f);
            return label;
        }

        private static Button CreateRetryButton(Transform parent, Sprite sprite)
        {
            var go = new GameObject("RetryButton");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color32(61, 90, 254, 255);
            var button = go.AddComponent<Button>();
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(340f, 110f);

            // Без гліфа ↺ — його немає в LiberationSans SDF.
            var label = CreateLabel(go.transform, "Label", "Retry",
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0.5f, 0.5f),
                50f, TextAlignmentOptions.Center);
            label.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static GameObject CreateBanner(Transform parent, Sprite sprite, string name, Color color,
            out TMP_Text outcomeLabel, out TMP_Text starsLabel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // банер не має перехоплювати натискання Retry
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(0f, 230f);

            outcomeLabel = CreateLabel(go.transform, "Label", name,
                new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0.5f, 0.5f),
                64f, TextAlignmentOptions.Center);
            outcomeLabel.rectTransform.sizeDelta = Vector2.zero;
            outcomeLabel.raycastTarget = false;

            starsLabel = CreateLabel(go.transform, "Stars", string.Empty,
                new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, new Vector2(0.5f, 0.5f),
                56f, TextAlignmentOptions.Center);
            starsLabel.rectTransform.sizeDelta = Vector2.zero;
            starsLabel.raycastTarget = false;

            go.SetActive(false);
            return go;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- Утиліти ----------

        /// <summary>
        /// Проставляє приватні [SerializeField]-поля й ГОЛОСНО валідує результат:
        /// null на вході або поле, що записалось порожнім, — помилка бутстрапа,
        /// а не тиха дірка в сцені, яку знайдуть уже в Play Mode.
        /// </summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                if (value == null)
                {
                    Debug.LogError($"[InkFlow] Спроба підв'язати null у поле '{field}' на {target.GetType().Name}");
                    continue;
                }

                var property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[InkFlow] Поле '{field}' не знайдено на {target.GetType().Name}");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var check = new SerializedObject(target);
            foreach (var (field, value) in fields)
                if (value != null && check.FindProperty(field)?.objectReferenceValue == null)
                    Debug.LogError($"[InkFlow] Поле '{field}' на {target.GetType().Name} записалось як null — " +
                                   "референс застарів. Запусти Bootstrap Scene ще раз.");
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            // Явна перевірка замість string.IsNullOrEmpty: у reference-збірках, якими
            // компілює Unity, вона не має [NotNullWhen(false)], тож компілятор НЕ звужує
            // тип і видає CS8604. `is null` звужує завжди.
            if (parent is null || parent.Length == 0)
                return; // дійшли до кореня Assets — створювати нічого

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
