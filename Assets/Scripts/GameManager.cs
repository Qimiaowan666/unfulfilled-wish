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
        IsGameOver = false;
        IsPaused = false;
        Time.timeScale = 1f;
        transform.SetParent(null);
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
        IsPaused = false;
        Time.timeScale = 1f;
        SaveSystem.Instance?.PrepareRespawn();   // 重生：全局态回存档 + 落火堆复活点
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadScene(string sceneName)
    {
        IsGameOver = false;
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
