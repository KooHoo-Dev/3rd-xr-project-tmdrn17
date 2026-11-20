using System.Collections.Generic;
using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>
    /// 라운드용 화살 묶음.
    /// 게임을 시작하면 현재 강화 단계의 화살 프리팹으로 최대 화살 수만큼 미리 만들어 꺼 두고(Prepare),
    /// 시위에 걸 때 하나씩 켜서 쓰며(Get), 게임이 끝나면 모두 끈다(DespawnAll).
    /// 강화하면 이전 단계 화살을 모두 지운다(Clear). 이펙트는 화살 프리팹 안에 들어 있어 화살과 함께 재사용된다.
    /// </summary>
    public class ArrowPool : MonoBehaviour
    {
        [Tooltip("강화 단계별 화살 프리팹 (TSV의 ArrowEffect = 인덱스). 없는 단계는 마지막 프리팹을 쓴다.")]
        [SerializeField] private Arrow[] arrowPrefab;

        private readonly List<Arrow> arrows = new List<Arrow>();
        private int preparedEffect = -1;

        /// <summary>
        /// 최대 화살 수만큼 준비한다. 모자라면 새로 만들고, 전부 꺼진 상태로 둔다.
        /// 이전에 다른 단계로 만들어 둔 화살이 있으면 지우고 새로 만든다.
        /// </summary>
        public void Prepare(int count, int effect)
        {
            if (effect != preparedEffect) Clear();
            DespawnAll();
            preparedEffect = effect;

            Arrow prefab = GetArrowPrefab(effect);
            if (prefab == null)
            {
                Debug.LogError("[ArrowPool] 화살 프리팹이 연결되지 않았습니다.", this);
                return;
            }

            while (arrows.Count < count)
            {
                Arrow arrow = Instantiate(prefab, transform);
                arrow.gameObject.SetActive(false);
                arrows.Add(arrow);
            }
        }

        /// <summary>꺼져 있는 화살 하나를 돌려준다. 켜는 건 Arrow.Nock이 시위 위치로 옮긴 뒤에 한다. 남은 화살이 없으면 null.</summary>
        public Arrow Get()
        {
            foreach (Arrow arrow in arrows)
            {
                if (arrow.gameObject.activeSelf) continue;
                return arrow;
            }
            return null;
        }

        /// <summary>날아가는 중·꽂힌 화살까지 모두 끈다 (게임 종료).</summary>
        public void DespawnAll()
        {
            foreach (Arrow arrow in arrows) arrow.Despawn();
        }

        /// <summary>만들어 둔 화살을 모두 지운다 (강화로 단계가 바뀔 때).</summary>
        public void Clear()
        {
            foreach (Arrow arrow in arrows) if (arrow != null) Destroy(arrow.gameObject);
            arrows.Clear();
            preparedEffect = -1;
        }

        private Arrow GetArrowPrefab(int effect)
        {
            if (arrowPrefab == null || arrowPrefab.Length == 0) return null;
            Arrow prefab = arrowPrefab[Mathf.Clamp(effect, 0, arrowPrefab.Length - 1)];
            return prefab != null ? prefab : arrowPrefab[0];
        }
    }
}
