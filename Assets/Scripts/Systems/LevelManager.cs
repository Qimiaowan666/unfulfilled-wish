using UnityEngine;
using System.Collections;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Boss")]
    public EnemyBase boss;
    public float victoryDelay = 2f;
    public string nextSceneName;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (boss != null)
        {
            AudioManager.Instance?.PlayBossBGM();
            StartCoroutine(WatchBoss());
        }

        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
            stats.OnDeath += OnPlayerDeath;
    }

    IEnumerator WatchBoss()
    {
        yield return new WaitUntil(() => boss == null || boss.CurrentHP <= 0f);
        yield return new WaitForSeconds(victoryDelay);
        OnBossDefeated();
    }

    void OnBossDefeated()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            GameManager.Instance?.LoadScene(nextSceneName);
        else
            Debug.Log("Boss defeated — no next scene assigned.");
    }

    void OnPlayerDeath()
    {
        GameManager.Instance?.TriggerGameOver();
    }
}
