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
        // 死亡状态不做事;boss 何时"消失"不归它管——打死 boss 会同时点燃两条路:
        //   ① EnemyBase.Die() → DisableAfterDeath 协程:WaitForSeconds(1s·缩放时间) → SetActive(false)
        //   ② BossBattleManager.WatchBoss 侦测到死亡 → OnBossDefeated → BossFinishUI.Play() 击破演出
        // 演出把 timeScale 压到≈0,于是 ① 那 1s(缩放时间)被冻住、永远走不到 → 死亡动画不会在 1s 被切;
        // 死亡 Animator 被演出转成 UnscaledTime 按真实时间播完(落地音 AnimBodyFall@1.167s 照常响),
        // boss 最终由 BossFinishUI 在演出末尾自己 SetActive(false) 收掉。
        // (所有 boss 击败都经 BossBattleManager → BossFinishUI,所以裸的 1s 一定被慢镜冻住,不会裸切 boss 死亡动画。)
    }
}
