using UnityEngine;
using UnityEngine.InputSystem;

public class CheckpointTrigger : MonoBehaviour
{
    public string checkpointID;
    public string displayName;
    public string prompt = "按 F 存档并恢复";

    [Tooltip("提示文字相对火堆精灵顶部再往上的世界高度")]
    public float promptHeightOffset = 0.45f;

    [Tooltip("复活落点（空子物体，摆在地面安全位置）。留空则用玩家站立位置 + 偏移")]
    public Transform respawnAnchor;

    bool playerInRange;
    SpriteRenderer promptSprite;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(checkpointID))
            checkpointID = gameObject.name;
        promptSprite = GetComponentInChildren<SpriteRenderer>();   // 火堆精灵，提示锚在它头顶
    }

    void Update()
    {
        if (!playerInRange) return;
        if (ShopUI.IsOpen || Time.timeScale <= 0f) return;

        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;   // F 存档（处决已改 R，不再冲突）

        Vector3? anchor = respawnAnchor != null ? respawnAnchor.position : (Vector3?)null;
        CheckpointManager.Instance?.ActivateCheckpoint(checkpointID, anchor);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
    }

    void OnGUI()
    {
        if (!playerInRange || ShopUI.IsOpen || Time.timeScale <= 0f) return;

        var cam = Camera.main;
        if (cam == null) return;

        // 锚在火堆精灵顶部上方的世界点 → 转屏幕坐标（OnGUI 的 y 自上而下，需翻转）
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
        style.normal.textColor = new Color(0.45f, 1f, 0.65f, 1f);

        const float w = 280f, h = 36f;
        GUI.Label(new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h), prompt, style);
    }
}
