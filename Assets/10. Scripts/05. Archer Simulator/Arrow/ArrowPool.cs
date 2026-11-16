using UnityEngine;
using UnityEngine.Pool;

namespace ArcherSimulator
{
    /// <summary>화살 오브젝트 풀 — Instantiate/Destroy 대신 재사용해 VR 프레임 드랍을 막는다.</summary>
    public class ArrowPool : MonoBehaviour
    {
        [SerializeField] private Arrow arrowPrefab;
        [SerializeField] private int prewarmCount = 12;
        [SerializeField] private int maxSize = 40;

        private ObjectPool<Arrow> pool;

        private void Awake()
        {
            pool = new ObjectPool<Arrow>(Create, OnGet, OnRelease, OnDestroyArrow, false, prewarmCount, maxSize);

            var warm = new Arrow[prewarmCount];
            for (int i = 0; i < prewarmCount; i++) warm[i] = pool.Get();
            foreach (Arrow arrow in warm) pool.Release(arrow);
        }

        public Arrow Get() => pool.Get();

        public void Release(Arrow arrow)
        {
            if (arrow == null) return;
            arrow.transform.SetParent(transform, false);
            pool.Release(arrow);
        }

        private Arrow Create()
        {
            Arrow arrow = Instantiate(arrowPrefab, transform);
            arrow.SetPool(this);
            return arrow;
        }

        private static void OnGet(Arrow arrow) => arrow.gameObject.SetActive(true);
        private static void OnRelease(Arrow arrow) => arrow.gameObject.SetActive(false);
        private static void OnDestroyArrow(Arrow arrow) => Destroy(arrow.gameObject);
    }
}
