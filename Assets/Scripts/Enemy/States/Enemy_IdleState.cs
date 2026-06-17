using UnityEngine;

public class Enemy_IdleState : Enemy_GroundedState
{
    public Enemy_IdleState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        enemy.PlayIdle();
        stateTimer = enemy.idleTime;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;

        if (stateTimer < 0f)
            stateMachine.ChangeState(enemy.moveState);
    }
}
