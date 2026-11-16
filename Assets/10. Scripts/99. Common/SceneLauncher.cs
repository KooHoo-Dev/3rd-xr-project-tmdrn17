using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudyXR.Common
{
    public class SceneLauncher : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private RectTransform buttonArea;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("버튼 배치")]
        [SerializeField] private int columns = 2;
        [SerializeField] private int rowsPerPage = 8;
        [SerializeField] private Vector2 buttonSize = new Vector2(440f, 60f);
        [SerializeField] private Vector2 spacing = new Vector2(16f, 12f);
        [SerializeField] private float fontSize = 24f;

        private int page;

        private int SceneCount => SceneManager.sceneCountInBuildSettings - 1;
        private int PerPage => Mathf.Max(1, columns * rowsPerPage);
        private int PageCount => Mathf.Max(1, (SceneCount + PerPage - 1) / PerPage);

        private void Start()
        {
            if (previousButton != null) previousButton.onClick.AddListener(() => ShowPage(page - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(page + 1));
            ShowPage(0);
        }

        private void ShowPage(int newPage)
        {
            page = Mathf.Clamp(newPage, 0, PageCount - 1);
            if (buttonArea == null) return;

            for (int i = buttonArea.childCount - 1; i >= 0; --i)
                Destroy(buttonArea.GetChild(i).gameObject);

            int first = page * PerPage;
            for (int slot = 0; slot < PerPage && first + slot < SceneCount; ++slot)
            {
                int buildIndex = first + slot + 1;
                string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
                CreateButton(slot, buildIndex, Label(path));
            }

            if (pageText != null) pageText.text = $"{page + 1} / {PageCount} 쪽 · 씬 {SceneCount}개";
            if (previousButton != null) previousButton.interactable = (page > 0);
            if (nextButton != null) nextButton.interactable = (page < PageCount - 1);
        }

        private static string Label(string path)
        {
            string scene = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            string module = folder != null && folder.Length >= 2 ? folder.Substring(0, 2) : "--";

            return $"{module} · {scene}";
        }

        private void CreateButton(
            int slot,
            int buildIndex,
            string label)
        {
            int column = slot % columns;
            int row = slot / columns;

            var go = new GameObject($"Scene {buildIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonArea, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = buttonSize;
            rect.anchoredPosition = new Vector2(column * (buttonSize.x + spacing.x), -row * (buttonSize.y + spacing.y));

            var image = go.GetComponent<Image>();
            image.color = new Color(0.20f, 0.45f, 0.85f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SceneManager.LoadScene(buildIndex));

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
