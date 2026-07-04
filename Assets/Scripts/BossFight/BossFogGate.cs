using System.Collections;
using UnityEngine;

// 雾门（类暗魂）：boss 战开始时关上（实体挡住出口 + 雾墙淡入），
// boss 死亡 / 读档重生重置时自动打开。挂在出口处的竖墙物体上。
public class BossFogGate : MonoBehaviour
{
    [Tooltip("挡路的实体碰撞（非 Trigger，放在玩家会碰撞的层如 Ground）；留空自动取本物体 Collider2D")]
    public Collider2D blocker;
    [Tooltip("雾墙视觉；留空自动取子物体 SpriteRenderer")]
    public SpriteRenderer fog;
    [Tooltip("看守的 boss：它死亡 / 消失时自动开门（由 BossIntroTrigger 在 Start 自动填入）")]
    public EnemyBase boss;
    [Tooltip("雾墙淡入/淡出时长")]
    public float fadeTime = 0.5f;

    bool closed;
    Coroutine fade;

    void Awake()
    {
        if (blocker == null) blocker = GetComponent<Collider2D>();
        if (fog == null) fog = GetComponentInChildren<SpriteRenderer>(true);
        OpenInstant();
    }

    void OnEnable()  { SaveSystem.AfterApply += OnSaveApplied; }
    void OnDisable() { SaveSystem.AfterApply -= OnSaveApplied; }

    // 读档 / 重生重置 → 开门（是否再次关上由 BossIntroTrigger 重新判定）
    void OnSaveApplied() => Open();

    void Update()
    {
        // 关着且 boss 已死 / 消失 → 自动开门（放玩家出去）
        if (closed && (boss == null || !boss.gameObject.activeInHierarchy || boss.CurrentHP <= 0f))
            Open();
    }

    public void Close()
    {
        if (closed) return;
        closed = true;
        if (blocker != null) blocker.enabled = true;
        StartFade(1f);
    }

    public void Open()
    {
        closed = false;
        if (blocker != null) blocker.enabled = false;
        StartFade(0f);
    }

    void OpenInstant()
    {
        closed = false;
        if (blocker != null) blocker.enabled = false;
        if (fog != null) { var c = fog.color; c.a = 0f; fog.color = c; }
    }

    void StartFade(float targetA)
    {
        if (fog == null) return;
        if (fade != null) StopCoroutine(fade);
        if (isActiveAndEnabled) fade = StartCoroutine(FadeRoutine(targetA));
        else { var c = fog.color; c.a = targetA; fog.color = c; }
    }

    IEnumerator FadeRoutine(float targetA)
    {
        float startA = fog.color.a, t = 0f;
        float dur = Mathf.Max(0.01f, fadeTime);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // 演出常把 timeScale 改了，用 unscaled
            var c = fog.color; c.a = Mathf.Lerp(startA, targetA, t / dur); fog.color = c;
            yield return null;
        }
        var cc = fog.color; cc.a = targetA; fog.color = cc;
        fade = null;
    }
}
