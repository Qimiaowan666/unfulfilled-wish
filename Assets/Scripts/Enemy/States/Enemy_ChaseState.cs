using UnityEngine;

public class Enemy_ChaseState : EnemyBaseState
{
    float lastTimeDetected;

    public Enemy_ChaseState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isMoving") { }

    public override void Enter()
    {
        base.Enter();
        lastTimeDetected = Time.time;
    }

    public override void Update()
    {
        base.Update();

        if (enemy.DetectPlayer())
            lastTimeDetected = Time.time;
        else if (Time.time > lastTimeDetected + enemy.battleTimeDuration)
        {
            enemy.LoseTarget();
            stateMachine.ChangeState(enemy.idleState);
            return;
        }

        if (enemy.player == null) return;

        float dist = enemy.GetHorizontalDistToPlayer();
        float dir  = Mathf.Sign(enemy.player.position.x - enemy.transform.position.x);
        enemy.SetFacing(dir);

        if (dist <= enemy.attackRange && enemy.attackCooldownTimer <= 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            stateMachine.ChangeState(enemy.specialCooldownTimer <= 0f
                ? (EnemyBaseState)enemy.dashAttackState
                : enemy.attackState);
            return;
        }

        if (dist < enemy.retreatDistance)
        {
            rb.linearVelocity = new Vector2(-dir * enemy.battleMoveSpeed * 0.5f, rb.linearVelocity.y);
            return;
        }

        if (dist <= enemy.preferredCombatDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(dir * enemy.battleMoveSpeed, rb.linearVelocity.y);
    }
}
