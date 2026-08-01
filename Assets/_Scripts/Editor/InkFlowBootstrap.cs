using System.IO;
using InkFlow.Gameplay;
using InkFlow.Levels;
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
    /// Одноразовий (ідемпотентний) бутстрап Фази 1: створює спрайт, Cell-префаб,
    /// ігрову сцену з HUD, Addressables-групу "Levels" і позначає рівні.
    /// Меню: Ink Flow → Setup → Bootstrap Phase 1. Batch:
    ///   Unity -batchmode -quit -projectPath &lt;root&gt; -executeMethod InkFlow.Editor.InkFlowBootstrap.BootstrapPhase1
    /// </summary>
    public static class InkFlowBootstrap
    {
        private const string SpritePath = "Assets/_Sprites/Square.png";
        private const string CellPrefabPath = "Assets/_Prefabs/Cell.prefab";
        private const string BurstFxPrefabPath = "Assets/_Prefabs/BurstFx.prefab";
        private const string ParticleMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string InputActionsPath = "Assets/_Scripts/Gameplay/InkFlowControls.inputactions";
        private const string UnlitSpriteMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        private static readonly string[] LevelAssetPaths =
        {
            "Assets/_ScriptableObjects/Levels/Level_001.asset",
            "Assets/_ScriptableObjects/Levels/Level_002.asset",
            "Assets/_ScriptableObjects/Levels/Level_003.asset"
        };

        [MenuItem("Ink Flow/Setup/Bootstrap Phase 1")]
        public static void BootstrapPhase1()
        {
            if (!EnsureTmpEssentials())
            {
                Debug.LogWarning("[InkFlow] TMP Essential Resources щойно імпортовано — " +
                                 "запусти Bootstrap Phase 1 ще раз, щоб добудувати сцену.");
                return;
            }

            // Кожен крок вантажить потрібні асети свіжими з AssetDatabase:
            // створення Addressables-налаштувань (перший запуск) тягне за собою
            // SaveAssets/Refresh, після якого референси, створені на попередніх
            // кроках, стають "fake null" і записались би в сцену як порожні.
            EnsureSquareSprite();
            EnsureCellPrefab();
            EnsureBurstFxPrefab();
            EnsureAddressableLevels();
            BuildGameScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[InkFlow] Bootstrap Phase 1 завершено: сцена Assets/Scenes/Game.unity готова до Play Mode.");
        }

        // ---------- TMP ----------

        private static bool EnsureTmpEssentials()
        {
            if (TMP_Settings.instance != null)
                return true;

            // TMP Essential Resources лежать усередині пакета ugui (Unity 6) або textmeshpro (старіші).
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

            Debug.LogError("[InkFlow] Не знайдено TMP Essential Resources.unitypackage — імпортуй вручну: Window → TextMeshPro → Import TMP Essential Resources.");
            return false;
        }

        // ---------- Спрайт ----------

        private static Sprite EnsureSquareSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null)
                return existing;

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

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        // ---------- Cell prefab ----------

        private static void EnsureCellPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<CellView>(CellPrefabPath) != null)
                return;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            EnsureFolder("Assets/_Prefabs");

            var cellGo = new GameObject("Cell");
            try
            {
                var spriteRenderer = cellGo.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingOrder = 1;
                var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
                if (unlit != null)
                    spriteRenderer.sharedMaterial = unlit; // не залежимо від 2D-освітлення

                var labelGo = new GameObject("DensityLabel");
                labelGo.transform.SetParent(cellGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                var label = labelGo.AddComponent<TextMeshPro>();
                label.text = "0";
                label.fontSize = 7f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.rectTransform.sizeDelta = Vector2.one;
                label.GetComponent<MeshRenderer>().sortingOrder = 2;

                var view = cellGo.AddComponent<CellView>();
                var so = new SerializedObject(view);
                so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
                so.FindProperty("densityLabel").objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(cellGo, CellPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(cellGo);
            }
        }

        // ---------- BurstFx prefab ----------

        private static void EnsureBurstFxPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<BurstEffect>(BurstFxPrefabPath) != null)
                return;

            EnsureFolder("Assets/_Prefabs");

            var fxGo = new GameObject("BurstFx");
            try
            {
                var ps = fxGo.AddComponent<ParticleSystem>();

                var main = ps.main;
                main.duration = 0.1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = 0.3f; // ТЗ: частки згасають за 300ms
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
                main.gravityModifier = 0f;
                main.maxParticles = 16;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12, 1, 0.01f) }); // ТЗ: 8-12 часток

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

                var renderer = fxGo.GetComponent<ParticleSystemRenderer>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.sortingOrder = 3; // над клітинками і підписами

                var fx = fxGo.AddComponent<BurstEffect>();
                var so = new SerializedObject(fx);
                so.FindProperty("particles").objectReferenceValue = ps;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(fxGo, BurstFxPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(fxGo);
            }
        }

        // ---------- Addressables ----------

        private static void EnsureAddressableLevels()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("Levels");
            if (group == null)
                group = settings.CreateGroup("Levels", false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            foreach (var path in LevelAssetPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    Debug.LogError($"[InkFlow] Рівень не знайдено: {path}");
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = $"Levels/{Path.GetFileNameWithoutExtension(path)}";
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
        }

        // ---------- Сцена ----------

        private static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Асети вантажимо СТРОГО ПІСЛЯ NewScene: закриття попередньої сцени
            // вивантажує незакорінені асети, тож будь-який референс, отриманий до
            // цього моменту (чи на попередніх кроках бутстрапа), стає "fake null"
            // і записався б у сцену порожнім полем.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var cellPrefab = AssetDatabase.LoadAssetAtPath<CellView>(CellPrefabPath);
            var burstFxPrefab = AssetDatabase.LoadAssetAtPath<BurstEffect>(BurstFxPrefabPath);
            if (sprite == null || cellPrefab == null || burstFxPrefab == null)
            {
                Debug.LogError($"[InkFlow] Не знайдено {SpritePath}, {CellPrefabPath} або {BurstFxPrefabPath} — сцена не збудована.");
                return;
            }

            var camera = CreateCamera();
            CreateGlobalLight();
            var gridController = CreateGridRig(camera, cellPrefab, burstFxPrefab);
            CreateGameManagement(gridController);
            CreateHud(sprite);
            CreateEventSystem();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static Camera CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f; // сітка 7×7 (~7.5 units) влазить у портрет
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 25, 38, 255);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<CameraShaker>();
            return camera;
        }

        private static void CreateGlobalLight()
        {
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static GridController CreateGridRig(Camera camera, CellView cellPrefab, BurstEffect burstFxPrefab)
        {
            var gridRoot = new GameObject("GridRoot");
            var pool = gridRoot.AddComponent<CellPool>();
            var gridView = gridRoot.AddComponent<GridView>();
            var swipeInput = gridRoot.AddComponent<SwipeInputHandler>();
            var gridController = gridRoot.AddComponent<GridController>();
            var effectPool = gridRoot.AddComponent<EffectPool>();
            var animator = gridRoot.AddComponent<GridAnimator>();

            var audioSource = gridRoot.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D-звук
            var chainAudio = gridRoot.AddComponent<ChainAudio>();

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                Debug.LogError($"[InkFlow] Не знайдено {InputActionsPath}");

            Wire(pool, ("cellPrefab", cellPrefab), ("contentRoot", gridRoot.transform));
            Wire(gridView, ("pool", pool));
            Wire(swipeInput, ("actionsAsset", actions), ("gridView", gridView), ("worldCamera", camera));
            Wire(effectPool, ("effectPrefab", burstFxPrefab), ("contentRoot", gridRoot.transform));
            Wire(chainAudio, ("source", audioSource));
            Wire(animator,
                ("view", gridView),
                ("effects", effectPool),
                ("chainAudio", chainAudio),
                ("cameraShaker", camera.GetComponent<CameraShaker>()));
            Wire(gridController, ("view", gridView), ("input", swipeInput), ("animator", animator));

            return gridController;
        }

        private static void CreateGameManagement(GridController gridController)
        {
            var managementGo = new GameObject("GameManagement");
            var loader = managementGo.AddComponent<LevelLoader>();
            var manager = managementGo.AddComponent<GameManager>();
            Wire(manager, ("levelLoader", loader), ("gridController", gridController));
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

            var movesLabel = CreateLabel(canvasGo.transform, "MovesLabel", "Ходи: —",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                anchoredPos: new Vector2(40f, -60f), pivot: new Vector2(0f, 1f),
                fontSize: 56f, alignment: TextAlignmentOptions.Left);

            var goalLabel = CreateLabel(canvasGo.transform, "GoalLabel", "Ціль: —",
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                anchoredPos: new Vector2(-40f, -60f), pivot: new Vector2(1f, 1f),
                fontSize: 56f, alignment: TextAlignmentOptions.Right);

            var retryButton = CreateRetryButton(canvasGo.transform, sprite);

            var winPanel = CreateBanner(canvasGo.transform, sprite, "WinPanel", "Перемога!",
                new Color32(0, 150, 90, 235));
            var losePanel = CreateBanner(canvasGo.transform, sprite, "LosePanel", "Ходи скінчились",
                new Color32(160, 40, 60, 235));

            var hud = canvasGo.AddComponent<HUDController>();
            Wire(hud, ("movesLabel", movesLabel), ("goalLabel", goalLabel), ("retryButton", retryButton));

            var winScreen = canvasGo.AddComponent<WinScreenController>();
            Wire(winScreen, ("panel", winPanel));
            var loseScreen = canvasGo.AddComponent<LoseScreenController>();
            Wire(loseScreen, ("panel", losePanel));
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
            rect.sizeDelta = new Vector2(500f, 90f);
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
            rect.anchoredPosition = new Vector2(0f, 70f);
            rect.sizeDelta = new Vector2(360f, 110f);

            // Без символу ↺ — його немає в LiberationSans SDF (TMP-ворнінг).
            CreateLabel(go.transform, "Label", "Retry",
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0.5f, 0.5f),
                52f, TextAlignmentOptions.Center).rectTransform.sizeDelta = Vector2.zero;

            return button;
        }

        private static GameObject CreateBanner(Transform parent, Sprite sprite, string name, string text, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // банер не блокує HUD-кнопку Retry
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 160f);
            rect.sizeDelta = new Vector2(0f, 180f);

            var label = CreateLabel(go.transform, "Label", text,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0.5f, 0.5f),
                72f, TextAlignmentOptions.Center);
            label.rectTransform.sizeDelta = Vector2.zero;
            label.raycastTarget = false;

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
        /// Проставляє приватні [SerializeField]-поля через SerializedObject і
        /// ГОЛОСНО валідує результат: null на вході чи "fake null" (застарілий
        /// референс, що записався порожнім) — це помилка бутстрапа, не тиха дірка в сцені.
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
            {
                if (value != null && check.FindProperty(field)?.objectReferenceValue == null)
                    Debug.LogError($"[InkFlow] Поле '{field}' на {target.GetType().Name} записалось як null — " +
                                   "референс застарів. Запусти Bootstrap Phase 1 ще раз.");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
