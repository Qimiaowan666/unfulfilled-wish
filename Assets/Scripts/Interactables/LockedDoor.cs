using System.Collections;
using UnityEngine;

// 钥匙门:继承 InteractTrigger —— 靠近显示木框提示(有钥匙=「开门」/ 没钥匙=「需要钥匙」),按 F 开(只有有钥匙才生效)。
// 开门表现沿用教程门那套:升闸动画(openFrames 关→开,停在全开帧)+ 穿门分层(frontRenderer 右半排在玩家之上)。
// 需要两个碰撞:一个 Trigger(给 InteractTrigger 检测玩家)+ blockingCollider(实体挡路)。
[RequireComponent(typeof(Collider2D))]
public class LockedDoor : InteractTrigger
{
    [Header("Save")]
    public string saveID;

    [Header("门")]
    [Tooltip("设了=开门顺带把玩家传送过去;留空=原地放行(玩家自己走过去,才看得到穿门效果)")]
    public Transform destination;
    public string lockedPrompt = "需要钥匙";       // 没钥匙时的提示词
    public string openPrompt   = "开门";           // 有钥匙时的提示词(基类自动加 F 键帽)
    public SpriteRenderer doorRenderer;            // 背景层主门图(本物体 SpriteRenderer),升闸动画驱动它
    public Collider2D blockingCollider;            // 实体挡路碰撞(非 Trigger)

    [Header("钥匙")]
    [Tooltip("查背包里有没有这件钥匙道具(留空 = 这扇门没钥匙、开不了)")]
    public ItemData requiredKeyItem;

    [Header("开门动画(可选,教程门那套升闸 + 穿门分层)")]
    [Tooltip("开门帧序列(关→开),按顺序播一遍停在最后一帧。留空=直接隐藏门图")]
    public Sprite[] openFrames;
    [Tooltip("开门动画帧率")]
    public float openFps = 24f;
    [Tooltip("前景层渲染器(门的右半,排序在玩家之上)→ 玩家走过去被它盖住,产生穿门感")]
    public SpriteRenderer frontRenderer;
    [Tooltip("前景层(右半)的开门帧,与 openFrames 同步播放")]
    public Sprite[] frontFrames;

    public bool IsOpen { get; private set; }
    public string SaveID => SaveIdUtility.GetSceneObjectID(this, saveID);

    Transform player;

    protected override void Reset()
    {
        base.Reset();                              // 把碰撞设成 Trigger 供检测(实体阻挡另配 blockingCollider)
        prompt = openPrompt;
        doorRenderer = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();
    }

    // —— 提示 / 触发(覆盖 InteractTrigger)——

    // 显示条件:门没开 + 基类条件(靠近/没暂停/商店没开)+ 角色面板没开
    protected override bool ShowPrompt    => !IsOpen && base.ShowPrompt && !CharacterPanelUI.IsOpen;
    // F 生效条件:显示中 + 有钥匙(没钥匙时提示照显示,但按 F 不开)
    protected override bool WantsInteract => ShowPrompt && HasKey();

    protected override void Update()
    {
        prompt = HasKey() ? openPrompt : lockedPrompt;   // 提示词随钥匙状态切换
        base.Update();                                   // 显示木框 + F→Interact
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);
        if (other.CompareTag(Tags.Player)) player = other.transform;   // 记下玩家(传送用)
    }
    protected override void OnPlayerExit() => player = null;

    // 提示锚在实体门框顶(玩家被挡住的高度附近),而不是高高的门图顶
    protected override Vector3 PromptPoint()
    {
        var col = blockingCollider != null ? blockingCollider : GetComponent<Collider2D>();
        if (col != null)
            return new Vector3(col.bounds.center.x, col.bounds.max.y + promptHeightOffset, 0f);
        return base.PromptPoint();
    }

    protected override void Interact()
    {
        if (IsOpen || !HasKey()) return;
        Open();
    }

    // 查背包里有没有这件钥匙道具(未设 requiredKeyItem = 这扇门没钥匙、开不了)
    bool HasKey()
    {
        return requiredKeyItem != null
            && InventorySystem.Instance != null
            && InventorySystem.Instance.items.Contains(requiredKeyItem);
    }

    void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        SaveSystem.Instance?.MarkDoorOpened(SaveID);
        if (blockingCollider != null) blockingCollider.enabled = false;   // 立刻放行
        SetInteractTriggerEnabled(false);                                 // 连交互触发体一起关 → 箭/冲刺能穿开着的门
        InteractPromptUI.Hide(this);
        AudioManager.Instance?.PlayDoorOpen();

        // destination 设了才传送(留空=原地放行,走过去看穿门)
        if (destination != null && player != null)
        {
            player.position = destination.position;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        if (openFrames != null && openFrames.Length > 0 && isActiveAndEnabled)
            StartCoroutine(PlayOpenFrames());   // 升闸动画(停在全开帧)
        else
            HideDoor();                         // 没动画 → 直接隐藏门图
    }

    // 读档恢复:【双向】——存档里开着就置开(停在全开帧),关着就置关(回到闭合帧)。
    // boss 门在 ForsakenShrine 走原地恢复(不重载),所以必须双向,否则"开门后读旧档"门关不回来。
    public void LoadOpened(bool opened)
    {
        IsOpen = opened;
        StopAllCoroutines();                                                // 停掉可能在播的升闸动画
        if (blockingCollider != null) blockingCollider.enabled = !opened;   // 开→放行,关→挡路
        SetInteractTriggerEnabled(!opened);                                 // 开→关交互触发体(免挡箭/冲刺),关→恢复供检测

        if (openFrames != null && openFrames.Length > 0)
        {
            int idx = opened ? openFrames.Length - 1 : 0;                   // 开→全开帧,关→闭合帧(第0帧)
            if (doorRenderer != null && openFrames[idx] != null) { doorRenderer.sprite = openFrames[idx]; doorRenderer.enabled = true; }
            if (frontRenderer != null && frontFrames != null && idx < frontFrames.Length && frontFrames[idx] != null)
            { frontRenderer.sprite = frontFrames[idx]; frontRenderer.enabled = true; }
        }
        else
        {
            if (doorRenderer  != null) doorRenderer.enabled  = !opened;     // 没动画:开→隐藏门图,关→显示
            if (frontRenderer != null) frontRenderer.enabled = !opened;
        }
    }

    void HideDoor()
    {
        if (doorRenderer  != null) doorRenderer.enabled  = false;
        if (frontRenderer != null) frontRenderer.enabled = false;
    }

    // 开门时连交互触发体(isTrigger)一起开关:它在 Ground 层、比实体框还大,留着会挡箭矢(Arrow.OnTriggerEnter)和冲刺扫描。
    // 开 → 关掉(开着的门实体+触发全无,任何东西可穿);关 → 恢复(供 InteractTrigger 检测玩家)。blockingCollider 是非 Trigger,不受影响。
    void SetInteractTriggerEnabled(bool on)
    {
        foreach (var c in GetComponents<Collider2D>())
            if (c.isTrigger) c.enabled = on;
    }

    // 播开门帧序列(关→开),停在最后一帧;背景层=门自身渲染器,前景层=frontRenderer 同步
    IEnumerator PlayOpenFrames()
    {
        float dt = 1f / Mathf.Max(1f, openFps);
        bool hasFront = frontRenderer != null && frontFrames != null && frontFrames.Length > 0;
        for (int i = 0; i < openFrames.Length; i++)
        {
            if (doorRenderer != null && openFrames[i] != null) doorRenderer.sprite = openFrames[i];   // 背景层(整门)
            if (hasFront && i < frontFrames.Length && frontFrames[i] != null)
                frontRenderer.sprite = frontFrames[i];                                                // 前景层(右半)同步
            yield return new WaitForSeconds(dt);
        }
    }
}
