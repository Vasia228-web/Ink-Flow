using InkFlow.Style;
using InkFlow.UI;
using TMPro;
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
    /// Збирає екран планети U1Glaze (S04PlanetSphere, S05PlanetHorizon, S12SmallScreen_05/06) фабриками <see cref="U1"/>:
    /// стовпчик (VerticalLayoutGroup) — шапка 56 («‹», нафта, шестерня) → назва планети h1 → підпис галактики →
    /// [«Горизонт»: рядок міток і лічильник] → гнучка ділянка планети → [«Сфера»: лічильник] → коралова
    /// «Поставити картинку» → підказка; на місці низу — панель «планета ожила». Поверх — лист зайнятої комірки.
    /// Обидві розкладки живуть у префабі; яку показати, вирішує екран за порогом <c>sphereMaxSlots</c>.
    /// Планети — шейдер InkFlow/Planet (SPEC: планети на макетах — заглушки), комірки, мітки, іскри — спрайти U1Glaze.
    /// Меню: Ink Flow → Setup → Build Planet Screen.
    /// </summary>
    public static class BuildPlanetScreen
    {
        private const string ScenePath = "Assets/Scenes/Planet.unity";
        private const string PrefabFolder = "Assets/_Prefabs/UI";
        private const string DesignSystemPath = "Assets/_ScriptableObjects/Style/DesignSystem.asset";
        private const string SpriteAssetPath = "Assets/_Sprites/UI/InkFlow Icons.asset";
        private const string ShaderPath = "Assets/_Shaders/InkFlowPlanet.shader";

        /// <summary>Комірок і міток у пулі: стеля слотів у GalaxyConfig ([Range(1, 24)]).</summary>
        private const int SlotPool = 24;

        /// <summary>Діаметр кулі за масштабу 1, px макета (S04: 291.5); під екран її масштабує PlanetScreen.</summary>
        private const float GlobePx = 291.5f;

        /// <summary>Бокс комірки за масштабу 1, px макета (спрайти слота — 72); розмір у розкладці — масштабом.</summary>
        private const float CellPx = 72f;

        [MenuItem("Ink Flow/Setup/Build Planet Screen")]
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
            if (AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath) == null)
            {
                Debug.LogError($"[InkFlow] Немає {DesignSystemPath}. Спершу: Ink Flow → Setup → Build UI Kit.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Асети — строго після NewScene: закриття сцени вивантажує незакорінені асети.
            var design = AssetDatabase.LoadAssetAtPath<DesignSystem>(DesignSystemPath);
            IconSprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var quad = LoadSprite("white-quad");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var cosmic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CosmicBackground.prefab");

            var missing = new System.Collections.Generic.List<string>();
            if (design == null) missing.Add(DesignSystemPath);
            if (quad == null) missing.Add($"{SpriteFolder}/white-quad.png");
            if (shader == null) missing.Add(ShaderPath);
            if (cosmic == null) missing.Add($"{PrefabFolder}/CosmicBackground.prefab");
            if (U1GlazeSprites.Load("slot_empty") == null) missing.Add("спрайти U1Glaze (Ink Flow → Setup → Build UI Kit)");
            if (missing.Count > 0)
            {
                Debug.LogError("[InkFlow] Планету НЕ зібрано — не знайдено:\n  " + string.Join("\n  ", missing));
                return;
            }

            CreateCamera(design!);

            var canvasGo = new GameObject("UI Root");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            PrefabUtility.InstantiatePrefab(cosmic, canvasGo.transform);

            var safe = Child(canvasGo, "SafeArea");
            Stretch(safe);
            var safeBinder = safe.AddComponent<SafeAreaBinder>();
            var navigation = safe.AddComponent<NavigationStack>();

            var uiRoot = canvasGo.AddComponent<UIRoot>();
            Wire(uiRoot, ("canvas", canvas), ("scaler", scaler), ("navigation", navigation), ("safeArea", safeBinder));

            var screenGo = Child(safe, "PlanetScreen");
            Stretch(screenGo);
            var screen = screenGo.AddComponent<PlanetScreen>();

            // ── Стовпчик ──
            var column = Child(screenGo, "Column");
            Stretch(column);
            var col = column.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset((int)U1.Px(16f), (int)U1.Px(16f), 0, (int)U1.Px(8f));
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            U1.ScreenHeader(column, design!, out var back, out var title, out var currency, out var gear);
            title.text = string.Empty; // підекран: «‹» замість назви, назва планети — рядком нижче (S04)

            var planetName = U1.Text(column, "PlanetName", "Кріос", design!, U1.Role.H1, design!.InkText);
            planetName.textWrappingMode = TextWrappingModes.NoWrap;
            planetName.overflowMode = TextOverflowModes.Ellipsis;
            var galaxyLabel = U1.Text(column, "GalaxyLabel", "ГАЛАКТИКА I · ПЕРВІСНА", design!, U1.Role.Label, design!.InkMuted);
            galaxyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            galaxyLabel.overflowMode = TextOverflowModes.Ellipsis;

            // «Горизонт»: мітки рядком і лічильник — угорі (S05).
            U1.Gap(column, 12f);
            var pipRow = BuildPipRow(column, design!, out var rowPips, out var rowShines);
            var horizonCounter = BuildCounter(column, "HorizonCounter", design!, out var horizonCount, out var horizonWord);

            // ── Ділянка планети (гнучка) ──
            var area = Child(column, "PlanetArea");
            area.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var globe = BuildGlobe(area, design!, quad!, shader!, out var stage);
            var horizon = BuildHorizon(area, design!, quad!, shader!, screenGo);

            // ── Низ: лічильник «Сфери», дія, підказка ──
            var sphereCounter = BuildCounter(column, "SphereCounter", design!, out var sphereCount, out var sphereWord);
            var actionGap = U1.Gap(column, 10f); // ховається разом із дією, коли низ замінює панель «ожила»
            var actionRow = Child(column, "ActionRow");
            var actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
            actionLayout.childControlWidth = actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;
            // flexibleHeight 0: childForceExpandHeight робить ряд гнучким — він ділив би залишок із планетою (крок 3).
            U1.Size(actionRow, 0f, 56f).flexibleHeight = 0f;
            var place = U1.Button(actionRow, "Place", "Поставити картинку", design!, CandyButton.Tone.Coral);
            var hintGap = U1.Gap(column, design!.PlanetHintGap); // поза гало кнопки (S04_01: 13.5 до літер)
            var hint = U1.Text(column, "Hint", "Крути планету пальцем і тапни на порожню комірку", design!, U1.Role.Caption, design!.InkMuted);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.overflowMode = TextOverflowModes.Ellipsis;

            var alive = BuildAlivePanel(column, design!, out var kicker, out var aliveTitle, out var aliveBody, out var aliveButton);
            var visitorFooter = BuildVisitorFooter(column, design!, out var visitorTitle, out var visitorButton);
            var aliveGroup = alive.gameObject.AddComponent<CanvasGroup>();

            // ── Лист зайнятої комірки (S04_04, S05_03) ──
            var sheet = BuildSheet(screenGo, design!, quad!, out var frame, out var picture, out var sheetName, out var badge,
                out var replace, out var remove, out var cancel);

            Wire(screen,
                ("design", design!), ("backButton", back), ("settingsButton", gear), ("currency", currency),
                ("planetName", planetName), ("galaxyLabel", galaxyLabel),
                ("horizonPipRow", pipRow), ("pipEmpty", U1.S("pip_empty")), ("pipFilled", U1.S("pip_filled")),
                ("horizonCounter", horizonCounter), ("horizonCount", horizonCount), ("horizonCountWord", horizonWord),
                ("column", column.GetComponent<RectTransform>()), ("area", area.GetComponent<RectTransform>()),
                ("stage", stage), ("globe", globe), ("horizon", horizon),
                ("sphereCounter", sphereCounter), ("sphereCount", sphereCount), ("sphereCountWord", sphereWord),
                ("actionGap", actionGap.GetComponent<RectTransform>()), ("actionRow", actionRow.GetComponent<RectTransform>()),
                ("placeButton", place), ("hintGap", hintGap.GetComponent<RectTransform>()), ("hint", hint),
                ("alivePanel", alive), ("alivePanelGroup", aliveGroup), ("aliveKicker", kicker), ("aliveTitle", aliveTitle), ("aliveBody", aliveBody),
                ("aliveButton", aliveButton),
                ("visitorFooter", visitorFooter), ("visitorTitle", visitorTitle), ("visitorButton", visitorButton),
                ("sheet", sheet), ("sheetFrame", frame), ("sheetPicture", picture), ("sheetName", sheetName),
                ("sheetBadge", badge), ("replaceButton", replace), ("removeButton", remove), ("cancelButton", cancel));
            WireArray(screen, "horizonPips", rowPips);
            WireArray(screen, "horizonPipShines", rowShines);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Синхронно й перед збереженням: OnValidate відкладений, і дефолти потрапили б у файл.
            uiRoot.ApplyScaler();
            screen.Apply();

            SaveScreenPrefab(screenGo);

            InkFlowBootstrap.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InkFlow] Планету зібрано: {ScenePath} (U1Glaze, «Сфера» / «Горизонт», коефіцієнт {DesignSystem.MockupToReference:0.000}).");
        }

        // ── Рядок міток «Горизонту» (S05): по одній на комірку; розмір і проміжок ставить екран за кількістю ──
        private static RectTransform BuildPipRow(GameObject parent, DesignSystem design, out Image[] pips, out Image[] shines)
        {
            var row = Child(parent, "PipRow");
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = U1.Px(design.HorizonPipGap);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            U1.Size(row, 0f, design.HorizonPipSize);
            pips = new Image[SlotPool];
            shines = new Image[SlotPool];
            for (var i = 0; i < SlotPool; i++)
            {
                var (pip, shine) = U1.Pip(row, $"Pip{i}", filled: false, Color.white, design.HorizonPipSize);
                U1.Size(pip.gameObject, design.HorizonPipSize, design.HorizonPipSize);
                pip.gameObject.SetActive(false);
                pips[i] = pip;
                shines[i] = shine;
            }
            return row.GetComponent<RectTransform>();
        }

        // ── Лічильник «N / M комірок заповнено» (S04, S05): число h3 ink, слово body ink-muted ──
        private static RectTransform BuildCounter(GameObject parent, string name, DesignSystem design, out TMP_Text count, out TMP_Text word)
        {
            var row = Child(parent, name);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = U1.Px(10f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            U1.Size(row, 0f, 28f).flexibleHeight = 0f;
            count = U1.Text(row, "Count", "5 / 8", design, U1.Role.H3, design.InkText);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            word = U1.Text(row, "Word", "комірок заповнено", design, U1.Role.Body, design.InkMuted);
            word.textWrappingMode = TextWrappingModes.NoWrap;
            return row.GetComponent<RectTransform>();
        }

        // ── «Сфера» (S04): гало, дуга видимої половини, куля, комірки під круглою маскою, кільце міток, іскри ──
        private static PlanetGlobe BuildGlobe(GameObject area, DesignSystem design, Sprite quad, Shader shader, out RectTransform stage)
        {
            var root = Child(area, "Sphere");
            Stretch(root);
            // Прозора площина для дотику: без графіки з raycastTarget EventSystem не доставить ні свайпу, ні тапу.
            var catcher = root.AddComponent<Image>();
            catcher.color = Color.clear;
            var view = root.AddComponent<PlanetGlobe>();

            var stageGo = Child(root, "Stage");
            stage = stageGo.GetComponent<RectTransform>();
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = Vector2.zero;

            var globeGo = Child(stageGo, "Globe");
            var globe = globeGo.GetComponent<RectTransform>();
            globe.anchorMin = globe.anchorMax = globe.pivot = new Vector2(0.5f, 0.5f);
            var d = U1.Px(GlobePx);
            globe.sizeDelta = new Vector2(d, d);

            // Порядок шарів: гало → гало «ожила» → дуга → куля → комірки (під маскою кулі) → мітки → іскри.
            var halo = Quad(globeGo, "Halo", quad, d * design.SphereHalo, d * design.SphereHalo);
            var aliveHalo = Quad(globeGo, "AliveHalo", quad, d * design.SphereAliveHalo, d * design.SphereAliveHalo);
            aliveHalo.gameObject.SetActive(false);
            var arc = Quad(globeGo, "Arc", quad, d * 2f * design.SphereArcOuter, d * 2f * design.SphereArcOuter);
            var disc = Quad(globeGo, "Disc", quad, d, d);

            // Шар комірок обрізаний кругом кулі (SPEC): біля краю квадрат на кулі виступає за силует.
            var clipGo = Child(globeGo, "CellsClip");
            var clip = clipGo.AddComponent<Image>();
            clip.sprite = U1.S("toggle_knob_on"); // круг набору U1Glaze: форма маски, сам не малюється
            clip.raycastTarget = false;
            Place(clip, Vector2.zero, new Vector2(d, d), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            clipGo.AddComponent<Mask>().showMaskGraphic = false;
            var cells = new SlotCell[SlotPool];
            for (var i = 0; i < SlotPool; i++)
                cells[i] = BuildCell(clipGo, design, $"Cell{i}");

            var pipsRoot = Child(globeGo, "Pips");
            var pipsRect = pipsRoot.GetComponent<RectTransform>();
            pipsRect.anchorMin = pipsRect.anchorMax = new Vector2(0.5f, 0.5f);
            pipsRect.sizeDelta = Vector2.zero;
            var pips = new Image[SlotPool];
            var shines = new Image[SlotPool];
            for (var i = 0; i < SlotPool; i++)
            {
                var (pip, shine) = U1.Pip(pipsRoot, $"Pip{i}", filled: false, Color.white, GlobePx * design.SpherePipSize);
                var rect = pip.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                pip.gameObject.SetActive(false);
                pips[i] = pip;
                shines[i] = shine;
            }

            // Іскри «планета ожила» (S04_05): шість навколо кулі, px макета від центру (Y угору) і розмір.
            var sparkleSpecs = new (float x, float y, float side)[]
            {
                (-156f, 137f, 18f), (97f, 187f, 11f), (156f, 127f, 15f), (-166f, -137f, 13f), (169f, -127f, 21f), (-88f, -190f, 11.5f),
            };
            var sparkles = new RectTransform[sparkleSpecs.Length];
            for (var i = 0; i < sparkleSpecs.Length; i++)
            {
                var (x, y, side) = sparkleSpecs[i];
                var go = Child(globeGo, $"Sparkle{i}");
                var image = go.AddComponent<Image>();
                image.sprite = U1.S("sparkle");
                image.color = Color.white;
                image.raycastTarget = false;
                Place(image, new Vector2(U1.Px(x), U1.Px(y)), new Vector2(U1.Px(side), U1.Px(side)),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                go.SetActive(false);
                sparkles[i] = go.GetComponent<RectTransform>();
            }

            Wire(view, ("design", design), ("planetShader", shader), ("area", root.GetComponent<RectTransform>()),
                ("globe", globe), ("disc", disc), ("halo", halo), ("aliveHalo", aliveHalo), ("arc", arc),
                ("pipEmpty", U1.S("pip_empty")), ("pipFilled", U1.S("pip_filled")));
            WireArray(view, "cells", cells);
            WireArray(view, "pips", pips);
            WireArray(view, "pipShines", shines);
            WireArray(view, "sparkles", sparkles);
            return view;
        }

        // ── «Горизонт» (S05): велика планета внизу ділянки, комірки рядами (місця ставить PlanetHorizon.Layout) ──
        private static PlanetHorizon BuildHorizon(GameObject area, DesignSystem design, Sprite quad, Shader shader, GameObject screen)
        {
            var root = Child(area, "Horizon");
            Stretch(root);
            var catcher = root.AddComponent<Image>();
            catcher.color = Color.clear;
            var view = root.AddComponent<PlanetHorizon>();
            var disc = Quad(root, "Disc", quad, U1.Px(design.HorizonRadius * 2f), U1.Px(design.HorizonRadius * 2f));
            var cellsRoot = Child(root, "Cells");
            Stretch(cellsRoot);
            var cells = new SlotCell[SlotPool];
            for (var i = 0; i < SlotPool; i++)
                cells[i] = BuildCell(cellsRoot, design, $"Cell{i}");

            // Іскри «планета ожила» (S05_04) у небі; розмір — px макета, місце ставить PlanetHorizon.Layout.
            var sparkleSides = new[] { 10f, 18f, 12f, 12f, 20f, 9f };
            var sparkles = new RectTransform[sparkleSides.Length];
            for (var i = 0; i < sparkleSides.Length; i++)
            {
                var go = Child(root, $"Sparkle{i}");
                var image = go.AddComponent<Image>();
                image.sprite = U1.S("sparkle");
                image.color = Color.white;
                image.raycastTarget = false;
                Place(image, Vector2.zero, new Vector2(U1.Px(sparkleSides[i]), U1.Px(sparkleSides[i])),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                go.SetActive(false);
                sparkles[i] = go.GetComponent<RectTransform>();
            }

            Wire(view, ("design", design), ("planetShader", shader), ("area", root.GetComponent<RectTransform>()),
                ("widthSource", screen.GetComponent<RectTransform>()),
                ("disc", disc), ("cellsRoot", cellsRoot.GetComponent<RectTransform>()));
            WireArray(view, "cells", cells);
            WireArray(view, "sparkles", sparkles);
            root.SetActive(false);
            return view;
        }

        /// <summary>
        /// Комірка (SPEC): зовнішній RectTransform (поворот t1, масштаб) → внутрішній (поворот t2) → лунка
        /// <c>slot_empty</c> / основа <c>slot_frame_base</c> + картинка з атласу (64 з 72) + рамка
        /// <c>slot_frame_stroke</c> кольору рідкості, кільце вибраної <c>slot_selected</c> (124 / 72 бокса).
        /// </summary>
        private static SlotCell BuildCell(GameObject parent, DesignSystem design, string name)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var box = U1.Px(CellPx);
            rect.sizeDelta = new Vector2(box, box);
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var innerGo = Child(go, "Inner");
            Stretch(innerGo);

            var selected = Sprite(innerGo, "Selected", "slot_selected");
            Place(selected, Vector2.zero, new Vector2(U1.Px(124f), U1.Px(124f)), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            selected.gameObject.SetActive(false);
            var empty = Sprite(innerGo, "Empty", "slot_empty");
            Stretch(empty.gameObject);
            var frameBase = Sprite(innerGo, "Base", "slot_frame_base");
            Stretch(frameBase.gameObject);
            var pixelsGo = Child(innerGo, "Pixels");
            Stretch(pixelsGo, U1.Px((CellPx - 64f) * 0.5f));
            var pixels = pixelsGo.AddComponent<RawImage>();
            pixels.raycastTarget = false;
            var stroke = Sprite(innerGo, "Stroke", "slot_frame_stroke");
            Stretch(stroke.gameObject);
            frameBase.gameObject.SetActive(false);
            pixelsGo.SetActive(false);
            stroke.gameObject.SetActive(false);

            var cell = go.AddComponent<SlotCell>();
            Wire(cell, ("inner", innerGo.GetComponent<RectTransform>()), ("group", group), ("empty", empty),
                ("frameBase", frameBase), ("pixels", pixels), ("stroke", stroke), ("selected", selected),
                ("emptyOwn", U1.S("slot_empty")), ("emptyReadOnly", U1.S("slot_empty_readonly")));
            go.SetActive(false);
            return cell;
        }

        // ── Низ чужої планети (S09_02/03): panel_card — «Планета гравця Вега · 6 / 8» h3, «Тут можна лише дивитися»
        // caption, скляна «До галактики гравця» ──
        private static RectTransform BuildVisitorFooter(GameObject parent, DesignSystem design, out TMP_Text title, out CandyButton button)
        {
            var panel = U1.Panel(parent, "VisitorFooter", "panel_card");
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(16f);
            layout.padding = new RectOffset(pad, pad, (int)U1.Px(14f), pad);
            layout.spacing = U1.Px(6f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            U1.Size(panel, 0f, 0f).flexibleHeight = 0f;

            title = U1.Text(panel, "Title", "Планета гравця · 0 / 8", design, U1.Role.H3, design.InkText);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            var note = U1.Text(panel, "Note", "Тут можна лише дивитися", design, U1.Role.Caption, design.InkMuted);
            note.textWrappingMode = TextWrappingModes.NoWrap;
            U1.Gap(panel, 4f);
            button = U1.Button(panel, "Back", "До галактики гравця", design, CandyButton.Tone.Glass);
            U1.Size(button.gameObject, 0f, 56f);
            panel.SetActive(false);
            return panel.GetComponent<RectTransform>();
        }

        // ── «Планета ожила» (S04_05, S05_04): panel_card з бірюзовою обводкою, кікер, «… ожила!», текст, «До галактики» ──
        private static RectTransform BuildAlivePanel(GameObject parent, DesignSystem design, out TMP_Text kicker, out TMP_Text title,
            out TMP_Text body, out CandyButton button)
        {
            var panel = U1.Panel(parent, "AlivePanel");
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            var pad = (int)U1.Px(17f);
            layout.padding = new RectOffset(pad, pad, (int)U1.Px(12f), (int)U1.Px(16f));
            layout.spacing = U1.Px(3f);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            // Висота — за вмістом: панель замінює низ стовпчика, ділянка планети забирає решту (куля піднімається).
            panel.AddComponent<LayoutElement>().flexibleHeight = 0f;

            // Бірюзова обводка (S04_05: тонка лінія по краю й слабке гало зовні) — кільце field_focus_ring, як його задумано
            // для поля («на 4 більше з кожного боку, бірюза 18 %»), поза розкладкою. Однопіксельного контуру з кутом
            // panel_card у наборі немає — лишається гало.
            var ringGo = Child(panel, "Ring");
            ringGo.transform.SetAsFirstSibling();
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = U1.S("field_focus_ring");
            ring.type = Image.Type.Sliced;
            ring.color = DesignSystem.WithAlpha(design.Teal, design.PlanetAliveRingAlpha);
            ring.raycastTarget = false;
            Stretch(ringGo, -U1.Px(3f));
            ringGo.AddComponent<LayoutElement>().ignoreLayout = true;

            kicker = U1.Text(panel, "Kicker", "УСІ КОМІРКИ ЗАПОВНЕНІ", design, U1.Role.Label, design.Teal300);
            title = U1.Text(panel, "Title", "Кріос ожила!", design, U1.Role.H1, design.InkText);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            body = U1.Text(panel, "Body", "Відкрито наступну планету: Сатурнія.", design, U1.Role.Body, design.InkMuted);
            body.textWrappingMode = TextWrappingModes.Normal;
            U1.Gap(panel, 8f); // 3 + 8 + 3 = 14 між текстом і кнопкою, як на S04_05
            button = U1.Button(panel, "ToGalaxy", "До галактики", design, CandyButton.Tone.Coral);
            U1.Size(button.gameObject, 0f, 56f);
            panel.SetActive(false);
            return panel.GetComponent<RectTransform>();
        }

        // ── Лист зайнятої комірки (S04_04, S05_03): ручка, картинка в рамці з мітками, назва, бейдж, дії ──
        private static RectTransform BuildSheet(GameObject parent, DesignSystem design, Sprite quad, out RarityFrameView frame,
            out PictureView picture, out TMP_Text name, out RarityBadgeView badge, out CandyButton replace, out CandyButton remove,
            out Button cancel)
        {
            var root = U1.Sheet(parent, "SlotSheet", design, out var content);
            // Лист — до самого низу екрана, а не safe area (S04_04: до 874): виступ, як у затемнення діалогів.
            var outset = U1.Px(U1.ScrimOutsetPx);
            content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -outset);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)U1.Px(20f), (int)U1.Px(20f), (int)U1.Px(34f), (int)(U1.Px(14f) + outset));

            // Ручка 44 × 5 (S04_04) — поза розкладкою, на 13 від верху.
            var handleGo = Child(content, "Handle");
            var handle = handleGo.AddComponent<Image>();
            handle.sprite = quad;
            handle.color = DesignSystem.WithAlpha(design.InkMuted, 0.4f);
            handle.raycastTarget = false;
            Place(handle, new Vector2(0f, -U1.Px(13f)), new Vector2(U1.Px(44f), U1.Px(5f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            handleGo.AddComponent<LayoutElement>().ignoreLayout = true;

            var row = Child(content, "Picture");
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = U1.Px(14f);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            frame = U1.FramedPicture(row, "Frame", design, big: true, sidePx: 84f, pipsRoomPx: 10f, out picture);
            var texts = Child(row, "Texts");
            var textCol = texts.AddComponent<VerticalLayoutGroup>();
            textCol.spacing = U1.Px(8f);
            textCol.childAlignment = TextAnchor.MiddleLeft;
            textCol.childControlWidth = textCol.childControlHeight = true;
            textCol.childForceExpandWidth = textCol.childForceExpandHeight = false;
            var textSize = texts.AddComponent<LayoutElement>();
            textSize.flexibleWidth = 1f;
            textSize.minWidth = 0f;
            name = U1.Text(texts, "Name", "Квітка", design, U1.Role.H2, design.InkText, TextAlignmentOptions.Left);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            badge = U1.RarityBadge(texts, "Rarity", design, Core.Rarity.Common);

            replace = U1.Button(content, "Replace", "Замінити", design, CandyButton.Tone.Teal);
            U1.Size(replace.gameObject, 0f, 56f);
            remove = U1.Button(content, "Remove", "Повернути в колекцію", design, CandyButton.Tone.Glass);
            U1.Size(remove.gameObject, 0f, 56f);
            cancel = U1.TextButton(content, "Cancel", "Скасувати", design, design.InkMuted);
            U1.Size(cancel.gameObject, 0f, 44f);

            root.SetActive(false);
            return root.GetComponent<RectTransform>();
        }

        private static Image Sprite(GameObject parent, string name, string sprite)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = U1.S(sprite);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Квад під шейдер. Спрайт потрібен саме білий: без нього Image не має UV для розгортки шейдера.</summary>
        private static Image Quad(GameObject parent, string name, Sprite quad, float width, float height)
        {
            var go = Child(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = quad;
            image.raycastTarget = false;
            Place(image, Vector2.zero, new Vector2(width, height), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            return image;
        }
    }
}
