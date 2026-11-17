using UnityEngine.Serialization;

namespace ArcherSimulator
{
    /// <summary>BowUpgrade.tsv 한 줄. 헤더 이름과 필드 이름이 같아야 한다.</summary>
    public class BowUpgradeDTO
    {
        public int Level;           // 0 = Lv1
        public string DisplayName;
        public float Range;         // 사거리 (m, 표시용)
        public int Damage;
        public int Cost;            // 이 단계로 올리는 비용
        public float ArrowSpeed;    // 최대 당김 시 화살 속도 (m/s)
        public int ArrowEffect;
        public int VisualLevel;     // BowController 외형 인덱스 (모델 수보다 크면 마지막 모델)
    }

    /// <summary>게임에서 쓰는 활 강화 단계 데이터.</summary>
    [System.Serializable]
    public class BowUpgradeDef
    {
        public int Level;
        public string DisplayName;
        public float Range;
        public int Damage;
        public int Cost;
        public float ArrowSpeed;
        public int ArrowEffect;
        public int VisualLevel;

        public string StatText => $"사거리 {Range:0}m · 데미지 {Damage}";
    }
}
