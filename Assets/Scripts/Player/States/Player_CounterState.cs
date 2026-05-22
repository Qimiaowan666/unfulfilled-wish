using UnityEngine;

public class Player_CounterState : PlayerBaseState
{
    bool isInCounterWindow;
    bool animFinished;

    public Player_CounterState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isCountering") { }

    public override void Enter()
    {
        base.Enter();
        isInCounterWindow = true;
        animFinished      = false;
        stateTimer        = player.counterWindow * 3f; // total fallback duration
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();

        if (!isInCounterWindow && animFinished)
        {
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
            return;
        }

        // Fallback: timer expired
        if (stateTimer < 0f)
        {
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
        }
    }

    public override void OnCounterWindowClosed()
    {
        isInCounterWindow = false;
    }

    public override void OnAnimationFinished()
    {
        animFinished = true;
    }

    public bool TryCounter(EnemyBase enemy)
    {
        if (!isInCounterWindow) return false;

        AudioManager.Instance?.PlayCounter();
        enemy.GetComponent<PoiseMeter>()?.TakePoiseDamage(player.counterPoiseDamage);
        return true;
    }
}
