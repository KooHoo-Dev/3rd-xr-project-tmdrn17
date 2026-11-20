using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace ArcherSimulator
{
    public enum GameState
    {
        Lobby,   // 스테이지·난이도 선택, 강화 가능 (사격 불가)
        Ready,   // 3초 카운트다운
        Playing, // 사격 (화살 수 제한)
        Result,  // 결과 화면
    }

    /// <summary>
    /// 로비 → 준비 → 플레이 → 결과 상태를 관리한다.
    /// 게임을 시작하면(Ready 이후) 강화가 막힌다 — UpgradeStation이 CanUpgrade를 확인한다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class StageSetup
        {
            public string displayName = "훈련장";
            public int shots = 10;
            public GameObject root;
            [Tooltip("난이도 거리만큼 z를 옮기는 표적 슬롯 루트")]
            public Transform targetSlots;
        }

        [Serializable]
        public class DifficultySetup
        {
            public string displayName = "쉬움";
            public float distance = 10f;
            public int targetHp = 100;
            public float scoreMultiplier = 1f;
        }

        public static GameManager Instance { get; private set; }

        [Header("스테이지 (기획서 1차 구현)")]
        [SerializeField] private StageSetup[] stages =
        {
            new StageSetup { displayName = "훈련장", shots = 10 },
            new StageSetup { displayName = "숲속 사격장", shots = 15 },
            new StageSetup { displayName = "성벽", shots = 18 },
        };

        [Header("난이도 (기획서 임시 수치)")]
        [SerializeField] private DifficultySetup[] difficulties =
        {
            new DifficultySetup { displayName = "쉬움", distance = 10f, targetHp = 100, scoreMultiplier = 1f },
            new DifficultySetup { displayName = "보통", distance = 20f, targetHp = 200, scoreMultiplier = 1.5f },
            new DifficultySetup { displayName = "어려움", distance = 35f, targetHp = 350, scoreMultiplier = 2.5f },
        };

        [Header("점수 (기획서 임시 수치)")]
        [SerializeField] private ScoreRule scoreRule = new ScoreRule();

        [Header("연결")]
        [SerializeField] private BowController bow;

        [Header("UI")]
        [SerializeField] private TMP_Text selectionText;
        [SerializeField] private TMP_Text countdownText;
        [Tooltip("타이틀 게시판의 남은 화살 표시")]
        [SerializeField] private TMP_Text arrowsText;
        [Tooltip("타이틀 게시판의 점수 표시")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private GameObject resultBoard;
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultDetailText;
        [SerializeField] private TMP_Text resultTotalText;
        [SerializeField] private TMP_Text resultStatsText;
        [Tooltip("결과 화면의 최고 기록 표시")]
        [SerializeField] private TMP_Text resultRecordText;

        [Header("진행")]
        [SerializeField] private int countdownSeconds = 3;
        [Tooltip("마지막 화살을 쏜 뒤 결과 화면까지 대기 (화살이 날아가는 시간)")]
        [SerializeField] private float resultDelay = 2.5f;

        private int stageIndex;
        private int difficultyIndex;
        private int arrowsLeft;
        private int arrowsFired;
        private readonly RoundScore score = new RoundScore();
        private Target[] stageTargets = Array.Empty<Target>(); // 현재 스테이지의 표적
        private readonly Dictionary<(int stage, int difficulty), int> bestScores = new();
        private Coroutine flow;

        public GameState State { get; private set; } = GameState.Lobby;
        public event Action<GameState> StateChanged;
        public event Action RestartStage;

        /// <summary>로비에서만 강화할 수 있다. 게임을 시작하면(Ready·Playing·Result) 막힌다.</summary>
        public bool CanUpgrade => State == GameState.Lobby;

        public StageSetup CurrentStage => stages[stageIndex];
        public DifficultySetup CurrentDifficulty => difficulties[difficultyIndex];
        public int ArrowsLeft => arrowsLeft;
        public int Score => score.Total(scoreRule);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] 씬에 GameManager가 둘 이상 있습니다.", this);
                return;
            }
            Instance = this;
            
            PointWallet.Add(100000);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (bow != null)
            {
                bow.CanNock = CanNockArrow;
                bow.ArrowFired += OnArrowFired;
            }

            // 모든 스테이지의 표적 명중을 받는다 (현재 스테이지·Playing일 때만 점수 반영)
            foreach (StageSetup stage in stages)
            {
                if (stage.targetSlots == null) continue;
                foreach (Target target in stage.targetSlots.GetComponentsInChildren<Target>(true))
                    target.Hit += OnTargetHit;
            }

            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (resultBoard != null) resultBoard.SetActive(false);
            ApplySelection();
            SetState(GameState.Lobby);
        }

        // ───────── 선택 (레이 UI 버튼) ─────────

        public void SelectStage(int index)
        {
            if (State != GameState.Lobby || stages.Length == 0) return;
            stageIndex = Mathf.Clamp(index, 0, stages.Length - 1);
            ApplySelection();
        }

        public void SelectDifficulty(int index)
        {
            if (State != GameState.Lobby || difficulties.Length == 0) return;
            difficultyIndex = Mathf.Clamp(index, 0, difficulties.Length - 1);
            ApplySelection();
        }

        /// <summary>선택한 스테이지만 켜고, 표적 슬롯을 난이도 거리로 옮긴다.</summary>
        private void ApplySelection()
        {
            for (int i = 0; i < stages.Length; i++)
            {
                StageSetup stage = stages[i];
                if (stage.root != null) stage.root.SetActive(i == stageIndex);
                if (stage.targetSlots == null) continue;
                Vector3 p = stage.targetSlots.localPosition;
                stage.targetSlots.localPosition = new Vector3(p.x, p.y, CurrentDifficulty.distance);
            }

            stageTargets = CurrentStage.targetSlots != null
                ? CurrentStage.targetSlots.GetComponentsInChildren<Target>(true)
                : Array.Empty<Target>();
            ResetTargets();

            if (selectionText != null)
                selectionText.text = $"선택: {CurrentStage.displayName} / {CurrentDifficulty.displayName}";
            UpdateHud();
        }

        // ───────── 진행 ─────────

        /// <summary>시작 제단의 Poke 버튼.</summary>
        public void StartGame()
        {
            if (State != GameState.Lobby) return;
            flow = StartCoroutine(RunRound());
        }

        private IEnumerator RunRound()
        {
            arrowsLeft = CurrentStage.shots;
            arrowsFired = 0;
            score.Reset(CurrentDifficulty.scoreMultiplier);
            ResetTargets();
            if (bow != null) bow.PrepareArrows(CurrentStage.shots); // 최대 화살 수만큼 화살·이펙트 생성
            SetState(GameState.Ready);

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                for (int n = countdownSeconds; n > 0; n--)
                {
                    countdownText.text = n.ToString();
                    yield return new WaitForSeconds(1f);
                }
                countdownText.text = "시작!";
            }

            SetState(GameState.Playing);

            if (countdownText != null)
            {
                yield return new WaitForSeconds(0.7f);
                countdownText.gameObject.SetActive(false);
            }
            flow = null;
        }

        /// <summary>게임 중(Playing)이고 화살이 남았을 때만 장전할 수 있다. 로비에서는 쏠 수 없다.</summary>
        private bool CanNockArrow() => State == GameState.Playing && arrowsLeft > 0;

        /// <summary>게임 종료: 당기던 화살과 날아가는·꽂힌 화살을 모두 끈다.</summary>
        private void DespawnAllArrows()
        {
            if (bow == null) return;
            bow.CancelDraw();
            if (bow.ArrowPool != null) bow.ArrowPool.DespawnAll();
        }

        private void OnArrowFired(Arrow arrow, float pull)
        {
            if (State != GameState.Playing) return;
            arrowsLeft = Mathf.Max(0, arrowsLeft - 1);
            arrowsFired++;
            UpdateHud();

            if (arrowsLeft == 0 && flow == null) flow = StartCoroutine(FinishAfterDelay());
        }

        // ───────── 점수 ─────────

        /// <summary>현재 스테이지 표적을 난이도 HP로 채우고 다시 켠다.</summary>
        private void ResetTargets()
        {
            foreach (Target target in stageTargets)
                if (target != null) target.ResetTarget(CurrentDifficulty.targetHp);
        }

        private void OnTargetHit(Target target, int damage, bool weakPoint)
        {
            if (State != GameState.Playing || Array.IndexOf(stageTargets, target) < 0) return;

            score.Hits++;
            if (weakPoint) score.WeakPointHits++;
            if (target.IsDestroyed) score.TargetsDestroyed++;
            UpdateHud();

            if (score.TargetsDestroyed >= stageTargets.Length) EndRound(true); // 모든 표적 파괴
        }

        /// <summary>이번 플레이 동안의 스테이지·난이도별 최고 기록을 갱신하면 true. (저장하지 않음)</summary>
        private bool UpdateBest(int total, out int best)
        {
            var key = (stageIndex, difficultyIndex);
            bestScores.TryGetValue(key, out best);
            if (total <= best) return false;
            best = total;
            bestScores[key] = best;
            return true;
        }

        private IEnumerator FinishAfterDelay()
        {
            yield return new WaitForSeconds(resultDelay);
            flow = null;
            EndRound(false);
        }

        /// <summary>라운드 종료. 모든 표적을 부수면 true(클리어), 화살이 떨어지면 false.</summary>
        public void EndRound(bool cleared)
        {
            if (State != GameState.Playing) return;
            StopFlow();
            DespawnAllArrows();

            score.ArrowsLeft = cleared ? arrowsLeft : 0; // 남은 화살 보너스는 클리어했을 때만
            int total = score.Total(scoreRule);
            bool newRecord = UpdateBest(total, out int best);
            PointWallet.Add(total); // 획득 점수 = 강화 재료 포인트
            int accuracy = arrowsFired > 0 ? Mathf.RoundToInt(100f * score.Hits / arrowsFired) : 0;

            SetState(GameState.Result);

            resultBoard.SetActive(true);
            resultTitleText.SetText(cleared ? "클리어!" : "실패");
            resultDetailText.SetText(score.Breakdown(scoreRule));
            resultTotalText.SetText($"획득 점수  {total:N0}");
            resultStatsText.SetText($"명중률 {accuracy}%  ·  발사 {arrowsFired}발  ·  남은 화살 {arrowsLeft}");
            resultRecordText.SetText((newRecord ? $"신기록!  최고 기록 {best:N0}" : $"최고 기록 {best:N0}") +
                                     $"  ·  +{total:N0} P (보유 {PointWallet.Points:N0} P)");
        }

        /// <summary>결과 화면 '다시 하기'</summary>
        public void Retry()
        {
            if (State != GameState.Result) return;
            if (resultBoard != null) resultBoard.SetActive(false);
            ReturnToLobby();
            RestartStage?.Invoke();
        }
        
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>결과 화면 '로비로' (진행 중 취소에도 사용 가능)</summary>
        public void ReturnToLobby()
        {
            if (State == GameState.Lobby) return;
            StopFlow();
            DespawnAllArrows();
            if (resultBoard != null) resultBoard.SetActive(false);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            ResetTargets(); // 부서진 표적을 로비에서 다시 보이게
            SetState(GameState.Lobby);
        }

        private void StopFlow()
        {
            if (flow == null) return;
            StopCoroutine(flow);
            flow = null;
        }

        private void SetState(GameState next)
        {
            State = next;
            UpdateHud();
            StateChanged?.Invoke(next);
        }

        private void UpdateHud()
        {
            if (arrowsText != null)
                arrowsText.text = State == GameState.Lobby ? $"화살 {CurrentStage.shots}발" : $"화살 {arrowsLeft} / {CurrentStage.shots}";
            if (scoreText != null)
                scoreText.text = State == GameState.Lobby ? CurrentDifficulty.displayName : $"점수 {Score:N0}";
        }
    }
}
