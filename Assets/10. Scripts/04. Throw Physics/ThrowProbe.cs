using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace StudyXR.ThrowPhysics
{
    public class ThrowProbe : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BallRack rack;

        [Header("표시")]
        [SerializeField] private TMP_Text boardText;

        [Header("측정")]
        [SerializeField] private float landHeight = 0.06f;
        [SerializeField] private float maxFlightSeconds = 6f;

        private PhysicalThrow[] throws;
        private bool[] pending;
        private float[] pendingSince;
        private bool[] pendingLauncher;
        private bool[] flying;
        private Vector3[] startPos;
        private float[] startTime;
        private float[] handSpeed;
        private float[] launcherSpeed;

        private int lastIndex = -1;
        private int handPartIndex = -1;
        private bool lastLauncher;
        private float lastSpeed;
        private float lastSpin;
        private float lastHandPart = -1f;
        private float lastTurnPart = -1f;
        private float lastScale;
        private float lastSmoothing;
        private float lastDistance = -1f;
        private float lastAir;
        private bool lastLanded;

        private void Awake()
        {
            int n = rack != null ? rack.Count : 0;
            pending = new bool[n];
            pendingSince = new float[n];
            pendingLauncher = new bool[n];
            flying = new bool[n];
            startPos = new Vector3[n];
            startTime = new float[n];
            handSpeed = new float[n];
            launcherSpeed = new float[n];
            throws = new PhysicalThrow[n];
            for (int i = 0; i < n; ++i)
            {
                throws[i] = rack.Body(i).GetComponent<PhysicalThrow>();
                handSpeed[i] = -1f;
                launcherSpeed[i] = -1f;
            }
        }

        private void OnEnable()
        {
            if (rack == null) return;
            for (int i = 0; i < rack.Count; ++i) rack.Grab(i).selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (rack == null) return;
            for (int i = 0; i < rack.Count; ++i) rack.Grab(i).selectExited.RemoveListener(OnReleased);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled) return;
            int i = rack.IndexOf(args.interactableObject.transform);

            if (i < 0) return;
            BeginFlight(i, false);

            handPartIndex = i;
        }

        public void BeginFlight(int index, bool byLauncher)
        {
            pending[index] = true;
            pendingSince[index] = Time.fixedTime;
            pendingLauncher[index] = byLauncher;
            flying[index] = false;
            lastScale = rack.Grab(index).throwVelocityScale;
            lastSmoothing = rack.Grab(index).throwSmoothingDuration;
        }

        private void FixedUpdate()
        {
            if (rack == null) return;

            for (int i = 0; i < rack.Count; ++i)
            {
                Rigidbody body = rack.Body(i);

                if (pending[i] && Time.fixedTime > pendingSince[i])
                {
                    pending[i] = false;
                    float speed = body.linearVelocity.magnitude;

                    if (pendingLauncher[i]) launcherSpeed[i] = speed; else handSpeed[i] = speed;
                    lastIndex = i; lastLauncher = pendingLauncher[i];
                    lastSpeed = speed; lastSpin = body.angularVelocity.magnitude;
                    bool byHand = pendingLauncher[i] == false && handPartIndex == i && throws[i] != null;
                    lastHandPart = byHand ? throws[i].LastHandVelocity.magnitude : -1f;
                    lastTurnPart = byHand ? throws[i].LastTurnVelocity.magnitude : -1f;
                    if (handPartIndex == i) handPartIndex = -1;
                    lastDistance = -1f; lastLanded = false;
                    startPos[i] = body.position; startTime[i] = Time.fixedTime; flying[i] = true;
                }
                else if (flying[i])
                {
                    float t = Time.fixedTime - startTime[i];

                    if (rack.Grab(i).isSelected) flying[i] = false;
                    else if (body.position.y <= landHeight && t > 0.05f) Land(i, body, t);
                    else if (t > maxFlightSeconds) flying[i] = false;
                }
            }
        }

        private void Land(
            int i,
            Rigidbody body,
            float t)
        {
            flying[i] = false;
            if (i != lastIndex) return;

            Vector3 d = body.position - startPos[i];
            d.y = 0f;
            lastDistance = d.magnitude;
            lastAir = t;
            lastLanded = true;
        }

        private void LateUpdate()
        {
            if (boardText == null || rack == null || rack.Count == 0) return;

            string text;

            if (lastIndex < 0)
            {
                text = "던져 보세요 — 트리거 : 잡고 놓기 · 왼손 스틱 누르기 : 시험 던지기";
            }
            else
            {
                string head = rack.Label(lastIndex) + " · " + (lastLauncher ? "발사기로" : "손으로");

                if (lastLauncher == false) head += $" · x{lastScale:F2} · {lastSmoothing:F2} s";

                string result;

                if (flying[lastIndex]) result = "날아가는 중…";
                else if (lastLanded) result = $"비거리 {lastDistance:F2} m · 체공 {lastAir:F2} s";
                else result = "바닥에 닿지 않음 (측정 못 함)";

                string parts = lastHandPart >= 0f ? $" (손 {lastHandPart:F2} · ω×r {lastTurnPart:F2})" : "";
                text = head + "\n" + $"놓는 순간 {lastSpeed:F2} m/s{parts} · 회전 {lastSpin:F1} rad/s" + "\n" + result;
            }

            if (rack.Count >= 2)
            {
                text += $"\n손     {rack.Label(0)} {Format(handSpeed[0])} · {rack.Label(1)} {Format(handSpeed[1])} m/s";
                text += $"\n발사기 {rack.Label(0)} {Format(launcherSpeed[0])} · {rack.Label(1)} {Format(launcherSpeed[1])} m/s";
            }

            boardText.text = text;
        }

        private static string Format(float speed)
        {
            return speed < 0f ? "—" : speed.ToString("F2");
        }
    }
}
