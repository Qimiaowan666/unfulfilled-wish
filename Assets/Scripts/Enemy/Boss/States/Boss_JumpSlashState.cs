using UnityEngine;

// 二段跳劈：起跳的同时播 atk2，跳到玩家头顶后垂直砸下。
// 命中窗口由 atk2 动画事件驱动（第9帧 AnimHitOpen / 第12帧 AnimHitClose / 第13帧 AnimFinish）。
// 上升段无敌，落地（AnimHitClose）后可被惩罚。
public class Boss_JumpSlashState : BossBaseState
{
    Vector2 startPos;
    Vector2 apex;
    Vector2 groundTarget;

    float elapsed;
    bool  inHitWindow;
    bool  hitInThisWindow;
    bool  animFinished;
    RigidbodyType2D originalBodyType;

    public Boss_JumpSlashState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isAttacking2") {}

    public override void Enter()
    {
        base.Enter();
        boss.Anim?.Play("atk2", 0, 0f);
        boss.StartAttackCooldown();
        AudioManager.Instance?.PlayBossAttack();

        startPos = boss.transform.position;
        float groundY = startPos.y;
        float targetX = boss.player != null ? boss.player.position.x : startPos.x;
        groundTarget = new Vector2(targetX, groundY);
        apex         = new Vector2(targetX, groundY + boss.jumpHeight);

        elapsed         = 0f;
        inHitWindow     = false;
        hitInThisWindow = false;
        animFinished    = false;
        stateTimer      = 5f;   // AnimFinish 兜底

        boss.SetInvincible(true);                       // 上升 + 砸下期间无敌
        originalBodyType  = rb.bodyType;
        rb.bodyType       = RigidbodyType2D.Kinematic;  // 自己控制位置
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        base.Update();   // stateTimer 倒计时（兜底）
        elapsed += Time.deltaTime;

        // 整个跳跃弧线压缩在落地前（第0-8帧），第9帧（jumpAirborneTime）砸到地面
        float airborne = boss.jumpAirborneTime;
        float riseTime = airborne * 0.55f;   // 上升占前 55%，砸下占后 45%

        if (elapsed < airborne)
        {
            if (elapsed <= riseTime)
            {
                // 上升到顶点（ease-out：起跳快、顶点慢）
                float a = riseTime > 0f ? elapsed / riseTime : 1f;
                a = 1f - (1f - a) * (1f - a);
                boss.transform.position = Vector2.Lerp(startPos, apex, a);
            }
            else
            {
                // 垂直砸下（ease-in：加速）
                float d = (elapsed - riseTime) / (airborne - riseTime);
                d *= d;
                boss.transform.position = Vector2.Lerp(apex, groundTarget, d);
            }
        }
        else
        {
            // 已砸到地面（第9帧附近）→ 解除无敌，玩家可惩罚
            if (boss.Invincible) EndAirborne();
            boss.transform.position = groundTarget;
        }

        TryHit();
        if (!animFinished && stateTimer < 0f) OnAnimationFinished();
    }

    public override void OnHitWindowOpen()   // atk2 第9帧 = 砸地瞬间
    {
        inHitWindow     = true;
        hitInThisWindow = false;
        if (boss.Invincible) EndAirborne();   // 兜底：确保砸地时已落地、可被惩罚
        TryHit();
    }

    public override void OnHitWindowClose()  // atk2 第12帧
    {
        TryHit();
        inHitWindow = false;
    }

    void TryHit()
    {
        if (!inHitWindow || hitInThisWindow) return;

        var hb = boss.GetHitbox(MinotaurBoss.HitboxKey.Atk2);
        if (hb == null) return;

        float mult   = boss.IsPhase2 ? boss.phase2AttackMultiplier : 1f;
        float damage = boss.attack * mult;
        if (boss.PerformAttack(damage, hb.offset, hb.size))
        {
            hitInThisWindow = true;
            Debug.Log($"[Boss] 跳劈命中 → 伤害 {damage}");
        }
    }

    void EndAirborne()
    {
        boss.SetInvincible(false);
        if (rb.bodyType == RigidbodyType2D.Kinematic)
            rb.bodyType = originalBodyType;
    }

    public override void OnAnimationFinished()  // atk2 第13帧
    {
        if (animFinished) return;
        animFinished = true;
        EndAirborne();   // 兜底：万一 AnimHitClose 没触发

        var next = boss.AdvanceCombo();
        if (next != null) { boss.ExecuteStep(next); return; }
        stateMachine.ChangeState(boss.battleState);
    }

    public override void Exit()
    {
        base.Exit();
        EndAirborne();
    }
}
