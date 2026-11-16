using StudyXR.Common;
using TMPro;
using UnityEngine;

namespace StudyXR.ThrowPhysics
{
    public class CanPyramidJudge : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Rigidbody[] cans;
        [SerializeField] private BoothRound round;

        [Header("표시")]
        [SerializeField] private TMP_Text label;

        [Header("판정")]
        [SerializeField] private float fallDrop = 0.06f;
        [SerializeField] private float slideDistance = 0.1f;

        [Header("점수")]
        [SerializeField] private int pointsPerCan = 5;
        [SerializeField] private int clearBonus = 20;

        private Respawnable[] respawns;
        private Vector3[] home;
        private bool[] counted;
        private bool bonusGiven;

        private int fallenCount;
        private int Total => cans != null ? cans.Length : 0;

        private void Awake()
        {
            respawns = new Respawnable[Total];
            for (int i = 0; i < Total; ++i) respawns[i] = cans[i].GetComponent<Respawnable>();
        }

        private void Start()
        {
            home = new Vector3[Total];
            counted = new bool[Total];
            for (int i = 0; i < Total; ++i) home[i] = cans[i].transform.position;
        }

        private void FixedUpdate()
        {
            if (Total == 0 || counted == null) return;

            for (int i = 0; i < Total; ++i)
            {
                if (counted[i]) continue;

                Vector3 d = cans[i].transform.position - home[i];
                float slide = new Vector2(d.x, d.z).magnitude;

                if (-d.y > fallDrop || slide > slideDistance)
                {
                    counted[i] = true;
                    fallenCount++;
                    if (round != null) round.AddScore(pointsPerCan, "캔 +" + pointsPerCan);
                }
            }

            if (fallenCount == Total && bonusGiven == false)
            {
                bonusGiven = true;
                if (round != null) round.AddScore(clearBonus, "캔 모두 +" + clearBonus);
            }
        }

        public void ResetCans()
        {
            for (int i = 0; i < Total; ++i)
            {
                if (respawns[i] != null) respawns[i].RespawnNow();
                if (counted != null) counted[i] = false;
            }
            fallenCount = 0;
            bonusGiven = false;
        }

        private void LateUpdate()
        {
            if (label == null) return;
            label.text = Total > 0 && fallenCount == Total ? "캔 모두 넘어뜨림!" : $"캔 {fallenCount} / {Total}";
        }
    }
}
