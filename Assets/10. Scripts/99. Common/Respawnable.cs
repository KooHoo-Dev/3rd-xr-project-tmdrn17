using System;
using ArcherSimulator;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.Common
{
    public class Respawnable : MonoBehaviour
    {
        [SerializeField] private float killHeight = -5f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float respawnDelay = 1f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float outsideTime;

        private bool pending;
        private RigidbodyInterpolation savedInterpolation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            SetHome(transform.position, transform.rotation);
        }

        // GameManager.Instance는 GameManager.Awake에서 채워지므로, 모든 Awake가 끝난 Start에서 구독한다.
        // GameManager가 없는 씬(공용 스크립트)에서도 쓰이므로 null이면 건너뛴다.
        private void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.RestartStage += Teleport;
        }

        // 씬을 닫거나 플레이를 멈출 때는 GameManager가 먼저 파괴돼 Instance가 null일 수 있다.
        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.RestartStage -= Teleport;
        }
        
        private void SetHome(Vector3 pos, Quaternion rot)
        {
            homePosition = pos;
            homeRotation = rot;
        }

        public void RespawnNow()
        {
            outsideTime = 0f;
            if (body != null && pending == false)
            {
                savedInterpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation.None;
                pending = true;
            }
            Teleport();
        }

        private void Teleport()
        {
            if (body != null)
            {
                if (body.isKinematic == false)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = homePosition;
                body.rotation = homeRotation;
            }
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        private void FixedUpdate()
        {
            if (pending == false) return;
            pending = false;
            body.interpolation = savedInterpolation;

            bool held = grab != null && grab.isSelected;

            if (held || body.isKinematic) return;
            Teleport();
        }

        private void OnDisable()
        {
            if (pending == false) return;
            pending = false;
            if (body != null) body.interpolation = savedInterpolation;
        }

        private void Update()
        {
            bool held = grab != null && grab.isSelected;
            bool outside = transform.position.y < killHeight ||
                           (transform.position - homePosition).sqrMagnitude > maxDistance * maxDistance;

            if (held || outside == false)
            {
                outsideTime = 0f;
                return;
            }

            outsideTime += Time.deltaTime;
            if (outsideTime >= respawnDelay) RespawnNow();
        }
    }
}
