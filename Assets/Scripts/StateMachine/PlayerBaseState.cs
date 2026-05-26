using UnityEngine;
using UnityEngine.InputSystem;

public abstract class PlayerBaseState
{
    protected PlayerController   player;
    protected PlayerStateMachine stateMachine;
    protected Rigidbody2D        rb;
    protected Animator           anim;
    protected PlayerInputBuffer  inputBuffer;
    protected PlayerInput        input;
    protected string             animBoolName;

    protected float stateTimer;
    protected bool  IsInputBlocked => ShopUI.IsOpen;

    public PlayerBaseState(PlayerController player, PlayerStateMachine stateMachine, string animBoolName)
    {
        this.player        = player;
        this.stateMachine  = stateMachine;
        this.animBoolName  = animBoolName;
        rb          = player.Rb;
        anim        = player.Anim;
        inputBuffer = player.InputBuffer;
        input       = player.Input;
    }

    public virtual void Enter()
    {
        anim.SetBool(animBoolName, true);
    }

    public virtual void Update()
    {
        stateTimer -= Time.deltaTime;
        UpdateAnimationParameters();
    }

    public virtual void UpdateAnimationParameters()
    {
        anim.SetFloat("yVelocity", rb.linearVelocity.y);
    }

    public virtual void Exit()
    {
        anim.SetBool(animBoolName, false);
    }

    public virtual void OnAnimationFinished()   { }
    public virtual void OnHitFrame()            { }
    public virtual void OnCounterWindowClosed() { }

    protected bool CheckGlobalTransitions()
    {
        if (IsInputBlocked) return false;
        if (this == player.dashState    || this == player.deadState ||
            this == player.stunnedState || this == player.counterState)
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
        return false;
    }
}
