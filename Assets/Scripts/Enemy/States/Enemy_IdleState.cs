using UnityEngine;

public class Enemy_IdleState : Enemy_GroundedState
{
    public Enemy_IdleState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isIdle") { }

    public override void Enter()
    {
        base.Enter();   // isIdle=true → Animator 播 idle(纯 bool 驱动)
        stateTimer = enemy.idleTime;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;

        if (stateTimer < 0f && !enemy.Passive && !enemy.Stationary)   // 被动/钉死态不巡逻，永远站着
            stateMachine.ChangeState(enemy.moveState);
    }
}
