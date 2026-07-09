using UnityEngine;
using TMPro;

// 交互提示(木框 + 文字)。常驻单例,跟随世界点显示在屏幕上(拾取/火堆/传送门共用一个)。
// 木框是 Box 上 Image 的 sprite —— 在预制/场景里随时换,脱离代码。
public class InteractPromptUI : MonoBehaviour
{
    public static InteractPromptUI Instance { get; private set; }

    [Tooltip("木框+文字的根(显隐用)")]
    public GameObject box;
    public RectTransform boxRect;
    public TMP_Text text;
    [Tooltip("相对世界点的屏幕像素偏移")]
    public Vector2 screenOffset = Vector2.zero;

    Camera cam;
    static Vector3 worldPos;
    static object  owner;

    // 常驻单例:实例摆在 Bootstrap.unity(随其它管理器),Awake 里 DontDestroyOnLoad 跟随到各场景。
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        if (box != null) box.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // who = 调用方(拾取/火堆/门),用于"只有当前显示者能关",避免互相误关
    public static void Show(Vector3 wp, string label, object who)
    {
        if (Instance == null) return;
        worldPos = wp; owner = who;
        if (Instance.text != null) Instance.text.text = "<b>F</b> " + label;
        if (Instance.box != null) Instance.box.SetActive(true);
        Instance.Reposition();
    }

    public static void Hide(object who)
    {
        if (Instance == null || (owner != null && owner != who)) return;
        owner = null;
        if (Instance.box != null) Instance.box.SetActive(false);
    }

    void LateUpdate() { if (box != null && box.activeSelf) Reposition(); }

    void Reposition()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || boxRect == null) return;
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z < 0f) { if (box != null) box.SetActive(false); return; }
        boxRect.position = (Vector2)sp + screenOffset;
    }

#if UNITY_EDITOR
    // 各交互物在 Scene 里画出"提示框会浮在哪"(锚点+大致框+高度连线),不进 Play 也能调 Prompt Height Offset
    public static void DrawAnchorGizmo(Vector3 anchor, Vector3 from)
    {
        var c = new Color(1f, 0.82f, 0.3f, 1f);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.45f);
        Gizmos.DrawLine(from, anchor);                              // 物体 → 锚点的高度线
        Gizmos.color = c;
        Gizmos.DrawWireCube(anchor, new Vector3(1.3f, 0.5f, 0f));   // 大致框(实际宽随文字变)
        Gizmos.DrawWireSphere(anchor, 0.06f);                       // 锚点
        UnityEditor.Handles.color = c;
        UnityEditor.Handles.Label(anchor + Vector3.up * 0.33f, "提示框");
    }
#endif
}
