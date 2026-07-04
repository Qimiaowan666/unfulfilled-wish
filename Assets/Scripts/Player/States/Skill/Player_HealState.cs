using UnityEngine;

// 治疗状态：持续施法 castDuration 秒。期间锁住玩家水平移动 + 不接收任何全局过渡（不能放别的技能 / dash / counter）
// 期间播 rest 动画（通过 isHealing bool + anim.Play 强切）+ 武侠绿气 sprite 帧动画
// 中途受击 → 打断不回血；完成 → 调 PlayerStats.TakeDamage(-amount) 回血
public class Player_HealState : PlayerBaseState
{
    public Skill_Heal skill;   // 由 Skill_Heal 在 ChangeState 前直接设
    bool       interrupted;
    GameObject activeVfx;

    public Player_HealState(PlayerController player, StateMachine sm)
        : base(player, sm, "isHealing") { }   // isHealing bool 维持 rest state 不被退出

    public override void Enter()
    {
        base.Enter();  
        if (skill == null) { stateMachine.ChangeState(player.idleState); return; }

        stateTimer        = skill.CastDuration;
        interrupted       = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // rest 动画由 isHealing 驱动(Entry→rest(isHealing) + rest→Exit(!isHealing)),纯 bool

        if (skill.VfxEnabled)
            activeVfx = VfxManager.PlayLoop("Vfx/HealAura", player.transform, (Vector3)skill.VfxLocalOffset, 1f,
                                            skill.VfxTint, player.GetComponentInChildren<SpriteRenderer>());
        AudioManager.Instance?.PlayHeal();

        player.Stats.OnDamaged += OnDamagedDuringCast;
        Debug.Log($"[Heal] 开始施法 {skill.CastDuration}s");
    }

    void OnDamagedDuringCast(float dmg)
    {
        if (dmg > 0f) interrupted = true;
    }

    public override void Update()
    {
        base.Update();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (interrupted)
        {
            Debug.Log("[Heal] 被打断！");
            ExitToGroundedOrFall();
            return;
        }

        if (stateTimer < 0f)
        {
            player.Stats.TakeDamage(-skill.HealAmount);
            Debug.Log($"[Heal] 完成 +{skill.HealAmount} HP");
            ExitToGroundedOrFall();
        }
    }

    void ExitToGroundedOrFall()
    {
        stateMachine.ChangeState(player.GroundedOrFall);
    }

    public override void Exit()
    {
        base.Exit();   // 设 isHealing = false → rest state 此时退出回 idle
        player.Stats.OnDamaged -= OnDamagedDuringCast;
        if (activeVfx != null) { VfxManager.StopLoop(activeVfx); activeVfx = null; }
    }
}
