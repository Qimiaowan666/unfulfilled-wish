using UnityEngine;

// 识破成功后的短暂停顿（不破韧性，不可处决）
public class Boss_StaggerState : BossBaseState
{
    public Boss_StaggerState(MinotaurBoss b, BossStateMachine sm) : base(b, sm, "isHit") {}

    public override void Enter()
    {
        base.Enter();
        rb.linearVelocity = Vector2.zero;
        stateTimer = boss.staggerDuration;
        boss.ResetCombo();    // 识破打断 → 当前连段清零，下次重新挑 pattern
        Debug.Log($"[Boss] 被识破打断，停顿 {boss.staggerDuration}s");
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (boss.CurrentHP <= 0f) return;   // OnDeath 会接管

        if (stateTimer < 0f)
            stateMachine.ChangeState(boss.battleState);
    }
}
