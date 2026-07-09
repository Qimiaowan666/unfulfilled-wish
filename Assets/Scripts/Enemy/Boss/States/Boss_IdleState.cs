using UnityEngine;

public class Boss_IdleState : BossBaseState
{
    public Boss_IdleState(MinotaurBoss b, StateMachine sm) : base(b, sm, "isIdle") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        // 沉睡/待命站定:每帧清水平速度。否则斜坡/物理抖动的残留速度会被 FeedLocomotionSpeed 喂进 Speed → 混合树误播 walk
        // (入场演出全程停在本态、combatEnabled=false,尤其要站稳)
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // 开战后锁定玩家直接进战斗(不再要求玩家在感知范围内)；未开战(沉睡)仍按感知触发
        if (boss.combatEnabled && boss.EnsurePlayer())
            stateMachine.ChangeState(boss.combatState);
    }
}
