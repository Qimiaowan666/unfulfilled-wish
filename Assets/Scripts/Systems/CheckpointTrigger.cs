using UnityEngine;
using UnityEngine.InputSystem;

public class CheckpointTrigger : MonoBehaviour
{
    public string checkpointID;
    public string displayName;
    public string prompt = "按 F 存档并恢复";

    [Tooltip("复活落点（空子物体，摆在地面安全位置）。留空则用玩家站立位置 + 偏移")]
    public Transform respawnAnchor;

    bool playerInRange;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(checkpointID))
            checkpointID = gameObject.name;
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

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(0.45f, 1f, 0.65f, 1f);

        GUI.Label(new Rect(Screen.width * 0.5f - 140f, Screen.height - 120f, 280f, 36f), prompt, style);
    }
}
