using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsGameOver { get; private set; }
    public bool IsPaused { get; private set; }

    public event System.Action OnGameOver;
    public event System.Action OnGamePaused;
    public event System.Action OnGameResumed;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        OnGameOver?.Invoke();
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        if (IsPaused) OnGamePaused?.Invoke();
        else OnGameResumed?.Invoke();
    }

    public void RestartScene()
    {
        IsGameOver = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);
}
