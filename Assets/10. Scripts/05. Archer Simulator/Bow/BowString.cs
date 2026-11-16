using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcherSimulator
{
    /// <summary>
    /// 활 시위. 활을 잡고 있는 "다른 손"만 잡을 수 있다.
    /// 시위를 잡은 손이 활 앞(+Z) 반대쪽으로 멀어진 거리로 당김 정도(0~1)를 계산한다.
    /// 오브젝트 위치 = 시위가 쉬고 있을 때의 화살 오늬 위치.
    /// </summary>
    public class BowString : XRBaseInteractable
    {
        [Header("연결")]
        [SerializeField] private XRGrabInteractable bow;

        [Header("당김")]
        [SerializeField] private float maxPullDistance = 0.5f;
        [Tooltip("반대쪽 손(컨트롤러)이 이 거리 안에서 그립을 누르면 시위를 잡는다.")]
        [SerializeField] private float grabRadius = 0.25f;
        [Tooltip("XRI 근거리 탐지가 시위 콜라이더를 못 찾아도, 거리만으로 시위를 직접 잡게 한다.")]
        [SerializeField] private bool directGrab = true;
        [SerializeField] private bool debugLog;

        [Header("햅틱")]
        [SerializeField] private float hapticStep = 0.08f;
        [SerializeField] private float hapticMaxAmplitude = 0.4f;
        [SerializeField] private float hapticDuration = 0.03f;

        private float lastHapticPull;
        private static readonly List<IXRInteractor> InteractorBuffer = new List<IXRInteractor>();

        /// <summary>0(쉼) ~ 1(최대 당김)</summary>
        public float Pull { get; private set; }

        /// <summary>현재 당김에 맞는 화살 오늬 위치 (손을 따라간다).</summary>
        public Vector3 NockPosition => transform.position - BowForward * (Pull * maxPullDistance);

        public IXRSelectInteractor Puller => isSelected ? interactorsSelecting[0] : null;

        public event Action Grabbed;
        /// <summary>놓았을 때 (당김 정도, 놓은 손). 취소(활을 떨어뜨림 등)면 당김 0.</summary>
        public event Action<float, IXRSelectInteractor> Released;

        private Vector3 BowForward => bow != null ? bow.transform.forward : transform.forward;

        protected override void Awake()
        {
            base.Awake();
            if (bow == null) bow = GetComponentInParent<XRGrabInteractable>();
        }

        public override bool IsHoverableBy(IXRHoverInteractor interactor)
        {
            return base.IsHoverableBy(interactor) && CanUse(interactor);
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            return base.IsSelectableBy(interactor) && CanUse(interactor);
        }

        /// <summary>활을 다른 손이 잡고 있고, 이 손이 시위 근처에 있을 때만.</summary>
        private bool CanUse(IXRInteractor interactor)
        {
            if (interactor is XRSocketInteractor || bow == null) return false;
            if (!IsBowHeldByOtherHand(interactor)) return false;
            if (interactor is IXRSelectInteractor selector && interactorsSelecting.Contains(selector)) return true; // 당기는 중
            return (interactor.transform.position - transform.position).sqrMagnitude <= grabRadius * grabRadius;
        }

        private bool IsBowHeldByOtherHand(IXRInteractor interactor)
        {
            foreach (IXRSelectInteractor holder in bow.interactorsSelecting)
            {
                if (holder is XRSocketInteractor) continue;
                if (!ReferenceEquals(holder, interactor)) return true;
            }
            return false;
        }

        /// <summary>
        /// 직접 잡기: 활을 쥔 손의 반대쪽 손이 시위 근처에서 그립(Select)을 누르고 있고 아무것도 안 잡았으면
        /// 이 시위를 바로 선택시킨다. (근거리 캐스터가 시위를 놓치거나 활 손잡이를 먼저 집는 문제 회피)
        /// </summary>
        private void Update()
        {
            if (!directGrab || isSelected || bow == null || interactionManager == null) return;

            interactionManager.GetRegisteredInteractors(InteractorBuffer);
            foreach (IXRInteractor interactor in InteractorBuffer)
            {
                if (!(interactor is XRBaseInputInteractor hand) || !hand.isActiveAndEnabled) continue;
                if (hand.hasSelection || !hand.isSelectActive) continue;
                if (!IsBowHeldByOtherHand(hand)) continue;

                float distance = Vector3.Distance(hand.transform.position, transform.position);
                if (distance > grabRadius)
                {
                    if (debugLog) Debug.Log($"[BowString] {hand.name} 그립 중이지만 시위까지 {distance:0.00}m (> {grabRadius}m)", this);
                    continue;
                }

                if (debugLog) Debug.Log($"[BowString] {hand.name}이(가) 시위를 잡음 ({distance:0.00}m)", this);
                interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)this);
                break;
            }
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected) return;

            IXRSelectInteractor hand = interactorsSelecting[0];
            Vector3 handPosition = hand.GetAttachTransform(this).position;
            float distance = Vector3.Dot(transform.position - handPosition, BowForward);
            Pull = Mathf.Clamp01(distance / Mathf.Max(0.01f, maxPullDistance));

            // 당길수록 진동이 강해진다
            if (Mathf.Abs(Pull - lastHapticPull) >= hapticStep)
            {
                lastHapticPull = Pull;
                SendHaptic(hand, hapticMaxAmplitude * Pull, hapticDuration);
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            Pull = 0f;
            lastHapticPull = 0f;
            Grabbed?.Invoke();
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            float pull = args.isCanceled ? 0f : Pull;
            Pull = 0f;
            Released?.Invoke(pull, args.interactorObject);
        }

        /// <summary>강제로 시위를 놓게 한다 (발사 없음).</summary>
        public void ForceRelease()
        {
            if (isSelected && interactionManager != null)
                interactionManager.CancelInteractableSelection((IXRSelectInteractable)this);
        }

        public static void SendHaptic(IXRInteractor interactor, float amplitude, float duration)
        {
            if (amplitude <= 0f) return;
            if (interactor is XRBaseInputInteractor input) input.SendHapticImpulse(amplitude, duration);
        }
    }
}
