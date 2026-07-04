using UnityEngine;

// 路由 hub state：不绑动画，进入即根据距离/冷却分发到 attack(连段) / chase / wait。
// 连段选择走统一 TryPickCombo:按距离 gating(近距任意、远距只缩距招/瞬移开场)+ 强制开场。
public class Boss_BattleState : BossBaseState
{
    public Boss_BattleState(MinotaurBoss b, StateMachine sm) : base(b, sm, "") {}

    public override void Update()
    {
        base.Update();

        if (!boss.KeepEngaged())   // 开战后锁定玩家不脱战；只有玩家真不在场才回 Idle
        {
            stateMachine.ChangeState(boss.idleState);
            return;
        }

        boss.FaceToward(boss.playerTransform.position);

        float dist = boss.GetHorizontalDistToPlayer();

        // 冷却好 → 抽一套连段(强制开场优先,否则按距离/权重)。抽中就打。
        if (boss.attackCooldownTimer <= 0f && boss.TryPickCombo(dist))
        {
            stateMachine.ChangeState(boss.attackState);
            return;
        }

        // 没抽中 / 冷却中：超出最远连段距离就去追，在范围内就站等
        if (dist > boss.MaxComboRange()) stateMachine.ChangeState(boss.chaseState);
        else                             stateMachine.ChangeState(boss.waitState);
    }
}
