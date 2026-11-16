using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.ThrowPhysics
{
    [RequireComponent(typeof(Rigidbody))]
    public class ScoreTarget : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BoothRound round;
        [SerializeField] private Transform player;

        [Header("표시")]
        [SerializeField] private TMP_Text label;

        [Header("점수")]
        [SerializeField] private string targetName = "과녁";
        [SerializeField] private int points = 10;
        [SerializeField] private float minSpeed = 3f;

        [Header("연출")]
        [SerializeField] private float knockAngle = 80f;
        [SerializeField] private float knockTime = 0.25f;

        private Rigidbody body;
        private Quaternion homeRotation;
        private Vector3 knockAxis;
        private float knockT;
        private bool scored;
        private bool knocking;
        private bool knocked;
        private string lastText = "";

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            homeRotation = transform.rotation;
            if (label != null) label.text = $"{targetName} · {points}점";
        }

        private void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody == null || c.rigidbody.GetComponent<XRGrabInteractable>() == null) return;

            Vector3 normal = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.zero;
            float speed = ImpactSpeed(c.relativeVelocity, normal);
            int got = PointsForHit(scored, speed, minSpeed, points);

            if (got > 0 && round != null && round.IsAccepting)
            {
                scored = true;
                round.AddScore(got, $"{targetName} +{got} ({speed:F1} m/s)");
                StartKnock();
            }

            lastText = got > 0 ?
                $"{speed:F1} m/s · 득점!"
                : scored
                    ? $"{speed:F1} m/s · 이미 쓰러짐"
                    : $"{speed:F1} m/s · 약함";
            if (got > 0 && (round == null || round.IsAccepting == false)) lastText = "라운드 끝";
        }

        private static float ImpactSpeed(Vector3 relativeVelocity, Vector3 contactNormal)
        {
            Vector3 n = contactNormal.normalized;

            return Mathf.Abs(Vector3.Dot(relativeVelocity, n));
        }

        private static int PointsForHit(
            bool alreadyScored,
            float impactSpeed,
            float minSpeed,
            int points)
        {
            if (alreadyScored) return 0;
            if (impactSpeed < minSpeed) return 0;
            return points;
        }

        private void StartKnock()
        {
            if (knocking || knocked) return;

            Vector3 away = player != null ? transform.position - player.position : transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            knockAxis = Vector3.Cross(Vector3.up, away);
            knockT = 0f;
            knocking = true;
        }

        private void FixedUpdate()
        {
            if (knocking == false) return;

            knockT += Time.fixedDeltaTime;
            float a = knockAngle * Mathf.Clamp01(knockT / knockTime);
            body.MoveRotation(Quaternion.AngleAxis(a, knockAxis) * homeRotation);

            if (knockT >= knockTime)
            {
                knocking = false;
                knocked = true;
            }
        }

        public void ResetTarget()
        {
            scored = false;
            knocking = false;
            knocked = false;
            body.MoveRotation(homeRotation);
            transform.rotation = homeRotation;
            lastText = "";
        }

        private void LateUpdate()
        {
            if (label == null) return;
            label.text = string.IsNullOrEmpty(lastText) ? $"{targetName} · {points}점" : $"{targetName}\n{lastText}";
        }
    }
}
