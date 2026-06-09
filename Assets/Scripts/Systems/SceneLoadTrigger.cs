using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 区域传送点（门户配对）：玩家进范围按 F 切场景，并落到目标场景里 targetPortalID 对应的传送点位置
// 配对用法：A 场景门 targetPortalID = "B_entrance"；B 场景某个门 portalID = "B_entrance" → 玩家从 A 进来就落在 B 的那个门
public class SceneLoadTrigger : MonoBehaviour
{
    public string portalID;          // 本传送点标识（作为落点时被 targetPortalID 匹配）。留空 = 用对象名
    public string sceneName = "ForsakenShrine";   // 目标场景
    public string targetPortalID;    // 落到目标场景里哪个传送点（留空 = 用场景默认出生位置，不移动玩家）
    public string prompt = "按 F 进入";

    // 跨场景传递：切场景后要把玩家落到哪个 portalID。LoadScene 后静态变量保留。
    static string pendingTargetPortalID;

    bool playerInRange;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(portalID))
            portalID = gameObject.name;
    }

    void Start()
    {
        // 本传送点是上个场景指定的落点 → 把玩家放到这里
        if (!string.IsNullOrEmpty(pendingTargetPortalID) && portalID == pendingTargetPortalID)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = transform.position;
                if (player.Rb != null) player.Rb.linearVelocity = Vector2.zero;
            }
            pendingTargetPortalID = null;
        }
    }

    void Update()
    {
        if (!playerInRange) return;
        if (ShopUI.IsOpen || Time.timeScale <= 0f) return;

        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

        // 记下落点，切场景后由目标传送点的 Start 把玩家放过去
        pendingTargetPortalID = string.IsNullOrWhiteSpace(targetPortalID) ? null : targetPortalID;

        if (GameManager.Instance != null)
            GameManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
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

    void OnDisable()
    {
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
        style.normal.textColor = new Color(0.62f, 0.82f, 1f, 1f);   // 淡蓝，区别于存档点绿字

        GUI.Label(new Rect(Screen.width * 0.5f - 140f, Screen.height - 120f, 280f, 36f), prompt, style);
    }
}
