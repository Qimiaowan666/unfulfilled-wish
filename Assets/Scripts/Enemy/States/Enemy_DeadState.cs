using UnityEngine;

public class Enemy_DeadState : EnemyBaseState
{
    public Enemy_DeadState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isDead") { }

    public override void Enter()
    {
        base.Enter();   // isDead=true → Animator 播 death(纯 bool 驱动)
        rb.linearVelocity = Vector2.zero;
    }
}
