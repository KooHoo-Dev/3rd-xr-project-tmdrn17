using UnityEngine;

namespace StudyXR.ThrowPhysics
{
    public class MassAwareThrow : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BallRack rack;

        [Header("보정")]
        [SerializeField] private float referenceMass = 0.15f;
        [SerializeField] private float minScale = 0.3f;
        [SerializeField] private float baseScale = 1f;
        [SerializeField] private bool massAware = false;

        private float[] scales;

        public float BaseScale => baseScale;
        public bool IsMassAware => massAware;

        public float ScaleOf(int i)
        {
            return scales != null && i >= 0 && i < scales.Length ? scales[i] : baseScale;
        }

        public void SetBaseScale(float s)
        {
            baseScale = s;
            Apply();
        }

        public void SetMassAware(bool on)
        {
            massAware = on;
            Apply();
        }

        private void Start()
        {
            Apply();
        }

        private void Apply()
        {
            if (rack == null) return;

            if (scales == null || scales.Length != rack.Count) scales = new float[rack.Count];

            for (int i = 0; i < rack.Count; ++i)
            {
                float s = massAware
                    ? ThrowScaleForMass(rack.Body(i).mass, referenceMass, baseScale, minScale)
                    : baseScale;
                s = Mathf.Clamp(s, 0f, 4f);

                scales[i] = s;
                rack.Grab(i).throwVelocityScale = s;
            }
        }

        private static float ThrowScaleForMass(
            float mass,
            float referenceMass,
            float baseScale,
            float minScale)
        {
            if (mass <= 0f) return baseScale;
            float scale = baseScale * Mathf.Sqrt(referenceMass / mass);

            return Mathf.Clamp(scale, minScale, baseScale);
        }
    }
}
