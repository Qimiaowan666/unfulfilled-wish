using UnityEngine;

// 商人 NPC：玩家进范围按 F 开店;开着时(timeScale=0)再按 F 关店。
[RequireComponent(typeof(ShopSystem))]
public class ShopNPCTrigger : InteractTrigger
{
    ShopSystem shop;
    ShopUI shopUI;

    void Awake() { shop = GetComponent<ShopSystem>(); }

    protected override void Reset() { base.Reset(); prompt = "交易"; promptHeightOffset = 0.6f; }

    // 商店开着(timeScale=0)F 仍要能按来关店 → 放宽输入条件;
    // 显示条件 ShowPrompt 仍用基类默认(店一开就隐藏提示,让位给商店 UI)
    protected override bool WantsInteract => ShopUI.IsOpen || Time.timeScale > 0f;

    protected override void OnPlayerEnter() { shopUI = FindAnyObjectByType<ShopUI>(); }
    protected override void OnPlayerExit()  { FindAnyObjectByType<ShopUI>()?.Close(); }

    protected override void Interact()
    {
        shopUI = shopUI != null ? shopUI : FindAnyObjectByType<ShopUI>();
        if (shopUI == null) return;

        if (ShopUI.IsOpen) shopUI.Close();
        else               shopUI.Open(shop);
    }
}
