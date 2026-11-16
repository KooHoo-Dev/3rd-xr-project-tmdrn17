using StudyXR.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.ThrowPhysics
{
    public class BallRack : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Rigidbody[] balls;
        [SerializeField] private XRGrabInteractable[] grabs;
        [SerializeField] private Respawnable[] respawns;
        [SerializeField] private Transform hand;
        [SerializeField] private ThrowProbe probe;

        [Header("회수")]
        [SerializeField] private bool autoReturn = true;
        [SerializeField] private float returnAfter = 1.5f;
        [SerializeField] private float returnDistance = 0.5f;
        [SerializeField] private float restSpeed = 0.05f;
        [SerializeField] private float floorHeight = 0.15f;
        [SerializeField] private float floorSpeed = 0.3f;

        [Header("버튼")]
        [SerializeField] private bool useSelectButton = true;

        [Header("시험 던지기")]
        [SerializeField] private float testHandSpeed = 9f;
        [SerializeField] private float testLoftDegrees = 25f;
        [SerializeField] private bool advanceOnTestThrow = false;

        [Header("시뮬레이터 테스트 입력")]
        [SerializeField] private bool useTestKeys = true;

        private Vector3[] home;
        private float[] restTimer;
        private bool testThrowQueued;
        private bool inputEnabled = true;
        private InputAction selectAction;
        private InputAction testThrowAction;

        public int Count => balls != null ? balls.Length : 0;
        public int Selected { get; private set; }

        public Rigidbody Body(int i) { return balls[i]; }
        public XRGrabInteractable Grab(int i) { return grabs[i]; }
        public string Label(int i) { return $"{balls[i].mass:F2} kg 공"; }

        public int IndexOf(Transform t)
        {
            for (int i = 0; i < Count; ++i)
                if (balls[i].transform == t) return i;
            return -1;
        }

        public bool IsBusy(int i)
        {
            return grabs[i].isSelected || balls[i].isKinematic;
        }

        public void SetInputEnabled(bool on)
        {
            inputEnabled = on;
            if (on == false) testThrowQueued = false;
        }

        private void SelectNext()
        {
            if (Count > 0) Selected = (Selected + 1) % Count;
        }

        public void ReturnBall(int i)
        {
            if (IsBusy(i)) return;
            respawns[i].RespawnNow();
            restTimer[i] = 0f;
        }

        public void ReturnAll()
        {
            for (int i = 0; i < Count; ++i) ReturnBall(i);
        }

        private void OnEnable()
        {
            if (useSelectButton)
                selectAction = QuestButtons.Create(QuestButton.B, "Ball Select", useTestKeys ? "<Keyboard>/f1" : null);
            testThrowAction = QuestButtons.Create(QuestButton.LeftStickClick, "Test Throw", useTestKeys ? "<Keyboard>/f4" : null);
            if (selectAction != null) selectAction.Enable();
            testThrowAction.Enable();
        }

        private void OnDisable()
        {
            if (selectAction != null)
            {
                selectAction.Disable();
                selectAction.Dispose();
                selectAction = null;
            }
            if (testThrowAction != null)
            {
                testThrowAction.Disable();
                testThrowAction.Dispose();
                testThrowAction = null;
            }
        }

        private void Start()
        {
            home = new Vector3[Count];
            restTimer = new float[Count];
            for (int i = 0; i < Count; ++i) home[i] = balls[i].transform.position;
        }

        private void Update()
        {
            ReadButtons();
            if (autoReturn) UpdateReturn();
        }

        private void ReadButtons()
        {
            if (inputEnabled == false) return;
            if (selectAction != null && selectAction.WasPressedThisFrame()) SelectNext();
            if (testThrowAction != null && testThrowAction.WasPressedThisFrame()) testThrowQueued = true;
        }

        private void UpdateReturn()
        {
            for (int i = 0; i < Count; ++i)
            {
                if (IsBusy(i))
                {
                    restTimer[i] = 0f;
                    continue;
                }

                Vector3 pos = balls[i].transform.position;
                Vector3 v = balls[i].linearVelocity;
                bool slow = (v.magnitude < restSpeed);
                bool rollingOnFloor = pos.y < floorHeight && new Vector2(v.x, v.z).magnitude < floorSpeed;
                bool settled = slow || rollingOnFloor;

                if (settled && (pos - home[i]).magnitude > returnDistance)
                {
                    restTimer[i] += Time.deltaTime;
                    if (restTimer[i] >= returnAfter) ReturnBall(i);
                }
                else
                {
                    restTimer[i] = 0f;
                }
            }
        }

        private void FixedUpdate()
        {
            if (testThrowQueued == false) return;
            testThrowQueued = false;

            if (Count == 0 || hand == null || IsBusy(Selected)) return;

            Rigidbody body = balls[Selected];
            Vector3 dir = Quaternion.AngleAxis(-testLoftDegrees, hand.right) * hand.forward;
            body.position = hand.position + hand.forward * 0.1f;
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = dir * testHandSpeed * grabs[Selected].throwVelocityScale;
            if (probe != null) probe.BeginFlight(Selected, false);
            if (advanceOnTestThrow) SelectNext();
        }
    }
}
