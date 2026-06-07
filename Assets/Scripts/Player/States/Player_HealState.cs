using UnityEngine;

// 治疗状态：持续施法 castDuration 秒。期间锁住玩家水平移动 + 不接收任何全局过渡（不能放别的技能 / dash / counter）
// 期间播 rest 动画（通过 isHealing bool + anim.Play 强切）+ 武侠绿气 sprite 帧动画
// 中途受击 → 打断不回血；完成 → 调 PlayerStats.TakeDamage(-amount) 回血
public class Player_HealState : PlayerBaseState
{
    Skill_Heal skill;
    bool       interrupted;
    GameObject activeVfx;

    public Player_HealState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isHealing") { }   // isHealing bool 维持 rest state 不被退出

    public void Configure(Skill_Heal skill)
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        base.Enter();   // 设 isHealing = true → 防止 rest 状态被 isStunned==false 立即退出
        if (skill == null) { stateMachine.ChangeState(player.idleState); return; }

        stateTimer        = skill.CastDuration;
        interrupted       = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // 强切 rest 动画（避开任何 transition 延迟）
        if (!string.IsNullOrEmpty(skill.AnimStateName) && anim != null)
            anim.Play(skill.AnimStateName, 0, 0f);

        SpawnVfx();

        player.Stats.OnDamaged += OnDamagedDuringCast;
        Debug.Log($"[Heal] 开始施法 {skill.CastDuration}s");
    }

    void SpawnVfx()
    {
        if (!skill.VfxEnabled || skill.VfxFrames == null || skill.VfxFrames.Length == 0) return;
        activeVfx = new GameObject("Vfx_HealAura");
        activeVfx.transform.SetParent(player.transform, false);   // 跟随玩家
        activeVfx.transform.localPosition = (Vector3)skill.VfxLocalOffset;
        activeVfx.transform.localScale    = new Vector3(skill.VfxLocalScale.x, skill.VfxLocalScale.y, 1f);
        activeVfx.AddComponent<Vfx_SpriteAnim>()
                 .Init(skill.VfxFrames, skill.VfxFps, skill.VfxTint,
                       skill.VfxSortingLayer, skill.VfxSortingOrder);
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
        stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
    }

    public override void Exit()
    {
        base.Exit();   // 设 isHealing = false → rest state 此时退出回 idle
        player.Stats.OnDamaged -= OnDamagedDuringCast;
        if (activeVfx != null) Object.Destroy(activeVfx);
    }
}
