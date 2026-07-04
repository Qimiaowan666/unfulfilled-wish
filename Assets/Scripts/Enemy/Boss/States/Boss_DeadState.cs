using UnityEngine;

public class Boss_DeadState : BossBaseState
{
    public Boss_DeadState(MinotaurBoss b, StateMachine sm) : base(b, sm, "isDead") {}

    public override void Enter()
    {
        base.Enter();
        // 强制切死亡 clip：避免死在攻击中途时，被攻击状态 Anim.Play 压住、isDead 过渡又不触发，导致定格攻击姿势
        boss.Anim?.Play("death", 0, 0f);
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayBossDeath();
    }

    public override void Update()
    {
        // 死亡状态不做事，等 EnemyBase.Die 协程 SetActive(false)
    }
}
