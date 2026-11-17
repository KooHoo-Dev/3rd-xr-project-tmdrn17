using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>
    /// 한 라운드의 점수 계산.
    /// (표적 파괴 × 100 + 약점 명중 × 20 + [클리어 시] 남은 화살 × 50) × 난이도 배율
    /// </summary>
    [System.Serializable]
    public class ScoreRule
    {
        public int targetDestroyed = 100;
        public int weakPointHit = 20;
        [Tooltip("클리어했을 때만 남은 화살 1발당")]
        public int arrowLeft = 50;
    }

    public class RoundScore
    {
        public int TargetsDestroyed;
        public int WeakPointHits;
        public int Hits;         // 표적에 꽂힌 화살 수 (명중률용)
        public int ArrowsLeft;   // 클리어 보너스용 (클리어하지 못하면 0)
        public float Multiplier = 1f;

        public void Reset(float multiplier)
        {
            TargetsDestroyed = WeakPointHits = Hits = ArrowsLeft = 0;
            Multiplier = multiplier;
        }

        public int BaseScore(ScoreRule rule) =>
            TargetsDestroyed * rule.targetDestroyed + WeakPointHits * rule.weakPointHit + ArrowsLeft * rule.arrowLeft;

        public int Total(ScoreRule rule) => Mathf.RoundToInt(BaseScore(rule) * Multiplier);

        public string Breakdown(ScoreRule rule) =>
            $"표적 파괴  {rule.targetDestroyed} × {TargetsDestroyed} = {rule.targetDestroyed * TargetsDestroyed}\n" +
            $"약점 명중  {rule.weakPointHit} × {WeakPointHits} = {rule.weakPointHit * WeakPointHits}\n" +
            $"남은 화살  {rule.arrowLeft} × {ArrowsLeft} = {rule.arrowLeft * ArrowsLeft}\n" +
            $"난이도 배율  × {Multiplier:0.0#}";
    }
}
