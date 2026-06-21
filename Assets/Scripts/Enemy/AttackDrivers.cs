using UnityEngine;

// ── 攻击驱动:编排招(跳劈/挑飞)的执行逻辑。挂在 EnemyAttack.driver([SerializeReference] 多态)。
//    留空 = 普通挥砍(走 Fire(i) 命中)。驱动自带运动 + 命中,统一攻击运行器把动画事件转发给它。
//    命中盒/伤害仍读这招的 hits[0](HitProfile),所以"打什么"还是数据。
[System.Serializable]
public abstract class AttackDriverBase
{
    public abstract void Begin(EnemyBase e, EnemyBase.EnemyAttack atk);   // 进招:记录起点 / 设无敌·kinematic
    public virtual void Tick(EnemyBase e, EnemyBase.EnemyAttack atk) {}   // 每帧:驱动位移
    public virtual void OnFire(EnemyBase e, EnemyBase.EnemyAttack atk, int i) {}   // 动画事件 Fire(i)
    public virtual void End(EnemyBase e, EnemyBase.EnemyAttack atk) {}    // 收尾:恢复无敌 / 物理
}

// ── 横劈挑飞 + 跳劈下砸(由 Boss_Atk3State 移植)。不锁定:空中可格挡/识破/翻滚。
//    窗口1(横劈)命中 → 挑起玩家,boss 延迟后同步起飞;窗口2(下劈)→ 一起砸回地面(伤害×slamMult)。
[System.Serializable]
public class LaunchDriver : AttackDriverBase
{
    public float riseHeight    = 4f;
    public float launchTime    = 0.4f;    // 升空用时
    public float bossJumpDelay = 0.25f;   // boss 比玩家晚多久起跳
    public float slamTime      = 0.25f;   // 砸回地面用时
    public float slamMult      = 2f;      // 落地伤害倍率
    public float holdDistance  = 2f;      // 挑飞时把玩家固定在 boss 正前方多远

    Vector2 bossStartPos, playerHold;
    float   elapsed, hitElapsed, slamElapsed;
    bool    launched, launchedOnce;
    PlayerController target;
    RigidbodyType2D originalBody;

    public override void Begin(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        bossStartPos = e.transform.position;
        elapsed = 0f; hitElapsed = -1f; slamElapsed = -1f;
        launched = false; launchedOnce = false;
        originalBody = e.Rb.bodyType;
        e.Rb.bodyType = RigidbodyType2D.Kinematic;
        e.Rb.linearVelocity = Vector2.zero;
    }

    // 动画事件 Fire(0)=横劈挑飞,Fire(1)=下劈砸
    public override void OnFire(EnemyBase e, EnemyBase.EnemyAttack atk, int i)
    {
        if (i == 0)
        {
            if (hitElapsed < 0f) hitElapsed = elapsed;   // 横劈连接 → boss/玩家起飞计时
            if (DoHit(e, atk, 1f)) TryLaunch(e);         // 命中(格挡也算)即挑起
        }
        else
        {
            if (slamElapsed < 0f) { slamElapsed = elapsed; AudioManager.Instance?.PlayBossSlam(); }   // 下劈砸地
            DoHit(e, atk, slamMult);
        }
    }

    bool DoHit(EnemyBase e, EnemyBase.EnemyAttack atk, float mult)
    {
        if (e.player == null || atk.hits == null || atk.hits.Length == 0 || atk.hits[0] == null) return false;
        var h = atk.hits[0];
        float dmg = e.attack * e.DamageMultiplier * h.damageMultiplier * mult;
        return e.PerformAttack(dmg, h.hitboxOffset, h.hitboxSize, h.red, h.knockback, h.hitStun);
    }

    void TryLaunch(EnemyBase e)
    {
        if (launchedOnce) return;
        var stats = e.player.GetComponent<PlayerStats>();
        if (stats != null && stats.IsInvulnerable) return;   // 翻滚 i-frame 躲过
        var pc = e.player.GetComponent<PlayerController>();
        if (pc == null) return;
        launchedOnce = true; launched = true; target = pc;
        float holdX = e.transform.position.x + e.FacingDir * holdDistance;
        playerHold = new Vector2(holdX, pc.transform.position.y);
        pc.BeginLift();
    }

    public override void Tick(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        elapsed += Time.deltaTime;

        float playerH = HeightFrom(hitElapsed);
        float bossH   = HeightFrom(hitElapsed >= 0f ? hitElapsed + bossJumpDelay : -1f);
        e.transform.position = new Vector2(bossStartPos.x, bossStartPos.y + bossH);

        if (launched && target != null)
        {
            var ts = target.GetComponent<PlayerStats>();
            if (ts != null && ts.IsInvulnerable) { target.EndLift(); target = null; launched = false; }   // 空中翻滚挣脱
            else
            {
                target.SetLiftPosition(new Vector2(playerHold.x, playerHold.y + playerH));
                if (slamElapsed >= 0f && playerH <= 0f) { target.EndLift(); target = null; launched = false; }   // 砸落到地释放
            }
        }
    }

    // 从 startElapsed 起算:升空 → 顶点悬停(等下劈)→ 下落
    float HeightFrom(float startElapsed)
    {
        if (startElapsed < 0f || elapsed < startElapsed) return 0f;
        float peak = riseHeight;
        if (slamElapsed < 0f)
        {
            float tRise = elapsed - startElapsed;
            if (tRise >= launchTime) return peak;
            float a = tRise / launchTime;
            a = 1f - (1f - a) * (1f - a);
            return peak * a;
        }
        float tFall = elapsed - slamElapsed;
        if (tFall >= slamTime) return 0f;
        float d = tFall / slamTime;
        d *= d;
        return peak * (1f - d);
    }

    public override void End(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        e.transform.position = new Vector2(bossStartPos.x, bossStartPos.y);   // 回地面
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = originalBody;
        e.Rb.linearVelocity = Vector2.zero;
        if (launched && target != null) { target.EndLift(); target = null; launched = false; }
    }
}

// ── 前冲 / 后撤:挥砍【中】朝玩家前冲(LungeForward)或背身后撤(LungeBackward)。命中走普通 Fire(i)。
//    取代旧 ComboStep.lungeDir/lungeSpeed —— 拆成两个直接选项,下拉里一步选定,不再嵌套 dir。
[System.Serializable]
public abstract class LungeDriver : AttackDriverBase
{
    public float speed = 4f;
    protected abstract bool Forward { get; }

    public override void Begin(EnemyBase e, EnemyBase.EnemyAttack atk) {}
    public override void Tick(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        if (speed <= 0f) return;
        float vx;
        if (Forward) vx = e.GetHorizontalDistToPlayer() > 1.5f ? e.FacingDir * speed : 0f;
        else { float d = -e.FacingDir; vx = e.LedgeAhead(d) ? 0f : d * speed; }
        e.Rb.linearVelocity = new Vector2(vx, e.Rb.linearVelocity.y);
    }
    public override void OnFire(EnemyBase e, EnemyBase.EnemyAttack atk, int i) => e.DoFireHit(i);   // 普通命中
}

[System.Serializable] public class LungeForward  : LungeDriver { protected override bool Forward => true;  }   // 前冲
[System.Serializable] public class LungeBackward : LungeDriver { protected override bool Forward => false; }   // 后撤

// ── 跳劈:出招【中】抛物线跳向玩家,边跳边劈(攻击 clip 与跳同时进行)。命中走 Fire(i)。腾空时无敌,落地解。
//    和 JumpMover(出招前=先跳完再砍)区别:这个是"跳和砍同时",符合跳劈的最初设计。
[System.Serializable]
public class JumpDriver : AttackDriverBase
{
    public float height   = 3f;     // 跳跃顶点高度
    public float time     = 0.75f;  // 起跳→落地用时(应 <= 攻击 clip 时长,落地时正好劈中)
    public float standoff = 2f;     // 落点比玩家靠后多少

    float elapsed; Vector2 start, apex, ground; RigidbodyType2D body; bool landed;

    public override void Begin(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        start = e.transform.position;
        float groundY = start.y;
        float pX  = e.player != null ? e.player.position.x : start.x;
        float dir = Mathf.Sign(pX - start.x); if (dir == 0f) dir = e.FacingDir;
        float targetX = pX - dir * standoff;
        ground = new Vector2(targetX, groundY);
        apex   = new Vector2(targetX, groundY + height);
        elapsed = 0f; landed = false;
        e.Invincible = true; body = e.Rb.bodyType; e.Rb.bodyType = RigidbodyType2D.Kinematic; e.Rb.linearVelocity = Vector2.zero;
    }
    public override void Tick(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        elapsed += Time.deltaTime;
        float air = time, rise = air * 0.55f;
        if (elapsed < air)
        {
            if (elapsed <= rise) { float a = rise > 0f ? elapsed / rise : 1f; a = 1f - (1f - a) * (1f - a); e.transform.position = Vector2.Lerp(start, apex, a); }
            else { float d = (elapsed - rise) / (air - rise); d *= d; e.transform.position = Vector2.Lerp(apex, ground, d); }
            return;
        }
        if (!landed) { landed = true; e.transform.position = ground; e.Invincible = false; }   // 落地:解无敌,接下来在地面把劈砍完
    }
    public override void OnFire(EnemyBase e, EnemyBase.EnemyAttack atk, int i) => e.DoFireHit(i);   // 劈中走普通命中
    public override void End(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        e.Invincible = false;
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = body;
    }
}
