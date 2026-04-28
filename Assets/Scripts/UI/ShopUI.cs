using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    public GameObject panel;
    public Transform itemListParent;
    public GameObject shopSlotPrefab;
    public Text goldText;

    ShopSystem shop;
    PlayerStats stats;

    public void Open(ShopSystem shopSystem)
    {
        shop = shopSystem;
        stats = FindAnyObjectByType<PlayerStats>();
        panel.SetActive(true);
        Refresh();
    }

    public void Close() => panel.SetActive(false);

    void Refresh()
    {
        foreach (Transform child in itemListParent) Destroy(child.gameObject);
        if (goldText && stats != null) goldText.text = $"Gold: {stats.gold}";

        foreach (var item in shop.itemStock)
            AddSlot(item.itemName, item.price, () => shop.BuyItem(item, stats));
        foreach (var eq in shop.equipmentStock)
            AddSlot(eq.equipmentName, eq.price, () => shop.BuyEquipment(eq, stats));
        foreach (var sk in shop.skillStock)
            AddSlot(sk.skillName, sk.price, () => shop.BuySkill(sk, stats));
    }

    void AddSlot(string label, int price, System.Func<bool> buyAction)
    {
        var slot = Instantiate(shopSlotPrefab, itemListParent);
        var texts = slot.GetComponentsInChildren<Text>();
        if (texts.Length >= 2) { texts[0].text = label; texts[1].text = $"{price}G"; }
        var btn = slot.GetComponent<Button>();
        btn?.onClick.AddListener(() => { buyAction(); Refresh(); });
    }
}
