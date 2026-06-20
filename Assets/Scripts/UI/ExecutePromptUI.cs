using UnityEngine;
using TMPro;

// 处决提示(木框 + "R 处决"):浮在最近的"可处决"(韧性破)敌人头顶。常驻单例,实例摆 Bootstrap。
// 木框/字体复用 InteractPromptUI 那套(由 Bootstrap 里搭好的 box/text 引用驱动,脱离代码)。
public class ExecutePromptUI : MonoBehaviour
{
    public static ExecutePromptUI Instance { get; private set; }

    [Tooltip("木框+文字根(显隐用)")]
    public GameObject box;
    public RectTransform boxRect;
    public TMP_Text text;
    public string label = "<b>R</b>";
    public Vector2 screenOffset = Vector2.zero;

    PlayerController player;
    EnemyBase target;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        if (text != null) text.text = label;
        if (box != null) box.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void LateUpdate()
    {
        if (player == null)
            player = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();

        target = (player != null && Time.timeScale > 0f) ? FindExecutableTarget() : null;
        if (target == null)
        {
            if (box != null && box.activeSelf) box.SetActive(false);
            return;
        }
        if (text != null) text.text = label;
        if (box != null && !box.activeSelf) box.SetActive(true);
        Reposition();
    }

    EnemyBase FindExecutableTarget()
    {
        LayerMask mask = player.enemyLayer;
        float range = player.executeRange;
        var nearby = mask.value == 0
            ? Physics2D.OverlapCircleAll(player.transform.position, range)
            : Physics2D.OverlapCircleAll(player.transform.position, range, mask);

        EnemyBase closest = null;
        float best = float.MaxValue;
        foreach (var col in nearby)
        {
            var e = col.GetComponent<EnemyBase>();
            if (e == null || !e.IsExecutable) continue;
            float d = (e.transform.position - player.transform.position).sqrMagnitude;
            if (d < best) { best = d; closest = e; }
        }
        return closest;
    }

    void Reposition()
    {
        var cam = Camera.main;
        if (cam == null || boxRect == null || target == null) return;

        // 逐怪高度:从敌人原点往上 executePromptHeight(每个怪 prefab 上单独调)
        Vector3 head = target.transform.position + Vector3.up * target.executePromptHeight;

        Vector3 sp = cam.WorldToScreenPoint(head);
        if (sp.z < 0f) { if (box != null) box.SetActive(false); return; }
        boxRect.position = (Vector2)sp + screenOffset;
    }
}
