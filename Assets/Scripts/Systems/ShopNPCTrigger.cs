using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ShopSystem))]
public class ShopNPCTrigger : MonoBehaviour
{
    public string prompt = "F Trade";

    ShopSystem shop;
    ShopUI shopUI;
    bool playerInRange;

    void Awake() => shop = GetComponent<ShopSystem>();

    void Update()
    {
        if (!playerInRange) return;
        if (!ShopUI.IsOpen && Time.timeScale <= 0f) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.fKey.wasPressedThisFrame) return;

        shopUI = shopUI != null ? shopUI : FindAnyObjectByType<ShopUI>();
        if (shopUI == null) return;

        if (ShopUI.IsOpen) shopUI.Close();
        else shopUI.Open(shop);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        shopUI = FindAnyObjectByType<ShopUI>();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        FindAnyObjectByType<ShopUI>()?.Close();
    }

    void OnGUI()
    {
        if (!playerInRange || ShopUI.IsOpen) return;
        if (Time.timeScale <= 0f) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.yellow;

        Rect rect = new Rect(Screen.width * 0.5f - 120f, Screen.height - 120f, 240f, 36f);
        GUI.Label(rect, prompt, style);
    }
}
