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
