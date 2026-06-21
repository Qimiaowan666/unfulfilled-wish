using UnityEngine;
using UnityEngine.InputSystem;

// 主角状态基类:公共部分(anim/rb/animBool/timer + Enter/Exit/Update)在 EntityState;这里加 owner + 输入 + 全局过渡。
public abstract class PlayerBaseState : EntityState
{
    protected PlayerController   player;
    protected PlayerInputBuffer  inputBuffer;
    protected PlayerInput        input;

    protected bool IsInputBlocked => ShopUI.IsOpen;

    public PlayerBaseState(PlayerController player, PlayerStateMachine stateMachine, string animBoolName)
        : base(stateMachine, animBoolName)
    {
        this.player = player;
        rb          = player.Rb;
        anim        = player.Anim;
        inputBuffer = player.InputBuffer;
        input       = player.Input;
    }

    public override void Update()
    {
        base.Update();
        UpdateAnimationParameters();
    }

    public virtual void UpdateAnimationParameters()
    {
        anim.SetFloat("yVelocity", rb.linearVelocity.y);
    }

    public virtual void OnAnimationFinished()   { }
    public virtual void OnHitFrame()            { }
    public virtual void OnCounterWindowClosed() { }

    protected bool CheckGlobalTransitions()
    {
        if (IsInputBlocked) return false;
        if (this == player.dashState    || this == player.deadState    ||
            this == player.stunnedState || this == player.counterState ||
            this == player.healState)   // 治疗施法期间不接收任何全局过渡
            return false;

        if (input.CounterPressed)
        {
            if (player.counterCooldownTimer > 0f)
            {
                Debug.Log($"[Player] 识破冷却中，剩 {player.counterCooldownTimer:F2}s");
            }
            else
            {
                player.StartCounterCooldown();
                stateMachine.ChangeState(player.counterState);
                return true;
            }
        }

        if (input.DashPressed && player.dashCooldownTimer <= 0f)
        {
            player.StartDashCooldown();
            stateMachine.ChangeState(player.dashState);
            return true;
        }

        // 技能槽 1（Q 键）→ 默认绑突刺斩
        if (input.Skill1Pressed && player.SkillManager != null)
        {
            if (player.SkillManager.TryUseSkill(PlayerSkillType.DashStrike))
                return true;
        }
        // 技能槽 2（E 键）→ 默认绑治疗
        if (input.Skill2Pressed && player.SkillManager != null)
        {
            if (player.SkillManager.TryUseSkill(PlayerSkillType.Heal))
                return true;
        }
        return false;
    }
}
