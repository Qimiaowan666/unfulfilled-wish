using UnityEngine;
using System;

// 地上的装备拾取物：玩家进范围按 F 捡起 → 加进背包(不自动装上,留给玩家手动装)→ 触发 PickedUp。
public class EquipmentPickup : InteractTrigger
{
    [Tooltip("捡到的装备(如 IronSword)")]
    public EquipmentData equipment;

    public event Action PickedUp;

    bool taken;

    protected override void Reset() { base.Reset(); prompt = "捡起"; }

    // 额外前提:还没捡 + 角色面板没开(基类已含 靠近/没暂停/商店没开)
    protected override bool ShowPrompt => base.ShowPrompt && !taken && !CharacterPanelUI.IsOpen;

    protected override void Interact()
    {
        taken = true;
        if (equipment != null) EquipmentSystem.Instance?.AddEquipment(equipment, false);   // 只进背包,玩家手动去面板装
        AudioManager.Instance?.PlayUIEquip();
        PickedUp?.Invoke();
        gameObject.SetActive(false);
    }

    // 读档后由 SaveSystem 调(含 inactive):已拥有这件装备=已捡(隐藏),没有=未捡(重新显示在地上)。
    // 双向 → 读"捡之前的档"地上装备会重新出现,不会凭空消失(对齐 ItemPickup.SyncToInventory)。
    public void SyncToEquipment()
    {
        bool owned = equipment != null && EquipmentSystem.Instance != null && EquipmentSystem.Instance.HasEquipment(equipment);
        taken = owned;
        gameObject.SetActive(!owned);
    }
}
