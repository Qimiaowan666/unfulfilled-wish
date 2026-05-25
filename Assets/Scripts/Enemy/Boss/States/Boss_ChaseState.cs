using UnityEngine;

public class Boss_ChaseState : BossBaseState
{
    public Boss_ChaseState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isMoving") {}

    public override void Update()
    {
        base.Update();

        if (!boss.DetectPlayer())
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        float dir  = Mathf.Sign(boss.player.position.x - boss.transform.position.x);
        boss.SetFacing(dir);

        float dist = boss.GetHorizontalDistToPlayer();
        if (dist <= boss.attackRange)
        {
            stateMachine.ChangeState(boss.battleState);  // 进入范围 → 让 hub 决策
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
