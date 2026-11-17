using System.Collections.Generic;
using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>
    /// 화살. +Z = 촉 방향. 오늬(꼬리) 위치는 몸통 CapsuleCollider의 뒤 끝 (기본 화살은 루트, 이펙트 화살은 루트 뒤쪽).
    /// 1) ArrowPool이 켜 주면 시위에 걸렸다가 발사된다 (BowController).
    ///    강화 단계 화살 프리팹(Arrow Lv2~)은 화살 모델 대신 이펙트가 화살 몸체다. 시위에 걸 때부터 재생한다.
    /// 2) 땅(ArrowGround)에 닿으면 바로 꺼진다.
    /// 3) 그 밖의 물체에 닿으면 멈추고 콜라이더를 끈 뒤, 맞은 물체를 따라다닌다. 게임이 끝나면 꺼진다.
    ///    표적(Target)이면 Damage만큼 데미지를 준다. 약점(TargetWeakPoint)이면 약점 명중.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        private enum ArrowState { Idle, Nocked, Flying, Stuck }

        private Rigidbody body;
        private ParticleSystem[] effects; // 자식 이펙트 (강화 단계 화살만)
        private Collider[] ownColliders;
        private ArrowState state = ArrowState.Idle;
        private Vector3 nockLocalPosition; // 루트 기준 오늬(꼬리) 위치
        private bool effectsPending;       // 시위 위치로 옮긴 뒤 이펙트를 켜야 함

        private Transform stuckTo;
        private Vector3 stuckLocalPosition;
        private Quaternion stuckLocalRotation;

        /// <summary>표적에 줄 데미지. 발사할 때 BowController가 정한다.</summary>
        public float Damage { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>(true);

            // 오늬(꼬리) 위치 = 화살 몸통 캡슐의 뒤 끝. 기본 화살은 루트가 꼬리(z 0)지만,
            // 이펙트 화살(Arrow Lv2~)은 루트가 촉이고 몸통이 뒤(-Z)로 뻗어 있어 꼬리가 z -0.83쯤이다.
            CapsuleCollider shaft = GetComponent<CapsuleCollider>();
            if (shaft != null && shaft.direction == 2)
                nockLocalPosition = shaft.center - Vector3.forward * (shaft.height * 0.5f);

            // 이펙트가 끝나면 스스로 지워지는 설정(Stop Action: Destroy/Callback)을 꺼서 화살과 함께 재사용한다.
            effects = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in effects)
            {
                ParticleSystem.MainModule main = ps.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        // ───────── 발사 ─────────

        /// <summary>시위에 건다. 물리·충돌을 끄고, 발사 직후 활에 걸리지 않게 활 Collider와의 충돌을 무시한다.</summary>
        public void Nock(IEnumerable<Collider> ignore)
        {
            state = ArrowState.Nocked;
            stuckTo = null;
            MakeKinematic();
            SetColliders(false);
            // 이펙트는 아직 켜지 않는다. 지금은 화살 묶음(Arrow Pool) 위치라, 여기서 켜고 시위로 순간이동하면
            // 월드 공간 파티클(이동 거리만큼 뿜는 불꽃 등)이 그 사이를 따라 쏟아져 뒤로 날아가는 것처럼 보인다.
            // → 첫 FollowNock에서 시위 위치로 옮긴 뒤에 처음부터 재생한다.
            SetEffects(false);
            effectsPending = true;

            if (ignore == null) return;
            foreach (Collider other in ignore)
            {
                if (other == null) continue;
                foreach (Collider own in ownColliders) Physics.IgnoreCollision(own, other, true);
            }
        }

        /// <summary>화살의 오늬(꼬리)가 시위 위치(position)에 오도록 화살을 놓는다.</summary>
        public void FollowNock(Vector3 position, Quaternion rotation)
        {
            if (state != ArrowState.Nocked) return;
            transform.SetPositionAndRotation(position - rotation * nockLocalPosition, rotation);

            if (!effectsPending) return;
            effectsPending = false;
            SetEffects(true); // 이펙트가 곧 화살 몸체: 시위 위치에 놓인 뒤 처음부터 재생
        }

        public void Launch(Vector3 velocity)
        {
            if (state != ArrowState.Nocked) return;
            state = ArrowState.Flying;

            SetColliders(true);
            body.isKinematic = false;
            // 빠른 화살이 얇은 표적을 뚫거나, 몸통·약점에 한 프레임에 같이 닿아 판정이 뒤섞이지 않게
            // 날아가는 동안만 연속 충돌 감지를 쓴다 (먼저 닿은 면 = 앞에 튀어나온 약점이 먼저 잡힌다).
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
        }

        // ───────── 도착 ─────────

        private void OnCollisionEnter(Collision collision)
        {
            if (state != ArrowState.Flying) return;

            // 땅에 닿으면 바로 꺼진다
            if (collision.collider.GetComponentInParent<ArrowGround>() != null)
            {
                Despawn();
                return;
            }

            // 그 밖의 물체: 멈추고 콜라이더를 끈 뒤 맞은 물체를 따라다닌다
            state = ArrowState.Stuck;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            MakeKinematic();
            SetColliders(false);

            stuckTo = collision.transform;
            stuckLocalPosition = stuckTo.InverseTransformPoint(transform.position);
            stuckLocalRotation = Quaternion.Inverse(stuckTo.rotation) * transform.rotation;

            // 표적이면 데미지 (약점 Collider면 약점 명중). 파괴되면 표적이 꺼지고 이 화살도 따라 꺼진다.
            Target target = collision.collider.GetComponentInParent<Target>();
            if (target != null)
                target.TakeHit(Damage, collision.collider.GetComponentInParent<TargetWeakPoint>() != null);
        }

        private void LateUpdate()
        {
            if (state != ArrowState.Stuck || stuckTo == null) return;
            if (stuckTo.gameObject.activeInHierarchy == false) // 꽂힌 표적이 파괴됨
            {
                Despawn();
                return;
            }
            transform.SetPositionAndRotation(stuckTo.TransformPoint(stuckLocalPosition), stuckTo.rotation * stuckLocalRotation);
        }

        // ───────── 끄기 ─────────

        /// <summary>화살을 끈다. 다시 시위에 걸 때 ArrowPool이 켠다.</summary>
        public void Despawn()
        {
            if (state == ArrowState.Idle) return;
            state = ArrowState.Idle;
            stuckTo = null;
            MakeKinematic();
            gameObject.SetActive(false);
        }

        /// <summary>물리를 멈춘다. 키네마틱은 연속 충돌 감지(ContinuousDynamic)를 못 쓰므로 Discrete로 먼저 되돌린다.</summary>
        private void MakeKinematic()
        {
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
        }

        private void SetColliders(bool on)
        {
            foreach (Collider own in ownColliders) own.enabled = on;
        }

        /// <summary>자식 이펙트를 처음부터 재생하거나, 지우고 멈춘다.</summary>
        private void SetEffects(bool on)
        {
            foreach (ParticleSystem ps in effects)
            {
                if (ps == null) continue;
                ps.Clear(false);
                if (on) ps.Play(false);
                else ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
