using UnityEngine;

public class Enemy_MoveState : Enemy_GroundedState
{
    float patrolDir = 1f;

    public Enemy_MoveState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isMoving") { }

    public override void Enter()
    {
        base.Enter();
        patrolDir = enemy.FacingDir;
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;

        float targetX = enemy.PatrolOrigin.x + patrolDir * enemy.patrolDistance;
        float dist    = Mathf.Abs(enemy.transform.position.x - targetX);

        rb.linearVelocity = new Vector2(patrolDir * enemy.patrolMoveSpeed, rb.linearVelocity.y);
        enemy.SetFacing(patrolDir);

        if (dist < 0.1f)
            stateMachine.ChangeState(enemy.idleState);
    }
}
