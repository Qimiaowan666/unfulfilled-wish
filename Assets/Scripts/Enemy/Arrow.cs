using System.Collections;
using UnityEngine;

// 弓箭手的箭矢投射物：直线飞行 → 命中玩家(走 owner 的格挡/识破/击退逻辑)或墙/地面 → 碎裂消失。
[RequireComponent(typeof(SpriteRenderer))]
public class Arrow : MonoBehaviour
{
    [Header("飞行")]
    public float speed    = 14f;
    public float lifetime = 3f;       // 没命中也会超时自毁
    [Tooltip("可命中的层(玩家 + 地面/墙)")]
    public LayerMask hitMask;

    [Header("命中碎裂 (ARROW HIT 帧)")]
    public Sprite[] hitFrames;
    public float    hitFrameTime = 0.04f;

    EnemyBase owner;
    float     damage;
    bool      unblockable;       // 无法格挡(需识破)
    float     knockback;         // 命中击退力
    float     hitStun;           // 命中玩家硬直时长(0=不硬直)
    Vector2   velocity;
    bool      consumed;
    SpriteRenderer sr;
    Transform followTarget;   // 命中玩家后跟随其移动(玩家被击退也不留在原地)
    Vector3   followOffset;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    // 由 GroundEnemy.SpawnProjectile 在放箭动画事件里调用；dir = 指向目标的方向向量(可带高低差)
    public void Launch(EnemyBase owner, Vector2 dir, float damage, bool unblockable, float knockback, float hitStun)
    {
        this.owner = owner; this.damage = damage;
        this.unblockable = unblockable; this.knockback = knockback; this.hitStun = hitStun;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        velocity = dir.normalized * speed;
        // 箭头转向飞行方向(精灵原本朝右；旋转一并处理左右朝向，无需翻 scale)
        float ang = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
        StartCoroutine(LifeTimeout());
    }

    void Update()
    {
        if (followTarget != null) { transform.position = followTarget.position + followOffset; return; }   // 命中后跟随玩家
        if (consumed) return;
        transform.position += (Vector3)(velocity * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        var stats = other.GetComponent<PlayerStats>();
        if (stats != null && owner != null)
        {
            owner.ApplyHitToCollider(other, damage, unblockable, knockback, hitStun);   // 复用近战那套(格挡/识破/击退)
            // 命中玩家：碎裂吸附到玩家表面(消除箭长造成的空隙)，并跟随被击退的玩家
            var follow = other.attachedRigidbody != null ? other.attachedRigidbody.transform : stats.transform;
            transform.position = other.ClosestPoint(transform.position);
            followTarget = follow;
            followOffset = transform.position - follow.position;
        }

        AudioManager.Instance?.PlayArrowImpact();   // 命中(玩家/墙)→ 命中音；超时不播
        Impact();
    }

    IEnumerator LifeTimeout()
    {
        yield return new WaitForSeconds(lifetime);
        if (!consumed) Impact();
    }

    void Impact()
    {
        consumed = true;
        StopAllCoroutines();
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (hitFrames != null && hitFrames.Length > 0) StartCoroutine(PlayHitThenDestroy());
        else Destroy(gameObject);
    }

    IEnumerator PlayHitThenDestroy()
    {
        for (int i = 0; i < hitFrames.Length; i++)
        {
            if (sr != null) sr.sprite = hitFrames[i];
            yield return new WaitForSeconds(hitFrameTime);
        }
        Destroy(gameObject);
    }
}
