using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace StudyXR.Common
{
    public class LauncherReturn : MonoBehaviour
    {
        private const string LauncherSceneName = "Launcher";

        private InputAction menuAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfStartedFromLauncher()
        {
            Scene first = SceneManager.GetActiveScene();

            if (first.buildIndex != 0 || first.name != LauncherSceneName) return;

            var go = new GameObject("Launcher Return");
            DontDestroyOnLoad(go);
            go.AddComponent<LauncherReturn>();
        }

        private void OnEnable()
        {
            menuAction = new InputAction(
                "Return To Launcher",
                InputActionType.Button,
                "<XRController>{LeftHand}/{MenuButton}");
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

        private void OnMenuPressed(InputAction.CallbackContext context)
        {
            if (SceneManager.GetActiveScene().buildIndex == 0) return;
            SceneManager.LoadScene(0);
        }
    }
}
