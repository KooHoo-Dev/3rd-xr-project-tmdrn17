using System;
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
    /// </summary>
    public class UpgradeStation : MonoBehaviour
    {
        [Serializable]
        public class BowLevel
        {
            public string displayName;
            public float range;
            public int damage;
            public int cost;
            [Tooltip("최대 당김 시 화살 속도 (m/s)")]
            public float arrowSpeed;
            public string effect;
        }

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

        [Header("활 단계 (기획서 4. 임시 수치)")]
        [SerializeField] private BowLevel[] levels =
        {
            new BowLevel { displayName = "Lv1 나무 활", range = 20f, damage = 40, cost = 0, arrowSpeed = 25f, effect = "기본 화살" },
            new BowLevel { displayName = "Lv2 강화 나무 활", range = 30f, damage = 60, cost = 500, arrowSpeed = 32f, effect = "흰색 트레일" },
            new BowLevel { displayName = "Lv3 철제 활", range = 40f, damage = 85, cost = 1200, arrowSpeed = 40f, effect = "불꽃 트레일 + 작은 폭발" },
            new BowLevel { displayName = "Lv4 정령의 활", range = 55f, damage = 120, cost = 2500, arrowSpeed = 50f, effect = "번개 트레일 + 큰 폭발" },
        };

        [SerializeField] private int level;

        private const string HintDock = "활을 거치대에 올려두면\n강화 메뉴가 열립니다";
        private const string HintLocked = "게임 진행 중에는\n강화할 수 없습니다";

        public int Level => level;
        public BowLevel CurrentLevel => levels[level];

        private GameManager Manager => gameManager != null ? gameManager : GameManager.Instance;
        private bool IsBowDocked => bowSocket != null && bowSocket.hasSelection;
        private bool IsUpgradeAllowed => Manager == null || Manager.CanUpgrade;

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
            level = Mathf.Clamp(level, 0, levels.Length - 1);
            ApplyLevelToBow();
            Refresh();
        }

        private void OnSocketChanged(BaseInteractionEventArgs args) => Refresh();
        private void OnGameStateChanged(GameState state) => Refresh();

        /// <summary>강화 버튼. 거치대에 활이 있고 로비 상태일 때만 진행된다.</summary>
        public void TryUpgrade()
        {
            if (!IsBowDocked || !IsUpgradeAllowed) return;
            if (level >= levels.Length - 1) return;

            // TODO: 포인트 시스템이 생기면 levels[level + 1].cost만큼 차감하고, 부족하면 막는다.
            level++;
            ApplyLevelToBow();
            Refresh();
        }

        private void ApplyLevelToBow()
        {
            if (bow == null || levels.Length == 0) return;
            bow.MaxArrowSpeed = CurrentLevel.arrowSpeed;
            bow.ArrowDamage = CurrentLevel.damage;
            bow.SetVisualLevel(level); // 단계별 활 외형
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

            if (levels.Length == 0) return;
            BowLevel current = CurrentLevel;
            if (currentText != null) currentText.text = $"현재  {current.displayName}\n사거리 {current.range:0}m · 데미지 {current.damage}";

            bool maxed = level >= levels.Length - 1;
            if (nextText != null)
            {
                nextText.text = maxed
                    ? "최고 단계입니다"
                    : $"다음  {levels[level + 1].displayName}\n사거리 {levels[level + 1].range:0}m · 데미지 {levels[level + 1].damage} · {levels[level + 1].effect}";
            }
            if (costText != null) costText.text = maxed ? "" : $"비용 {levels[level + 1].cost:N0} P";
            if (upgradeButton != null) upgradeButton.interactable = !maxed;
        }
    }
}
