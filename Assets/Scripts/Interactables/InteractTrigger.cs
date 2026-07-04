using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 交互触发器基类:玩家进触发区 → 头顶木框提示(InteractPromptUI),按 F 触发。
// 拾取/火堆/传送门/商人都继承它。子类只需实现 Interact();可选重写:
//   ShowPrompt(显示条件) / WantsInteract(F 生效条件) / PromptPoint(锚点) / ResolvePromptSprite(锚精灵) /
//   OnPlayerEnter / OnPlayerExit。
[RequireComponent(typeof(Collider2D))]
public abstract class InteractTrigger : MonoBehaviour
{
    [Tooltip("动作词(自动加 F 键帽):捡起 / 恢复 / 离开 / 交易…")]
    public string prompt = "交互";
    [Tooltip("提示框相对锚点再往上的世界高度")]
    public float promptHeightOffset = 2f;

    protected bool playerInRange;

    SpriteRenderer cachedSprite;
    bool spriteResolved;

    // —— "最近优先"仲裁 ——
    // 多个交互区重叠时,每帧在"在范围内的交互物"里按到玩家的距离挑最近者:
    // 只有最近者显示提示(promptWinner)/ 响应 F(actionWinner),其余闭嘴 → 避免一次按 F 触发一堆。
    static readonly List<InteractTrigger> s_inRange = new List<InteractTrigger>();
    static InteractTrigger s_promptWinner;
    static InteractTrigger s_actionWinner;
    static int s_resolvedFrame = -1;

    static void ResolveWinners()
    {
        if (s_resolvedFrame == Time.frameCount) return;   // 每帧只算一次,所有实例共享结果
        s_resolvedFrame = Time.frameCount;
        s_promptWinner = null;
        s_actionWinner = null;

        var player = PlayerController.Instance;
        if (player == null) return;
        Vector2 pp = player.transform.position;

        float bestShow = float.MaxValue, bestAct = float.MaxValue;
        for (int i = s_inRange.Count - 1; i >= 0; i--)
        {
            var c = s_inRange[i];
            if (c == null) { s_inRange.RemoveAt(i); continue; }
            if (!c.playerInRange) continue;
            float d = ((Vector2)c.transform.position - pp).sqrMagnitude;
            if (c.ShowPrompt    && d < bestShow) { bestShow = d; s_promptWinner = c; }   // 最近的、想显示提示的
            if (c.WantsInteract && d < bestAct)  { bestAct  = d; s_actionWinner = c; }   // 最近的、F 能生效的
        }
    }

    protected virtual void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    protected virtual void Update()
    {
        ResolveWinners();

        if (s_promptWinner == this) InteractPromptUI.Show(PromptPoint(), prompt, this);
        else                        InteractPromptUI.Hide(this);

        if (s_actionWinner == this && Pressed())   // 仲裁已含 playerInRange + WantsInteract
            Interact();
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(Tags.Player)) return;
        playerInRange = true;
        if (!s_inRange.Contains(this)) s_inRange.Add(this);
        OnPlayerEnter();
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(Tags.Player)) return;
        playerInRange = false;
        s_inRange.Remove(this);
        InteractPromptUI.Hide(this);
        OnPlayerExit();
    }

    protected virtual void OnDisable()
    {
        playerInRange = false;
        s_inRange.Remove(this);
        if (s_promptWinner == this) s_promptWinner = null;
        if (s_actionWinner == this) s_actionWinner = null;
        InteractPromptUI.Hide(this);
    }

    // —— 子类按需重写 ——

    protected abstract void Interact();   // F 按下做什么(唯一必须实现)

    // 显示条件:默认 靠近 + 没暂停 + 商店没开
    protected virtual bool ShowPrompt => playerInRange && Time.timeScale > 0f && !ShopUI.IsOpen;

    // F 生效条件:默认等于显示条件。"提示隐藏时也要能按"(商人关店)重写它
    protected virtual bool WantsInteract => ShowPrompt;

    // 提示锚点:默认 锚精灵顶 + offset。无精灵的(传送门)重写
    protected virtual Vector3 PromptPoint()
    {
        var sr = PromptSprite;
        float wx = sr != null ? sr.bounds.center.x : transform.position.x;
        float wy = (sr != null ? sr.bounds.max.y : transform.position.y) + promptHeightOffset;
        return new Vector3(wx, wy, 0f);
    }

    // 锚精灵:默认自身/子物体第一个 SpriteRenderer;运行时缓存,编辑器现取(Gizmo 用)
    protected SpriteRenderer PromptSprite
    {
        get
        {
            if (!Application.isPlaying) return ResolvePromptSprite();
            if (!spriteResolved) { cachedSprite = ResolvePromptSprite(); spriteResolved = true; }
            return cachedSprite;
        }
    }
    protected virtual SpriteRenderer ResolvePromptSprite() => GetComponentInChildren<SpriteRenderer>();

    protected virtual void OnPlayerEnter() {}
    protected virtual void OnPlayerExit()  {}

    protected static bool Pressed()
    {
        var kb = Keyboard.current;
        return kb != null && kb.fKey.wasPressedThisFrame;
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected() =>
        InteractPromptUI.DrawAnchorGizmo(PromptPoint(), transform.position);
#endif
}
