using UnityEngine;

public class Boss_WaitState : BossBaseState
{
    public Boss_WaitState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isIdle") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();

        if (!boss.KeepEngaged())   // 开战后永不脱战
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        float dir = Mathf.Sign(boss.player.position.x - boss.transform.position.x);
        boss.SetFacing(dir);

        float dist = boss.GetHorizontalDistToPlayer();

        // 玩家跑出范围 → 重新决策
        if (dist > boss.attackRange)
        {
            stateMachine.ChangeState(boss.battleState);
            return;
        }

        // 冷却好 → 重新决策（让 hub 选攻击）
        if (boss.attackCooldownTimer <= 0f)
        {
            stateMachine.ChangeState(boss.battleState);
            return;
        }

        // 站着等
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}
