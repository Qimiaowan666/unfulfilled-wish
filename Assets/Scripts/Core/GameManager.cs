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

        // 教程场景：死亡不重载、就地复活 → 内存里的教程进度(开过的门/已过的站)原样保留，且完全不碰存读档
        if (SceneManager.GetActiveScene().name == SceneNames.Tutorial && RespawnInPlace())
            return;

        SaveSystem.Instance?.PrepareRespawn();   // 重生：全局态回存档 + 落火堆复活点

        // 死亡 → 回到“上次火堆所在的场景”(存档 sceneName);没存档则重载当前场景
        string target = SceneManager.GetActiveScene().name;
        var data = SaveSystem.Instance?.Load();
        if (data != null && !string.IsNullOrEmpty(data.sceneName))
            target = data.sceneName;
        SceneManager.LoadScene(target);
    }

    // 就地复活(教程用)：重置玩家(满血/原地起身/idle) + 复活还活着的练习怪。不重载场景，
    // 所以开过的门、已过的站在内存里保留。已击败的怪死亡后已 SetActive(false)，不在查找列表里、不会被复活。
    // 返回 false = 没玩家、无法就地复活，交回调用方走正常重载。
    bool RespawnInPlace()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player == null || player.Stats == null) return false;

        player.Stats.RestoreAll();   // 清死亡标志 deathTriggered + 满血满体力
        player.Revive();             // 解死亡态：Unlock 状态机 + 恢复物理 + 回 idle(光 ChangeState 会被死亡锁忽略，人起不来)

        // 原地复活(死哪起哪，不传送)→ 教程不走回头路；只复活"会在检查点重生"的怪(非 permadeath)，
        // 与 SaveSystem.RefreshRespawnableEnemies 口径一致：permadeath 练习怪保持当前态，不被重置满血/位置。
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.Initialized && e.RespawnsAtCheckpoint) e.Respawn();

        return true;
    }

    public void LoadScene(string sceneName)
    {
        IsGameOver = false;
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
