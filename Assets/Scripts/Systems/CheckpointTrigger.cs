using UnityEngine;
using UnityEngine.InputSystem;

public class CheckpointTrigger : MonoBehaviour
{
    public string checkpointID;
    public string displayName;
    public string prompt = "F 存档并恢复";

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
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

        CheckpointManager.Instance?.ActivateCheckpoint(checkpointID, transform.position);
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
