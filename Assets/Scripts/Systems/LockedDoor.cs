using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class LockedDoor : MonoBehaviour
{
    [Header("Save")]
    public string saveID;

    public Transform destination;
    public string lockedPrompt = "需要两把钥匙";
    public string openPrompt = "F 开门";
    public SpriteRenderer doorRenderer;
    public Collider2D blockingCollider;

    bool playerInRange;
    Transform player;
    public bool IsOpen { get; private set; }
    public string SaveID => SaveIdUtility.GetSceneObjectID(this, saveID);

    void Reset()
    {
        blockingCollider = GetComponent<Collider2D>();
        doorRenderer = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (blockingCollider == null)
            blockingCollider = GetComponent<Collider2D>();
        if (doorRenderer == null)
            doorRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (!playerInRange || player == null) return;
        if (Time.timeScale <= 0f) return;

        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;
        if (LevelKeyManager.Instance == null || !LevelKeyManager.Instance.HasRequiredKeys()) return;

        Open();
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

    void Open()
    {
        PlayerInputBuffer.ClearAll();
        SaveSystem.Instance?.MarkDoorOpened(SaveID);
        SetOpenState(true);
        AudioManager.Instance?.PlayDoorOpen();

        if (destination != null)
        {
            player.position = destination.position;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }
    }

    public void LoadOpened(bool opened)
    {
        SetOpenState(opened);
    }

    void SetOpenState(bool opened)
    {
        IsOpen = opened;
        if (blockingCollider != null)
            blockingCollider.enabled = !opened;
        if (doorRenderer != null)
            doorRenderer.enabled = !opened;

        enabled = !opened;
    }

    void OnGUI()
    {
        if (!playerInRange || Time.timeScale <= 0f) return;

        bool canOpen = LevelKeyManager.Instance != null && LevelKeyManager.Instance.HasRequiredKeys();
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = canOpen ? Color.green : Color.yellow;

        string message = canOpen ? openPrompt : lockedPrompt;
        GUI.Label(new Rect(Screen.width * 0.5f - 160f, Screen.height - 156f, 320f, 36f), message, style);
    }
}
