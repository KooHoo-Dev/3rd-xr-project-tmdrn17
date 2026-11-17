using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcherSimulator
{
    /// <summary>
    /// 활 본체.
    /// 1) 먼저 잡은 손이 활을 잡고, 다른 손으로 시위를 잡으면 화살이 장전된다.
    /// 2) 시위를 당긴 정도로 활 당김 애니메이션을 재생한다.
    /// 3) 시위를 놓으면 당김 정도 × 최대 속도로 화살을 쏜다.
    /// 4) 다시 시위를 잡으면 1)부터 반복.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class BowController : MonoBehaviour
    {
        /// <summary>강화 단계별 활 외형. 해당 단계에서 켜지는 모델, 그 모델의 Animator, 쉬는 시위 위치.</summary>
        [Serializable]
        public class LevelVisual
        {
            public GameObject root;
            public Animator animator;
            [Tooltip("이 활의 쉬는 시위(화살 오늬) 위치. 단계가 바뀌면 시위 잡는 곳이 여기로 옮겨진다.")]
            public Transform stringRest;
        }

        /// <summary>화살 효과 한 단계의 이름 (강화 메뉴 표시용). 이펙트 자체는 ArrowPool의 단계별 화살 프리팹에 들어 있다.</summary>
        [Serializable]
        public class ArrowEffectVisual
        {
            public string displayName;
        }

        [Header("연결")]
        [SerializeField] private BowString bowString;
        [SerializeField] private ArrowPool arrowPool;
        [SerializeField] private Animator bowAnimator;

        [Header("레벨별 외형 (Lv1, Lv2, Lv3 …)")]
        [Tooltip("모델이 없는 단계(예: Lv4)는 마지막 외형을 그대로 쓴다.")]
        [SerializeField] private LevelVisual[] levelVisuals;

        [Header("화살 효과 (TSV의 ArrowEffect = 인덱스. 0 = 효과 없음)")]
        [SerializeField] private ArrowEffectVisual[] arrowEffects;

        [Header("발사")]
        [SerializeField] private float maxArrowSpeed = 25f;
        [SerializeField] private float arrowDamage = 40f;
        [Tooltip("이보다 덜 당기고 놓으면 발사하지 않고 화살을 거둔다.")]
        [SerializeField, Range(0f, 1f)] private float minReleasePull = 0.15f;

        [Header("애니메이션 (Blink Bow 'Shooting' 상태)")]
        [SerializeField] private string drawStateName = "Shooting";
        [Tooltip("클립에서 시위를 끝까지 당긴 지점(정규화 시간). 당김 1 = 이 시점.")]
        [SerializeField, Range(0.05f, 1f)] private float fullDrawNormalizedTime = 0.5f;
        [Tooltip("놓은 뒤 시위가 원위치로 돌아가는 속도 (당김/초)")]
        [SerializeField] private float stringReturnSpeed = 10f;

        [Header("햅틱")]
        [SerializeField] private float fireHapticAmplitude = 0.8f;
        [SerializeField] private float fireHapticDuration = 0.08f;

        private XRGrabInteractable grab;
        private Collider[] bowColliders;
        private Arrow nockedArrow;
        private float shownPull;
        private int drawStateHash;

        /// <summary>화살이 발사됐을 때 (화살, 당김 정도)</summary>
        public event Action<Arrow, float> ArrowFired;

        /// <summary>장전 허용 여부. 비어 있으면 항상 허용 (GameManager가 지정).</summary>
        public Func<bool> CanNock { get; set; }

        public float MaxArrowSpeed { get => maxArrowSpeed; set => maxArrowSpeed = Mathf.Max(0f, value); }
        public float ArrowDamage { get => arrowDamage; set => arrowDamage = Mathf.Max(0f, value); }
        public bool IsDrawing => bowString != null && bowString.isSelected;

        /// <summary>현재 화살 효과 단계 (UpgradeStation이 TSV의 ArrowEffect로 지정).</summary>
        public int ArrowEffect { get; set; }

        /// <summary>효과 단계의 표시 이름. 목록에 없으면 빈 문자열.</summary>
        public string GetArrowEffectName(int effect)
        {
            ArrowEffectVisual visual = GetArrowEffect(effect);
            return visual != null ? visual.displayName : "";
        }

        /// <summary>현재 강화 단계의 화살을 라운드 화살 수만큼 미리 만든다 (게임 시작 시).</summary>
        public void PrepareArrows(int count)
        {
            if (arrowPool == null) return;
            arrowPool.Prepare(count, ArrowEffect);
        }

        /// <summary>이전 단계로 만들어 둔 화살을 모두 지운다 (강화 시).</summary>
        public void ClearArrows()
        {
            ReturnNockedArrow();
            if (arrowPool != null) arrowPool.Clear();
        }

        private ArrowEffectVisual GetArrowEffect(int effect) =>
            arrowEffects != null && effect >= 0 && effect < arrowEffects.Length ? arrowEffects[effect] : null;
        public ArrowPool ArrowPool => arrowPool;

        /// <summary>당기는 중이면 걸린 화살을 끄고 시위를 놓게 한다 (게임 종료 시).</summary>
        public void CancelDraw()
        {
            ReturnNockedArrow();
            if (bowString != null && bowString.isSelected) bowString.ForceRelease();
        }

        /// <summary>활을 잡고 있는 손 (거치대 소켓 제외).</summary>
        public IXRSelectInteractor HoldingHand =>
            grab.interactorsSelecting.FirstOrDefault(i => (i is XRSocketInteractor) == false);

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (bowString == null) bowString = GetComponentInChildren<BowString>();

            // 활을 이미 한 손이 쥐고 있으면 다른 손은 활 몸체를 잡을 수 없다 (손 바꿔 쥐기 방지).
            // → 두 번째 손에게 남는 대상은 시위(BowString)뿐이라 시위가 잡힌다.
            grab.hoverFilters.Add(new XRHoverFilterDelegate((interactor, _) => CanHandTakeBow(interactor)));
            grab.selectFilters.Add(new XRSelectFilterDelegate((interactor, _) => CanHandTakeBow(interactor)));
            bowColliders = GetComponentsInChildren<Collider>(true).Where(c => c.isTrigger == false).ToArray();
            drawStateHash = Animator.StringToHash(drawStateName);
            if (bowAnimator != null) bowAnimator.speed = 0f;
            if (levelVisuals != null && levelVisuals.Length > 0) SetVisualLevel(0);
        }

        /// <summary>거치대 소켓이거나, 아무 손도 활을 쥐고 있지 않거나, 이미 쥐고 있는 그 손이면 허용.</summary>
        private bool CanHandTakeBow(IXRInteractor interactor)
        {
            if (interactor is XRSocketInteractor) return true;
            IXRSelectInteractor holder = HoldingHand;
            return holder == null || ReferenceEquals(holder, interactor);
        }

        public int VisualLevel { get; private set; }

        /// <summary>
        /// 강화 단계(0 = Lv1)에 맞는 활 외형으로 바꾼다. 해당 모델만 켜고, 당김 애니메이션과 시위 위치도 그 모델 것으로 바꾼다.
        /// </summary>
        public void SetVisualLevel(int level)
        {
            if (levelVisuals == null || levelVisuals.Length == 0) return;

            // 당기는 중이면 취소 (화살은 거둔다)
            ReturnNockedArrow();
            if (bowString != null && bowString.isSelected) bowString.ForceRelease();

            int index = Mathf.Clamp(level, 0, levelVisuals.Length - 1);
            for (int i = 0; i < levelVisuals.Length; i++)
            {
                if (levelVisuals[i].root != null) levelVisuals[i].root.SetActive(i == index);
            }

            LevelVisual visual = levelVisuals[index];
            bowAnimator = visual.animator;
            if (bowAnimator != null) bowAnimator.speed = 0f;
            if (visual.stringRest != null && bowString != null) bowString.transform.position = visual.stringRest.position;

            VisualLevel = index;
            shownPull = 0f;
            ApplyAnimation(0f);
        }

        private void OnEnable()
        {
            grab.selectExited.AddListener(OnBowSelectExited);
            if (bowString == null) return;
            bowString.Grabbed += OnStringGrabbed;
            bowString.Released += OnStringReleased;
        }

        private void OnDisable()
        {
            grab.selectExited.RemoveListener(OnBowSelectExited);
            if (bowString != null)
            {
                bowString.Grabbed -= OnStringGrabbed;
                bowString.Released -= OnStringReleased;
            }
            ReturnNockedArrow();
        }

        private void LateUpdate()
        {
            float target = bowString != null ? bowString.Pull : 0f;
            shownPull = target >= shownPull ? target : Mathf.MoveTowards(shownPull, target, stringReturnSpeed * Time.deltaTime);
            ApplyAnimation(shownPull);
            FollowNock();
        }

        // ─── 시위 ───

        private void OnStringGrabbed()
        {
            if (nockedArrow != null || arrowPool == null) return;
            if (HoldingHand == null) return; // 활과 시위를 둘 다 잡았을 때만
            if (CanNock != null && CanNock() == false) return;

            nockedArrow = arrowPool.Get();
            if (nockedArrow == null) return; // 이번 라운드에 준비된 화살을 다 씀
            nockedArrow.Nock(bowColliders);
            FollowNock();
        }

        private void OnStringReleased(float pull, IXRSelectInteractor hand)
        {
            if (nockedArrow == null) return;
            if (pull < minReleasePull)
            {
                ReturnNockedArrow();
                return;
            }

            Arrow arrow = nockedArrow;
            nockedArrow = null;
            arrow.Damage = arrowDamage;
            arrow.Launch(transform.forward * (maxArrowSpeed * pull));

            BowString.SendHaptic(hand, fireHapticAmplitude, fireHapticDuration);
            BowString.SendHaptic(HoldingHand, fireHapticAmplitude * 0.6f, fireHapticDuration);
            ArrowFired?.Invoke(arrow, pull);
        }

        /// <summary>당기는 중에 활을 놓치면 화살은 거두고 시위도 놓게 한다.</summary>
        private void OnBowSelectExited(SelectExitEventArgs args)
        {
            if (HoldingHand != null) return;
            ReturnNockedArrow();
            if (bowString != null) bowString.ForceRelease();
        }

        private void ReturnNockedArrow()
        {
            if (nockedArrow == null) return;
            nockedArrow.Despawn();
            nockedArrow = null;
        }

        private void FollowNock()
        {
            if (nockedArrow == null || bowString == null) return;
            nockedArrow.FollowNock(bowString.NockPosition, Quaternion.LookRotation(transform.forward, transform.up));
        }

        private void ApplyAnimation(float pull)
        {
            if (bowAnimator == null || bowAnimator.runtimeAnimatorController == null || bowAnimator.isActiveAndEnabled == false) return;
            bowAnimator.Play(drawStateHash, 0, pull * fullDrawNormalizedTime);
            bowAnimator.Update(0f);
        }
    }
}
