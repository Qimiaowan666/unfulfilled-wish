using UnityEngine;

public class Boss_NormalAttackState : BossBaseState
{
    bool useAtk2;
    bool inHitWindow;       // 当前窗口是否开放
    bool hitInThisWindow;   // 本窗口内是否已命中
    bool animFinished;
    int  windowCount;       // 已完成窗口数

    public Boss_NormalAttackState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isAttacking") {}

    // 由 MinotaurBoss.EnterAttack 调用，指定本次播 atk1 还是 atk2
    public void Configure(bool useAtk2)
    {
        this.useAtk2 = useAtk2;
    }

    public override void Enter()
    {
        animBoolName = useAtk2 ? "isAttacking2" : "isAttacking";

        base.Enter();
        // 强制从 0 帧重播：避免 atk1→atk1 / atk2→atk2 时 bool 未变化导致 Animator 不重启
        boss.Anim?.Play(useAtk2 ? "atk2" : "atk1", 0, 0f);

        boss.StartAttackCooldown();
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossAttack();

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
        TryHit();              // Open 帧立即检测一次
    }

    public override void OnHitWindowClose()
    {
        TryHit();              // Close 帧关闭前再检测一次
        if (hitInThisWindow) windowCount++;
        inHitWindow = false;
    }

    void TryHit()
    {
        if (!inHitWindow || hitInThisWindow) return;

        var hb = boss.GetHitbox(useAtk2 ? MinotaurBoss.HitboxKey.Atk2 : MinotaurBoss.HitboxKey.Atk1);
        if (hb == null) return;

        float mult   = boss.IsPhase2 ? boss.phase2AttackMultiplier : 1f;
        float damage = boss.attack * mult;
        if (boss.PerformAttack(damage, hb.offset, hb.size))
        {
            hitInThisWindow = true;
            Debug.Log($"[Boss] {(useAtk2 ? "Atk2" : "Atk1")} 命中 → 伤害 {damage}");
        }
    }

    public override void OnAnimationFinished()
    {
        if (animFinished) return;
        animFinished = true;

        // 连段未结束 → 直接接下一段（含位移），绕过 BattleState 的距离/冷却检查
        var next = boss.AdvanceCombo();
        if (next != null)
        {
            boss.ExecuteStep(next);
            return;
        }

        stateMachine.ChangeState(boss.battleState);
    }
}
