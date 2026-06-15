using UnityEngine;

// 二阶段入口：触发「双方定身 → 咆哮强化 → 恢复」过渡演出。
// 演出/定身期间原地不动；退出(切回 battleState)由 MinotaurBoss 的过渡协程驱动。
public class Boss_EnragedState : BossBaseState
{
    public Boss_EnragedState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isIdle") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        boss.BeginPhase2Transition();
    }

    public override void Update()
    {
        // 定身期间保持原地（只清水平速度，保留重力）；不在这里切状态，交给过渡协程
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}
