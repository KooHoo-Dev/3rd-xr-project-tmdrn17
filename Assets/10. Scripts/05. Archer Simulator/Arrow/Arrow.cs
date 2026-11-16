using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>화살에 맞았을 때 반응할 오브젝트(표적 등)가 구현한다. 맞은 Collider의 부모에서 찾는다.</summary>
    public interface IArrowHittable
    {
        void OnArrowHit(Arrow arrow, Collider hitCollider, Vector3 point);
    }

    /// <summary>
    /// 화살. 루트 = 오늬(꼬리), +Z = 촉 방향.
    /// 1) 당김 정도에 따른 속도로 발사된다 (BowController).
    /// 2) 중력 + 공기저항(속도² 비례)을 받으며 포물선으로 날고, 촉은 항상 진행 방향을 향한다.
    /// 3) 표적이나 땅에 닿으면 꽂히고, 일정 시간 뒤 풀로 돌아간다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        public enum ArrowState { Idle, Nocked, Flying, Stuck }

        [Header("크기")]
        [SerializeField] private float length = 0.8f;
        [SerializeField] private float embedDepth = 0.12f;

        [Header("비행")]
        [Tooltip("공기저항 계수 k (감속도 = k × 속도²)")]
        [SerializeField] private float dragCoefficient = 0.003f;
        [SerializeField] private float maxFlightTime = 8f;

        [Header("수명")]
        [SerializeField] private float lifetimeAfterHit = 5f;

        private Rigidbody body;
        private Collider[] ownColliders;
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private ArrowPool pool;
        private Vector3 lastVelocity;
        private float flightTime;
        private Coroutine despawnRoutine;

        private Transform stuckTo;
        private Vector3 stuckLocalPosition;
        private Quaternion stuckLocalRotation;

        public ArrowState State { get; private set; } = ArrowState.Idle;
        public float Damage { get; private set; }

        /// <summary>무언가에 꽂혔을 때 (화살, 맞은 Collider)</summary>
        public event Action<Arrow, Collider> Landed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        internal void SetPool(ArrowPool owner) => pool = owner;

        /// <summary>시위에 걸린 상태. 물리·충돌 꺼짐. 활 Collider와는 충돌하지 않게 한다.</summary>
        public void Nock(IEnumerable<Collider> ignore, float damage)
        {
            StopDespawn();
            State = ArrowState.Nocked;
            Damage = damage;
            stuckTo = null;
            Landed = null;

            MakeKinematic();
            SetColliders(false);

            if (ignore == null) return;
            foreach (Collider other in ignore)
            {
                if (other == null || !ignoredColliders.Add(other)) continue;
                foreach (Collider own in ownColliders) Physics.IgnoreCollision(own, other, true);
            }
        }

        public void FollowNock(Vector3 position, Quaternion rotation)
        {
            if (State != ArrowState.Nocked) return;
            transform.SetPositionAndRotation(position, rotation);
        }

        public void Launch(Vector3 velocity)
        {
            if (State != ArrowState.Nocked) return;
            State = ArrowState.Flying;
            flightTime = 0f;
            lastVelocity = velocity;

            SetColliders(true);
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (State != ArrowState.Flying) return;

            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed > 0.1f)
            {
                body.AddForce(-dragCoefficient * speed * velocity, ForceMode.Acceleration);
                body.MoveRotation(Quaternion.LookRotation(velocity));
                lastVelocity = velocity;
            }

            flightTime += Time.fixedDeltaTime;
            if (flightTime > maxFlightTime) Despawn();
        }

        private void LateUpdate()
        {
            if (State != ArrowState.Stuck || stuckTo == null) return;
            transform.SetPositionAndRotation(stuckTo.TransformPoint(stuckLocalPosition), stuckTo.rotation * stuckLocalRotation);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (State != ArrowState.Flying) return;

            Vector3 direction = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : transform.forward;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position + direction * length;
            Stick(collision.collider, point, direction);
        }

        private void Stick(Collider hit, Vector3 point, Vector3 direction)
        {
            State = ArrowState.Stuck;
            MakeKinematic();
            SetColliders(false);

            // 촉이 embedDepth만큼 박히도록 맞은 지점 기준으로 다시 놓는다
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 position = point - direction * (length - embedDepth);
            transform.SetPositionAndRotation(position, rotation);

            stuckTo = hit.transform;
            stuckLocalPosition = stuckTo.InverseTransformPoint(position);
            stuckLocalRotation = Quaternion.Inverse(stuckTo.rotation) * rotation;

            hit.GetComponentInParent<IArrowHittable>()?.OnArrowHit(this, hit, point);
            Landed?.Invoke(this, hit);

            despawnRoutine = StartCoroutine(DespawnAfter(lifetimeAfterHit));
        }

        private IEnumerator DespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            despawnRoutine = null;
            Despawn();
        }

        /// <summary>풀로 돌려보낸다 (풀이 없으면 파괴).</summary>
        public void Despawn()
        {
            if (State == ArrowState.Idle) return;
            StopDespawn();
            State = ArrowState.Idle;
            stuckTo = null;
            Landed = null;
            MakeKinematic();

            if (pool != null) pool.Release(this);
            else Destroy(gameObject);
        }

        private void StopDespawn()
        {
            if (despawnRoutine == null) return;
            StopCoroutine(despawnRoutine);
            despawnRoutine = null;
        }

        private void MakeKinematic()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }

        private void SetColliders(bool on)
        {
            foreach (Collider own in ownColliders) own.enabled = on;
        }
    }
}
