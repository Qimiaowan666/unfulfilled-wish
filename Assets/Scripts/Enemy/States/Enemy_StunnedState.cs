using UnityEngine;

public class Enemy_StunnedState : EnemyBaseState
{
    float duration;

    public Enemy_StunnedState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isStunned") { }

    public void SetDuration(float d) => duration = d;

    public override void Enter()
    {
        base.Enter();
        stateTimer        = duration;
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (stateTimer < 0f)
        {
            enemy.ResetPoise();
            stateMachine.ChangeState(enemy.player != null
                ? (EnemyBaseState)enemy.chaseState
                : enemy.idleState);
        }
    }
}
