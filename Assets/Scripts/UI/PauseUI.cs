using UnityEngine;
using UnityEngine.InputSystem;

public class PauseUI : MonoBehaviour
{
    public GameObject panel;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            GameManager.Instance?.TogglePause();
    }

    void Start()
    {
        panel.SetActive(false);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGamePaused  += () => panel.SetActive(true);
            GameManager.Instance.OnGameResumed += () => panel.SetActive(false);
        }
    }

    public void Resume() => GameManager.Instance?.TogglePause();

    public void Restart()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.RestartScene();
    }
}
