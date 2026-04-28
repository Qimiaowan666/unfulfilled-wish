using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public GameObject panel;

    void Start()
    {
        panel.SetActive(false);
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver += Show;
    }

    void Show()
    {
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        GameManager.Instance.RestartScene();
    }
}
