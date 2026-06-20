using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 宝箱：玩家进范围按 F 开启,逐帧播掀盖动画并获得里面的 金币/装备/道具。开过不再开,且持久化(读档不重刷)。
// 继承 InteractTrigger:进出触发 + 木框提示 + F 输入 + Gizmo 全复用,这里只写"开箱"。
public class ChestInteract : InteractTrigger
{
    [Header("宝箱内容")]
    public int gold;                    // 金币
    public EquipmentData[] equipment;   // 装备(进背包,不自动装)
    public ItemData[] items;            // 道具

    [Header("外观")]
    public Sprite   closedSprite;       // 关着(静止)
    public Sprite[] openFrames;         // 开箱动画(依次播,末帧 = 全开)
    public float    openFps = 12f;      // 动画帧率
    public Sprite   emptySprite;        // 开启后的"空箱"图(东西被拿走),动画放完/读档时显示

    [Tooltip("存档唯一 ID(留空 = 用物体名);命名 Chest_<场景>_<编号>")]
    public string saveID;
    public string SaveID => string.IsNullOrWhiteSpace(saveID) ? gameObject.name : saveID;

    bool opened;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (!opened && closedSprite != null && sr != null) sr.sprite = closedSprite;
    }

    protected override void Reset() { base.Reset(); prompt = "开启"; }

    // 开过就不再提示;其余沿用基类(靠近 + 没暂停 + 商店没开)
    protected override bool ShowPrompt => base.ShowPrompt && !opened;

    protected override void Interact()
    {
        if (opened) return;
        opened = true;

        // 内容立即发放(保证拿到,哪怕动画途中离开)
        var stats = PlayerController.Instance != null ? PlayerController.Instance.Stats : FindAnyObjectByType<PlayerStats>();
        if (gold > 0) stats?.AddGold(gold);
        if (equipment != null) foreach (var eq in equipment) if (eq != null) EquipmentSystem.Instance?.AddEquipment(eq, false);
        if (items != null)     foreach (var it in items)     if (it != null) InventorySystem.Instance?.AddItem(it);

        AudioManager.Instance?.PlayChestOpen();        // 吱呀掀盖 + 金币哗啦
        SaveSystem.Instance?.MarkDoorOpened(SaveID);   // 复用"门"的已开持久化 → 读档不重刷
        InteractPromptUI.Hide(this);

        // 拼"获得"文案;等开箱动画放完再弹窗(timeScale=0 会冻住动画,所以不能开箱当下就弹)
        var got = new List<string>();
        if (equipment != null) foreach (var eq in equipment) if (eq != null) got.Add(eq.equipmentName);
        if (items != null)     foreach (var it in items)     if (it != null) got.Add(it.itemName);
        if (gold > 0) got.Add($"{gold} 金币");
        string reward = got.Count > 0 ? string.Join("\n", got) : null;

        if (openFrames != null && openFrames.Length > 0 && isActiveAndEnabled)
            StartCoroutine(PlayOpen(reward));          // 掀盖动画 → 放完切空箱 + 弹窗
        else { SetSprite(RestSprite); if (reward != null) RewardPopupUI.Show(reward); }
    }

    IEnumerator PlayOpen(string reward)
    {
        float dt = 1f / Mathf.Max(1f, openFps);
        foreach (var f in openFrames)
        {
            if (f != null && sr != null) sr.sprite = f;
            yield return new WaitForSeconds(dt);
        }
        SetSprite(RestSprite);                            // 东西被拿走 → 切成空箱
        if (reward != null) RewardPopupUI.Show(reward);   // 居中弹窗 + 暂停
    }

    Sprite OpenSprite => (openFrames != null && openFrames.Length > 0) ? openFrames[openFrames.Length - 1] : null;
    Sprite RestSprite => emptySprite != null ? emptySprite : OpenSprite;   // 开过后的静止图:优先空箱
    void SetSprite(Sprite s) { if (sr != null && s != null) sr.sprite = s; }

    // 读档恢复:已开 → 显示空箱(没空箱图则用全开末帧);未开 → 关图。由 SaveSystem.ApplyDoorStates 调
    public void LoadOpened(bool isOpened)
    {
        opened = isOpened;
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        SetSprite(isOpened ? RestSprite : closedSprite);
    }
}
