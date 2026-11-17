using System;
using UnityEngine;

namespace ArcherSimulator
{
    /// <summary>
    /// 보유 포인트 = 강화 재료. 라운드에서 얻은 점수가 그대로 쌓이고, 활을 강화할 때 쓴다.
    /// 저장하지 않는다 — 플레이를 다시 시작하면 0부터.
    /// </summary>
    public static class PointWallet
    {
        public static int Points { get; private set; }

        /// <summary>포인트가 바뀌었을 때 (새 보유량)</summary>
        public static event Action<int> Changed;

        // 도메인 리로드를 끈 상태로 플레이해도 0부터 시작하게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Points = 0;
            Changed = null;
        }

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            Set(Points + amount);
        }

        /// <summary>충분하면 차감하고 true, 부족하면 그대로 두고 false.</summary>
        public static bool TrySpend(int amount)
        {
            if (amount < 0 || Points < amount) return false;
            Set(Points - amount);
            return true;
        }

        private static void Set(int value)
        {
            Points = Mathf.Max(0, value);
            Changed?.Invoke(Points);
        }
    }
}
