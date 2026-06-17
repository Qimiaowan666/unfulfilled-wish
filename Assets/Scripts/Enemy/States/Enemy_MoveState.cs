using UnityEngine;

public class Enemy_MoveState : Enemy_GroundedState
{
    float patrolDir = 1f;

    public Enemy_MoveState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        patrolDir = enemy.FacingDir;
        enemy.PlayMove();
    }

    public override void Update()
    {
        base.Update();   // 感知到玩家 → Chase
        if (stateMachine.currentState != this) return;

        // 撞墙 or 前方是悬崖 → 原地掉头继续巡逻(不进 Idle)
        if (enemy.WallAhead(patrolDir) || enemy.LedgeAhead(patrolDir))
        {
            patrolDir = -patrolDir;
            enemy.SetFacing(patrolDir);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(patrolDir * enemy.patrolMoveSpeed, rb.linearVelocity.y);
        enemy.SetFacing(patrolDir);

        // 到巡逻边界 → 停一下(Idle)，回来时朝反向(SetFacing 让 Idle→Move 读到反向)
        float targetX = enemy.PatrolOrigin.x + patrolDir * enemy.patrolDistance;
        if (Mathf.Abs(enemy.transform.position.x - targetX) < 0.1f)
        {
            patrolDir = -patrolDir;
            enemy.SetFacing(patrolDir);
            stateMachine.ChangeState(enemy.idleState);
        }
    }
}
