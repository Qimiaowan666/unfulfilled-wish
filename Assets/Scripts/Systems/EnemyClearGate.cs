using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// 清场雾门:把指定的一组怪(enemies)全部击败 → 雾散开门(关挡路碰撞 + 雾墙淡出)。
// 自身不另存档:开没开从「这些怪是否已死」推导 → 所以 enemies 里的怪要勾 permanentDeath
//   (否则读档/火堆复活,雾又会判成关)。SaveSystem.AfterApply 在怪态恢复完后再判一次,解决读档时序,
//   而且是【双向】对齐:读"杀怪前的旧档"怪复活 → 雾重新淡入挡住(不会泄漏成开)。
[RequireComponent(typeof(Collider2D))]
public class EnemyClearGate : MonoBehaviour
{
    [Tooltip("挡路的实心碰撞(留空=本物体上的非触发碰撞)")]
    public Collider2D blockingCollider;
    [Tooltip("雾墙渲染器(开门淡出 a→0、关门淡入 a→1);留空=本物体 SpriteRenderer")]
    [FormerlySerializedAs("doorRenderer")]
    public SpriteRenderer fogRenderer;
    [Tooltip("雾墙淡入/淡出时长")]
    public float fadeTime = 0.5f;
    [Tooltip("要清光的怪——全部死亡/消失才开门。务必给这些怪勾 permanentDeath,否则读档复活、雾又起")]
    public EnemyBase[] enemies;

    bool opened;          // 初始 false = 雾起着(碰撞 + 雾墙都在)
    Coroutine fade;

    void Reset()
    {
        blockingCollider = GetComponent<Collider2D>();
        fogRenderer      = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        if (fogRenderer == null) fogRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (enemies != null)
            foreach (var e in enemies) if (e != null) e.OnDied += OnEnemyDied;
        SaveSystem.AfterApply += OnAfterApply;
    }

    void OnDisable()
    {
        if (enemies != null)
            foreach (var e in enemies) if (e != null) e.OnDied -= OnEnemyDied;
        SaveSystem.AfterApply -= OnAfterApply;
    }

    void Start() => Sync(false);                                   // 兜底:无存档新局也按"怪是否全死"对齐一次
    void OnEnemyDied() { if (AllDefeated()) SetOpen(true, true); } // 实时击杀打空 → 开 + 播音
    void OnAfterApply() => Sync(false);                           // 读档恢复完(怪态已应用)→ 静默双向对齐

    // 按"怪是否全死"对齐雾门状态(开/关都管)
    void Sync(bool playSound) => SetOpen(AllDefeated(), playSound);

    bool AllDefeated()
    {
        if (enemies == null) return false;
        foreach (var e in enemies)
            if (e != null && e.gameObject.activeSelf && e.CurrentHP > 0f) return false;   // 还有活的
        return true;
    }

    void SetOpen(bool open, bool playSound)
    {
        if (open == opened) return;
        opened = open;
        if (blockingCollider != null) blockingCollider.enabled = !open;   // 开→放行,关→挡路
        StartFade(open ? 0f : 1f);                                        // 开→雾散(a0),关→雾起(a1)
        if (open && playSound) AudioManager.Instance?.PlayDoorOpen();
    }

    void StartFade(float targetA)
    {
        if (fogRenderer == null) return;
        if (fade != null) StopCoroutine(fade);
        if (isActiveAndEnabled) fade = StartCoroutine(FadeRoutine(targetA));
        else { var c = fogRenderer.color; c.a = targetA; fogRenderer.color = c; }   // 没激活→直接置
    }

    IEnumerator FadeRoutine(float targetA)
    {
        float startA = fogRenderer.color.a, t = 0f;
        float dur = Mathf.Max(0.01f, fadeTime);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // 演出常改 timeScale,用 unscaled
            var c = fogRenderer.color; c.a = Mathf.Lerp(startA, targetA, t / dur); fogRenderer.color = c;
            yield return null;
        }
        var cc = fogRenderer.color; cc.a = targetA; fogRenderer.color = cc;
        fade = null;
    }

    void OnDrawGizmosSelected()
    {
        var col = blockingCollider != null ? blockingCollider : GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = opened ? new Color(0.4f, 1f, 0.5f, 0.5f) : new Color(1f, 0.5f, 0.2f, 0.5f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
