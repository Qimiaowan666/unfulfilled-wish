using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ShopSystem))]
public class ShopNPCTrigger : MonoBehaviour
{
    public string prompt = "F 交易";

    [Tooltip("提示文字相对商人精灵顶部再往上的世界高度")]
    public float promptHeightOffset = 0.4f;

    ShopSystem shop;
    ShopUI shopUI;
    bool playerInRange;
    SpriteRenderer promptSprite;

    void Awake()
    {
        shop = GetComponent<ShopSystem>();
        promptSprite = GetComponentInChildren<SpriteRenderer>();   // 商人精灵，提示锚在它头顶
    }

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

        var cam = Camera.main;
        if (cam == null) return;

        // 锚在商人精灵顶部上方的世界点 → 转屏幕坐标（OnGUI 的 y 自上而下，需翻转）
        float wx = promptSprite != null ? promptSprite.bounds.center.x : transform.position.x;
        float wy = (promptSprite != null ? promptSprite.bounds.max.y : transform.position.y) + promptHeightOffset;
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(wx, wy, 0f));
        if (sp.z < 0f) return;   // 在相机背后

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.yellow;

        const float w = 240f, h = 36f;
        GUI.Label(new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h), prompt, style);
    }
}
