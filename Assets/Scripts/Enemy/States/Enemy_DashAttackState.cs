using UnityEngine;

public class Enemy_DashAttackState : EnemyBaseState
{
    float warningTimer;
    float dashTimer;
    float dashDir;
    bool  warned;
    bool  hitDealt;

    public Enemy_DashAttackState(AoTenguEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm, "isDashing") { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        warningTimer = enemy.specialAttackWarningDuration;
        dashTimer    = enemy.dashAttackDuration;
        dashDir      = enemy.player != null
            ? Mathf.Sign(enemy.player.position.x - enemy.transform.position.x)
            : enemy.FacingDir;
        warned   = false;
        hitDealt = false;
        enemy.StartAttackCooldown();
        enemy.StartSpecialCooldown();
        AudioManager.Instance?.PlayEnemyAttack();
    }

    public override void Update()
    {
        base.Update();

        if (!warned)
        {
            warningTimer -= Time.deltaTime;
            if (warningTimer <= 0f)
            {
                warned = true;
                if (enemy.TryTriggerCounter())
                {
                    stateMachine.ChangeState(enemy.chaseState);
                    return;
                }
            }
            return;
        }

        dashTimer -= Time.deltaTime;
        rb.linearVelocity = new Vector2(dashDir * enemy.dashAttackSpeed, rb.linearVelocity.y);

        if (!hitDealt)
        {
            var hb = enemy.GetHitbox(AoTenguEnemy.HitboxKey.DashAttack);
            if (hb != null)
            {
                if (enemy.PerformAttack(enemy.attack * 2f, hb.offset, hb.size))
                    hitDealt = true;
            }
            else if (enemy.GetHorizontalDistToPlayer() <= enemy.attackRange)
            {
                enemy.HitPlayer(enemy.attack * 2f, enemy.poiseDamagePerHit * 1.5f);
                hitDealt = true;
            }
        }

        if (dashTimer <= 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            stateMachine.ChangeState(enemy.player != null
                ? (EnemyBaseState)enemy.chaseState
                : enemy.idleState);
        }
    }
}
