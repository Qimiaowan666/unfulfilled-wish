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

    public override void Update()
    {
        base.Update();

        // 突进招(lungeSpeed>0)：朝玩家冲，靠近到 ~1.2 就停(避免穿过去)
        var atk = enemy.CurrentAttack;
        if (atk != null && atk.lungeSpeed > 0f)
            rb.linearVelocity = new Vector2(
                enemy.GetHorizontalDistToPlayer() > 1.5f ? enemy.FacingDir * atk.lungeSpeed : 0f,
                rb.linearVelocity.y);

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
