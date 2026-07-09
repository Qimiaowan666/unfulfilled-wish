using UnityEngine;

// boss 战斗态(合并了旧的 Battle 决策 hub + Chase 走近 + Wait 站等)。
// 每帧决策:脱战没?→ 面向玩家 → 冷却好且抽中连段就出手 → 否则太远走近 / 够近站等 CD。
// 动画:声明 isIdle 撑住 Locomotion,idle↔walk 由 Speed(水平速度,MinotaurBoss.Update 每帧喂)自动切。
public class Boss_CombatState : BossBaseState
{
    public Boss_CombatState(MinotaurBoss b, StateMachine sm) : base(b, sm, "isIdle") {}

    public override void Update()
    {
        base.Update();

        if (!boss.KeepEngaged())   // 开战后锁定玩家不脱战;只有玩家真不在场才回 Idle
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        boss.FaceToward(boss.playerTransform.position);
        float dist = boss.GetHorizontalDistToPlayer();

        // 冷却好 + 抽中一套连段 → 打(强制开场优先,否则按距离/权重)
        if (boss.attackCooldownTimer <= 0f && boss.TryPickCombo(dist))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            stateMachine.ChangeState(boss.attackState);
            return;
        }

        // 没抽中 / 冷却中:超出最远连段距离就走近,在范围内就站定等 CD(boss 不后撤)
        if (dist > boss.MaxComboRange())
        {
            float dir   = boss.DirToward(boss.playerTransform.position);
            float speed = boss.moveSpeed * (boss.IsPhase2 ? boss.phase2SpeedBonus : 1f);
            rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);   // 站等 CD → Speed=0 → idle
        }
    }

    public override void Exit()
    {
        base.Exit();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}
