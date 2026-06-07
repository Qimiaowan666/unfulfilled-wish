using UnityEngine;

// atk3 = 横劈挑飞 + 跳劈下砸。
// 第1对命中窗口(横劈)：把范围内玩家挑起，boss 同步起跳；玩家升到顶点后悬停。
// 第2对命中窗口(下劈)：boss + 玩家一起砸回地面，造成砸击伤害。
// 不锁定 —— 空中可格挡/识破/翻滚；横劈无视格挡（无论防御都被挑），翻滚 i-frame 可躲；
// 整个 atk3 只挑一次（逃脱后第二段不会再挑）。
public class Boss_Atk3State : BossBaseState
{
    Vector2 bossStartPos;
    Vector2 playerStartPos;
    float   elapsed;
    float   hitElapsed;      // 横劈连接时刻(第1窗口)；<0 未连接。玩家此刻飞、boss 延迟后飞
    float   slamElapsed;     // 下劈时刻(第2窗口)；<0 未下劈
    bool    animFinished;
    bool    launched;        // 当前是否挂着玩家
    bool    launchedOnce;    // 整个 atk3 只挑一次（防逃脱后再挑）
    bool    inHitWindow;
    bool    hitInThisWindow;
    int     windowCount;     // 第几个命中窗口（1=横劈，2=下劈）
    PlayerController target;
    RigidbodyType2D originalBodyType;

    public Boss_Atk3State(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isAttacking") {}

    public override void Enter()
    {
        base.Enter();
        boss.Anim?.Play("atk3", 0, 0f);
        if (boss.Anim != null) boss.Anim.speed = boss.atk3AnimSpeed > 0f ? boss.atk3AnimSpeed : 1f;
        boss.StartAttackCooldown();
        AudioManager.Instance?.PlayBossAttack();

        bossStartPos    = boss.transform.position;
        elapsed         = 0f;
        hitElapsed      = -1f;
        slamElapsed     = -1f;
        animFinished    = false;
        launched        = false;
        launchedOnce    = false;
        inHitWindow     = false;
        hitInThisWindow = false;
        windowCount     = 0;
        stateTimer      = 5f;

        originalBodyType  = rb.bodyType;
        rb.bodyType       = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    public override void OnHitWindowOpen()
    {
        windowCount++;
        inHitWindow     = true;
        hitInThisWindow = false;

        if (windowCount == 1 && hitElapsed < 0f) hitElapsed = elapsed;   // 横劈连接（玩家飞，boss 延迟后飞）
        if (windowCount >= 2 && slamElapsed < 0f) slamElapsed = elapsed; // 下劈 → 一起砸下

        TryHit();
    }

    public override void OnHitWindowClose()
    {
        TryHit();
        inHitWindow = false;
    }

    // 命中即结算伤害；第1窗口(横劈)命中还会挑起玩家
    void TryHit()
    {
        if (!inHitWindow || hitInThisWindow) return;
        if (boss.player == null) return;
        var hb = boss.GetHitbox(MinotaurBoss.HitboxKey.Atk3);
        if (hb == null) return;

        float mult = boss.IsPhase2 ? boss.phase2AttackMultiplier : 1f;
        float dmg  = boss.attack * mult * (windowCount >= 2 ? boss.atk3SlamMultiplier : 1f);
        if (boss.PerformAttack(dmg, hb.offset, hb.size))   // 格挡也算命中，只是不掉血
        {
            hitInThisWindow = true;
            if (windowCount == 1) TryLaunch();   // 横劈命中 → 挑起
        }
    }

    void TryLaunch()
    {
        if (launchedOnce) return;   // 整个 atk3 只挑一次

        var stats = boss.player.GetComponent<PlayerStats>();
        if (stats != null && stats.IsInvulnerable) return;   // 翻滚 i-frame 躲过（格挡无效）

        var pc = boss.player.GetComponent<PlayerController>();
        if (pc == null) return;

        launchedOnce   = true;
        launched       = true;
        target         = pc;
        // 固定到 boss 正前方一段距离，避免玩家走近时挑起来重叠
        float holdX    = boss.transform.position.x + boss.FacingDir * boss.atk3HoldDistance;
        playerStartPos = new Vector2(holdX, pc.transform.position.y);
        pc.BeginLift();
        Debug.Log("[Boss] atk3 横劈命中 → 挑起");
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;
        TryHit();   // 窗口期间持续判定

        // 玩家在横劈瞬间起飞；boss 晚 atk3BossJumpDelay 再起飞追上
        float playerH = HeightFrom(hitElapsed);
        float bossH   = HeightFrom(hitElapsed >= 0f ? hitElapsed + boss.atk3BossJumpDelay : -1f);

        boss.transform.position = new Vector2(bossStartPos.x, bossStartPos.y + bossH);

        if (launched && target != null)
        {
            var ts = target.GetComponent<PlayerStats>();
            if (ts != null && ts.IsInvulnerable)
            {
                // 空中翻滚 → 挣脱
                target.EndLift(); target = null; launched = false;
                Debug.Log("[Boss] 玩家翻滚挣脱挑飞");
            }
            else
            {
                target.SetLiftPosition(new Vector2(playerStartPos.x, playerStartPos.y + playerH));
                // 砸落到地面 → 释放
                if (slamElapsed >= 0f && playerH <= 0f) { target.EndLift(); target = null; launched = false; }
            }
        }

        if (!animFinished && stateTimer < 0f) OnAnimationFinished();
    }

    // 从 startElapsed 起算：升空 → 顶点悬停（等下劈）→ 下落
    float HeightFrom(float startElapsed)
    {
        if (startElapsed < 0f || elapsed < startElapsed) return 0f;
        float peak = boss.atk3RiseHeight;

        if (slamElapsed < 0f)
        {
            float tRise = elapsed - startElapsed;
            if (tRise >= boss.atk3LaunchTime) return peak;   // 悬停在顶点等下劈
            float a = tRise / boss.atk3LaunchTime;
            a = 1f - (1f - a) * (1f - a);                    // ease-out 上升
            return peak * a;
        }

        float tFall = elapsed - slamElapsed;
        if (tFall >= boss.atk3SlamTime) return 0f;
        float d = tFall / boss.atk3SlamTime;
        d *= d;                                              // ease-in 砸下
        return peak * (1f - d);
    }

    public override void OnAnimationFinished()
    {
        if (animFinished) return;
        animFinished = true;
        Cleanup();

        var next = boss.AdvanceCombo();
        if (next != null) { boss.ExecuteStep(next); return; }
        stateMachine.ChangeState(boss.battleState);
    }

    void Cleanup()
    {
        if (boss.Anim != null) boss.Anim.speed = 1f;
        boss.transform.position = new Vector2(bossStartPos.x, bossStartPos.y);   // 回地面
        if (rb.bodyType == RigidbodyType2D.Kinematic) rb.bodyType = originalBodyType;
        rb.linearVelocity = Vector2.zero;
        if (launched && target != null) { target.EndLift(); target = null; launched = false; }
    }

    public override void Exit()
    {
        base.Exit();
        Cleanup();
    }
}
