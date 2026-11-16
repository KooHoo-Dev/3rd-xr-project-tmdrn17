using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace StudyXR.Common
{
    public class PlayerHeightApplier : MonoBehaviour
    {
        private static PlayerHeightApplier instance;

        private XROrigin origin;
        private bool skipThisScene;
        private float baseHeight;
        private float lastWritten = float.NaN;
        private float applied;

        public static float EyeHeight
        {
            get
            {
                if (instance == null || instance.origin == null || instance.origin.Camera == null) return float.NaN;
                return instance.origin.CameraInOriginSpaceHeight;
            }
        }

        public static float RawEyeHeight => EyeHeight - (instance != null ? instance.applied : 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("Player Height");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlayerHeightApplier>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindOrigin();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindOrigin();
        }

        private void FindOrigin()
        {
            origin = FindAnyObjectByType<XROrigin>();
            skipThisScene = (FindAnyObjectByType<ARSession>() != null);
            lastWritten = float.NaN;
            applied = 0f;
        }

        private void LateUpdate()
        {
            if (origin == null || origin.CameraFloorOffsetObject == null) return;

            Transform offset = origin.CameraFloorOffsetObject.transform;
            Vector3 local = offset.localPosition;

            if (float.IsNaN(lastWritten) || Mathf.Approximately(local.y, lastWritten) == false) baseHeight = local.y;

            applied = skipThisScene ? 0f : PlayerHeight.Offset;
            local.y = baseHeight + applied;
            offset.localPosition = local;
            lastWritten = local.y;
        }
    }
}
