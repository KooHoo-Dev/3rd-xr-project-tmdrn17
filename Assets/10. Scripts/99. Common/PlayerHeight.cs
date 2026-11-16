using System;
using UnityEngine;

namespace StudyXR.Common
{
    public static class PlayerHeight
    {
        public const float SeatedOffset = 0.40f;
        public const float StandingOffset = 0f;
        public const float StandingEyeHeight = 1.60f;
        public const float Step = 0.05f;
        private const float MinOffset = -0.50f;
        private const float MaxOffset = 1.00f;

        private const string PrefsKey = "StudyXR.PlayerHeight.Offset";

        private static float DefaultOffset => Application.isEditor ? StandingOffset : SeatedOffset;

        public static event Action<float> Changed;

        private static bool loaded;
        private static float cached;

        public static float Offset
        {
            get
            {
                if (loaded == false)
                {
                    cached = PlayerPrefs.GetFloat(PrefsKey, DefaultOffset);
                    loaded = true;
                }
                return cached;
            }
            set
            {
                float clamped = Mathf.Clamp(Mathf.Round(value * 100f) / 100f, MinOffset, MaxOffset);
                cached = clamped;
                loaded = true;
                PlayerPrefs.SetFloat(PrefsKey, clamped);
                PlayerPrefs.Save();
                Changed?.Invoke(clamped);
            }
        }

        public static bool IsSeated => (Mathf.Abs(Offset - SeatedOffset) < 0.005f);
        public static bool IsStanding => (Mathf.Abs(Offset - StandingOffset) < 0.005f);

        public static string ModeName => IsSeated ?
            "앉아서"
            : IsStanding
                ? "서서"
                : "직접 맞춤";
    }
}
