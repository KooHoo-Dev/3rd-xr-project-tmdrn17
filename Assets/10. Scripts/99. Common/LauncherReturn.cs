using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyXR.Common
{
    /// <summary>
    /// 런처 씬(빌드 0번 "Launcher")에서 시작했는지 기억하고, 런처로 돌아가는 기능을 준다.
    /// 왼손 메뉴 버튼(≡)은 QuitMenu가 받으며, 런처에서 시작했으면 종료 메뉴에 "런처로" 버튼이 나온다.
    /// </summary>
    public static class LauncherReturn
    {
        private const string LauncherSceneName = "Launcher";

        /// <summary>런처에서 시작했는지 (런처로 돌아갈 수 있는지).</summary>
        public static bool Active { get; private set; }

        /// <summary>지금 런처가 아닌 씬에 있어서 런처로 돌아갈 수 있는지.</summary>
        public static bool CanReturn => Active && SceneManager.GetActiveScene().buildIndex != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckStartedFromLauncher()
        {
            Scene first = SceneManager.GetActiveScene();
            Active = first.buildIndex == 0 && first.name == LauncherSceneName;
        }

        public static void Return()
        {
            if (!CanReturn) return;
            SceneManager.LoadScene(0);
        }
    }
}
