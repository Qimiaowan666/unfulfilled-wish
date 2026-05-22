using UnityEngine;

public class Enemy_DeadState : EnemyBaseState
{
    public Enemy_DeadState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isDead") { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
    }
}
