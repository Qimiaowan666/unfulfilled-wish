using UnityEngine;

public class Enemy_StunnedState : EnemyBaseState
{
    float duration;

    public Enemy_StunnedState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public void SetDuration(float d) => duration = d;

    public override void Enter()
    {
        base.Enter();
        enemy.PlayHurt();
        stateTimer        = duration;
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (stateTimer < 0f)
            stateMachine.ChangeState(enemy.player != null
                ? (EnemyBaseState)enemy.chaseState
                : enemy.idleState);
    }

    public override void Exit()
    {
        base.Exit();
        enemy.ResetPoise();   // 无论怎么离开硬直都恢复韧性，避免韧性条永久卡空
    }
}
