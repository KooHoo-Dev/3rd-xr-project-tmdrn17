using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.Common
{
    public class PlayerHeightPanel : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button seatedButton;
        [SerializeField] private Button standingButton;
        [SerializeField] private Button lowerButton;
        [SerializeField] private Button higherButton;
        [SerializeField] private Button fitButton;

        [Header("표시")]
        [SerializeField] private TMP_Text readout;
        [SerializeField] private Color normalColor = new Color(0.20f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.60f, 0.15f, 1f);

        private float nextRefresh;

        private void Start()
        {
            if (seatedButton != null)
                seatedButton.onClick.AddListener(() => PlayerHeight.Offset = PlayerHeight.SeatedOffset);
            if (standingButton != null)
                standingButton.onClick.AddListener(() => PlayerHeight.Offset = PlayerHeight.StandingOffset);
            if (lowerButton != null) lowerButton.onClick.AddListener(() => PlayerHeight.Offset -= PlayerHeight.Step);
            if (higherButton != null) higherButton.onClick.AddListener(() => PlayerHeight.Offset += PlayerHeight.Step);
            if (fitButton != null) fitButton.onClick.AddListener(FitToCurrentPose);
            Refresh();
        }

        private void OnEnable()
        {
            PlayerHeight.Changed += OnChanged;
        }

        private void OnDisable()
        {
            PlayerHeight.Changed -= OnChanged;
        }

        private void OnChanged(float offset)
        {
            Refresh();
        }

        private void FitToCurrentPose()
        {
            float raw = PlayerHeightApplier.RawEyeHeight;

            if (float.IsNaN(raw)) return;
            PlayerHeight.Offset = PlayerHeight.StandingEyeHeight - raw;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            Refresh();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.2f;

            Paint(seatedButton, PlayerHeight.IsSeated);
            Paint(standingButton, PlayerHeight.IsStanding);

            if (readout == null) return;
            float offset = PlayerHeight.Offset;
            float eye = PlayerHeightApplier.EyeHeight;
            string eyeText = float.IsNaN(eye) ? "-" : $"{eye:0.00} m";
            readout.text = $"지금: <b>{PlayerHeight.ModeName}</b>  ·  보정 {offset:+0.00;-0.00;0.00} m\n눈높이 {eyeText} (서 있는 눈높이 약 {PlayerHeight.StandingEyeHeight:0.0} m)";
        }

        private void Paint(Button button, bool selected)
        {
            if (button == null || button.targetGraphic == null) return;
            button.targetGraphic.color = selected ? selectedColor : normalColor;
        }
    }
}
