using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace StudyXR.Common
{
    /// <summary>
    /// 게임 종료 메뉴. 어느 씬, 어느 상황에서든 왼손 메뉴 버튼(≡)을 누르면 눈앞에 "게임을 종료할까요?" 창이 뜬다.
    /// - 게임 종료: 앱을 끈다 (에디터에서는 플레이 모드 종료).
    /// - 계속하기 / 메뉴 버튼 다시 누르기: 창을 닫는다.
    /// - 런처로: 런처 씬에서 시작했을 때만 보인다 (LauncherReturn).
    /// 씬에 따로 둘 필요 없이 게임이 시작될 때 스스로 만들어지고, 씬이 바뀌어도 남는다.
    /// 메타(Oculus) 버튼은 Quest 시스템 전용이라 앱에서 읽을 수 없다 (누르면 Quest 시스템 메뉴가 뜬다).
    /// </summary>
    public class QuitMenu : MonoBehaviour
    {
        private const string MenuButtonPath = "<XRController>{LeftHand}/{MenuButton}";
        private const string KeyboardFallback = "<Keyboard>/escape";

        private const float Distance = 1.2f;       // 눈앞 거리 (m)
        private const float PixelsToMeters = 0.001f; // 캔버스 1px = 1mm

        private static readonly Color PanelColor = new Color(0.085f, 0.115f, 0.1f, 0.96f);
        private static readonly Color ContinueColor = new Color(0.2f, 0.45f, 0.85f);
        private static readonly Color LauncherColor = new Color(0.35f, 0.4f, 0.45f);
        private static readonly Color QuitColor = new Color(0.8f, 0.25f, 0.22f);

        private InputAction menuAction;
        private Canvas canvas;
        private Button continueButton;
        private Button launcherButton;
        private Button quitButton;

        public static QuitMenu Instance { get; private set; }
        public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStart()
        {
            if (Instance != null) return;
            var go = new GameObject("Quit Menu");
            DontDestroyOnLoad(go);
            go.AddComponent<QuitMenu>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUI();
            SetOpen(false);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnEnable()
        {
            menuAction = new InputAction("Quit Menu", InputActionType.Button, MenuButtonPath);
            menuAction.AddBinding(KeyboardFallback);
            menuAction.performed += OnMenuPressed;
            menuAction.Enable();
        }

        private void OnDisable()
        {
            if (menuAction == null) return;
            menuAction.performed -= OnMenuPressed;
            menuAction.Disable();
            menuAction.Dispose();
            menuAction = null;
        }

        // 씬이 바뀌면 창을 닫는다 (새 씬의 카메라 기준으로 다시 열어야 하므로).
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetOpen(false);

        private void OnMenuPressed(InputAction.CallbackContext context) => SetOpen(!IsOpen);

        // ───────── 열기 / 닫기 ─────────

        public void SetOpen(bool open)
        {
            if (canvas == null) return;
            if (open)
            {
                LayoutButtons();
                PlaceInFrontOfPlayer();
            }
            canvas.gameObject.SetActive(open);
        }

        /// <summary>플레이어 눈앞(수평 방향)에 플레이어를 바라보게 놓는다.</summary>
        private void PlaceInFrontOfPlayer()
        {
            Camera head = Camera.main;
            if (head == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up); // 바로 위·아래를 볼 때
            forward.Normalize();

            canvas.transform.SetPositionAndRotation(head.transform.position + forward * Distance, Quaternion.LookRotation(forward));
            canvas.worldCamera = head;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────── UI 만들기 ─────────

        private void BuildUI()
        {
            var go = new GameObject("Quit Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(transform, false);

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100; // 다른 월드 UI보다 위에
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(640f, 340f);
            rect.localScale = Vector3.one * PixelsToMeters;

            Image panel = go.AddComponent<Image>();
            panel.color = PanelColor;

            Label(rect, "Title", "게임을 종료할까요?", 44f, new Vector2(0f, 85f), new Vector2(600f, 70f), Color.white);
            Label(rect, "Hint", "왼손 메뉴(≡) 버튼을 다시 누르면 닫힙니다", 22f, new Vector2(0f, 25f), new Vector2(600f, 40f),
                new Color(0.75f, 0.8f, 0.78f));

            continueButton = MakeButton(rect, "Continue Button", "계속하기", ContinueColor);
            continueButton.onClick.AddListener(() => SetOpen(false));
            launcherButton = MakeButton(rect, "Launcher Button", "런처로", LauncherColor);
            launcherButton.onClick.AddListener(LauncherReturn.Return);
            quitButton = MakeButton(rect, "Quit Button", "게임 종료", QuitColor);
            quitButton.onClick.AddListener(Quit);
        }

        /// <summary>런처에서 시작했을 때만 "런처로" 버튼을 보이고, 보이는 버튼을 가로로 고르게 놓는다.</summary>
        private void LayoutButtons()
        {
            bool showLauncher = LauncherReturn.CanReturn;
            launcherButton.gameObject.SetActive(showLauncher);

            Button[] shown = showLauncher
                ? new[] { continueButton, launcherButton, quitButton }
                : new[] { continueButton, quitButton };
            float width = showLauncher ? 180f : 240f;
            float step = width + 20f;
            for (int i = 0; i < shown.Length; i++)
            {
                var rect = (RectTransform)shown[i].transform;
                rect.sizeDelta = new Vector2(width, 80f);
                ((RectTransform)rect.GetChild(0)).sizeDelta = rect.sizeDelta;
                rect.anchoredPosition = new Vector2((i - (shown.Length - 1) * 0.5f) * step, -80f);
            }
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Vector2 position, Vector2 box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;

            var label = go.AddComponent<TextMeshProUGUI>(); // TMP 기본 폰트 = NotoSansKR (한글)
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(RectTransform parent, string name, string text, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(240f, 80f);

            go.GetComponent<Image>().color = color;
            Label(rect, "Label", text, 30f, Vector2.zero, rect.sizeDelta, Color.white);
            return go.GetComponent<Button>();
        }
    }
}
