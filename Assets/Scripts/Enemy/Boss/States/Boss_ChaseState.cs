using UnityEngine;

public class Boss_ChaseState : BossBaseState
{
    public Boss_ChaseState(MinotaurBoss b, StateMachine sm) : base(b, sm, "isMoving") {}

    public override void Update()
    {
        base.Update();

        if (!boss.KeepEngaged())   // 开战后永不脱战，一路追到底
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        float dir  = boss.DirToward(boss.playerTransform.position);
        boss.SetFacing(dir);

        float dist = boss.GetHorizontalDistToPlayer();
        if (dist <= boss.MaxComboRange())
        {
            stateMachine.ChangeState(boss.battleState);  // 进入最远连段范围 → 让 hub 决策
            return;
        }

        // 冷却一好就回 Battle 重新决策（去抽突进技能扑过去），不再傻走全程
        if (boss.attackCooldownTimer <= 0f)
        {
            stateMachine.ChangeState(boss.battleState);
            return;
        }

        float speed = boss.moveSpeed * (boss.IsPhase2 ? boss.phase2SpeedBonus : 1f);
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
    }

    public override void Exit()
    {
        base.Exit();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}
