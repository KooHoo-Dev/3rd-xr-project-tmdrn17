using System.IO;
using System.Linq;
using StudyXR.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcherSimulator.EditorTools
{
    /// <summary>
    /// 타이틀 씬 (앱 이름 · 제작자 · 게임 시작 / 게임 종료).
    /// 시작하면 검은 화면에서 페이드 인하고, '게임 시작'을 누르면 페이드 아웃 → Archer Simulator 씬 → 페이드 인.
    /// 메뉴: Archer Simulator > 타이틀 씬 만들기 (다시 실행하면 타이틀 씬을 새로 만든다)
    /// </summary>
    public static partial class ArcherSceneBuilder
    {
        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private static readonly Color QuitButtonColor = new Color(0.55f, 0.22f, 0.2f);

        [MenuItem("Archer Simulator/타이틀 씬 만들기")]
        public static void BuildTitleScene()
        {
            if (EditorUtility.DisplayDialog("Archer Simulator · 타이틀 씬",
                    $"'{TitleScenePath}'를 새로 만듭니다. (이미 있으면 덮어씁니다)\n\n" +
                    "• 앱 이름 · 제작자 · 게임 시작 / 게임 종료 버튼\n" +
                    "• 시작할 때 페이드 인, 게임 시작 시 페이드 아웃 후 게임 씬으로 이동\n" +
                    "• Build Settings: 타이틀 씬을 0번(앱 시작 씬)으로 둡니다.\n" +
                    $"• 앱 이름(Product Name)을 '{AppTitle}'(으)로 맞춥니다.\n" +
                    "• 게임 씬 로비의 예전 타이틀('Title Sign')을 지웁니다.",
                    "생성", "취소") == false) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
            if (rigPrefab == null)
            {
                EditorUtility.DisplayDialog("Archer Simulator", $"XR Origin 프리팹을 찾지 못했습니다.\n{XROriginPath}", "확인");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Refs.Clear();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            mainCamera = rig.GetComponentInChildren<Camera>(true);

            BuildXR(scene, rig); // XR 루트 · Interaction Manager · Event System · Screen Fade
            ScreenFade fade = rig.GetComponentInChildren<ScreenFade>(true);
            if (fade != null)
            {
                var so = new SerializedObject(fade);
                so.FindProperty("fadeInOnStart").boolValue = true; // 앱 첫 화면: 검은 화면에서 페이드 인
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            ApplyRenderSettings();
            BuildLights();
            BuildStage(); // 게임과 같은 배경 (사로 · 산 · 나무)
            Physics.SyncTransforms(); // 간판 기둥이 바닥을 찾을 수 있도록
            BuildTitleUI(fade);

            Directory.CreateDirectory(Path.GetDirectoryName(TitleScenePath));
            EditorSceneManager.SaveScene(scene, TitleScenePath);

            SetTitleFirstInBuildSettings();
            PlayerSettings.productName = AppTitle;
            AssetDatabase.SaveAssets();
            RemoveLobbyTitleSign();

            Debug.Log($"[ArcherSceneBuilder] 타이틀 씬을 만들었습니다: {TitleScenePath}");
        }

        private static void BuildTitleUI(ScreenFade fade)
        {
            var root = new GameObject("Title");

            RectTransform c = WorldCanvas("Title Board", root.transform, new Vector3(0f, 1.55f, 2.4f), new Vector2(900f, 620f));
            Backdrop(c);

            TextMeshProUGUI title = Label(c, "App Title", AppTitle, 84f, new Vector2(0f, 215f), new Vector2(860f, 110f), color: AccentColor);
            title.fontStyle = FontStyles.Bold;
            Label(c, "Sub Title", "아처 시뮬레이터 VR", 34f, new Vector2(0f, 145f), new Vector2(860f, 50f), color: SubTextColor);
            Label(c, "Maker", "제작  " + MakerName, 30f, new Vector2(0f, 90f), new Vector2(860f, 46f));
            UIImage(c, "Divider", new Color(1f, 1f, 1f, 0.15f), new Vector2(760f, 3f), new Vector2(0f, 45f));

            Button start = MakeButton(c, "Start Button", "게임 시작", new Vector2(0f, -45f), new Vector2(420f, 100f), 40f);
            Button quit = MakeButton(c, "Quit Button", "게임 종료", new Vector2(0f, -165f), new Vector2(420f, 80f), 30f);
            quit.GetComponent<Image>().color = QuitButtonColor;

            Label(c, "Hint Text", "컨트롤러로 버튼을 가리키고 트리거를 당기세요", 22f, new Vector2(0f, -260f), new Vector2(860f, 40f),
                color: SubTextColor);

            var screen = root.AddComponent<TitleScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("startButton").objectReferenceValue = start;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("screenFade").objectReferenceValue = fade;
            so.FindProperty("gameScene").stringValue = Path.GetFileNameWithoutExtension(ScenePath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>타이틀 씬을 0번(앱 시작 씬)으로, 게임 씬을 그 뒤에 둔다.</summary>
        private static void SetTitleFirstInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => File.Exists(s.path) && s.path != TitleScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));
            if (scenes.All(s => s.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>예전에 게임 씬 로비에 띄웠던 타이틀(Title Sign)을 지운다. 타이틀 씬이 대신한다.</summary>
        private static void RemoveLobbyTitleSign()
        {
            if (File.Exists(ScenePath) == false) return;

            Scene game = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            GameObject lobby = game.GetRootGameObjects().FirstOrDefault(g => g.name == "Lobby");
            Transform sign = lobby != null ? lobby.transform.Find(TitleSignName) : null;
            if (sign != null)
            {
                Object.DestroyImmediate(sign.gameObject);
                EditorSceneManager.MarkSceneDirty(game);
                EditorSceneManager.SaveScene(game);
                Debug.Log("[ArcherSceneBuilder] 게임 씬 로비의 예전 타이틀(Title Sign)을 지웠습니다.");
            }
            EditorSceneManager.CloseScene(game, true);
        }
    }
}
