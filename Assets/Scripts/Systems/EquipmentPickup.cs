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
}
