using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyXR.Common
{
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Renderer fadeRenderer;
        [SerializeField] private float defaultDuration = 0.25f;
        [SerializeField] private Color color = Color.black;

        [Header("씬 전환")]
        [Tooltip("씬이 시작될 때 검은 화면에서 페이드 인 (앱의 첫 씬용)")]
        [SerializeField] private bool fadeInOnStart;
        [SerializeField] private float sceneFadeDuration = 0.8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // LoadScene으로 넘어온 다음 씬은 검은 화면에서 시작해 페이드 인한다.
        private static bool fadeInNextScene;

        private MaterialPropertyBlock block;
        private Coroutine running;
        private float alpha;
        private bool startWithFadeIn;

        public bool IsFading => (running != null);

        private void Awake()
        {
            startWithFadeIn = fadeInOnStart || fadeInNextScene;
            fadeInNextScene = false;
            Apply(startWithFadeIn ? 1f : 0f);
        }

        private void Start()
        {
            if (startWithFadeIn) running = StartCoroutine(FadeIn(sceneFadeDuration));
        }

        /// <summary>
        /// 페이드 아웃한 뒤 씬을 불러온다. 다음 씬의 ScreenFade는 검은 화면에서 페이드 인한다.
        /// 씬에 ScreenFade가 없으면 바로 불러온다.
        /// </summary>
        public static void LoadSceneWithFade(string sceneName)
        {
            ScreenFade fade = FindFirstObjectByType<ScreenFade>();
            if (fade == null || fade.isActiveAndEnabled == false)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            fade.LoadScene(sceneName);
        }

        public void LoadScene(string sceneName, float duration = -1f)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeOutAndLoad(sceneName, duration < 0f ? sceneFadeDuration : duration));
        }

        private IEnumerator FadeOutAndLoad(string sceneName, float seconds)
        {
            yield return Blend(1f, seconds);
            fadeInNextScene = true;
            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator FadeIn(float seconds)
        {
            // 씬을 막 불러온 첫 프레임은 deltaTime이 커서 페이드가 건너뛰어지므로 한 프레임 기다린다.
            yield return null;
            yield return Blend(0f, seconds);
            running = null;
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
