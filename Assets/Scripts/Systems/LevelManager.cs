using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 常驻关卡协调器：放在 Bootstrap，跨场景存活（DontDestroyOnLoad 单例）。
// 每次场景加载后自动查找本场景的 boss（EnemyBase.isBoss），接管 boss BGM + 胜利检测，
// 击败后按 boss 自带的 nextSceneOnDefeat 切场景。
// → 不再需要每个战斗场景手动挂 LevelManager / 连 boss 引用。
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Boss Battle")]
    public float victoryDelay = 2f;

    EnemyBase currentBoss;
    Coroutine watchRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(SetupSceneBoss());
    }

    IEnumerator SetupSceneBoss()
    {
        yield return null;   // 等场景物体 Awake
        yield return null;   // 再等一帧，确保 SaveSystem 已 apply 敌人存档态（已击败的 boss 不会被误判为存活）

        if (watchRoutine != null) { StopCoroutine(watchRoutine); watchRoutine = null; }

        currentBoss = FindSceneBoss();
        if (currentBoss != null)
        {
            AudioManager.Instance?.PlayBossBGM();
            watchRoutine = StartCoroutine(WatchBoss());
        }
    }

    EnemyBase FindSceneBoss()
    {
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.isBoss && e.gameObject.activeInHierarchy && e.CurrentHP > 0f)
                return e;
        return null;
    }

    IEnumerator WatchBoss()
    {
        yield return new WaitUntil(() => currentBoss == null || currentBoss.CurrentHP <= 0f);
        yield return new WaitForSeconds(victoryDelay);
        OnBossDefeated();
    }

    void OnBossDefeated()
    {
        string next = currentBoss != null ? currentBoss.nextSceneOnDefeat : null;
        if (!string.IsNullOrEmpty(next))
            GameManager.Instance?.LoadScene(next);
        else
            Debug.Log("Boss defeated — no next scene assigned.");
    }
}
