using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public GameObject panel;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        BindRestartButtons();

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver += Show;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= Show;
    }

    void Show()
    {
        if (panel != null) panel.SetActive(true);
        Time.timeScale = 0f;
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
}
