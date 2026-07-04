using UnityEngine;

// 地上的道具拾取物：靠近显示"捡起"提示,按 F 捡 → 进背包(InventorySystem)。
// 读档由 SaveSystem 读完背包后统一调 SyncToInventory(含 inactive)做【双向】同步:
//   背包有这件 = 已捡 → 隐藏;背包没有 = 未捡 → 显示在地上。
// 这样读"捡之前的档"拾取物会重新出现(不会凭空消失),读"捡之后的档"保持隐藏。
// 不能挂自己的 AfterApply 订阅:物体一旦 SetActive(false) 就收不到事件,得由外部(SaveSystem)驱动。
public class ItemPickup : InteractTrigger
{
    [Tooltip("捡到的道具(进背包)")]
    public ItemData item;

    bool taken;

    protected override void Reset() { base.Reset(); prompt = "捡起"; }

    // 额外前提:还没捡 + 角色面板没开(基类已含 靠近/没暂停/商店没开)
    protected override bool ShowPrompt => base.ShowPrompt && !taken && !CharacterPanelUI.IsOpen;

    protected override void Interact()
    {
        taken = true;
        if (item != null) InventorySystem.Instance?.AddItem(item);   // 进背包,玩家可在角色面板看到
        AudioManager.Instance?.PlayKeyPickup();
        gameObject.SetActive(false);
    }

    // 由 SaveSystem 读档后调(含 inactive):背包有=已捡(隐藏),没有=未捡(重新显示在地上)
    public void SyncToInventory()
    {
        bool owned = item != null && InventorySystem.Instance != null && InventorySystem.Instance.items.Contains(item);
        taken = owned;
        gameObject.SetActive(!owned);
    }
}
