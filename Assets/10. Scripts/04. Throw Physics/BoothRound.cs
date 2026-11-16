using StudyXR.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StudyXR.ThrowPhysics
{
    public class BoothRound : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BallRack rack;
        [SerializeField] private ScoreTarget[] targets;
        [SerializeField] private CanPyramidJudge cans;
        [SerializeField] private BasketSensor basket;

        [Header("표시")]
        [SerializeField] private TMP_Text boardText;

        [Header("라운드")]
        [SerializeField] private float roundSeconds = 60f;

        [Header("시뮬레이터 테스트 입력")]
        [SerializeField] private bool useTestKeys = true;

        private bool running;
        private bool finished;
        private float remaining;
        private int score;
        private int best;
        private string lastDetail = "";
        private bool inputEnabled = true;
        private InputAction startAction;
        private InputAction resetAction;

        public bool IsAccepting => (finished == false);

        public void StartRound()
        {
            ResetField();
            score = 0;
            remaining = roundSeconds;
            running = true;
            finished = false;
            lastDetail = "시작!";
        }

        public void SetInputEnabled(bool on)
        {
            inputEnabled = on;
        }

        public void ResetField()
        {
            if (targets != null)
                for (int i = 0; i < targets.Length; ++i) targets[i].ResetTarget();
            if (cans != null) cans.ResetCans();
            if (basket != null) basket.ClearAll();
            if (rack != null) rack.ReturnAll();
        }

        public void AddScore(int points, string detail)
        {
            if (finished) return;
            score += points;
            lastDetail = detail;
        }

        private void OnEnable()
        {
            startAction = QuestButtons.Create(QuestButton.A, "Round Start", useTestKeys ? "<Keyboard>/f3" : null);
            resetAction = QuestButtons.Create(QuestButton.B, "Field Reset", useTestKeys ? "<Keyboard>/f5" : null);
            startAction.Enable();
            resetAction.Enable();
        }

        private void OnDisable()
        {
            startAction.Disable();
            startAction.Dispose();
            resetAction.Disable();
            resetAction.Dispose();
        }

        private void Update()
        {
            ReadButtons();

            if (running == false) return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                running = false;
                finished = true;
                best = Mathf.Max(best, score);
                lastDetail = "끝! 다시 하려면 라운드 시작";
            }
        }

        private void ReadButtons()
        {
            if (inputEnabled == false) return;
            if (startAction.WasPressedThisFrame()) StartRound();
            if (resetAction.WasPressedThisFrame()) ResetField();
        }

        private void LateUpdate()
        {
            if (boardText == null) return;

            string state = running ?
                $"남은 시간 {Mathf.CeilToInt(remaining)} 초"
                : finished
                    ? "라운드 끝"
                    : "연습 중 — 오른손 A : 라운드 시작";
            boardText.text = $"{state}\n점수 {score} · 최고 {best}\n{lastDetail}";
        }
    }
}
