using UnityEngine;

public class Player_ExecuteState : PlayerBaseState
{
    public EnemyBase pendingEnemy; // set by GroundedState before ChangeState
    bool hitApplied;
    bool animFinished;

    public Player_ExecuteState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isExecuting") { }

    public override void Enter()
    {
        if (pendingEnemy == null || !pendingEnemy.IsExecutable)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        hitApplied   = false;
        animFinished = false;
        stateTimer   = player.executeDuration * 3f;
        base.Enter();
        player.Stats.SetInvulnerable(true);
        AudioManager.Instance?.PlayExecuteDraw();
        rb.linearVelocity = Vector2.zero;
    }

    public override void Update()
    {
        base.Update();

        if (animFinished || stateTimer < 0f)
        {
            player.Stats.SetInvulnerable(false);
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
        }
    }

    public override void OnHitFrame()
    {
        if (hitApplied) return;
        hitApplied = true;

        if (pendingEnemy != null && pendingEnemy.IsExecutable)
        {
            pendingEnemy.OnExecuted(player.Stats.attack * player.executeDamageMultiplier);
            AudioManager.Instance?.PlayExecuteStrike();
        }
    }

    public override void OnAnimationFinished()
    {
        animFinished = true;
    }

    public override void Exit()
    {
        base.Exit();
        player.Stats.SetInvulnerable(false);
        pendingEnemy = null;
    }
}
