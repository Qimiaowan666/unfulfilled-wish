using UnityEngine;

// 被 boss 吼击退的反应状态：借 isHealing 维持播放 rest 动画(纯动画用途)，硬直不可操作，
// 不清水平速度(承接外部冲击波击退滑行)，持续 Duration 后回 idle/fall。由 boss 二阶段吼叫触发。
public class Player_KnockedState : PlayerBaseState
{
    public float Duration = 1.5f;

    public Player_KnockedState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isHealing") { }   // 借 isHealing 维持 rest 动画

    public override void Enter()
    {
        base.Enter();                          // isHealing = true → 维持 rest 状态
        if (anim != null) anim.Play("rest", 0, 0f);   // 强切 rest，避开过渡延迟
        stateTimer = Duration;
    }

    public override void Update()
    {
        // 不清速度(让击退滑行)，不接收输入/全局过渡(硬直)
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            stateMachine.ChangeState(player.GroundedOrFall);
    }
}
