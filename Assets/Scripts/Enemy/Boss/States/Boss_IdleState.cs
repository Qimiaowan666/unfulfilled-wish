using UnityEngine;

public class Boss_IdleState : BossBaseState
{
    public Boss_IdleState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isIdle") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        if (boss.DetectPlayer())
            stateMachine.ChangeState(boss.battleState);
    }
}
