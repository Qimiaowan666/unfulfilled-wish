using UnityEngine;

public class Enemy_AttackState : EnemyBaseState
{
    public Enemy_AttackState(GroundEnemy enemy, EnemyStateMachine sm)
        : base(enemy, sm) { }

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        // 兜底超时：跟随当前招动画时长(+余量)，避免长招(如降帧的多连射)被固定 1.5s 提前切断。
        // 正常仍由动画事件 AnimationTrigger 退出；clip 取不到才退回 1.5s。
        float clipLen = enemy.CurrentAttackClipLength();
        stateTimer = clipLen > 0.01f ? clipLen + 0.3f : 1.5f;
        enemy.ResetAttackSwings();
        enemy.PlayCurrentAttack();  // 播当前选中的招(含变身后缀)
    }

    // 招式无论怎么结束(正常退出 / 被韧性破·死亡打断)都清掉红闪预警，避免 WarnEnd 没轮到导致身体卡红
    public override void Exit()
    {
        base.Exit();
        enemy.GetComponent<DamageFeedback>()?.ClearWarning();
    }

    public override void Update()
    {
        base.Update();

        // 出招位移：前冲(朝玩家、靠近到 ~1.5 就停免穿模) / 后撤(背对玩家、到边缘就停免掉平台)
        var atk = enemy.CurrentAttack;
        if (atk != null && atk.lungeDir != LungeDir.None && atk.lungeSpeed > 0f)
        {
            float vx = 0f;
            if (atk.lungeDir == LungeDir.Forward)
                vx = enemy.GetHorizontalDistToPlayer() > 1.5f ? enemy.FacingDir * atk.lungeSpeed : 0f;
            else // Backward：朝背对玩家方向后撤，边缘检测防掉下平台
            {
                float dir = -enemy.FacingDir;
                vx = enemy.LedgeAhead(dir) ? 0f : dir * atk.lungeSpeed;
            }
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        }

        if (triggerCalled || stateTimer < 0f)
        {
            // 冷却从"攻击结束"算；招设了 cooldownOverride 用它，否则用敌人默认 attackCooldown
            var a = enemy.CurrentAttack;
            enemy.StartAttackCooldown(a != null && a.cooldownOverride > 0f ? a.cooldownOverride : enemy.attackCooldown);
            stateMachine.ChangeState(enemy.player != null
                ? (EnemyBaseState)enemy.chaseState
                : enemy.idleState);
        }
    }
}
