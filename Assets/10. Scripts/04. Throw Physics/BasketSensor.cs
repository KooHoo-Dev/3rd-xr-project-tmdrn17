using System.Collections.Generic;
using StudyXR.Common;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.ThrowPhysics
{
    [RequireComponent(typeof(BoxCollider))]
    public class BasketSensor : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BoothRound round;

        [Header("표시")]
        [SerializeField] private TMP_Text label;

        [Header("점수")]
        [SerializeField] private int points = 15;

        [Header("회수")]
        [SerializeField] private float returnDelay = 1f;

        private readonly List<Rigidbody> entered = new List<Rigidbody>();
        private readonly List<float> enteredAt = new List<float>();
        private float lastGoalTime = -100f;

        public void ClearAll()
        {
            entered.Clear();
            enteredAt.Clear();
            lastGoalTime = -100f;
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;

            if (body == null || body.GetComponent<XRGrabInteractable>() == null) return;
            if (body.linearVelocity.y > 0.5f) return;
            if (entered.Contains(body)) return;

            entered.Add(body);
            enteredAt.Add(Time.time);

            if (round != null && round.IsAccepting)
            {
                round.AddScore(points, "바구니 +" + points);
                lastGoalTime = Time.time;
            }
        }

        private void Update()
        {
            for (int i = entered.Count - 1; i >= 0; --i)
            {
                if (Time.time - enteredAt[i] < returnDelay) continue;

                Respawnable respawn = entered[i].GetComponent<Respawnable>();

                if (respawn != null) respawn.RespawnNow();
                entered.RemoveAt(i);
                enteredAt.RemoveAt(i);
            }
        }

        private void LateUpdate()
        {
            if (label == null) return;
            label.text = Time.time - lastGoalTime < 1.5f ? $"골인! +{points}" : $"바구니 · {points}점";
        }
    }
}
