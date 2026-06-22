using UnityEngine;

// 段前位移驱动(出招【前】把敌人挪到位):可插拔,挂在 ComboStep.preMove([SerializeReference] 多态)。
// 任何敌人都能用,参数随实例走。和 AttackDriverBase(出招【中】编排)并列,各管一个相位。
[System.Serializable]
public abstract class StepMover
{
    public abstract void Begin(EnemyBase e);   // 进入段前位移:setup
    public abstract bool Tick(EnemyBase e);    // 每帧;return true = 到位,可挥砍
    public virtual void Cancel(EnemyBase e) {} // 被打断:恢复物理/无敌/可见
}

// 逼近:朝玩家走到 reach(命中距离)内就停(或遇崖/超时停)
[System.Serializable]
public class ApproachMover : StepMover
{
    [Tooltip("走到离玩家这么近就停、开始挥砍(= 命中距离;太大→还没贴近就挥、打空,太小→贴脸才挥。配套那招的 hitboxOffset/Size)")]
    public float reach   = 1.2f;
    [Tooltip("逼近移动速度(米/秒);<=0 = 用这只怪的 battleMoveSpeed(战斗移速)")]
    public float speed   = 0f;
    [Tooltip("最长逼近时间(秒),走够了还没到也停下挥砍 → 防卡(玩家跑远/隔墙时不至于无限追)")]
    public float maxTime = 0.8f;
    float timer;
    public override void Begin(EnemyBase e)
    {
        timer = maxTime;
        // 已在 reach 内就不播 run(下一帧 Tick 直接转挥砍)→ 贴脸起手不闪走路
        if (e.player == null || e.GetHorizontalDistToPlayer() > reach) { e.SetAnimBool("isMoving"); e.PlayMove(); }
    }
    public override bool Tick(EnemyBase e)
    {
        timer -= Time.deltaTime;
        if (e.player == null) return true;
        float dir  = e.DirToward(e.player.position);
        float dist = e.GetHorizontalDistToPlayer();
        if (timer <= 0f || dist <= reach || e.LedgeAhead(dir)) { e.Rb.linearVelocity = new Vector2(0f, e.Rb.linearVelocity.y); return true; }
        float spd = speed > 0f ? speed : e.AttackMoveSpeed;   // speed 留 0 = 用战斗移速
        e.Rb.linearVelocity = new Vector2(dir * spd, e.Rb.linearVelocity.y);
        return false;
    }
}

// 后撤:背对玩家走,遇崖/超时停
[System.Serializable]
public class RetreatMover : StepMover
{
    public float maxTime = 0.8f;
    float timer;
    public override void Begin(EnemyBase e) { timer = maxTime; e.SetAnimBool("isMoving"); e.PlayMove(); }
    public override bool Tick(EnemyBase e)
    {
        timer -= Time.deltaTime;
        if (e.player == null) return true;
        float back = -e.DirToward(e.player.position);
        if (timer <= 0f || e.LedgeAhead(back)) { e.Rb.linearVelocity = new Vector2(0f, e.Rb.linearVelocity.y); return true; }
        e.Rb.linearVelocity = new Vector2(back * e.AttackMoveSpeed, e.Rb.linearVelocity.y);
        return false;
    }
}

// 瞬移(闪身):淡出 → 现身玩家身后/身前/另一侧(带贴墙修正)→ 淡入 → 硬直。飞行中无敌。
[System.Serializable]
public class TeleportMover : StepMover
{
    public enum Side { Behind, Front, OtherSide }
    public Side      side      = Side.Behind;
    public float     offset    = 4f;     // 现身在玩家身后/前多远
    public float     duration  = 0.35f;  // 淡出+淡入总时长
    public float     clearance = 0.6f;   // 落点距墙安全距离
    public float     landing   = 0.12f;  // 现身后到出招的硬直
    public LayerMask wallLayer;          // 落点墙体检测层(留空按名字找 Ground)

    float elapsed; bool landed; Vector2 target; RigidbodyType2D body;

    public override void Begin(EnemyBase e)
    {
        target = ComputeTarget(e);
        elapsed = 0f; landed = false;
        AudioManager.Instance?.PlayBossTeleport();
        e.Invincible = true; body = e.Rb.bodyType; e.Rb.bodyType = RigidbodyType2D.Kinematic; e.Rb.linearVelocity = Vector2.zero;
    }
    public override bool Tick(EnemyBase e)
    {
        elapsed += Time.deltaTime;
        if (!landed)
        {
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            if (t < 0.5f) e.SetSpriteAlpha(1f - t / 0.5f);
            else { e.transform.position = target; e.SetSpriteAlpha((t - 0.5f) / 0.5f); }
            if (t >= 1f)
            {
                landed = true;
                e.transform.position = target; e.SetSpriteVisible(true); e.SetSpriteAlpha(1f);
                e.Invincible = false;
                if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = body;
                if (e.player != null) e.SetFacing(e.DirToward(e.player.position));
            }
            return false;
        }
        return elapsed >= duration + landing;
    }
    public override void Cancel(EnemyBase e)
    {
        e.Invincible = false;
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = body;
        e.SetSpriteVisible(true); e.SetSpriteAlpha(1f);
    }

    Vector2 ComputeTarget(EnemyBase e)
    {
        if (e.player == null) return e.transform.position;
        float y = e.transform.position.y;
        if (side == Side.Behind || side == Side.Front)
        {
            int pf = 1; var pc = e.player.GetComponent<PlayerController>(); if (pc != null) pf = pc.FacingDir;
            float sign = side == Side.Behind ? -pf : pf;
            return Clamp(e, new Vector2(e.player.position.x + sign * offset, y));
        }
        float s = Mathf.Sign(e.transform.position.x - e.player.position.x); if (s == 0f) s = 1f;
        return Clamp(e, new Vector2(e.player.position.x - s * offset, y));
    }
    Vector2 Clamp(EnemyBase e, Vector2 desired)
    {
        if (e.player == null) return desired;
        LayerMask mask = wallLayer.value != 0 ? wallLayer : LayerMask.GetMask(Layers.Ground);
        if (mask.value == 0) return desired;
        var col = e.GetComponent<Collider2D>();
        float halfW = (col != null ? col.bounds.extents.x : 0.5f) + clearance;
        Vector2 origin = new Vector2(e.player.position.x, desired.y);
        float dx = desired.x - origin.x; float dist = Mathf.Abs(dx);
        if (dist < 0.01f) return desired;
        float sign = Mathf.Sign(dx);
        float probe = dist + halfW;
        if (!Physics2D.Raycast(origin, new Vector2(sign, 0f), probe, mask)) return desired;
        if (!Physics2D.Raycast(origin, new Vector2(-sign, 0f), probe, mask)) return new Vector2(origin.x - sign * offset, desired.y);
        var hit = Physics2D.Raycast(origin, new Vector2(sign, 0f), probe, mask);
        return new Vector2(hit.point.x - sign * halfW, desired.y);
    }
}

// 跳劈接近:抛物线跳到玩家身位(落点靠后一个身位)。飞行中无敌。
[System.Serializable]
public class JumpMover : StepMover
{
    public float height   = 3f;     // 跳跃顶点高度
    public float time     = 0.75f;  // 起跳→落地用时
    public float standoff = 2f;     // 落点比玩家靠后多少

    float elapsed; Vector2 start, apex, ground; RigidbodyType2D body;

    public override void Begin(EnemyBase e)
    {
        start = e.transform.position;
        float groundY = start.y;
        float pX  = e.player != null ? e.player.position.x : start.x;
        float dir = Mathf.Sign(pX - start.x); if (dir == 0f) dir = e.FacingDir;
        float targetX = pX - dir * standoff;
        ground = new Vector2(targetX, groundY);
        apex   = new Vector2(targetX, groundY + height);
        elapsed = 0f;
        e.Invincible = true; body = e.Rb.bodyType; e.Rb.bodyType = RigidbodyType2D.Kinematic; e.Rb.linearVelocity = Vector2.zero;
    }
    public override bool Tick(EnemyBase e)
    {
        elapsed += Time.deltaTime;
        float air = time, rise = air * 0.55f;
        if (elapsed < air)
        {
            if (elapsed <= rise) { float a = rise > 0f ? elapsed / rise : 1f; a = 1f - (1f - a) * (1f - a); e.transform.position = Vector2.Lerp(start, apex, a); }
            else { float d = (elapsed - rise) / (air - rise); d *= d; e.transform.position = Vector2.Lerp(apex, ground, d); }
            return false;
        }
        e.transform.position = ground;
        e.Invincible = false;
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = body;
        return true;
    }
    public override void Cancel(EnemyBase e)
    {
        e.Invincible = false;
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = body;
    }
}
