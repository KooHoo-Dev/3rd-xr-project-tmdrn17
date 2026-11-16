using System;
using System.Collections;
using UnityEngine;

namespace StudyXR.Common
{
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Renderer fadeRenderer;
        [SerializeField] private float defaultDuration = 0.25f;
        [SerializeField] private Color color = Color.black;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock block;
        private Coroutine running;
        private float alpha;

        public bool IsFading => (running != null);

        private void Awake()
        {
            Apply(0f);
        }

        private void OnDisable()
        {
            running = null;
            Apply(0f);
        }

        public void FadeOutIn(Action midAction, float duration = -1f)
        {
            if (running != null) StopCoroutine(running);
            float seconds = duration < 0f ? defaultDuration : duration;
            running = StartCoroutine(Run(seconds, midAction));
        }

        private IEnumerator Run(float seconds, Action midAction)
        {
            yield return Blend(1f, seconds);
            if (midAction != null) midAction();
            yield return Blend(0f, seconds);
            running = null;
        }

        private IEnumerator Blend(float target, float seconds)
        {
            float start = alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(start, target, t / seconds));
                yield return null;
            }
            Apply(target);
        }

        private void Apply(float value)
        {
            alpha = value;
            if (fadeRenderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();

            fadeRenderer.enabled = (value > 0f);
            fadeRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, value));
            fadeRenderer.SetPropertyBlock(block);
        }
    }
}
