using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcherSimulator
{
    /// <summary>
    /// 표적. 화살이 몸통(Body)이나 약점(TargetWeakPoint)에 꽂히면 데미지를 받고, HP가 0이 되면 꺼진다.
    /// HP는 난이도에 따라 GameManager가 ResetTarget으로 정한다.
    /// </summary>
    public class Target : MonoBehaviour
    {
        [Tooltip("약점 명중 시 데미지 배율")]
        [SerializeField] private float weakPointMultiplier = 2f;

        [Header("HP 바")]
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;

        private int maxHp = 100;
        private int hp = 100;

        public int Hp => hp;
        public bool IsDestroyed => hp <= 0;

        /// <summary>명중했을 때 (표적, 준 데미지, 약점 여부). 파괴 여부는 IsDestroyed로 확인.</summary>
        public event Action<Target, int, bool> Hit;

        /// <summary>HP를 채우고 다시 켠다.</summary>
        public void ResetTarget(int newMaxHp)
        {
            maxHp = Mathf.Max(1, newMaxHp);
            hp = maxHp;
            gameObject.SetActive(true);
            UpdateBar();
        }

        /// <summary>Arrow가 꽂힐 때 호출한다.</summary>
        public void TakeHit(float damage, bool weakPoint)
        {
            if (IsDestroyed) return;

            int dealt = Mathf.Max(1, Mathf.RoundToInt(damage * (weakPoint ? weakPointMultiplier : 1f)));
            hp = Mathf.Max(0, hp - dealt);
            UpdateBar();
            Hit?.Invoke(this, dealt, weakPoint);

            // 꺼지면 꽂혀 있던 화살도 Arrow.LateUpdate에서 함께 꺼진다
            if (IsDestroyed) gameObject.SetActive(false);
        }

        private void UpdateBar()
        {
            if (hpFill != null) hpFill.fillAmount = (float)hp / maxHp;
            if (hpText != null) hpText.text = $"{hp} / {maxHp}";
        }
    }
}
