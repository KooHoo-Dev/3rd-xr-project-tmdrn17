using StudyXR.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StudyXR.ThrowPhysics
{
    public class ThrowTuningPanel : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BallRack rack;
        [SerializeField] private MassAwareThrow massAware;
        [SerializeField] private GameObject panelRoot;

        [Header("표시")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private bool visibleOnStart = false;

        [Header("시뮬레이터 테스트 입력")]
        [SerializeField] private bool useTestKeys = true;

        private bool inputEnabled = true;
        private InputAction panelAction;
        private InputAction massAction;

        public void SetInputEnabled(bool on)
        {
            inputEnabled = on;
        }

        public void SetSmoothing(float seconds)
        {
            if (rack == null) return;
            for (int i = 0; i < rack.Count; ++i) rack.Grab(i).throwSmoothingDuration = seconds;
        }

        public void SetVelocityScale(float scale)
        {
            if (massAware != null) massAware.SetBaseScale(scale);
        }

        public void SetMassAware(bool on)
        {
            if (massAware != null) massAware.SetMassAware(on);
        }

        public void TogglePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(!panelRoot.activeSelf);
        }

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(visibleOnStart);
        }

        private void OnEnable()
        {
            panelAction = QuestButtons.Create(QuestButton.X, "Tuning Panel", useTestKeys ? "<Keyboard>/f5" : null);
            massAction = QuestButtons.Create(QuestButton.RightStickClick, "Mass Aware", useTestKeys ? "<Keyboard>/f6" : null);
            panelAction.Enable();
            massAction.Enable();
        }

        private void OnDisable()
        {
            panelAction.Disable();
            panelAction.Dispose();
            massAction.Disable();
            massAction.Dispose();
        }

        private void Update()
        {
            if (inputEnabled == false) return;
            if (panelAction.WasPressedThisFrame()) TogglePanel();
            if (massAction.WasPressedThisFrame() && massAware != null) massAware.SetMassAware(!massAware.IsMassAware);
        }

        private void LateUpdate()
        {
            if (panelRoot == null || panelRoot.activeSelf == false) return;
            if (statusText == null || rack == null || massAware == null || rack.Count < 2) return;

            float s = rack.Grab(0).throwSmoothingDuration;
            int frames = Mathf.Max(2, Mathf.RoundToInt(s / Mathf.Max(0.001f, Time.smoothDeltaTime)));
            string on = massAware.IsMassAware ? "켜짐" : "꺼짐";

            statusText.text =
                $"스무딩 창 {s:F2} s · 배율 x{massAware.BaseScale:F1} · 질량 보정 {on}\n" +
                $"최근 {frames}프레임의 추세 → 놓는 순간 손 v + 회전 ω × r\n" +
                $"지금 배율 : {rack.Label(0)} x{massAware.ScaleOf(0):F2} · {rack.Label(1)} x{massAware.ScaleOf(1):F2}";
        }
    }
}
