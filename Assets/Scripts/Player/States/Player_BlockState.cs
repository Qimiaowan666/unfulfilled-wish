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

        if (CheckGlobalTransitions()) return;   // 按住右键时再按左键 → 切识破

        if (IsInputBlocked) return;

        if (!input.BlockHeld)
            stateMachine.ChangeState(player.IsGrounded ? (PlayerBaseState)player.idleState : player.fallState);
    }

    public void ReceiveAttack(float damage)
    {
        if (InPerfectWindow)
        {
            Debug.Log($"[Player] 完美格挡！来袭伤害 {damage}，剩余完美窗口 {perfectBlockTimer:F2}s");
            player.Stats.RedeemGhostHP(player.Stats.perfectBlockHealAmount);
            AudioManager.Instance?.PlayPerfectBlock();
            CameraShake.Instance?.Shake(0.08f, 0.03f);
        }
        else
        {
            Debug.Log($"[Player] 普通格挡。来袭伤害 {damage}，已按住 {(player.perfectBlockWindow - perfectBlockTimer):F2}s");
            player.Stats.OnNormalBlock(damage);
            AudioManager.Instance?.PlayBlock();
            CameraShake.Instance?.Shake(0.06f, 0.025f);
        }
    }
}
