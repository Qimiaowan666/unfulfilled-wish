using UnityEngine;

public class Boss_RushState : BossBaseState
{
    const float warmupTime = 0.4f;

    bool  rushStarted;
    bool  inHitWindow;
    bool  hitInThisWindow;
    bool  animFinished;
    int   windowCount;
    float finishTimer;

    public Boss_RushState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isAttacking2") {}

    public override void Enter()
    {
        base.Enter();
        boss.rushTimer = boss.rushAttackCooldown;
        boss.StartAttackCooldown();
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossRush();

        boss.DamageFeedback?.FlashWarning(0.35f);

        stateTimer      = warmupTime + boss.rushDuration + 0.3f;
        finishTimer     = 5f;
        rushStarted     = false;
        inHitWindow     = false;    // 默认关闭，等 AnimHitOpen 触发才开
        hitInThisWindow = false;
        animFinished    = false;
        windowCount     = 0;
    }

    public override void Update()
    {
        base.Update();
        finishTimer -= Time.deltaTime;

        // 前摇结束 → 开始冲刺
        if (!rushStarted && stateTimer <= boss.rushDuration + 0.3f)
        {
            rushStarted = true;
            if (boss.player != null)
            {
                float dir = Mathf.Sign(boss.player.position.x - boss.transform.position.x);
                rb.linearVelocity = new Vector2(dir * boss.rushSpeed, rb.linearVelocity.y);
            }
        }

        TryHit();
        if (!animFinished && finishTimer < 0f) OnAnimationFinished();
    }

    public override void OnHitWindowOpen()
    {
        inHitWindow     = true;
        hitInThisWindow = false;
        TryHit();
    }

    public override void OnHitWindowClose()
    {
        TryHit();
        if (hitInThisWindow) windowCount++;
        inHitWindow = false;
    }

    void TryHit()
    {
        if (!inHitWindow || hitInThisWindow) return;
        float mult   = boss.IsPhase2 ? boss.phase2AttackMultiplier : 1f;
        float damage = boss.attack * mult;
        if (boss.PerformAttack(damage))
        {
            hitInThisWindow = true;
            Debug.Log($"[Boss] 冲刺攻击命中 → 伤害 {damage}");
            rb.linearVelocity = Vector2.zero;
        }
    }

    public override void OnAnimationFinished()
    {
        if (animFinished) return;
        animFinished = true;
        stateMachine.ChangeState(boss.battleState);
    }

    public override void Exit()
    {
        base.Exit();
        rb.linearVelocity = Vector2.zero;
    }
}
