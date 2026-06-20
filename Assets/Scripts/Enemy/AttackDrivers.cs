using UnityEngine;

// 招的预警方式:无 / 身体红闪 / 红闪+指示器(指示器由 EnemyBase.ShowAttackIndicator 实现,boss 覆盖)
public enum AttackTelegraph { None, Flash, Indicator }

// ── 攻击驱动:编排招(跳劈/挑飞)的执行逻辑。挂在 EnemyAttack.driver([SerializeReference] 多态)。
//    留空 = 普通挥砍(走 Fire(i) 命中)。驱动自带运动 + 命中,统一攻击运行器把动画事件转发给它。
//    命中盒/伤害仍读这招的 hits[0](HitProfile),所以"打什么"还是数据。
[System.Serializable]
public abstract class AttackDriverBase
{
    protected bool inWindow;        // 命中窗口开放中(boss 风格:AnimHitOpen 开 / AnimHitClose 关)
    protected bool hitThisWindow;   // 本窗口内是否已命中(防一窗多次)

    public abstract void Begin(EnemyBase e, EnemyBase.EnemyAttack atk);   // 进招:记录起点 / 设无敌·kinematic
    public virtual void Tick(EnemyBase e, EnemyBase.EnemyAttack atk) {}   // 每帧:驱动位移
    public virtual void OnHitOpen(EnemyBase e, EnemyBase.EnemyAttack atk) {}
    public virtual void OnHitClose(EnemyBase e, EnemyBase.EnemyAttack atk) {}
    public virtual void End(EnemyBase e, EnemyBase.EnemyAttack atk) {}    // 收尾:恢复无敌 / 物理

    // 用这招的第 0 下(hits[0])做一次命中判定(可乘额外倍率,如砸地)
    protected void TryHit(EnemyBase e, EnemyBase.EnemyAttack atk, float extraMult)
    {
        if (!inWindow || hitThisWindow) return;
        if (atk.hits == null || atk.hits.Length == 0 || atk.hits[0] == null) return;
        var h = atk.hits[0];
        float dmg = e.attack * e.DamageMultiplier * h.damageMultiplier * extraMult;
        if (e.PerformAttack(dmg, h.hitboxOffset, h.hitboxSize, h.red, h.knockback, h.hitStun))
            hitThisWindow = true;
    }
}

// ── 二段跳劈:起跳弧线砸到玩家头顶(由 Boss_JumpSlashState 移植)。
//    上升 + 砸下期间无敌;命中窗口(AnimHitOpen=砸地帧)做一次砸击。
[System.Serializable]
public class JumpArcDriver : AttackDriverBase
{
    public float jumpHeight   = 3f;
    public float airborneTime = 0.75f;   // 起跳→落地用时(对齐砸地帧)
    public float standoff     = 2f;      // 落点比玩家靠后多少(留身位)

    Vector2 startPos, apex, groundTarget;
    float   elapsed;
    bool    airborneEnded;
    RigidbodyType2D originalBody;

    public override void Begin(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        startPos = e.transform.position;
        float groundY = startPos.y;
        float pX = e.player != null ? e.player.position.x : startPos.x;
        float approachDir = Mathf.Sign(pX - startPos.x);
        if (approachDir == 0f) approachDir = e.FacingDir;
        float targetX = pX - approachDir * standoff;
        groundTarget = new Vector2(targetX, groundY);
        apex         = new Vector2(targetX, groundY + jumpHeight);

        elapsed = 0f; airborneEnded = false; inWindow = false; hitThisWindow = false;
        e.Invincible = true;
        originalBody = e.Rb.bodyType;
        e.Rb.bodyType = RigidbodyType2D.Kinematic;
        e.Rb.linearVelocity = Vector2.zero;
    }

    public override void Tick(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        elapsed += Time.deltaTime;
        float airborne = airborneTime;
        float riseTime = airborne * 0.55f;   // 上升占前 55%,砸下占后 45%

        if (elapsed < airborne)
        {
            if (elapsed <= riseTime)
            {
                float a = riseTime > 0f ? elapsed / riseTime : 1f;
                a = 1f - (1f - a) * (1f - a);                 // ease-out 上升
                e.transform.position = Vector2.Lerp(startPos, apex, a);
            }
            else
            {
                float d = (elapsed - riseTime) / (airborne - riseTime);
                d *= d;                                       // ease-in 砸下
                e.transform.position = Vector2.Lerp(apex, groundTarget, d);
            }
        }
        else
        {
            if (!airborneEnded) EndAirborne(e);
            e.transform.position = groundTarget;
        }
        if (inWindow) TryHit(e, atk, 1f);
    }

    public override void OnHitOpen(EnemyBase e, EnemyBase.EnemyAttack atk)   // 砸地瞬间
    {
        inWindow = true; hitThisWindow = false;
        AudioManager.Instance?.PlayBossSlam();
        if (!airborneEnded) EndAirborne(e);   // 砸地即落地、可被惩罚
        TryHit(e, atk, 1f);
    }
    public override void OnHitClose(EnemyBase e, EnemyBase.EnemyAttack atk) { TryHit(e, atk, 1f); inWindow = false; }
    public override void End(EnemyBase e, EnemyBase.EnemyAttack atk) => EndAirborne(e);

    void EndAirborne(EnemyBase e)
    {
        if (airborneEnded) return;
        airborneEnded = true;
        e.Invincible = false;
        if (e.Rb.bodyType == RigidbodyType2D.Kinematic) e.Rb.bodyType = originalBody;
    }
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
    int     windowCount;
    PlayerController target;
    RigidbodyType2D originalBody;

    public override void Begin(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        bossStartPos = e.transform.position;
        elapsed = 0f; hitElapsed = -1f; slamElapsed = -1f;
        launched = false; launchedOnce = false; windowCount = 0;
        inWindow = false; hitThisWindow = false;
        originalBody = e.Rb.bodyType;
        e.Rb.bodyType = RigidbodyType2D.Kinematic;
        e.Rb.linearVelocity = Vector2.zero;
    }

    public override void OnHitOpen(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        windowCount++;
        inWindow = true; hitThisWindow = false;
        if (windowCount == 1 && hitElapsed  < 0f) hitElapsed  = elapsed;                                   // 横劈连接
        if (windowCount >= 2 && slamElapsed < 0f) { slamElapsed = elapsed; AudioManager.Instance?.PlayBossSlam(); }   // 下劈砸地
        TryHitLaunch(e, atk);
    }
    public override void OnHitClose(EnemyBase e, EnemyBase.EnemyAttack atk) { TryHitLaunch(e, atk); inWindow = false; }

    void TryHitLaunch(EnemyBase e, EnemyBase.EnemyAttack atk)
    {
        if (!inWindow || hitThisWindow || e.player == null) return;
        if (atk.hits == null || atk.hits.Length == 0 || atk.hits[0] == null) return;
        var h = atk.hits[0];
        float mult = windowCount >= 2 ? slamMult : 1f;
        float dmg  = e.attack * e.DamageMultiplier * h.damageMultiplier * mult;
        if (e.PerformAttack(dmg, h.hitboxOffset, h.hitboxSize, h.red, h.knockback, h.hitStun))
        {
            hitThisWindow = true;
            if (windowCount == 1) TryLaunch(e);
        }
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
        TryHitLaunch(e, atk);

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
