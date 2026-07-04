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
        SaveSystem.AfterApply += RefreshBoss;   // 同场景原地读档没有 sceneLoaded，靠这个重新接管 boss
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SaveSystem.AfterApply -= RefreshBoss;
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
        RefreshBoss();
    }

    // 重新查找本场景 boss + 重启监测（场景加载后 / 任意读档 apply 后都会调）
    void RefreshBoss()
    {
        if (watchRoutine != null) { StopCoroutine(watchRoutine); watchRoutine = null; }

        currentBoss = FindSceneBoss();
        if (currentBoss != null)
        {
            // 这里不放 boss BGM：进场/读档时 boss 总是沉睡（BossIntroTrigger 会把它睡回去并重置遭遇），
            // 区域曲(area1)由 AudioManager 场景默认 + 读档时 RefreshSceneBGM 负责；
            // boss 曲只在玩家触发遭遇、吼叫开打那一刻由 BossIntroTrigger.StartCombat 放。
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
        var b = currentBoss;
        yield return new WaitUntil(() => b == null || b.CurrentHP <= 0f);
        bool active = b != null && b.gameObject.activeInHierarchy;
        // 只有 boss「战斗中被打死」才触发：对象还在 + HP≤0 + 仍激活(战斗死亡会留着播死亡动画；
        // 读档把 boss 还原成已击败会 SetActive(false)，借此区分，避免开局/读通关档误触发演出)。
        if (b != null && b.CurrentHP <= 0f && active && b == currentBoss)
            OnBossDefeated();
    }

    void OnBossDefeated()
    {
        string next = currentBoss != null ? currentBoss.nextSceneOnDefeat : null;
        if (!string.IsNullOrEmpty(next))
        {
            StartCoroutine(DelayedLoad(next));                  // 有下一关 → 等一下切场景
            return;
        }

        // 最终 boss 已击破 → 存一次完整自动档:把"boss 已死 + 背包/装备/金币/当前状态"原子写进 save_auto,
        // 让死亡/继续都拿到自洽快照(杜绝"boss 死了但掉落没了"的 desync),并让通关前的进度持久。
        // (有 nextSceneOnDefeat 的 boss 上面已 return,不在这存——那种要等切到下一场景后再存,
        //  否则继续会落回已清空、无法再触发切场景的 boss 场景。)
        SaveSystem.Instance?.AutoSaveAtPlayer();

        // 最终 boss → 九日式击破演出 → 出现通关门(玩家走近按 F 才通关)
        if (BossFinishUI.Instance != null && currentBoss != null)
            BossFinishUI.Instance.Play(currentBoss, ShowEnding);
        else
            StartCoroutine(DelayedEnding());
    }

    void ShowEnding()
    {
        var gate = FindAnyObjectByType<VictoryGate>();
        if (gate != null) gate.Appear();         // 有通关门 → 出现，等玩家交互
        else VictoryUI.Instance?.Show();         // 没门兜底 → 直接通关画面
    }

    IEnumerator DelayedLoad(string next)
    {
        yield return new WaitForSeconds(victoryDelay);
        GameManager.Instance?.LoadScene(next);
    }

    IEnumerator DelayedEnding()
    {
        yield return new WaitForSeconds(victoryDelay);
        ShowEnding();
    }
}
