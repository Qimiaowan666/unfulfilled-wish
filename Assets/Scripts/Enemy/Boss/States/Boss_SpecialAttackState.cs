using UnityEngine;

public class Boss_SpecialAttackState : BossBaseState
{
    bool inHitWindow;
    bool hitInThisWindow;
    bool animFinished;
    int  windowCount;

    public Boss_SpecialAttackState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isSpecialAttacking") {}

    public override void Enter()
    {
        base.Enter();
        boss.StartAttackCooldown();
        boss.specialTimer = boss.specialAttackCooldown;
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossAttack();

        boss.DamageFeedback?.FlashWarning(boss.specialWarningDuration);
        boss.ShowIndicator(boss.specialWarningDuration);

        stateTimer      = 5f;
        inHitWindow     = false;    // 默认关闭，等 AnimHitOpen 触发才开
        hitInThisWindow = false;
        animFinished    = false;
        windowCount     = 0;
    }

    public override void Update()
    {
        base.Update();
        TryHit();
        if (!animFinished && stateTimer < 0f) OnAnimationFinished();
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
        float damage = boss.attack * 2.5f * mult;
        if (boss.PerformAttack(damage, isSpecialAttack: true))
        {
            hitInThisWindow = true;
            Debug.Log($"[Boss] 特殊攻击命中（可识破，无视格挡） → 伤害 {damage}");
        }
    }

    public override void OnAnimationFinished()
    {
        if (animFinished) return;
        animFinished = true;
        stateMachine.ChangeState(boss.battleState);
    }
}
