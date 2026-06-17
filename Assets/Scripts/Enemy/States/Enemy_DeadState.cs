using UnityEngine;

public class Enemy_DeadState : EnemyBaseState
{
    public Enemy_DeadState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        enemy.PlayDeath();
        rb.linearVelocity = Vector2.zero;
    }
}
