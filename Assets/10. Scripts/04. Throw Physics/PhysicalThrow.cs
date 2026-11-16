using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.ThrowPhysics
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PhysicalThrow : MonoBehaviour
    {
        private const int Capacity = 64;
        private const float JumpSpeed = 30f;

        [Header("놓기")]
        [SerializeField] private float maxReleaseSpeed = 25f;

        [Header("회전 감쇠")]
        [SerializeField] private float flightAngularDamping = 0.05f;
        [SerializeField] private float rollAngularDamping = 2.5f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Transform hand;
        private Vector3 leverLocal;

        private readonly float[] times = new float[Capacity];
        private readonly Vector3[] positions = new Vector3[Capacity];
        private readonly Quaternion[] rotations = new Quaternion[Capacity];
        private int count;
        private int newest = -1;

        private readonly float[] midTimes = new float[Capacity];
        private readonly Vector3[] linear = new Vector3[Capacity];
        private readonly Vector3[] angular = new Vector3[Capacity];

        public Vector3 LastHandVelocity { get; private set; }
        public Vector3 LastTurnVelocity { get; private set; }

        private bool restoreInterpolation;
        private RigidbodyInterpolation savedInterpolation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
            hand = null;
            if (restoreInterpolation)
            {
                restoreInterpolation = false;
                body.interpolation = savedInterpolation;
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            hand = args.interactorObject.transform;
            leverLocal = Quaternion.Inverse(hand.rotation) * (transform.position - hand.position);
            count = 0;
            newest = -1;
        }

        private void LateUpdate()
        {
            if (hand == null || grab.isSelected == false) return;
            leverLocal = Quaternion.Inverse(hand.rotation) * (transform.position - hand.position);
            Record(Time.time, hand.position, hand.rotation);
        }

        private void Record(
            float t,
            Vector3 pos,
            Quaternion rot)
        {
            if (count > 0)
            {
                float dt = t - times[newest];

                if (dt <= 0f) return;
                if ((pos - positions[newest]).magnitude / dt > JumpSpeed) count = 0;
            }
            newest = (newest + 1) % Capacity;
            times[newest] = t;
            positions[newest] = pos;
            rotations[newest] = rot;
            count = Mathf.Min(count + 1, Capacity);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            Transform h = hand;
            hand = null;
            if (h == null || args.isCanceled || body.isKinematic) return;

            Record(Time.time, h.position, h.rotation);

            float window = Mathf.Max(0.005f, grab.throwSmoothingDuration);
            Vector3 handVelocity;
            Vector3 spin;
            EstimateAtRelease(window, out handVelocity, out spin);

            Vector3 r = h.rotation * leverLocal;
            Vector3 turn = Vector3.Cross(spin, r);
            float scale = grab.throwVelocityScale;
            LastHandVelocity = handVelocity * scale;
            LastTurnVelocity = turn * scale;
            Vector3 releaseVelocity = Vector3.ClampMagnitude((handVelocity + turn) * scale, maxReleaseSpeed);
            Vector3 releaseSpin = spin * grab.throwAngularVelocityScale;

            Vector3 start = h.position + r;

            if (restoreInterpolation == false)
            {
                savedInterpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation.None;
                restoreInterpolation = true;
            }
            body.position = start;
            transform.position = start;
            body.linearVelocity = releaseVelocity;
            body.angularVelocity = releaseSpin;
            body.angularDamping = flightAngularDamping;
        }

        private void FixedUpdate()
        {
            if (restoreInterpolation == false) return;
            restoreInterpolation = false;
            body.interpolation = savedInterpolation;
        }

        private void EstimateAtRelease(
            float window,
            out Vector3 handVelocity,
            out Vector3 spin)
        {
            handVelocity = Vector3.zero;
            spin = Vector3.zero;
            if (count < 2) return;

            float end = times[newest];
            int n = 0;
            for (int k = 0; k < count - 1; ++k)
            {
                int b = (newest - k + Capacity) % Capacity;
                int a = (b - 1 + Capacity) % Capacity;
                float dt = times[b] - times[a];
                float mid = (times[a] + times[b]) * 0.5f;

                if (n >= 2 && end - mid > window) break;

                Quaternion dq = rotations[b] * Quaternion.Inverse(rotations[a]);
                dq.ToAngleAxis(out float degrees, out Vector3 axis);
                if (degrees > 180f) degrees -= 360f;
                Vector3 w = axis * (degrees * Mathf.Deg2Rad / dt);

                if (float.IsNaN(w.x) || float.IsInfinity(w.x)) w = Vector3.zero;

                midTimes[n] = mid;
                linear[n] = (positions[b] - positions[a]) / dt;
                angular[n] = w;
                n++;
            }

            handVelocity = TrendAt(linear, n, end);
            spin = TrendAt(angular, n, end);
        }

        private Vector3 TrendAt(
            Vector3[] values,
            int n,
            float at)
        {
            float meanT = 0f;
            Vector3 meanV = Vector3.zero;
            for (int i = 0; i < n; ++i)
            {
                meanT += midTimes[i];
                meanV += values[i];
            }
            meanT /= n;
            meanV /= n;
            if (n < 3) return meanV;

            float stt = 0f;
            Vector3 svt = Vector3.zero;
            for (int i = 0; i < n; ++i)
            {
                float d = midTimes[i] - meanT;
                stt += d * d;
                svt += d * (values[i] - meanV);
            }
            return stt > 1e-8f ? meanV + svt / stt * (at - meanT) : meanV;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (grab.isSelected == false) body.angularDamping = rollAngularDamping;
        }
    }
}
