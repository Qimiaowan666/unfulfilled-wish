using UnityEngine;

// 相机纵向"死区带"目标：X 永远跟玩家；Y 只和【进场景时的初始高度 baseY】比 ——
//   · 玩家 Y 在 [baseY - downRange, baseY + upRange] 内 → 镜头 Y 不动；
//   · 超出该带 → 镜头按"超出量"上/下移。
// baseY 进场景捕获一次、**永不跟随平台**：跳上高台镜头也不上去，人只是在画面里靠上；
// 只有冲出死区带（跳很高 / 掉很深）镜头才动，回到带内自动复原。
// 用法：场景里空物体挂本脚本，把 CinemachineCamera 的 Tracking Target 指到它。
public class CameraFollowTarget : MonoBehaviour
{
    [Header("纵向死区带（世界单位，相对初始高度）")]
    [Tooltip("玩家高过初始高度超过这个值，镜头才开始上移")]
    public float upRange = 5f;
    [Tooltip("玩家低过初始高度超过这个值，镜头才开始下移")]
    public float downRange = 3f;

    Transform player;
    Entity    entity;
    float     baseY;
    bool      captured;

    void LateUpdate()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(Tags.Player);
            if (p == null) return;
            player   = p.transform;
            entity   = p.GetComponent<Entity>();
            captured = false;   // 新找到玩家（含重生重建）→ 重新捕获初始高度
        }
        if (!captured)
        {
            // 传送/出生可能先在半空（落点功能随后把玩家放到地面）→ 等真正落地再锚，避免锚在空中
            if (entity != null && !entity.IsGrounded)
            {
                transform.position = new Vector3(player.position.x, player.position.y, transform.position.z);   // 落地前先跟着玩家
                return;
            }
            baseY    = player.position.y;   // 落地这一刻锚定初始高度，之后固定不变
            captured = true;
        }

        float dy   = player.position.y - baseY;
        float over = 0f;
        if (dy >  upRange)        over = dy - upRange;     // 高过带 → 上移超出量
        else if (dy < -downRange) over = dy + downRange;   // 低过带 → 下移超出量

        transform.position = new Vector3(player.position.x, baseY + over, transform.position.z);
    }

#if UNITY_EDITOR
    // Scene 里画出死区带（上/下边线），方便调 upRange/downRange
    void OnDrawGizmosSelected()
    {
        float b = Application.isPlaying && captured ? baseY : transform.position.y;
        float x = player != null ? player.position.x : transform.position.x;
        const float w = 7f;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(new Vector3(x - w, b + upRange, 0f),   new Vector3(x + w, b + upRange, 0f));
        Gizmos.DrawLine(new Vector3(x - w, b - downRange, 0f), new Vector3(x + w, b - downRange, 0f));
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.3f);
        Gizmos.DrawLine(new Vector3(x, b + upRange, 0f), new Vector3(x, b - downRange, 0f));
    }
#endif
}
