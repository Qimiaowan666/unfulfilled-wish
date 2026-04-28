using UnityEngine;

[RequireComponent(typeof(ShopSystem))]
public class ShopNPCTrigger : MonoBehaviour
{
    ShopSystem shop;

    void Awake() => shop = GetComponent<ShopSystem>();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var shopUI = FindAnyObjectByType<ShopUI>();
        shopUI?.Open(shop);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        FindAnyObjectByType<ShopUI>()?.Close();
    }
}
