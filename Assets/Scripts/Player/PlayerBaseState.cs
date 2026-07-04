using UnityEngine;
using UnityEngine.InputSystem;

// 主角状态基类:公共部分(anim/rb/animBool/timer + Enter/Exit/Update)在 EntityState;这里加 owner + 输入 + 全局过渡。
public abstract class PlayerBaseState : EntityState
{
    protected PlayerController   player;
    protected PlayerInput        input;

    public PlayerBaseState(PlayerController player, StateMachine stateMachine, string animBoolName)
        : base(stateMachine, animBoolName)
    {
        this.player = player;
        rb          = player.Rb;
        anim        = player.Anim;
        input       = player.Input;
    }

    public override void Update()
    {
        base.Update();
    }

    public virtual void OnAnimationFinished()   { }
    public virtual void OnHitFrame()            { }
    public virtual void OnCounterWindowClosed() { }

    // 攻击 / 格挡:地面、空中都能触发,抽出共用。注意只给 Grounded/Aired 调,不放进 CheckGlobalTransitions
    // (后者攻击态也会调,放进去会让挥砍被"再按攻击/格挡"打断)。
    protected bool CheckCombatTransitions()
    {
        if (input.AttackPressed) { stateMachine.ChangeState(player.attackState); return true; }
        if (input.BlockHeld)     { stateMachine.ChangeState(player.blockState);  return true; }
        return false;
    }

    // 调用它的子类里update了就能用
    protected bool CheckGlobalTransitions()
    {
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
