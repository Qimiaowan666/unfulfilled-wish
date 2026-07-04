using UnityEngine;
using UnityEngine.SceneManagement;

// 区域传送点（门户配对）：玩家进范围按 F 切场景，并落到目标场景里 targetPortalID 对应的传送点(贴地)。
// 配对用法：A 场景门 targetPortalID = "B_entrance"；B 场景某个门 portalID = "B_entrance" → 玩家从 A 进来就落在 B 的那个门
public class SceneLoadTrigger : InteractTrigger
{
    public string portalID;          // 本传送点标识（作为落点时被 targetPortalID 匹配）。留空 = 用对象名
    public string sceneName = "ForsakenShrine";   // 目标场景
    public string targetPortalID;    // 落到目标场景里哪个传送点（留空 = 用场景默认出生位置，不移动玩家）

    // 跨场景传递：切场景后要把玩家落到哪个 portalID。LoadScene 后静态变量保留。
    static string pendingTargetPortalID;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(portalID))
            portalID = gameObject.name;
    }

    protected override void Reset() { base.Reset(); prompt = "离开"; }

    void Start()
    {
        // 本传送点是上个场景指定的落点 → 把玩家放到这里(贴地)
        if (!string.IsNullOrEmpty(pendingTargetPortalID) && portalID == pendingTargetPortalID)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = GroundedDropPoint(player);
                if (player.Rb != null) player.Rb.linearVelocity = Vector2.zero;
            }
            pendingTargetPortalID = null;
        }
    }

    // 落点贴地:从传送点向下射线找地面,把玩家脚底放到地上(传送门精灵高,中心在半空,否则会悬空再下落)
    Vector3 GroundedDropPoint(PlayerController player)
    {
        Vector3 dest = transform.position;
        var col  = player.GetComponent<Collider2D>();
        LayerMask mask = player.groundLayer.value != 0 ? player.groundLayer : LayerMask.GetMask("Ground");
        var hit = Physics2D.Raycast(dest, Vector2.down, 60f, mask);
        if (hit.collider != null && col != null)
        {
            float pivotToFoot = player.transform.position.y - col.bounds.min.y;   // 轴心到脚底的距离
            dest.y = hit.point.y + pivotToFoot + 0.02f;                           // 脚底贴地(留一点皮，不嵌进去)
        }
        return dest;
    }

    // 门没有精灵锚点,提示直接挂在 transform 上方
    protected override Vector3 PromptPoint() => transform.position + Vector3.up * promptHeightOffset;

    protected override void Interact()
    {
        // 记下落点，切场景后由目标传送点的 Start 把玩家放过去
        pendingTargetPortalID = string.IsNullOrWhiteSpace(targetPortalID) ? null : targetPortalID;

        if (GameManager.Instance != null)
            GameManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
