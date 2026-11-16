using StudyXR.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace StudyXR.ThrowPhysics
{
    public class ImpulseLauncher : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BallRack rack;
        [SerializeField] private ThrowProbe probe;
        [SerializeField] private Transform aim;
        [SerializeField] private NearFarInteractor rightInteractor;

        [Header("표시")]
        [SerializeField] private TMP_Text statusText;

        [Header("발사")]
        [SerializeField] private float minImpulse = 0.3f;
        [SerializeField] private float maxImpulse = 1.8f;
        [SerializeField] private float chargeSeconds = 1.2f;
        [SerializeField] private float muzzleDistance = 0.15f;

        [Header("시뮬레이터 테스트 입력")]
        [SerializeField] private bool useTestKeys = true;

        private bool charging;
        private bool fireQueued;
        private float charge01;
        private int chargingIndex;
        private bool hasFired;
        private float lastImpulse;
        private float lastExpected;
        private bool inputEnabled = true;
        private InputAction fireAction;

        public void SetInputEnabled(bool on)
        {
            inputEnabled = on;
            if (on == false) CancelCharge();
        }

        private void CancelCharge()
        {
            if (charging == false && fireQueued == false) return;

            charging = false;
            fireQueued = false;
            charge01 = 0f;
            if (rack == null) return;

            rack.Body(chargingIndex).isKinematic = false;
            rack.Grab(chargingIndex).enabled = true;
            rack.ReturnBall(chargingIndex);
        }

        private void Update()
        {
            if (rack == null || aim == null) return;

            bool pressed = IsPressed();

            if (pressed && charging == false && CanStartCharge())
            {
                charging = true;
                charge01 = 0f;
                chargingIndex = rack.Selected;
                rack.Body(chargingIndex).isKinematic = true;
                rack.Grab(chargingIndex).enabled = false;
            }

            if (charging == false) return;

            if (pressed)
            {
                charge01 = Mathf.Min(1f, charge01 + Time.deltaTime / chargeSeconds);
                rack.Body(chargingIndex).transform.position = aim.position + aim.forward * muzzleDistance;
            }
            else
            {
                charging = false;
                fireQueued = true;
            }
        }

        private void OnEnable()
        {
            fireAction = QuestButtons.Create(QuestButton.A, "Launcher Fire", useTestKeys ? "<Keyboard>/f3" : null);
            fireAction.Enable();
        }

        private void OnDisable()
        {
            if (charging && rack != null) rack.Grab(chargingIndex).enabled = true;
            if (fireAction != null)
            {
                fireAction.Disable();
                fireAction.Dispose();
                fireAction = null;
            }
        }

        private bool IsPressed()
        {
            return inputEnabled && fireAction != null && fireAction.IsPressed();
        }

        private bool CanStartCharge()
        {
            if (rack.IsBusy(rack.Selected)) return false;
            return rightInteractor == null || rightInteractor.hasSelection == false;
        }

        private void FixedUpdate()
        {
            if (fireQueued == false) return;
            fireQueued = false;

            Rigidbody body = rack.Body(chargingIndex);
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            lastImpulse = Mathf.Lerp(minImpulse, maxImpulse, charge01);
            lastExpected = lastImpulse / body.mass;
            body.AddForce(aim.forward * lastImpulse, ForceMode.Impulse);
            rack.Grab(chargingIndex).enabled = true;
            if (probe != null) probe.BeginFlight(chargingIndex, true);
            charge01 = 0f;
            hasFired = true;
        }

        private void LateUpdate()
        {
            if (statusText == null || rack == null) return;

            string state;

            if (charging)
            {
                int cells = Mathf.Clamp(Mathf.FloorToInt(charge01 * 5f + 0.001f), 0, 5);
                state = "충전 " + new string('■', cells) + new string('□', 5 - cells) + $" {Mathf.RoundToInt(charge01 * 100f)}%";
            }
            else
            {
                state = "오른손 A 를 누르고 있으면 충전";
            }

            string last = hasFired
                ? $"마지막 : J {lastImpulse:F2} N·s → J / m = {lastExpected:F2} m/s"
                : "마지막 : 아직 없음";

            statusText.text = $"발사기 · {rack.Label(rack.Selected)} · {state}\n{last}";
        }
    }
}
