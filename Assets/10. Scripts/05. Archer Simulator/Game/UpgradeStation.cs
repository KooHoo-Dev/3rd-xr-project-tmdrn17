using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcherSimulator
{
    /// <summary>
    /// 강화 작업대. 거치대 소켓에 활이 올라가 있고, 로비 상태일 때만 강화 메뉴가 열린다.
    /// 게임을 시작하면(GameManager.CanUpgrade == false) 활을 올려놔도 강화가 진행되지 않는다.
    /// 단계별 수치는 StreamingAssets/BowUpgrade.tsv에서 읽는다 (BowUpgradeTable).
    /// </summary>
    public class UpgradeStation : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private XRSocketInteractor bowSocket;
        [SerializeField] private BowController bow;

        [Header("UI")]
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text currentText;
        [SerializeField] private TMP_Text nextText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button upgradeButton;

        [Tooltip("시작 강화 단계 (0 = Lv1). 단계별 수치는 StreamingAssets/BowUpgrade.tsv")]
        [SerializeField] private int level;

        private readonly BowUpgradeTable table = new();

        private const string HintDock = "활을 거치대에 올려두면\n강화 메뉴가 열립니다";
        private const string HintLocked = "게임 진행 중에는\n강화할 수 없습니다";

        public int Level => level;
        public BowUpgradeTable Table => table;
        public BowUpgradeDef CurrentLevel => table.Get(level);
        public bool MaxLevel => level >= table.MaxLevel;

        private GameManager Manager => gameManager != null ? gameManager : GameManager.Instance;
        private bool IsBowDocked => bowSocket != null && bowSocket.hasSelection;
        private bool IsUpgradeAllowed => Manager == null || Manager.CanUpgrade;

        private void Awake()
        {
            if (!table.Load())
                Debug.LogError("[UpgradeStation] BowUpgrade.tsv를 읽지 못해 강화를 사용할 수 없습니다.", this);
        }

        private void OnEnable()
        {
            if (bowSocket != null)
            {
                bowSocket.selectEntered.AddListener(OnSocketChanged);
                bowSocket.selectExited.AddListener(OnSocketChanged);
            }
            if (upgradeButton != null) upgradeButton.onClick.AddListener(TryUpgrade);
            if (Manager != null) Manager.StateChanged += OnGameStateChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (bowSocket != null)
            {
                bowSocket.selectEntered.RemoveListener(OnSocketChanged);
                bowSocket.selectExited.RemoveListener(OnSocketChanged);
            }
            if (upgradeButton != null) upgradeButton.onClick.RemoveListener(TryUpgrade);
            if (Manager != null) Manager.StateChanged -= OnGameStateChanged;
        }

        private void Start()
        {
            level = Mathf.Clamp(level, 0, Mathf.Max(0, table.MaxLevel));
            ApplyLevelToBow();
            Refresh();
        }

        private void OnSocketChanged(BaseInteractionEventArgs args) => Refresh();
        private void OnGameStateChanged(GameState state) => Refresh();

        /// <summary>강화 버튼. 거치대에 활이 있고 로비 상태일 때만 진행된다.</summary>
        public void TryUpgrade()
        {
            if (!IsBowDocked || !IsUpgradeAllowed) return;
            if (MaxLevel) return; // 최고 단계

            // TODO: 포인트 시스템이 생기면 next.Cost만큼 차감하고, 부족하면 막는다.
            level++;
            if (bow != null) bow.ClearArrows(); // 이전 단계로 만들어 둔 화살·이펙트는 지운다
            ApplyLevelToBow();
            Refresh();
        }

        private void ApplyLevelToBow()
        {
            BowUpgradeDef current = CurrentLevel;
            if (bow == null || current == null) return;
            bow.MaxArrowSpeed = current.ArrowSpeed;
            bow.ArrowDamage = current.Damage;
            bow.ArrowEffect = current.ArrowEffect; // 화살이 맞았을 때 터지는 이펙트 (TSV의 ArrowEffect)
            bow.SetVisualLevel(current.VisualLevel); // 단계별 활 외형 (TSV의 VisualLevel)
        }

        /// <summary>" · 효과 이름". 활에 이름이 등록되지 않은 효과면 빈 문자열.</summary>
        private string EffectText(BowUpgradeDef def)
        {
            string name = bow != null ? bow.GetArrowEffectName(def.ArrowEffect) : "";
            return string.IsNullOrEmpty(name) ? "" : $" · {name}";
        }

        private void Refresh()
        {
            bool docked = IsBowDocked;
            bool allowed = IsUpgradeAllowed;

            if (upgradePanel != null) upgradePanel.SetActive(docked && allowed);
            if (hintText != null)
            {
                hintText.gameObject.SetActive(!(docked && allowed));
                hintText.text = allowed ? HintDock : HintLocked;
            }

            BowUpgradeDef current = CurrentLevel;
            BowUpgradeDef next = table.Get(MaxLevel ? level : level + 1);
            if (current == null)
            {
                if (currentText != null) currentText.text = "강화 데이터 없음";
                if (nextText != null) nextText.text = "";
                if (costText != null) costText.text = "";
                if (upgradeButton != null) upgradeButton.interactable = false;
                return;
            }

            if (currentText != null) currentText.text = $"현재  {current.DisplayName}\n{current.StatText}";
            
            if (nextText != null) nextText.text = MaxLevel ? "최고 단계입니다" : $"다음  {next.DisplayName}\n{next.StatText}{EffectText(next)}";
            if (costText != null) costText.text = MaxLevel ? "" : $"비용 {next.Cost:N0} P";
            if (upgradeButton != null) upgradeButton.interactable = !MaxLevel;
        }
    }
}
