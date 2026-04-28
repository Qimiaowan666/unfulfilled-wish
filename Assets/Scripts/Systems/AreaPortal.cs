using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class AreaPortal : MonoBehaviour
{
    public Transform destination;
    public string prompt = "F 进入";

    bool playerInRange;
    Transform player;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Update()
    {
        if (!playerInRange || player == null || destination == null) return;
        if (Time.timeScale <= 0f) return;

        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

        Teleport();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        player = other.transform;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        player = null;
    }

    void Teleport()
    {
        PlayerInputBuffer.ClearAll();
        player.position = destination.position;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    void OnGUI()
    {
        if (!playerInRange || Time.timeScale <= 0f) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(Screen.width * 0.5f - 140f, Screen.height - 156f, 280f, 36f), prompt, style);
    }
}
