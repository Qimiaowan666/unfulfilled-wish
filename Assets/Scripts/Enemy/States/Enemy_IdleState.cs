using UnityEngine;

public class Enemy_IdleState : Enemy_GroundedState
{
    public Enemy_IdleState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isIdle") { }

    public override void Enter()
    {
        base.Enter();
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
