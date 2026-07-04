using System.Collections;
using UnityEngine;

// 教程机关：一堵挡路的墙。由对应的 TutorialSequence 完成全部步骤后调 Open() 打开
// (关碰撞放行 + 升闸动画 + 穿门分层 + 开门音)。开门"条件/计数"都在 TutorialSequence 里,门只管"挡 + 开 + 演出"。
[RequireComponent(typeof(Collider2D))]
public class TutorialGate : MonoBehaviour
{
    [Tooltip("挡路的实心碰撞体(留空=本物体上的非触发碰撞体)")]
    public Collider2D blockingCollider;

    [Header("开门动画(可选)")]
    [Tooltip("开门帧序列(关→开),按顺序播一遍后停在最后一帧。留空则直接隐藏门图")]
    public Sprite[] openFrames;
    [Tooltip("开门动画帧率")]
    public float openFps = 24f;

    [Header("穿门分层(可选)")]
    [Tooltip("前景层渲染器(门的右半,排序在玩家之上)→ 玩家走过去会被它盖住,产生穿门感")]
    public SpriteRenderer frontRenderer;
    [Tooltip("前景层(右半)的开门帧,与 openFrames 同步播放")]
    public Sprite[] frontFrames;

    public event System.Action Opened;   // 门打开那一刻触发(TutorialEnemy 订阅来消失等)

    [Tooltip("存档标识(留空=用物体名);手动存档里开过的门,读档恢复成开")]
    public string saveID;
    public string SaveID => SaveIdUtility.WithScene(this, string.IsNullOrEmpty(saveID) ? gameObject.name : saveID);
    public bool IsOpen => opened;

    bool           opened;
    SpriteRenderer backRenderer;   // 背景层主门图(门物体自身的 SpriteRenderer),升闸动画驱动它

    void Reset() { blockingCollider = GetComponent<Collider2D>(); }

    void Awake()
    {
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        backRenderer = GetComponent<SpriteRenderer>();
    }

    // 由对应 TutorialSequence(全部步骤完成)调用 → 放行 + 演出
    public void Open()
    {
        if (opened) return;
        opened = true;
        SaveSystem.Instance?.MarkDoorOpened(SaveID);                       // 手动存档持久化:读档恢复成开
        if (blockingCollider != null) blockingCollider.enabled = false;   // 立刻放行
        Opened?.Invoke();                                                 // 通知订阅者(如练习敌人消失)
        AudioManager.Instance?.PlayDoorOpen();

        if (openFrames != null && openFrames.Length > 0 && isActiveAndEnabled)
            StartCoroutine(PlayOpenFrames());   // 播升闸动画(停在全开帧)
        else
            HideDoor();                         // 没动画 / 组件未激活 → 直接隐藏门图
    }

    // 读档恢复:【双向】——开过的门置开(停在全开帧),没开的置关(回闭合帧 + 挡路)。
    // 教程目前恒整场景重载(门每次重建为关),所以关方向暂用不上;此处双向是防御性自洽,
    // 万一以后教程走原地读档 / 门被用到别处,不会出现"读旧档门关不回来"的泄漏。对齐 LockedDoor.LoadOpened。
    public void LoadOpened(bool isOpen)
    {
        if (isOpen == opened) return;   // 状态没变,免重复
        opened = isOpen;
        StopAllCoroutines();                                              // 掐掉可能在播的升闸
        if (blockingCollider != null) blockingCollider.enabled = !isOpen; // 开→放行,关→挡路

        if (openFrames != null && openFrames.Length > 0)
        {
            int idx = isOpen ? openFrames.Length - 1 : 0;                 // 开→全开帧,关→闭合帧(第0帧)
            if (backRenderer != null && openFrames[idx] != null) { backRenderer.sprite = openFrames[idx]; backRenderer.enabled = true; }
            if (frontRenderer != null && frontFrames != null && idx < frontFrames.Length && frontFrames[idx] != null)
            { frontRenderer.sprite = frontFrames[idx]; frontRenderer.enabled = true; }
        }
        else
        {
            if (backRenderer  != null) backRenderer.enabled  = !isOpen;   // 没动画:开→隐藏,关→显示
            if (frontRenderer != null) frontRenderer.enabled = !isOpen;
        }

        if (isOpen) Opened?.Invoke();   // 只在开门时通知订阅者(练习敌人消失 / TutorialSequence 清击败目标)
    }

    void HideDoor()
    {
        if (backRenderer  != null) backRenderer.enabled  = false;
        if (frontRenderer != null) frontRenderer.enabled = false;
    }

    // 播开门帧序列(关→开),停在最后一帧;背景层=门自身渲染器,前景层=frontRenderer 同步
    IEnumerator PlayOpenFrames()
    {
        float dt = 1f / Mathf.Max(1f, openFps);
        bool hasFront = frontRenderer != null && frontFrames != null && frontFrames.Length > 0;
        for (int i = 0; i < openFrames.Length; i++)
        {
            if (backRenderer != null && openFrames[i] != null) backRenderer.sprite = openFrames[i];   // 背景层(整门)
            if (hasFront && i < frontFrames.Length && frontFrames[i] != null)
                frontRenderer.sprite = frontFrames[i];                                                // 前景层(右半)同步
            yield return new WaitForSeconds(dt);
        }
    }

    // 选中时画一下机关范围
    void OnDrawGizmosSelected()
    {
        var col = blockingCollider != null ? blockingCollider : GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = opened ? new Color(0.4f, 1f, 0.5f, 0.5f) : new Color(1f, 0.5f, 0.2f, 0.5f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
