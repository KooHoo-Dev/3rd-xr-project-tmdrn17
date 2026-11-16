using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace StudyXR.Common
{
    public class PerfHud : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;
        [SerializeField] private TMP_Text label;
        [SerializeField] private InputActionReference toggleAction;

        [Header("표시")]
        [SerializeField] private bool visibleOnStart = false;
        [SerializeField] private float distance = 0.6f;
        [SerializeField] private Vector2 viewOffset = new Vector2(-0.18f, -0.12f);

        [Header("Quest 버튼")]
        [SerializeField] private bool useQuestButton = true;

        private const float SampleSeconds = 0.5f;

        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private float sampleTime;
        private int sampleFrames;
        private float smoothedFps;
        private bool visible;
        private InputAction questToggle;

        public float SmoothedFps => smoothedFps;
        public bool Visible => visible;

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            SetVisible(visibleOnStart);
        }

        private void OnEnable()
        {
            if (useQuestButton)
            {
                questToggle = QuestButtons.Create(QuestButton.Y, "Perf HUD Toggle", "<Keyboard>/f2");
                questToggle.performed += OnTogglePressed;
                questToggle.Enable();
            }

            if (toggleAction == null || toggleAction.action == null) return;
            toggleAction.action.performed += OnTogglePressed;
            if (toggleAction.action.enabled == false) toggleAction.action.Enable();
        }

        private void OnDisable()
        {
            if (questToggle != null)
            {
                questToggle.performed -= OnTogglePressed;
                questToggle.Disable();
                questToggle.Dispose();
                questToggle = null;
            }

            if (toggleAction != null && toggleAction.action != null) toggleAction.action.performed -= OnTogglePressed;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (label != null) label.enabled = value;
        }

        private void OnTogglePressed(InputAction.CallbackContext context)
        {
            SetVisible(!visible);
        }

        private void Update()
        {
            sampleTime += Time.unscaledDeltaTime;
            sampleFrames++;
            if (sampleTime < SampleSeconds) return;

            smoothedFps = sampleFrames / sampleTime;
            float frameMs = sampleTime / sampleFrames * 1000f;
            sampleTime = 0f;
            sampleFrames = 0;
            if (visible && label != null) label.text = $"FPS {smoothedFps:0.0}\n{frameMs:0.0} ms\n{RefreshRate():0} Hz";
        }

        private void LateUpdate()
        {
            if (visible == false || head == null || label == null) return;
            Vector3 local = new Vector3(viewOffset.x, viewOffset.y, distance);
            label.transform.SetPositionAndRotation(head.position + head.rotation * local, head.rotation);
        }

        private float RefreshRate()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
                if (display.running && display.TryGetDisplayRefreshRate(out float rate)) return rate;
            return (float)Screen.currentResolution.refreshRateRatio.value;
        }
    }
}
