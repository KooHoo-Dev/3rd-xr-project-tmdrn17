using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudyXR.Common;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArcherSimulator.EditorTools
{
    /// <summary>
    /// 「아처 시뮬레이터 VR 상세 컨셉 기획서」 기준으로 Archer Simulator 씬의 기본 레이아웃을 만든다.
    /// 씬 구성·환경·UI 스타일은 00. Study/04. Throw Physics/00. Throw Range 씬의 규칙을 따른다.
    ///   XR / Lights / Stage(환경) / Systems  +  Lobby / Range / Game Stages / UI
    /// 메뉴: Archer Simulator > 씬 기본 세팅 (다시 실행하면 빌더가 만든 것만 지우고 새로 만든다)
    /// </summary>
    public static class ArcherSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Archer Simulator.unity";
        private const string MaterialFolder = "Assets/20. Archer Simulator/Materials";
        private const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        private const string InteractionLayerSettingsPath = "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset";
        private const string Synty = "Assets/Synty/PolygonFantasyKingdom/Prefabs/";
        private const string SyntyGeneric = "Assets/Synty/PolygonGeneric/Prefabs/Environment/";
        private const string SkyboxPath = "Assets/Synty/PolygonGeneric/Materials/Skybox_01.mat";
        private const string GrassPath = "Assets/Synty/PolygonFantasyKingdom/Materials/Ground/Grass_01.mat";
        private const string UserBowPath = "Assets/Prefab/Bow/Bow1_1_1.prefab"; // 사용자 활 프리팹 (Lv1)
        private const string BowModelPath ="Assets/Blink/Art/Weapons/Stylized/Bows/_PrefabsBows/Bow1_1_1.prefab";
        private const string BowControllerPath = "Assets/Blink/Art/Weapons/Stylized/Animator/Bow1_1.controller";
        private const string BowFbxPath = "Assets/Blink/Art/Weapons/Stylized/Bows/Meshes_Bows/Bow1_1.fbx";
        private const string ArrowModelPath = "Assets/Blink/Art/Weapons/Stylized/Bows/_PrefabsBows/ArrowBow1_1.prefab";
        private const string PrefabFolder = "Assets/20. Archer Simulator/Prefabs";
        private const float ArrowLength = 0.8f;
        private const string PushButtonPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/DemoAssets/Prefabs/Interactables/Push Button.prefab";

        // 빌더가 만드는 루트. (이전 버전 이름 포함) — 다시 실행하면 지운다.
        private static readonly string[] BuilderRoots =
        {
            "Lights", "Stage", "Systems", "Lobby", "Range", "Game Stages", "UI",
            "[Systems]", "[Lobby]", "[Range]", "[Stages]", "[UI]",
            "Directional Light", "Floor",
        };
        private static readonly string[] BuilderChildren = { "Screen Fade", "Wrist HUD" };

        // Interaction Layer 인덱스 (InteractionLayerSettings)
        private const int DefaultLayer = 0;
        private const int BowLayer = 3;
        private const int ArrowLayer = 4;
        private const int TeleportLayer = 31;
        private const int UILayer = 5;

        // Throw Range 스타일
        private const float BoardScale = 0.0013f;
        private const float GroundTile = 10.4f;
        private static readonly Color PanelColor = new Color(0.085f, 0.115f, 0.1f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.45f, 0.85f);
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.45f);
        private static readonly Color SubTextColor = new Color(0.75f, 0.8f, 0.78f);

        // 기획서 3. 난이도 설정 (임시) — 표적 슬롯 기본 위치는 쉬움(10m)
        private static readonly (string label, float distance, Color color)[] Difficulties =
        {
            ("쉬움 10m", 10f, new Color(0.35f, 0.8f, 0.35f)),
            ("보통 20m", 20f, new Color(0.95f, 0.8f, 0.25f)),
            ("어려움 35m", 35f, new Color(0.9f, 0.3f, 0.25f)),
        };

        private static TMP_FontAsset font;
        private static Camera mainCamera;

        // 빌드 중 모아 두었다가 마지막에 GameManager 등에 연결하는 참조
        private static class Refs
        {
            public static ArrowPool ArrowPool;
            public static BowController Bow;
            public static XRBaseInteractable StartButton;
            public static TMP_Text BoardArrows, BoardScore, Selection;
            public static TMP_Text ResultTitle, ResultDetail, ResultTotal, ResultStats, ResultRecord;
            public static GameObject ResultBoard;
            public static TMP_Text Countdown;
            public static Button Retry, Lobby;
            public static readonly List<Button> StageButtons = new List<Button>();
            public static readonly List<Button> DifficultyButtons = new List<Button>();
            public static readonly List<(GameObject root, Transform slots)> Stages = new List<(GameObject, Transform)>();

            public static void Clear()
            {
                ArrowPool = null; Bow = null; StartButton = null;
                BoardArrows = BoardScore = Selection = null;
                ResultTitle = ResultDetail = ResultTotal = ResultStats = ResultRecord = null;
                ResultBoard = null; Countdown = null; Retry = Lobby = null;
                StageButtons.Clear(); DifficultyButtons.Clear(); Stages.Clear();
            }
        }

        [MenuItem("Archer Simulator/씬 기본 세팅")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (EditorUtility.DisplayDialog("Archer Simulator", $"현재 씬이 '{ScenePath}'가 아닙니다.\n해당 씬을 열고 진행할까요?", "열기", "취소") == false) return;
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            if (EditorUtility.DisplayDialog("Archer Simulator · 씬 기본 세팅",
                    "기획서 + Throw Range 씬 규칙으로 레이아웃을 생성합니다.\n\n" +
                    "• 루트 Lights/Stage/Systems/Lobby/Range/Game Stages/UI와 기본 'Directional Light'·'Floor'는 지우고 다시 만듭니다.\n" +
                    "• XR Origin은 'XR' 루트 아래로 옮기고, 왼손 연속 이동·Jump를 끕니다.\n" +
                    "• 하늘·안개(Render Settings)를 Throw Range와 같게 맞춥니다.\n" +
                    "• Interaction Layer 3·4에 'Bow'·'Arrow' 이름을 등록합니다.\n" +
                    "• 이 씬을 Build Settings에 추가하고 저장합니다.",
                    "생성", "취소") == false) return;

            Run(scene);
        }

        /// <summary>확인 창 없이 바로 생성한다. 현재 열린 씬이 Archer Simulator 씬이어야 한다 (자동화용).</summary>
        public static void RunWithoutPrompt()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError($"[ArcherSceneBuilder] 현재 씬이 '{ScenePath}'가 아니라 실행하지 않았습니다.");
                return;
            }
            Run(scene);
        }

        private static void Run(Scene scene)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Archer Simulator 씬 기본 세팅");

            Refs.Clear();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[ArcherSceneBuilder] 한글 폰트를 찾지 못했습니다: {FontPath}");
            EnsureInteractionLayerNames();
            ClearPrevious(scene);

            GameObject rig = FindRig(scene);
            mainCamera = rig != null ? rig.GetComponentInChildren<Camera>(true) : Object.FindFirstObjectByType<Camera>();

            BuildXR(scene, rig);
            ApplyRenderSettings();

            var created = new List<GameObject> { BuildLights(), BuildStage(), BuildSystems() };
            Physics.SyncTransforms(); // 간판 기둥이 바닥을 찾을 수 있도록
            created.Add(BuildLobby());
            created.Add(BuildRange());
            created.Add(BuildGameStages());
            created.Add(BuildUI());
            WireGame(created[2].transform); // Systems
            foreach (GameObject root in created) Undo.RegisterCreatedObjectUndo(root, "Create " + root.name);

            AddSceneToBuildSettings();
            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArcherSceneBuilder] Archer Simulator 씬 기본 세팅 완료");
        }

        // ───────────────────────── 준비 ─────────────────────────

        private static void EnsureInteractionLayerNames()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(InteractionLayerSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[ArcherSceneBuilder] InteractionLayerSettings를 찾지 못했습니다. Bow/Arrow 레이어 이름은 수동으로 지정하세요.");
                return;
            }

            var so = new SerializedObject(settings);
            SerializedProperty names = so.FindProperty("m_LayerNames");
            SetLayerName(names, BowLayer, "Bow");
            SetLayerName(names, ArrowLayer, "Arrow");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void SetLayerName(SerializedProperty names, int index, string name)
        {
            if (names == null || index >= names.arraySize) return;
            SerializedProperty element = names.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(element.stringValue)) element.stringValue = name;
            else if (element.stringValue != name)
                Debug.LogWarning($"[ArcherSceneBuilder] Interaction Layer {index}는 이미 '{element.stringValue}'로 사용 중입니다. '{name}'로 바꾸지 않았습니다.");
        }

        private static void ClearPrevious(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (BuilderRoots.Contains(root.name)) Undo.DestroyObjectImmediate(root);
            }

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && BuilderChildren.Contains(t.name)) Undo.DestroyObjectImmediate(t.gameObject);
            }

            GameObject xr = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "XR");
            if (xr == null) return;
            foreach (Transform child in xr.transform.Cast<Transform>().ToArray())
            {
                if (child.name == "XR Interaction Manager" || child.name == "Event System") Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static GameObject FindRig(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.Contains("XR Origin")) return root;
                if (root.name != "XR") continue;
                foreach (Transform child in root.transform)
                    if (child.name.Contains("XR Origin")) return child.gameObject;
            }
            return null;
        }

        // ───────────────────────── XR ─────────────────────────

        private static void BuildXR(Scene scene, GameObject rig)
        {
            GameObject xr = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "XR");
            if (xr == null)
            {
                xr = new GameObject("XR");
                Undo.RegisterCreatedObjectUndo(xr, "Create XR");
            }
            xr.transform.SetAsFirstSibling();

            var manager = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
            Undo.RegisterCreatedObjectUndo(manager, "Create XR Interaction Manager");
            manager.transform.SetParent(xr.transform, false);
            manager.transform.SetSiblingIndex(0);

            if (rig != null)
            {
                Undo.SetTransformParent(rig.transform, xr.transform, "Move XR Origin");
                rig.transform.SetSiblingIndex(1);
                Undo.RecordObject(rig.transform, "Reset XR Origin");
                rig.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                ConfigureRig(rig.transform);
                BuildScreenFade();
            }
            else
            {
                Debug.LogWarning("[ArcherSceneBuilder] XR Origin을 찾지 못했습니다. Screen Fade를 만들지 않았습니다.");
            }

            // 레이 UI(메뉴·난이도·상점)용
            var eventSystem = new GameObject("Event System", typeof(EventSystem), typeof(XRUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create Event System");
            eventSystem.transform.SetParent(xr.transform, false);
        }

        /// <summary>Throw Range와 같은 Rig 오버라이드: 왼손 연속 이동 끄기(텔레포트만), Jump 끄기 — 멀미 방지.</summary>
        private static void ConfigureRig(Transform rig)
        {
            Transform left = FindDeep(rig, "Left Controller");
            if (left != null)
            {
                foreach (MonoBehaviour mb in left.GetComponents<MonoBehaviour>())
                {
                    if (mb == null || mb.GetType().Name != "ControllerInputActionManager") continue;
                    var so = new SerializedObject(mb);
                    SerializedProperty smooth = so.FindProperty("m_SmoothMotionEnabled");
                    if (smooth == null) continue;
                    smooth.boolValue = false;
                    so.ApplyModifiedProperties();
                }
            }

            Transform jump = FindDeep(rig, "Jump");
            if (jump != null)
            {
                Undo.RecordObject(jump.gameObject, "Disable Jump");
                jump.gameObject.SetActive(false);
            }
        }

        /// <summary>카메라 앞 페이드 쿼드 (StudyXR.Common.ScreenFade) — 장면 전환 페이드 인/아웃.</summary>
        private static void BuildScreenFade()
        {
            if (mainCamera == null) return;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Screen Fade";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(mainCamera.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.12f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = FadeMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var fade = quad.AddComponent<ScreenFade>();
            var so = new SerializedObject(fade);
            so.FindProperty("fadeRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(quad, "Create Screen Fade");
        }

        // ───────────────────────── Lights / Render Settings ─────────────────────────

        private static GameObject BuildLights()
        {
            var root = new GameObject("Lights");
            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            sun.transform.localPosition = new Vector3(0f, 5f, 0f);
            sun.transform.localRotation = new Quaternion(0.28257102f, -0.3261904f, 0.102847435f, 0.89620084f);

            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowNormalBias = 0.4f;
            RenderSettings.sun = light;
            return root;
        }

        private static void ApplyRenderSettings()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.77f, 0.87f, 0.91f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 170f;
        }

        // ───────────────────────── Stage (환경) ─────────────────────────

        private static GameObject BuildStage()
        {
            var root = new GameObject("Stage");
            Transform r = root.transform;

            // Synty 바닥 타일 (10.4m 격자, y -0.25) — x -20.8~20.8, z -10.4~52
            Transform ground = Child(r, "Ground");
            ground.gameObject.AddComponent<ArrowGround>(); // 화살이 닿으면 바로 꺼지는 땅
            for (int ix = -2; ix <= 2; ix++)
            for (int iz = -1; iz <= 5; iz++)
                Prop("Environments/SM_Env_Ground_Flat_01.prefab", ground, new Vector3(ix * GroundTile, -0.25f, iz * GroundTile), 0f);

            var grass = AssetDatabase.LoadAssetAtPath<Material>(GrassPath);
            Prim(PrimitiveType.Plane, "Ground Underlay", ground, new Vector3(0f, -0.18f, 20.8f), new Vector3(5.2f, 1f, 7.3f),
                grass != null ? grass : Mat("M_Range_Ground", new Color(0.36f, 0.5f, 0.28f)), keepCollider: false);

            // 로비 바닥 콜라이더: 텔레포트 가능 (6×6m)
            GameObject floor = Prim(PrimitiveType.Plane, "Floor Collider", r, Vector3.zero, new Vector3(0.6f, 1f, 0.6f), null);
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
            floor.AddComponent<TeleportationArea>().interactionLayers = 1 << TeleportLayer;
            floor.AddComponent<ArrowGround>();

            // 사거리 바닥 콜라이더: 화살이 떨어지는 면. 텔레포트 불가 — 사격 중 시점 고정.
            GameObject range = Prim(PrimitiveType.Plane, "Range Collider", r, new Vector3(0f, -0.001f, 20.8f), new Vector3(5.2f, 1f, 7.3f), null);
            Object.DestroyImmediate(range.GetComponent<MeshRenderer>());
            range.AddComponent<ArrowGround>();

            // 배경
            Transform scenery = Child(r, "Scenery");
            Prop(SyntyGeneric + "SM_Gen_Env_Background_Mountain_01.prefab", scenery, new Vector3(0f, -2f, 140f), 0f);
            Prop(SyntyGeneric + "SM_Gen_Env_Background_Mountain_02.prefab", scenery, new Vector3(-110f, -2f, 100f), 0f);
            Prop(SyntyGeneric + "SM_Gen_Env_Background_Mountain_03.prefab", scenery, new Vector3(110f, -2f, 100f), 0f);
            Prop("Environments/SM_Env_Tree_Large_01.prefab", scenery, new Vector3(-6f, 0f, -6f), 0f);
            Prop("Environments/SM_Env_Tree_Large_02.prefab", scenery, new Vector3(7f, 0f, -7f), 120f);
            Prop("Environments/SM_Env_Tree_Large_01.prefab", scenery, new Vector3(-16f, 0f, 4f), 200f);
            Prop("Environments/SM_Env_Tree_Large_02.prefab", scenery, new Vector3(16f, 0f, 2f), 60f);

            // 사거리 양옆 울타리 (Throw Range Lane과 같은 방식)
            Transform fences = Child(scenery, "Range Fences");
            for (float z = 2.5f; z <= 45f; z += 2.5f)
            {
                Prop("Props/SM_Prop_Fence_01.prefab", fences, new Vector3(-9f, 0f, z), 0f);
                Prop("Props/SM_Prop_Fence_02.prefab", fences, new Vector3(9f, 0f, z), 0f);
            }

            return root;
        }

        // ───────────────────────── Systems ─────────────────────────

        private static GameObject BuildSystems()
        {
            var root = new GameObject("Systems");

            Child(root.transform, "Physics Settings").gameObject.AddComponent<SceneTimestep>();

            Transform perf = Child(root.transform, "Perf HUD");
            var hud = perf.gameObject.AddComponent<PerfHud>();
            TextMeshPro perfLabel = Label3D(perf, "Perf Label", "FPS", 15.6f, Vector3.zero, new Vector2(26f, 7.8f),
                new Color(0.7f, 1f, 0.7f), TextAlignmentOptions.Left);
            perfLabel.transform.localScale = Vector3.one * 0.01f;
            var so = new SerializedObject(hud);
            so.FindProperty("label").objectReferenceValue = perfLabel;
            if (mainCamera != null) so.FindProperty("head").objectReferenceValue = mainCamera.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 화살 묶음 (게임 시작 시 최대 화살 수만큼 생성)
            Arrow arrowPrefab = BuildArrowPrefab();
            Transform poolTf = Child(root.transform, "Arrow Pool");
            Refs.ArrowPool = poolTf.gameObject.AddComponent<ArrowPool>();
            var poolSo = new SerializedObject(Refs.ArrowPool);
            // 강화 단계(ArrowEffect)별 화살 프리팹 (Arrow, Arrow Lv2~Lv4)
            SerializedProperty arrowPrefabs = poolSo.FindProperty("arrowPrefab");
            arrowPrefabs.arraySize = ArrowEffects.Length;
            for (int i = 0; i < ArrowEffects.Length; i++)
                arrowPrefabs.GetArrayElementAtIndex(i).objectReferenceValue = LoadLevelArrow(i, arrowPrefab);
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            Child(root.transform, "Game Manager").gameObject.AddComponent<GameManager>(); // WireGame에서 연결

            return root;
        }

        // ───────────────────────── 게임 연결 ─────────────────────────

        private static void WireGame(Transform systems)
        {
            var manager = systems.GetComponentInChildren<GameManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("bow").objectReferenceValue = Refs.Bow;
            so.FindProperty("selectionText").objectReferenceValue = Refs.Selection;
            so.FindProperty("countdownText").objectReferenceValue = Refs.Countdown;
            so.FindProperty("arrowsText").objectReferenceValue = Refs.BoardArrows;
            so.FindProperty("scoreText").objectReferenceValue = Refs.BoardScore;
            so.FindProperty("resultBoard").objectReferenceValue = Refs.ResultBoard;
            so.FindProperty("resultTitleText").objectReferenceValue = Refs.ResultTitle;
            so.FindProperty("resultDetailText").objectReferenceValue = Refs.ResultDetail;
            so.FindProperty("resultTotalText").objectReferenceValue = Refs.ResultTotal;
            so.FindProperty("resultStatsText").objectReferenceValue = Refs.ResultStats;
            so.FindProperty("resultRecordText").objectReferenceValue = Refs.ResultRecord;

            SerializedProperty stages = so.FindProperty("stages");
            for (int i = 0; i < Refs.Stages.Count && i < stages.arraySize; i++)
            {
                SerializedProperty stage = stages.GetArrayElementAtIndex(i);
                stage.FindPropertyRelative("root").objectReferenceValue = Refs.Stages[i].root;
                stage.FindPropertyRelative("targetSlots").objectReferenceValue = Refs.Stages[i].slots;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 레이 UI 버튼 · Poke 시작 버튼 → GameManager
            for (int i = 0; i < Refs.StageButtons.Count; i++)
                UnityEventTools.AddIntPersistentListener(Refs.StageButtons[i].onClick, manager.SelectStage, i);
            for (int i = 0; i < Refs.DifficultyButtons.Count; i++)
                UnityEventTools.AddIntPersistentListener(Refs.DifficultyButtons[i].onClick, manager.SelectDifficulty, i);
            if (Refs.StartButton != null) UnityEventTools.AddVoidPersistentListener(Refs.StartButton.selectEntered, manager.StartGame);
            if (Refs.Retry != null) UnityEventTools.AddPersistentListener(Refs.Retry.onClick, manager.Retry);
            if (Refs.Lobby != null) UnityEventTools.AddPersistentListener(Refs.Lobby.onClick, manager.ReturnToLobby);

            // 활 ↔ 화살 풀
            if (Refs.Bow != null)
            {
                var bowSo = new SerializedObject(Refs.Bow);
                bowSo.FindProperty("arrowPool").objectReferenceValue = Refs.ArrowPool;
                bowSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 강화 작업대 → GameManager
            foreach (UpgradeStation station in Object.FindObjectsByType<UpgradeStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var stationSo = new SerializedObject(station);
                stationSo.FindProperty("gameManager").objectReferenceValue = manager;
                WireTitleBoardLabels(stationSo);
                stationSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>화살 프리팹: 루트 = 오늬(꼬리), +Z = 촉. Blink 화살 모델을 길이 0.8m로 맞춘다.</summary>
        private static Arrow BuildArrowPrefab()
        {
            EnsureFolder(PrefabFolder);
            var root = new GameObject("Arrow");

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.012f;
            col.height = ArrowLength;
            col.center = new Vector3(0f, 0f, ArrowLength * 0.5f);

            Transform visual = Child(root.transform, "Visual");
            GameObject model = Prop(ArrowModelPath, visual, Vector3.zero, 0f);
            if (model != null)
            {
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                Transform fit = FitModel(visual, model, Vector3.forward, ArrowLength);
                // 꼬리가 z=0에 오도록
                fit.position += new Vector3(0f, 0f, root.transform.position.z - WorldBounds(model).min.z);
            }
            else
            {
                GameObject shaft = Prim(PrimitiveType.Cylinder, "Shaft", visual, new Vector3(0f, 0f, ArrowLength * 0.5f),
                    new Vector3(0.015f, ArrowLength * 0.5f, 0.015f), Mat("M_Wood", new Color(0.5f, 0.33f, 0.18f)), keepCollider: false);
                shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            root.AddComponent<Arrow>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Arrow.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Arrow>();
        }

        // ───────────────────────── Lobby ─────────────────────────

        private static GameObject BuildLobby()
        {
            var root = new GameObject("Lobby");
            Transform r = root.transform;

            Child(r, "Arrival"); // 플레이어 기준 위치(사로 뒤 고정 사격 위치)
            Prim(PrimitiveType.Cube, "Firing Line", r, new Vector3(0f, 0.005f, 0.5f), new Vector3(3f, 0.01f, 0.05f),
                Mat("M_Firing_Line", Color.white), keepCollider: false);

            BuildTitleBoard(r, new Vector3(-1.25f, 1.75f, 1.45f));
            BuildStageSelectBoard(r, new Vector3(-1.85f, 1.4f, 0.2f));
            BuildStartAltar(r, new Vector3(0.75f, 0f, 0.55f));
            BuildWorkbench(r, new Vector3(1.5f, 0f, -0.3f));
            BuildArrowSupply(r, new Vector3(0.55f, 0f, -0.45f));

            Transform props = Child(r, "Props");
            Prop("Props/BattleGround/SM_Prop_Weapon_Rack_01.prefab", props, new Vector3(-1.2f, 0f, -2.2f), 0f);
            Prop("Props/SM_Prop_Barrel_Stack_01.prefab", props, new Vector3(-2.4f, 0f, -2.2f), 30f);
            Prop("Props/SM_Prop_Crate_Stack_01.prefab", props, new Vector3(2.4f, 0f, -2.3f), -20f);
            Prop("Props/Banners/SM_Prop_Flag_Pole_01.prefab", props, new Vector3(-2.6f, 0f, 1.8f), 0f);
            Prop("Props/Banners/SM_Prop_Flag_Pole_01.prefab", props, new Vector3(2.6f, 0f, 1.8f), 0f);

            return root;
        }

        private static void BuildTitleBoard(Transform parent, Vector3 position)
        {
            RectTransform c = WorldCanvas("Title Board", parent, position, new Vector2(760f, 460f));
            Backdrop(c);
            Label(c, "Title", "아처 시뮬레이터 VR", 52f, new Vector2(0f, 170f), new Vector2(700f, 80f), color: AccentColor);
            Label(c, "Points Text", "보유 포인트  0 P", 30f, new Vector2(0f, 85f), new Vector2(700f, 50f));
            Label(c, "Bow Level Text", "현재 활  Lv1 나무 활", 30f, new Vector2(0f, 35f), new Vector2(700f, 50f));
            BuildBoardStatus(c);
            Label(c, "Hint Text", "스테이지·난이도를 고른 뒤 제단의 버튼을 눌러 시작하세요", 22f, new Vector2(0f, -165f), new Vector2(700f, 60f),
                color: SubTextColor);
        }

        /// <summary>타이틀 게시판의 진행 상황 줄 (남은 화살 · 점수). 로비에서는 화살 수 / 난이도.</summary>
        private static void BuildBoardStatus(RectTransform board)
        {
            UIImage(board, "Status Divider", new Color(1f, 1f, 1f, 0.15f), new Vector2(660f, 3f), new Vector2(0f, -5f));
            Refs.BoardArrows = Label(board, "Arrows Text", "화살 10발", 34f, new Vector2(-170f, -55f), new Vector2(330f, 60f));
            Refs.BoardScore = Label(board, "Score Text", "쉬움", 34f, new Vector2(170f, -55f), new Vector2(330f, 60f), color: AccentColor);
        }

        /// <summary>
        /// 씬 전체를 다시 만들지 않고, 현재 씬의 손목 HUD를 지운 뒤 남은 화살·점수를 타이틀 게시판으로 옮긴다.
        /// (씬에서 직접 고친 내용은 그대로 둔다)
        /// </summary>
        [MenuItem("Archer Simulator/HUD를 타이틀 게시판으로 옮기기")]
        public static void MoveHudToTitleBoard()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var board = all.FirstOrDefault(t => t.name == "Title Board") as RectTransform;
            var manager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (board == null || manager == null)
            {
                EditorUtility.DisplayDialog("Archer Simulator", "현재 씬에서 'Title Board' 또는 GameManager를 찾지 못했습니다.\n먼저 '씬 기본 세팅'을 실행하세요.", "확인");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("HUD를 타이틀 게시판으로 옮기기");

            Refs.Clear();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            mainCamera = Camera.main;

            // 손목 HUD 제거
            foreach (Transform t in all)
                if (t != null && t.name == "Wrist HUD") Undo.DestroyObjectImmediate(t.gameObject);

            // 이전에 옮긴 적이 있으면 다시 만든다
            foreach (Transform child in board.Cast<Transform>().ToArray())
                if (child.name is "Backdrop" or "Status Divider" or "Arrows Text" or "Score Text")
                    Undo.DestroyObjectImmediate(child.gameObject);

            // 게시판을 키우고 기존 줄을 새 배치로 옮긴다
            Undo.RecordObject(board, "Resize Title Board");
            board.sizeDelta = new Vector2(760f, 460f);
            var layout = new Dictionary<string, Vector2>
            {
                { "Title", new Vector2(0f, 170f) },
                { "Points Text", new Vector2(0f, 85f) },
                { "Bow Level Text", new Vector2(0f, 35f) },
                { "Hint Text", new Vector2(0f, -165f) },
            };
            foreach (RectTransform child in board.Cast<Transform>().OfType<RectTransform>())
            {
                if (layout.TryGetValue(child.name, out Vector2 position) == false) continue;
                Undo.RecordObject(child, "Move Title Board Label");
                child.anchoredPosition = position;
            }

            Physics.SyncTransforms();
            Backdrop(board);
            BuildBoardStatus(board);
            foreach (string name in new[] { "Backdrop", "Status Divider", "Arrows Text", "Score Text" })
            {
                Transform created = board.Find(name);
                if (created != null) Undo.RegisterCreatedObjectUndo(created.gameObject, "Create " + name);
            }

            var so = new SerializedObject(manager);
            so.FindProperty("arrowsText").objectReferenceValue = Refs.BoardArrows;
            so.FindProperty("scoreText").objectReferenceValue = Refs.BoardScore;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            Scene scene = board.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ArcherSceneBuilder] 남은 화살·점수 표시를 타이틀 게시판으로 옮겼습니다.");
        }

        private static void BuildStageSelectBoard(Transform parent, Vector3 position)
        {
            RectTransform c = WorldCanvas("Stage Select Board", parent, position, new Vector2(760f, 600f));
            Backdrop(c);

            Label(c, "Stage Header", "스테이지", 30f, new Vector2(0f, 255f), new Vector2(700f, 50f), color: AccentColor);
            string[] stages =
            {
                "훈련장  ·  표적 3  ·  10발",
                "숲속 사격장  ·  표적 5  ·  15발",
                "성벽  ·  표적 6  ·  18발",
            };
            for (int i = 0; i < stages.Length; i++)
                Refs.StageButtons.Add(MakeButton(c, $"Stage {i + 1:00}", stages[i], new Vector2(0f, 185f - i * 78f), new Vector2(600f, 64f), 24f));

            Label(c, "Difficulty Header", "난이도", 30f, new Vector2(0f, -55f), new Vector2(700f, 50f), color: AccentColor);
            string[] labels = { "쉬움\n10m · HP 100 · ×1.0", "보통\n20m · HP 200 · ×1.5", "어려움\n35m · HP 350 · ×2.5" };
            string[] names = { "Easy", "Normal", "Hard" };
            for (int i = 0; i < labels.Length; i++)
                Refs.DifficultyButtons.Add(MakeButton(c, $"Difficulty {names[i]}", labels[i], new Vector2(-235f + i * 235f, -140f), new Vector2(220f, 90f), 20f));

            Refs.Selection = Label(c, "Selection Text", "선택: 훈련장 / 쉬움", 24f, new Vector2(0f, -245f), new Vector2(700f, 50f), color: SubTextColor);
        }

        private static void BuildStartAltar(Transform parent, Vector3 position)
        {
            Transform altar = Child(parent, "Start Altar");
            altar.localPosition = position;
            altar.localRotation = FacePlayer(position);

            const float height = 0.95f;
            Prim(PrimitiveType.Cube, "Pedestal", altar, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.35f, height, 0.35f),
                Mat("M_Stone", new Color(0.55f, 0.55f, 0.58f)));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PushButtonPath);
            if (prefab != null)
            {
                var button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, altar);
                button.name = "Start Button (Poke)";
                button.transform.localPosition = new Vector3(0f, height, 0f);
                button.transform.localRotation = Quaternion.identity;
                Refs.StartButton = button.GetComponentInChildren<XRBaseInteractable>(true);
            }
            else
            {
                Debug.LogWarning($"[ArcherSceneBuilder] Poke 버튼 프리팹을 찾지 못했습니다: {PushButtonPath}");
            }

            Label3D(altar, "Start Label", "게임 시작", 2.4f, new Vector3(0f, height + 0.35f, 0f), new Vector2(4f, 1f), AccentColor);
        }

        private static void BuildWorkbench(Transform parent, Vector3 position)
        {
            Transform bench = Child(parent, "Upgrade Workbench");
            bench.localPosition = position;

            Prop("Props/Furniture/SM_Prop_Workbench_01_Preset.prefab", bench, new Vector3(0.9f, 0f, 0f), -90f);
            Prop("Props/SM_Prop_Anvil_02.prefab", bench, new Vector3(0.7f, 0f, 1.3f), -60f);
            Prim(PrimitiveType.Cube, "Bow Stand", bench, new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 1f, 0.4f),
                Mat("M_Wood", new Color(0.5f, 0.33f, 0.18f)));

            // 거치대 소켓: Bow 레이어만 받는다. 활을 올려 두면 강화 UI가 열린다.
            Transform socketTf = Child(bench, "Bow Socket");
            socketTf.localPosition = new Vector3(0f, 1.6f, 0f);
            var trigger = socketTf.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.25f;
            var socket = socketTf.gameObject.AddComponent<XRSocketInteractor>();
            socket.interactionLayers = 1 << BowLayer;

            RectTransform upgrade = WorldCanvas("Upgrade Board", bench, Vector3.zero, new Vector2(620f, 420f), face: false);
            upgrade.position = new Vector3(1.75f, 1.55f, 0.75f);
            upgrade.rotation = FacePlayer(upgrade.position);
            Backdrop(upgrade);
            Label(upgrade, "Header", "활 강화", 36f, new Vector2(0f, 160f), new Vector2(580f, 56f), color: AccentColor);
            TextMeshProUGUI current = Label(upgrade, "Current Bow Text", "현재  Lv1 나무 활\n사거리 20m · 데미지 40", 24f, new Vector2(0f, 80f), new Vector2(580f, 80f));
            TextMeshProUGUI next = Label(upgrade, "Next Bow Text", "다음  Lv2 강화 나무 활\n사거리 30m · 데미지 60 · 흰색 트레일", 24f, new Vector2(0f, -10f), new Vector2(580f, 80f),
                color: new Color(0.6f, 0.9f, 1f));
            TextMeshProUGUI cost = Label(upgrade, "Cost Text", "비용 500 P", 24f, new Vector2(0f, -85f), new Vector2(580f, 44f));
            Button upgradeButton = MakeButton(upgrade, "Upgrade", "강화", new Vector2(0f, -155f), new Vector2(260f, 64f), 28f);

            TextMeshPro hint = Label3D(bench, "Upgrade Hint", "활을 거치대에 올려두면\n강화 메뉴가 열립니다", 1.6f,
                Vector3.zero, new Vector2(6f, 1.4f), Color.white);
            hint.transform.position = bench.TransformPoint(new Vector3(0f, 1.2f, -0.25f));
            hint.transform.rotation = FacePlayer(hint.transform.position);
            upgrade.gameObject.SetActive(false);

            // 활은 처음에 거치대에 놓여 있다.
            BowController bow = BuildBow(parent, socketTf.position);
            socket.startingSelectedInteractable = bow.GetComponent<XRGrabInteractable>();

            // 강화 작업대: 거치대에 활 + 로비 상태일 때만 강화 메뉴가 열린다 (GameManager는 WireGame에서 연결)
            var station = bench.gameObject.AddComponent<UpgradeStation>();
            var so = new SerializedObject(station);
            so.FindProperty("bowSocket").objectReferenceValue = socket;
            so.FindProperty("bow").objectReferenceValue = bow;
            so.FindProperty("upgradePanel").objectReferenceValue = upgrade.gameObject;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("currentText").objectReferenceValue = current;
            so.FindProperty("nextText").objectReferenceValue = next;
            so.FindProperty("costText").objectReferenceValue = cost;
            so.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static BowController BuildBow(Transform parent, Vector3 worldPosition)
        {
            Transform bow = Child(parent, "Bow");
            bow.SetPositionAndRotation(worldPosition, Quaternion.identity);

            var body = bow.gameObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.angularDamping = 2.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 손잡이(먼저 잡은 손)
            var grip = Child(bow, "Grip Collider").gameObject.AddComponent<BoxCollider>();
            grip.size = new Vector3(0.06f, 0.18f, 0.06f);
            Transform gripPoint = Child(bow, "Grip Point");

            // 외형: 강화 단계별 활 (Lv1~Lv3). Lv1만 켜 두고 UpgradeStation이 단계에 맞춰 바꾼다.
            Transform visual = Child(bow, "Visual");
            List<LevelVisualData> levels = BuildBowVisuals(bow, visual);

            // 시위: 활을 잡지 않은 다른 손만 잡을 수 있는 BowString (위치 = 쉬는 시위 = 화살 오늬)
            Transform stringTf = Child(bow, "Bow String");
            stringTf.position = levels.Count > 0 && levels[0].stringRest != null
                ? levels[0].stringRest.position
                : bow.TransformPoint(new Vector3(0f, 0f, -0.15f));
            var stringCollider = stringTf.gameObject.AddComponent<SphereCollider>();
            stringCollider.radius = 0.06f;
            var bowString = stringTf.gameObject.AddComponent<BowString>();
            bowString.colliders.Clear();
            bowString.colliders.Add(stringCollider);

            // 활 몸체 잡기 — Throw Range 공 설정과 동일 (Instantaneous, Grip Point, 예측 비주얼)
            var bowGrab = bow.gameObject.AddComponent<XRGrabInteractable>();
            bowGrab.colliders.Clear();
            bowGrab.colliders.Add(grip);
            bowGrab.attachTransform = gripPoint;
            bowGrab.useDynamicAttach = false;
            bowGrab.throwOnDetach = false;
            bowGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            bowGrab.predictedVisualsTransform = visual;
            bowGrab.interactionLayers = (1 << DefaultLayer) | (1 << BowLayer);

            // 떨어뜨리거나 멀리 던지면 처음 자리(거치대)로 복귀
            var respawn = bow.gameObject.AddComponent<Respawnable>();
            var so = new SerializedObject(respawn);
            so.FindProperty("killHeight").floatValue = -2f;
            so.FindProperty("maxDistance").floatValue = 25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var stringSo = new SerializedObject(bowString);
            stringSo.FindProperty("bow").objectReferenceValue = bowGrab;
            stringSo.ApplyModifiedPropertiesWithoutUndo();

            // 활 컨트롤러: 시위 잡기 → 장전, 당김 → 애니메이션, 놓기 → 발사 (화살 풀은 WireGame에서 연결)
            var controllerComp = bow.gameObject.AddComponent<BowController>();
            var controllerSo = new SerializedObject(controllerComp);
            controllerSo.FindProperty("bowString").objectReferenceValue = bowString;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            AssignLevelVisuals(controllerComp, levels);

            Refs.Bow = controllerComp;
            return controllerComp;
        }

        private struct LevelVisualData
        {
            public GameObject root;
            public Animator animator;
            public Transform stringRest;
        }

        /// <summary>
        /// Visual 아래에 Lv1~Lv3 활 모델을 만든다. 각 모델은 1.2m로 맞추고, 시위 본이 뒤(-Z)로 가게 돌리고,
        /// 쉬는 시위 위치에 'String Rest'를 둔다. 프리팹에 Animator가 없으면 해당 컨트롤러를 붙인다.
        /// </summary>
        private static List<LevelVisualData> BuildBowVisuals(Transform bow, Transform visual)
        {
            var result = new List<LevelVisualData>();
            var sources = BowPrefabTools.Bows.ToList();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sources[0].prefab) == null)
                sources[0] = (BowModelPath, BowControllerPath); // 사용자 프리팹이 없으면 Blink 원본

            for (int i = 0; i < sources.Count; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(sources[i].prefab) == null) continue;

                Transform level = Child(visual, $"Lv{i + 1}");
                GameObject model = Prop(sources[i].prefab, level, Vector3.zero, 0f);
                foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                FitModel(level, model, Vector3.up, BowPrefabTools.BowLength);

                // 시위가 뒤(-Z)로 가도록 돌리고, 쉬는 시위 위치 기록
                Transform stringRest = Child(level, "String Rest");
                Transform stringBone = model.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => string.Equals(t.name, "STRING", System.StringComparison.OrdinalIgnoreCase));
                if (stringBone != null)
                {
                    Vector3 p = bow.InverseTransformPoint(stringBone.position);
                    level.localRotation = Quaternion.Euler(0f, 180f - Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg, 0f);
                    p = bow.InverseTransformPoint(stringBone.position);
                    p.x = 0f;
                    if (p.z > -0.05f) p.z = -0.15f;
                    stringRest.position = bow.TransformPoint(p);
                }
                else
                {
                    stringRest.position = bow.TransformPoint(new Vector3(0f, 0f, -0.15f));
                    Debug.LogWarning($"[ArcherSceneBuilder] Lv{i + 1} 활에서 시위 본(STRING)을 찾지 못했습니다. 앞뒤 방향을 직접 확인하세요.");
                }

                // 당김 애니메이션 — 프리팹에 이미 Animator가 있으면 그대로 쓴다
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                if (animator.runtimeAnimatorController == null)
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(sources[i].controller);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                level.gameObject.SetActive(result.Count == 0);
                result.Add(new LevelVisualData { root = level.gameObject, animator = animator, stringRest = stringRest });
            }

            if (result.Count == 0)
            {
                Prim(PrimitiveType.Capsule, "Placeholder", visual, Vector3.zero, new Vector3(0.04f, 0.6f, 0.04f),
                    Mat("M_Wood", new Color(0.5f, 0.33f, 0.18f)), keepCollider: false);
            }
            return result;
        }

        private static void AssignLevelVisuals(BowController bow, List<LevelVisualData> levels)
        {
            var so = new SerializedObject(bow);
            so.FindProperty("bowAnimator").objectReferenceValue = levels.Count > 0 ? levels[0].animator : null;
            SerializedProperty list = so.FindProperty("levelVisuals");
            list.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = levels[i].root;
                element.FindPropertyRelative("animator").objectReferenceValue = levels[i].animator;
                element.FindPropertyRelative("stringRest").objectReferenceValue = levels[i].stringRest;
            }

            // 화살 효과 이름 (TSV ArrowEffect 0~3). 이펙트는 단계별 화살 프리팹에 들어 있다.
            SerializedProperty effects = so.FindProperty("arrowEffects");
            effects.arraySize = ArrowEffects.Length;
            for (int i = 0; i < ArrowEffects.Length; i++)
                effects.GetArrayElementAtIndex(i).FindPropertyRelative("displayName").stringValue = ArrowEffects[i].name;
            so.ApplyModifiedProperties();
        }

        /// <summary>단계 화살 프리팹. 없으면 기본 화살.</summary>
        private static Arrow LoadLevelArrow(int effect, Arrow fallback)
        {
            string path = ArrowEffects[effect].arrowPrefab;
            Arrow arrow = path != null ? AssetDatabase.LoadAssetAtPath<Arrow>(path) : null;
            return arrow != null ? arrow : fallback;
        }

        /// <summary>
        /// 강화 단계별 화살 효과: 이름, 화살 프리팹 (Arrow.prefab의 Variant + 촉에 Hovl 이펙트).
        /// 0단계는 기본 Arrow.prefab.
        /// </summary>
        private static readonly (string name, string arrowPrefab)[] ArrowEffects =
        {
            ("기본 화살", null),
            ("자연의 섬광", PrefabFolder + "/Arrow Lv2.prefab"), // Projectile 1 nature arrow (VFX loop)
            ("칼날 폭발", PrefabFolder + "/Arrow Lv3.prefab"),   // Projectile 8 dagger (반복 재생으로 덮어씀)
            ("용의 일격", PrefabFolder + "/Arrow Lv4.prefab"),   // Projectile dragon punch (VFX loop)
        };

        /// <summary>
        /// 씬 전체를 다시 만들지 않고, 현재 씬의 활 외형만 레벨별(Lv1~Lv3) 모델로 바꾼다.
        /// </summary>
        [MenuItem("Archer Simulator/활 레벨별 외형 적용")]
        public static void ApplyLevelVisualsToScene()
        {
            var bow = Object.FindFirstObjectByType<BowController>(FindObjectsInactive.Include);
            if (bow == null)
            {
                EditorUtility.DisplayDialog("Archer Simulator", "현재 씬에서 BowController를 찾지 못했습니다.\n먼저 '씬 기본 세팅'을 실행하세요.", "확인");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("활 레벨별 외형 적용");

            Transform bowTf = bow.transform;
            Quaternion savedRotation = bowTf.rotation;
            Undo.RecordObject(bowTf, "Reset Bow Rotation");
            bowTf.rotation = Quaternion.identity; // FitModel은 회전 없는 기준에서 맞춘다

            Transform oldVisual = bowTf.Find("Visual");
            if (oldVisual != null) Undo.DestroyObjectImmediate(oldVisual.gameObject);
            Transform visual = Child(bowTf, "Visual");
            List<LevelVisualData> levels = BuildBowVisuals(bowTf, visual);
            Undo.RegisterCreatedObjectUndo(visual.gameObject, "Create Bow Visual");

            var grab = bow.GetComponent<XRGrabInteractable>();
            Undo.RecordObject(grab, "Set Predicted Visual");
            grab.predictedVisualsTransform = visual;

            BowString bowString = bow.GetComponentInChildren<BowString>(true);
            if (bowString != null && levels.Count > 0 && levels[0].stringRest != null)
            {
                Undo.RecordObject(bowString.transform, "Move Bow String");
                bowString.transform.position = levels[0].stringRest.position;
            }

            Undo.RecordObject(bow, "Assign Level Visuals");
            AssignLevelVisuals(bow, levels);
            bowTf.rotation = savedRotation;

            Undo.CollapseUndoOperations(undoGroup);
            Scene scene = bow.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ArcherSceneBuilder] 활 외형 {levels.Count}단계를 적용했습니다.");
        }

        private static void BuildArrowSupply(Transform parent, Vector3 position)
        {
            Transform supply = Child(parent, "Arrow Supply");
            supply.localPosition = position;

            GameObject barrel = Prop("Props/SM_Prop_Barrel_Arrows_01.prefab", supply, Vector3.zero, 0f);
            float top = barrel != null ? WorldBounds(barrel).max.y : 0.9f;

            Transform spawn = Child(supply, "Arrow Spawn Point");
            spawn.position = new Vector3(supply.position.x, top + 0.05f, supply.position.z);
        }

        // ───────────────────────── Range ─────────────────────────

        private static GameObject BuildRange()
        {
            var root = new GameObject("Range");
            Transform markers = Child(root.transform, "Distance Markers");

            foreach (var (label, distance, color) in Difficulties)
            {
                Transform marker = Child(markers, $"Marker {distance:0}m");
                marker.localPosition = new Vector3(0f, 0f, distance);

                Prim(PrimitiveType.Cube, "Line", marker, new Vector3(0f, 0.01f, 0f), new Vector3(15f, 0.02f, 0.08f),
                    Mat($"M_Marker_{distance:0}m", color), keepCollider: false);
                Prim(PrimitiveType.Cube, "Sign Post", marker, new Vector3(-7.8f, 0.75f, 0f), new Vector3(0.1f, 1.5f, 0.1f),
                    Mat("M_Wood", new Color(0.5f, 0.33f, 0.18f)));
                Label3D(marker, "Label", label, 6f, new Vector3(-7.8f, 1.9f, 0f), new Vector2(12f, 2.6f), color);
            }

            return root;
        }

        // ───────────────────────── Game Stages ─────────────────────────

        private static GameObject BuildGameStages()
        {
            var root = new GameObject("Game Stages");
            Transform r = root.transform;

            // 1. 훈련장: 고정 표적 3개, 목표 10발
            Transform training = StageRoot(r, "Stage 01 · 훈련장", true);
            Transform slots = TargetSlots(training);
            float[] trainingX = { -3f, 0f, 3f };
            for (int i = 0; i < trainingX.Length; i++) BuildTarget(slots, i + 1, new Vector3(trainingX[i], 1.5f, 0f));
            Transform trainingDecor = Child(training, "Decor");
            for (int i = 0; i < 5; i++)
            {
                Prop("Props/SM_Prop_Hay_Bale_01.prefab", trainingDecor, new Vector3(-7.5f, 0.53f, 6f + i * 8f), 90f);
                Prop("Props/SM_Prop_Sack_01.prefab", trainingDecor, new Vector3(7.5f, 0f, 8f + i * 8f), i * 40f);
            }
            Prop("Props/BattleGround/SM_Prop_Dummy_Archery_01.prefab", trainingDecor, new Vector3(-6f, 0f, 44f), 180f);
            Prop("Props/BattleGround/SM_Prop_Target_01.prefab", trainingDecor, new Vector3(6f, 0f, 44f), 180f);

            // 2. 숲속 사격장: 고정 표적 5개(나무 뒤 일부 가려짐), 목표 15발
            Transform forest = StageRoot(r, "Stage 02 · 숲속 사격장", false);
            slots = TargetSlots(forest);
            float[] forestX = { -6f, -3f, 0f, 3f, 6f };
            float[] forestY = { 1.4f, 2.0f, 1.6f, 1.2f, 1.8f };
            for (int i = 0; i < forestX.Length; i++) BuildTarget(slots, i + 1, new Vector3(forestX[i], forestY[i], 0f));
            Transform forestDecor = Child(forest, "Decor");
            // 쉬움(10m) 기준 표적 가장자리를 가리는 나무
            Prop("Environments/SM_Env_Tree_Thin_01.prefab", forestDecor, new Vector3(-4.0f, 0f, 7f), 0f);
            Prop("Environments/SM_Env_Tree_Thin_02.prefab", forestDecor, new Vector3(0.35f, 0f, 7f), 90f);
            Prop("Environments/SM_Env_Tree_Thin_03.prefab", forestDecor, new Vector3(2.45f, 0f, 7f), 200f);
            Prop("Environments/SM_Env_Bush_Cluster_01.prefab", forestDecor, new Vector3(-5f, 0f, 9f), 0f);
            Prop("Environments/SM_Env_Bush_Cluster_02.prefab", forestDecor, new Vector3(5f, 0f, 9f), 0f);
            for (int i = 0; i < 7; i++)
                Prop($"Environments/SM_Env_Tree_Pine_Cluster_0{(i % 2 == 0 ? 1 : 2)}.prefab", forestDecor, new Vector3(-12f + i * 4f, 0f, 44f), i * 47f);

            // 3. 성벽: 고정 표적 6개(높이 다르게 배치), 목표 18발
            Transform castle = StageRoot(r, "Stage 03 · 성벽", false);
            slots = TargetSlots(castle);
            float[] castleX = { -5f, -3f, -1f, 1f, 3f, 5f };
            float[] castleY = { 1.2f, 2.8f, 4.2f, 1.8f, 3.4f, 1.2f };
            for (int i = 0; i < castleX.Length; i++) BuildTarget(slots, i + 1, new Vector3(castleX[i], castleY[i], 0f));
            Transform castleDecor = Child(castle, "Decor");
            TileRow("Castle/SM_Bld_Castle_Wall_01.prefab", castleDecor, 40f, -14f, 14f, 180f);
            Prop("Props/Banners/SM_Prop_Battle_Banner_01.prefab", castleDecor, new Vector3(-8f, 0f, 38f), 180f);
            Prop("Props/Banners/SM_Prop_Battle_Banner_02.prefab", castleDecor, new Vector3(8f, 0f, 38f), 180f);

            return root;
        }

        private static Transform StageRoot(Transform parent, string name, bool active)
        {
            Transform stage = Child(parent, name);
            stage.gameObject.SetActive(active);
            return stage;
        }

        /// <summary>표적 슬롯 루트. 난이도에 따라 z(10/20/35m)를 옮기는 기준점.</summary>
        private static Transform TargetSlots(Transform stage)
        {
            Transform slots = Child(stage, "Target Slots");
            slots.localPosition = new Vector3(0f, 0f, Difficulties[0].distance);
            Refs.Stages.Add((stage.gameObject, slots));
            return slots;
        }

        /// <summary>표적: 몸통(Body)과 약점(WeakPoint, 중앙 원)을 별도 Collider로 분리 + 월드 스페이스 HP 바.</summary>
        private static void BuildTarget(Transform slots, int index, Vector3 localPosition)
        {
            Transform t = Child(slots, $"Target {index:00}");
            t.localPosition = localPosition;

            float height = localPosition.y;
            Material wood = Mat("M_Wood", new Color(0.5f, 0.33f, 0.18f));
            Prim(PrimitiveType.Cube, "Leg L", t, new Vector3(-0.35f, -height * 0.5f, 0.12f), new Vector3(0.07f, height, 0.07f), wood);
            Prim(PrimitiveType.Cube, "Leg R", t, new Vector3(0.35f, -height * 0.5f, 0.12f), new Vector3(0.07f, height, 0.07f), wood);

            Quaternion faceZ = Quaternion.Euler(90f, 0f, 0f);
            GameObject bodyGo = Prim(PrimitiveType.Cylinder, "Body", t, Vector3.zero, new Vector3(1.2f, 0.08f, 1.2f),
                Mat("M_Target_Body", new Color(0.95f, 0.93f, 0.85f)));
            bodyGo.transform.localRotation = faceZ;
            ConvexMeshCollider(bodyGo);

            GameObject ring = Prim(PrimitiveType.Cylinder, "Ring", t, new Vector3(0f, 0f, -0.002f), new Vector3(0.75f, 0.081f, 0.75f),
                Mat("M_Target_Ring", new Color(0.2f, 0.45f, 0.85f)), keepCollider: false);
            ring.transform.localRotation = faceZ;

            GameObject weak = Prim(PrimitiveType.Cylinder, "WeakPoint", t, new Vector3(0f, 0f, -0.012f), new Vector3(0.3f, 0.09f, 0.3f),
                Mat("M_Target_WeakPoint", new Color(0.9f, 0.15f, 0.12f)));
            weak.transform.localRotation = faceZ;
            ConvexMeshCollider(weak);

            // HP 바 (항상 표시)
            RectTransform bar = WorldCanvas("HP Bar", t, new Vector3(0f, 0.85f, 0f), new Vector2(400f, 70f), interactive: false,
                background: false, scale: 0.0025f, face: false);
            Image back = UIImage(bar, "Background", new Color(0f, 0f, 0f, 0.7f), new Vector2(400f, 40f), new Vector2(0f, -10f));
            Image fill = UIImage(back.transform, "Fill", new Color(0.3f, 0.9f, 0.35f), new Vector2(392f, 32f), Vector2.zero);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            Label(bar, "HP Text", "100 / 100", 26f, new Vector2(0f, -10f), new Vector2(400f, 40f));

            SetupTarget(t, undo: false);
        }

        /// <summary>표적 오브젝트에 Target·TargetWeakPoint를 붙이고 HP 바를 연결한다. 이미 있으면 연결만 다시 한다.</summary>
        private static void SetupTarget(Transform t, bool undo)
        {
            Transform weak = t.Find("WeakPoint");
            if (weak != null && weak.GetComponent<TargetWeakPoint>() == null)
            {
                if (undo) Undo.AddComponent<TargetWeakPoint>(weak.gameObject);
                else weak.gameObject.AddComponent<TargetWeakPoint>();
            }

            Target target = t.GetComponent<Target>();
            if (target == null) target = undo ? Undo.AddComponent<Target>(t.gameObject) : t.gameObject.AddComponent<Target>();

            var so = new SerializedObject(target);
            Transform fill = t.Find("HP Bar/Background/Fill");
            Transform text = t.Find("HP Bar/HP Text");
            so.FindProperty("hpFill").objectReferenceValue = fill != null ? fill.GetComponent<Image>() : null;
            so.FindProperty("hpText").objectReferenceValue = text != null ? text.GetComponent<TMP_Text>() : null;
            if (undo) so.ApplyModifiedProperties();
            else so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>UpgradeStation의 pointsText·bowLevelText를 타이틀 게시판 라벨에 연결한다 (Apply는 호출한 쪽에서).</summary>
        private static void WireTitleBoardLabels(SerializedObject stationSo)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            TMP_Text Find(string name) =>
                all.FirstOrDefault(t => t.name == name && t.parent != null && t.parent.name == "Title Board")?.GetComponent<TMP_Text>();
            stationSo.FindProperty("pointsText").objectReferenceValue = Find("Points Text");
            stationSo.FindProperty("bowLevelText").objectReferenceValue = Find("Bow Level Text");
        }

        /// <summary>
        /// 씬 전체를 다시 만들지 않고, 현재 씬의 표적에 판정(Target)을 붙이고 결과 화면 최고 기록 줄을 GameManager에 연결한다.
        /// </summary>
        [MenuItem("Archer Simulator/표적 판정·점수·포인트 연결")]
        public static void SetupTargetsAndScore()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var manager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            Transform[] targets = all.Where(t => t.parent != null && t.parent.name == "Target Slots" && t.name.StartsWith("Target ")).ToArray();
            if (manager == null || targets.Length == 0)
            {
                EditorUtility.DisplayDialog("Archer Simulator", "현재 씬에서 GameManager 또는 표적(Target Slots/Target NN)을 찾지 못했습니다.\n먼저 '씬 기본 세팅'을 실행하세요.", "확인");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("표적 판정·점수·포인트 연결");

            foreach (Transform t in targets) SetupTarget(t, undo: true);

            Transform record = all.FirstOrDefault(t => t.name == "Record Text" && t.parent != null && t.parent.name == "Result Board");
            var so = new SerializedObject(manager);
            so.FindProperty("resultRecordText").objectReferenceValue = record != null ? record.GetComponent<TMP_Text>() : null;
            so.ApplyModifiedProperties();

            // 강화 작업대 → 타이틀 게시판의 보유 포인트·현재 활 줄
            foreach (UpgradeStation station in Object.FindObjectsByType<UpgradeStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var stationSo = new SerializedObject(station);
                WireTitleBoardLabels(stationSo);
                stationSo.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(undoGroup);
            Scene scene = manager.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ArcherSceneBuilder] 표적 {targets.Length}개에 판정을 붙이고 점수·포인트 UI를 연결했습니다." +
                      (record == null ? " (결과 화면 'Record Text'는 찾지 못함)" : ""));
        }

        // ───────────────────────── UI ─────────────────────────

        private static GameObject BuildUI()
        {
            var root = new GameObject("UI");
            Transform r = root.transform;

            // 결과 화면
            RectTransform c = WorldCanvas("Result Board", r, new Vector3(0f, 1.6f, 3.2f), new Vector2(800f, 620f));
            Backdrop(c);
            Refs.ResultTitle = Label(c, "Result Title", "클리어!", 64f, new Vector2(0f, 240f), new Vector2(740f, 90f), color: AccentColor);
            Refs.ResultDetail = Label(c, "Score Breakdown", "표적 파괴  100 × 3 = 300\n약점 명중  20 × 0 = 0\n남은 화살  50 × 1 = 50\n난이도 배율  × 1.0",
                24f, new Vector2(0f, 100f), new Vector2(700f, 170f));
            Refs.ResultTotal = Label(c, "Total Score Text", "획득 점수  350", 40f, new Vector2(0f, -25f), new Vector2(700f, 60f), color: new Color(0.6f, 0.9f, 1f));
            Refs.ResultStats = Label(c, "Stats Text", "명중률 0%  ·  남은 화살 0", 24f, new Vector2(0f, -90f), new Vector2(700f, 44f));
            Refs.ResultRecord = Label(c, "Record Text", "최고 기록 0  ·  +0 P (보유 0 P)", 24f, new Vector2(0f, -140f), new Vector2(700f, 44f), color: SubTextColor);
            Refs.Retry = MakeButton(c, "Retry", "다시 하기", new Vector2(-150f, -230f), new Vector2(260f, 70f), 28f);
            Refs.Lobby = MakeButton(c, "Back To Lobby", "로비로", new Vector2(150f, -230f), new Vector2(260f, 70f), 28f);
            Refs.ResultBoard = c.gameObject;
            c.gameObject.SetActive(false);

            // 3초 카운트다운
            TextMeshPro countdown = Label3D(r, "Countdown", "3", 15f, new Vector3(0f, 2.3f, 6f), new Vector2(4f, 2.5f), AccentColor);
            countdown.gameObject.SetActive(false);
            Refs.Countdown = countdown;

            return root;
        }

        // ───────────────────────── 빌드 설정 ─────────────────────────

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => File.Exists(s.path)).ToList();
            if (scenes.All(s => s.path != ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Quaternion FacePlayer(Vector3 worldPosition)
        {
            var flat = new Vector3(worldPosition.x, 0f, worldPosition.z);
            return flat.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            if (keepCollider == false) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void ConvexMeshCollider(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mesh = go.AddComponent<MeshCollider>();
            mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;
        }

        private static Material Mat(string name, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>ScreenFade용 URP Unlit 투명 머티리얼 (_BaseColor 알파로 페이드).</summary>
        private static Material FadeMaterial()
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/M_Screen_Fade.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Overlay;
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) == false) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static GameObject Prop(string relativePath, Transform parent, Vector3 localPosition, float yaw)
        {
            string path = relativePath.StartsWith("Assets/") ? relativePath : Synty + relativePath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[ArcherSceneBuilder] 프리팹 없음: {path}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        private static void TileRow(string relativePath, Transform parent, float z, float xMin, float xMax, float yaw)
        {
            GameObject first = Prop(relativePath, parent, new Vector3(xMin, 0f, z), yaw);
            if (first == null) return;

            float width = WorldBounds(first).size.x;
            if (width < 0.5f) return;
            for (float x = xMin + width; x <= xMax; x += width)
                Prop(relativePath, parent, new Vector3(x, 0f, z), yaw);
        }

        /// <summary>
        /// 모델을 'Model Fit' 래퍼에 넣고, 가장 긴 축을 axis(컨테이너 기준 Y 또는 Z)에 맞춰 길이를 맞춘 뒤 컨테이너 중심에 둔다.
        /// 모델 루트 자체는 건드리지 않아 Animator가 루트를 덮어써도 정렬이 유지된다. 컨테이너는 회전이 없어야 한다.
        /// </summary>
        private static Transform FitModel(Transform container, GameObject model, Vector3 axis, float length)
        {
            Transform fit = Child(container, "Model Fit");
            model.transform.SetParent(fit, false);

            Vector3 size = WorldBounds(model).size;
            Vector3 longest = size.x >= size.y && size.x >= size.z ? Vector3.right
                : size.y >= size.z ? Vector3.up : Vector3.forward;
            fit.localRotation = Quaternion.FromToRotation(longest, axis);

            Vector3 fitted = WorldBounds(model).size;
            float current = Mathf.Max(Mathf.Abs(Vector3.Dot(fitted, axis)), 0.001f);
            fit.localScale = Vector3.one * (length / current);
            fit.position += container.position - WorldBounds(model).center;
            return fit;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ─── UI (Throw Range 간판 스타일) ───

        /// <summary>월드 스페이스 캔버스. face가 true면 플레이어(원점)를 바라본다. 배경은 루트 Image(어두운 녹색).</summary>
        private static RectTransform WorldCanvas(string name, Transform parent, Vector3 localPosition, Vector2 sizePx,
            bool interactive = true, bool background = true, float scale = BoardScale, bool face = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localPosition = localPosition;
            rect.sizeDelta = sizePx;
            rect.localScale = Vector3.one * scale;
            if (face) rect.rotation = FacePlayer(rect.position);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;

            if (interactive) go.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (background)
            {
                var image = go.AddComponent<Image>();
                image.color = PanelColor;
                image.raycastTarget = interactive;
            }
            return rect;
        }

        /// <summary>
        /// 캔버스 뒤 나무 간판(판·테두리·지붕·기둥). Throw Range의 Backdrop과 같은 비율:
        /// 단위 = 캔버스 px/1000, 테두리 0.045, 기둥은 바닥(콜라이더)까지 내린다.
        /// </summary>
        private static void Backdrop(RectTransform canvas, bool posts = true)
        {
            Transform bd = Child(canvas, "Backdrop");
            bd.localScale = Vector3.one * 1000f;
            bd.SetAsFirstSibling();

            Material face = Mat("M_Board_Face", new Color(0.18f, 0.13f, 0.09f));
            Material wood = Mat("M_Board_Frame", new Color(0.45f, 0.3f, 0.17f));
            float w = canvas.sizeDelta.x / 1000f + 0.07f;
            float h = canvas.sizeDelta.y / 1000f + 0.07f;

            Prim(PrimitiveType.Cube, "Board", bd, new Vector3(0f, 0f, 0.019f), new Vector3(w, h, 0.03f), face, keepCollider: false);
            Prim(PrimitiveType.Cube, "Frame Top", bd, new Vector3(0f, h / 2f - 0.0225f, -0.009f), new Vector3(w, 0.045f, 0.03f), wood, keepCollider: false);
            Prim(PrimitiveType.Cube, "Frame Bottom", bd, new Vector3(0f, -h / 2f + 0.0225f, -0.009f), new Vector3(w, 0.045f, 0.03f), wood, keepCollider: false);
            Prim(PrimitiveType.Cube, "Frame Left", bd, new Vector3(-w / 2f + 0.0225f, 0f, -0.009f), new Vector3(0.045f, h - 0.09f, 0.03f), wood, keepCollider: false);
            Prim(PrimitiveType.Cube, "Frame Right", bd, new Vector3(w / 2f - 0.0225f, 0f, -0.009f), new Vector3(0.045f, h - 0.09f, 0.03f), wood, keepCollider: false);
            GameObject cap = Prim(PrimitiveType.Cube, "Cap", bd, new Vector3(0f, h / 2f + 0.03f, 0.01f), new Vector3(w + 0.04f, 0.035f, 0.12f), wood, keepCollider: false);
            cap.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            if (posts == false) return;
            float top = h / 2f - 0.086f;
            float thickness = 0.07f / bd.lossyScale.x;
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * (w / 2f - 0.085f);
                Vector3 topWorld = bd.TransformPoint(new Vector3(x, top, 0.069f));
                float groundY = Physics.Raycast(topWorld, Vector3.down, out RaycastHit hit, 20f) ? hit.point.y : 0f;
                float bottom = bd.InverseTransformPoint(new Vector3(topWorld.x, groundY, topWorld.z)).y;
                Prim(PrimitiveType.Cube, $"Post {i + 1}", bd, new Vector3(x, (top + bottom) / 2f, 0.069f),
                    new Vector3(thickness, top - bottom, thickness), wood, keepCollider: false);
            }
        }

        private static Image UIImage(Transform parent, string name, Color color, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 position, Vector2 box,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Throw Range 버튼: 단색 Image + Button(Color Tint) + 흰색 'Label'.</summary>
        private static Button MakeButton(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = UILayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = ButtonColor;

            TextMeshProUGUI label = Label(rect, "Label", text, fontSize, Vector2.zero, Vector2.zero);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            return go.GetComponent<Button>();
        }

        /// <summary>3D TextMeshPro 라벨 (Throw Range의 Marker/Target Label 방식). fontSize 10 ≈ 1m.</summary>
        private static TextMeshPro Label3D(Transform parent, string name, string text, float fontSize, Vector3 localPosition, Vector2 box,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localPosition = localPosition;
            rect.sizeDelta = box;

            var label = go.GetComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            return label;
        }
    }
}
