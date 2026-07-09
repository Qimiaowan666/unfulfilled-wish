using UnityEngine;

// 巡逻态(合并了旧的 Idle + Move):站一下 → 走一段 → 到墙/崖/边界掉头 → 再站一下,循环踱步;感知到玩家 → Chase。
// 动画:只点亮 isIdle 撑住 Locomotion 混合树,idle↔walk 由 Speed(水平速度,GroundEnemy.Update 每帧喂)自动切。
public class Enemy_PatrolState : EnemyBaseState
{
    float patrolDir = 1f;
    float pauseTimer;   // >0 = 站立喘息中

    public Enemy_PatrolState(GroundEnemy enemy, StateMachine sm)
        : base(enemy, sm, "isIdle") { }

    public override void Enter()
    {
        base.Enter();                 // isIdle=true → 待在 Locomotion
        patrolDir  = enemy.FacingDir;
        pauseTimer = enemy.idleTime;  // 进来先站一下(和旧 idle→move 的节奏一致)
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        if (enemy.DetectPlayer()) { stateMachine.ChangeState(enemy.chaseState); return; }   // 站着/踱步时感知到玩家 → 追

        // 被动 / 钉死态:永远站着,不巡逻
        if (enemy.Passive || enemy.Stationary)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 站立喘息阶段
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 行走阶段:撞墙 / 前方悬崖 → 掉头 + 停一下(别走下平台)
        if (enemy.WallAhead(patrolDir) || enemy.LedgeAhead(patrolDir))
        {
            Turn();
            return;
        }

        rb.linearVelocity = new Vector2(patrolDir * enemy.patrolMoveSpeed, rb.linearVelocity.y);
        enemy.SetFacing(patrolDir);

        // 到巡逻边界 → 掉头 + 停一下
        float targetX = enemy.PatrolOrigin.x + patrolDir * enemy.patrolDistance;
        if (Mathf.Abs(enemy.transform.position.x - targetX) < 0.1f)
            Turn();
    }

    void Turn()
    {
        patrolDir = -patrolDir;
        enemy.SetFacing(patrolDir);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        pauseTimer = enemy.idleTime;
    }
}
