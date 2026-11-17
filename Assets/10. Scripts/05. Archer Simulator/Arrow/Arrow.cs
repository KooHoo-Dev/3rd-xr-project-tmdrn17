using System.Collections.Generic;
using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>
    /// 화살. 루트 = 오늬(꼬리), +Z = 촉 방향.
    /// 1) ArrowPool이 켜 주면 시위에 걸렸다가 발사된다 (BowController).
    ///    강화 단계 화살 프리팹(Arrow Lv2~)은 화살 모델 대신 이펙트가 화살 몸체다. 시위에 걸 때부터 재생한다.
    /// 2) 땅(ArrowGround)에 닿으면 바로 꺼진다.
    /// 3) 그 밖의 물체에 닿으면 멈추고 콜라이더를 끈 뒤, 맞은 물체를 따라다닌다. 게임이 끝나면 꺼진다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        private enum ArrowState { Idle, Nocked, Flying, Stuck }

        private Rigidbody body;
        private ParticleSystem[] effects; // 자식 이펙트 (강화 단계 화살만)
        private Collider[] ownColliders;
        private ArrowState state = ArrowState.Idle;

        private Transform stuckTo;
        private Vector3 stuckLocalPosition;
        private Quaternion stuckLocalRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>(true);

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
            body.isKinematic = true;
            SetColliders(false);
            SetEffects(true); // 이펙트가 곧 화살 몸체: 시위에 걸 때부터 처음부터 재생

            if (ignore == null) return;
            foreach (Collider other in ignore)
            {
                if (other == null) continue;
                foreach (Collider own in ownColliders) Physics.IgnoreCollision(own, other, true);
            }
        }

        public void FollowNock(Vector3 position, Quaternion rotation)
        {
            if (state != ArrowState.Nocked) return;
            transform.SetPositionAndRotation(position, rotation);
        }

        public void Launch(Vector3 velocity)
        {
            if (state != ArrowState.Nocked) return;
            state = ArrowState.Flying;

            SetColliders(true);
            body.isKinematic = false;
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
            body.isKinematic = true;
            SetColliders(false);

            stuckTo = collision.transform;
            stuckLocalPosition = stuckTo.InverseTransformPoint(transform.position);
            stuckLocalRotation = Quaternion.Inverse(stuckTo.rotation) * transform.rotation;
        }

        private void LateUpdate()
        {
            if (state != ArrowState.Stuck || stuckTo == null) return;
            transform.SetPositionAndRotation(stuckTo.TransformPoint(stuckLocalPosition), stuckTo.rotation * stuckLocalRotation);
        }

        // ───────── 끄기 ─────────

        /// <summary>화살을 끈다. 다시 시위에 걸 때 ArrowPool이 켠다.</summary>
        public void Despawn()
        {
            if (state == ArrowState.Idle) return;
            state = ArrowState.Idle;
            stuckTo = null;
            body.isKinematic = true;
            gameObject.SetActive(false);
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
