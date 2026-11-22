using StudyXR.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcherSimulator
{
    /// <summary>
    /// 타이틀 화면. 씬이 시작되면 ScreenFade가 검은 화면에서 페이드 인하고,
    /// '게임 시작'을 누르면 페이드 아웃한 뒤 게임 씬으로 넘어간다 (게임 씬에서 다시 페이드 인).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private ScreenFade screenFade;
        [SerializeField] private string gameScene = "Archer Simulator";

        private bool leaving;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        }

        public void StartGame()
        {
            if (leaving) return;
            leaving = true;
            SetButtonsInteractable(false);

            if (screenFade != null && screenFade.isActiveAndEnabled) screenFade.LoadScene(gameScene);
            else SceneManager.LoadScene(gameScene);
        }

        public void QuitGame()
        {
            if (leaving) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (startButton != null) startButton.interactable = interactable;
            if (quitButton != null) quitButton.interactable = interactable;
        }
    }
}
