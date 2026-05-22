using UnityEngine;

public class Player_BlockState : PlayerBaseState
{
    float perfectBlockTimer;
    public bool InPerfectWindow => perfectBlockTimer > 0f;

    public Player_BlockState(PlayerController player, PlayerStateMachine sm)
        : base(player, sm, "isBlocking") { }

    public override void Enter()
    {
        base.Enter();
        perfectBlockTimer = player.perfectBlockWindow;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public override void Update()
    {
        base.Update();
        if (perfectBlockTimer > 0f) perfectBlockTimer -= Time.deltaTime;

        if (IsInputBlocked) return;

        if (!input.BlockHeld)
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
    }

    public void ReceiveAttack(float damage)
    {
        if (InPerfectWindow)
        {
            player.Stats.OnPerfectBlock();
            AudioManager.Instance?.PlayPerfectBlock();
            CameraShake.Instance?.Shake(0.08f, 0.03f);
        }
        else
        {
            player.Stats.OnNormalBlock(damage);
            AudioManager.Instance?.PlayBlock();
            CameraShake.Instance?.Shake(0.06f, 0.025f);
        }
    }
}
