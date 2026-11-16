using UnityEngine;

namespace StudyXR.Common
{
    public class SceneTimestep : MonoBehaviour
    {
        [SerializeField] private float fixedTimestep = 1f / 72f;

        private float previous;

        private void OnEnable()
        {
            previous = Time.fixedDeltaTime;
            Time.fixedDeltaTime = Mathf.Max(0.001f, fixedTimestep);
        }

        private void OnDisable()
        {
            Time.fixedDeltaTime = previous;
        }
    }
}
