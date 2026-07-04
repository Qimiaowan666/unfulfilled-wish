using UnityEngine;

// 通关门：boss 击破演出结束后由 LevelManager 调 Appear() 出现。
// 继承 InteractTrigger（和传送门/火堆/拾取一套木框）：玩家进触发区 → 头顶木框 “F 通关”，按 F → 通关画面(VictoryUI)。默认隐藏。
// 读档时按“场上有没有存活 boss”自动收起/出现：
//   · 读到未击破档(boss 复活) → Hide（避免不打 boss 直接通关）
//   · 读到已击破档(boss 已死)   → Appear（避免死 boss 无门卡死）
public class VictoryGate : InteractTrigger
{
    [Tooltip("门的视觉根（默认隐藏，Appear 时显示）")]
    public GameObject visual;

    bool active;

    protected override void Reset() { base.Reset(); prompt = "通关"; }

    void Awake()
    {
        if (visual != null) visual.SetActive(false);
    }

    void OnEnable() { SaveSystem.AfterApply += OnSaveApplied; }
    protected override void OnDisable()
    {
        base.OnDisable();
        SaveSystem.AfterApply -= OnSaveApplied;
    }

    // 读档/重生 apply 后：有存活 boss → 收起门；没有(已击破) → 出现门
    void OnSaveApplied()
    {
        if (BossAlive()) Hide();
        else             Appear();
    }

    bool BossAlive()
    {
        foreach (var e in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.isBoss && e.gameObject.activeInHierarchy && e.CurrentHP > 0f)
                return true;
        return false;
    }

    public void Appear()
    {
        active = true;
        if (visual != null) visual.SetActive(true);
    }

    public void Hide()
    {
        active = false;
        if (visual != null) visual.SetActive(false);
        InteractPromptUI.Hide(this);
    }

    // 门没有精灵锚点，提示直接挂 transform 上方（同传送门）
    protected override Vector3 PromptPoint() => transform.position + Vector3.up * promptHeightOffset;

    // 只有门已出现 + 通关界面没开 才显示提示 / 可交互
    protected override bool ShowPrompt => active && !VictoryUI.IsOpen && base.ShowPrompt;

    protected override void Interact()
    {
        active = false;
        InteractPromptUI.Hide(this);
        VictoryUI.Instance?.Show();
    }
}
