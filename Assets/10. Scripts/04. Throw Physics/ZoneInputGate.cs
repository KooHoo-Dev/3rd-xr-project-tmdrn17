using StudyXR.Common;
using UnityEngine;

namespace StudyXR.ThrowPhysics
{
    public class ZoneInputGate : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private ZoneHub hub;
        [SerializeField] private int zoneIndex = 0;

        [Header("이 구역의 입력 (비어 있으면 건너뜀)")]
        [SerializeField] private BallRack rack;
        [SerializeField] private ImpulseLauncher launcher;
        [SerializeField] private ThrowTuningPanel tuning;
        [SerializeField] private BoothRound round;

        private void OnEnable()
        {
            if (hub != null) hub.ZoneChanged += Apply;
        }

        private void OnDisable()
        {
            if (hub != null) hub.ZoneChanged -= Apply;
        }

        private void Start()
        {
            Apply(hub != null ? hub.Current : zoneIndex);
        }

        private void Apply(int currentZone)
        {
            bool here = (currentZone == zoneIndex);

            if (rack != null) rack.SetInputEnabled(here);
            if (launcher != null) launcher.SetInputEnabled(here);
            if (tuning != null) tuning.SetInputEnabled(here);
            if (round != null) round.SetInputEnabled(here);
        }
    }
}
