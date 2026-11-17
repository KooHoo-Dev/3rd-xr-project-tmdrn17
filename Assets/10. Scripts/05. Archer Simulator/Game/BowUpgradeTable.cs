using System.Collections.Generic;
using Jay.FileIO;
using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>
    /// 활 강화 데이터의 로드 / 보관 / 조회. (MissionTable과 같은 구조)
    /// 수치를 바꾸려면 StreamingAssets/BowUpgrade.tsv만 고치면 된다.
    /// </summary>
    public class BowUpgradeTable
    {
        private const string TableName = "BowUpgrade.tsv";

        private readonly List<BowUpgradeDef> defs = new();

        public int Count => defs.Count;
        public int MaxLevel => defs.Count - 1;
        public IReadOnlyList<BowUpgradeDef> Defs => defs;

        /// <summary>단계 데이터. 범위 밖이거나 표를 못 읽었으면 null.</summary>
        public BowUpgradeDef Get(int level) => level >= 0 && level < defs.Count ? defs[level] : null;

        public bool Load()
        {
            defs.Clear();

            List<BowUpgradeDTO> rows = TSVReader.ReadStreamingTable<BowUpgradeDTO>(TableName);
            if (rows == null) return false;

            // Level 순으로 정렬해 인덱스 = 단계가 되게 한다.
            rows.Sort((a, b) => a.Level.CompareTo(b.Level));

            for (int i = 0; i < rows.Count; i++)
            {
                BowUpgradeDTO dto = rows[i];
                if (dto.Level != i)
                    Debug.LogWarning($"[BowUpgradeTable] Level이 0부터 연속이 아닙니다: {dto.Level} (기대값 {i})");

                defs.Add(new BowUpgradeDef
                {
                    Level = i,
                    DisplayName = dto.DisplayName,
                    Range = dto.Range,
                    Damage = dto.Damage,
                    Cost = dto.Cost,
                    ArrowSpeed = dto.ArrowSpeed,
                    ArrowEffect = dto.ArrowEffect,
                    VisualLevel = dto.VisualLevel,
                });
            }

            return defs.Count > 0;
        }
    }
}
