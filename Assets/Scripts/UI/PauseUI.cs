using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseUI : MonoBehaviour
{
    public GameObject panel;
    public Button resumeButton;

    void Update()
    {
        if (ShopUI.IsOpen) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            PlayerInputBuffer.ClearAll();
            GameManager.Instance?.TogglePause();
        }
    }

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        EnsureResumeButton();
        BindRestartButtons();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGamePaused += () => { if (panel != null) panel.SetActive(true); };
            GameManager.Instance.OnGameResumed += () => { if (panel != null) panel.SetActive(false); };
        }
    }

    public void Resume()
    {
        PlayerInputBuffer.ClearAll();

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsPaused)
                GameManager.Instance.TogglePause();
        }
        else
        {
            Time.timeScale = 1f;
            if (panel != null) panel.SetActive(false);
        }
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.RestartScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void BindRestartButtons()
    {
        Transform root = panel != null ? panel.transform : transform;
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (!button.name.ToLowerInvariant().Contains("restart")) continue;

            button.onClick.RemoveListener(Restart);
            button.onClick.AddListener(Restart);
        }
    }

    void EnsureResumeButton()
    {
        if (panel == null) return;

        if (resumeButton == null)
            resumeButton = FindResumeButton();

        if (resumeButton == null)
            resumeButton = CreateResumeButton();

        resumeButton.onClick.RemoveListener(Resume);
        resumeButton.onClick.AddListener(Resume);
    }

    Button FindResumeButton()
    {
        Transform root = panel != null ? panel.transform : transform;
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            string lowerName = button.name.ToLowerInvariant();
            if (lowerName.Contains("resume") || lowerName.Contains("continue") || button.name.Contains("继续"))
                return button;
        }

        return null;
    }

    Button CreateResumeButton()
    {
        var buttonObject = new GameObject("ResumeButton", typeof(RectTransform));
        buttonObject.transform.SetParent(panel.transform, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 10f);
        rect.sizeDelta = new Vector2(180f, 42f);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.32f, 0.24f, 0.95f);

        var button = buttonObject.AddComponent<Button>();

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObject.AddComponent<Text>();
        text.text = "继续";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return button;
    }
}
